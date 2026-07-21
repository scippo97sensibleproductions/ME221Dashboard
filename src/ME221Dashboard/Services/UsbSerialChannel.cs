#if ANDROID
using System.Threading.Channels;
using Android.Hardware.Usb;
using Anotherlab.UsbSerialForAndroid.Driver;
using ME221.Comms.Channels;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using ME221Dashboard.Comms;
using ME221Dashboard.Platforms.Android.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services;

public sealed class UsbSerialChannel(
    UsbManager usbManager,
    UsbSerialPort port,
    ME221Dashboard.Comms.ChannelOptions options,
    ILogger<UsbSerialChannel>? logger = null)
    : IChannel
{
    private readonly ILogger<UsbSerialChannel> _logger = logger ?? NullLogger<UsbSerialChannel>.Instance;
    private readonly UsbManager _usbManager = usbManager ?? throw new ArgumentNullException(nameof(usbManager));
    private readonly UsbSerialPort _port = port ?? throw new ArgumentNullException(nameof(port));
    private readonly int _baudRate = options.BaudRate;
    private readonly int _dataBits = options.DataBits;
    private readonly int _stopBits = options.StopBits;
    private readonly int _parity = options.Parity;
    private readonly int _sendTimeoutMs = options.SendTimeoutMs;

    /// <summary>Serializes writes — BulkTransfer is not thread-safe on the same endpoint.</summary>
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    /// More retries with exponential backoff — transient USB errors are common
    /// during engine cranking/running due to electrical noise.
    /// </summary>
    private const int MaxSendRetries = 5;
    private const int RetryDelayMs = 100;

    /// <summary>
    /// Read timeout — must be short enough to not starve writes, long enough to
    /// avoid busy-spinning. With BulkTransfer (enableAsyncReads: false) this
    /// actually works — RequestWait() ignore the timeout.
    /// </summary>
    private const int ReadTimeoutMs = 350;

    /// <summary>
    /// Number of confirmed-device-removal IOExceptions before declaring dead.
    /// Transient errors (device still attached) don't count — only increments
    /// when IsDeviceStillAttached() returns false.
    /// </summary>
    private const int MaxHardReceiveFailures = 10;

    private const int ReadBufferSize = 8192;

    private readonly System.Threading.Channels.Channel<MessageFrame> _incomingChannel =
        System.Threading.Channels.Channel.CreateBounded<MessageFrame>(new BoundedChannelOptions(512)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private readonly FrameBuffer _frameBuffer = new(bufferSize: 32768);
    private UsbDeviceConnection? _connection;
    private Task? _receiveTask;
    private CancellationTokenSource? _receiveCts;
    private volatile DeviceStatus _status = DeviceStatus.Closed;
    private volatile bool _isOpen;
    private UsbPowerManager? _powerManager;

    public bool IsOpen => _status is DeviceStatus.Opened or DeviceStatus.Connected;
    public DeviceStatus Status => _status;
    public IAsyncEnumerable<MessageFrame> IncomingFrames => _incomingChannel.Reader.ReadAllAsync();

    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        if (_port == null || _connection == null)
            throw new InvalidOperationException("USB serial port is not open");

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var bytes = frame.ToArray();
            int backoffMs = RetryDelayMs;

            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    _port.Write(bytes, _sendTimeoutMs);
                    return;
                }
                catch (Java.IO.IOException ex) when (attempt < MaxSendRetries && !cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("UsbSerialChannel: write failed (attempt {Attempt}/{Max}) — {Message}",
                        attempt + 1, MaxSendRetries + 1, ex.Message);

                    if (!IsDeviceStillAttached())
                    {
                        _logger.LogWarning("UsbSerialChannel: USB device removed during write — aborting");
                        throw;
                    }

                    await Task.Delay(backoffMs + Random.Shared.Next(0, 50), cancellationToken).ConfigureAwait(false);
                    backoffMs = Math.Min(backoffMs * 2, 1000);
                }
                catch (Java.IO.IOException)
                {
                    _logger.LogError("UsbSerialChannel: write failed after {0} retries — request will fail, channel stays open", MaxSendRetries + 1);
                    throw;
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        if (_connection != null) return;
        _status = DeviceStatus.Opening;

        // Hold a partial WakeLock while the channel is open. Without this,
        // Android doze mode can suspend the USB host controller.
        _powerManager = new UsbPowerManager(_logger);
        _powerManager.Acquire();

        try
        {
            var device = _port.Driver?.Device;
            if (device == null)
                throw new InvalidOperationException("No USB device associated with this port");

            _connection = _usbManager.OpenDevice(device);
            if (_connection == null)
                throw new InvalidOperationException($"Failed to open USB device {device.DeviceName}. Check USB permission.");

            var stopBits = _stopBits switch { 2 => StopBits.Two, _ => StopBits.One };
            var parity = _parity switch { 1 => Parity.Odd, 2 => Parity.Even, _ => Parity.None };

            using var openCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            openCts.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                await Task.Run(() => _port.Open(_connection), openCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("USB port open timed out after 5s — device may not be supported");
            }

            _port.SetParameters(_baudRate, _dataBits, stopBits, parity);

            _isOpen = true;
            _status = DeviceStatus.Opened;
            _logger.LogInformation("UsbSerialChannel: opened {DeviceName} at {BaudRate} baud (WakeLock held)",
                device.DeviceName, _baudRate);

            _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _receiveTask = Task.Run(() => ReceiveLoop(_receiveCts.Token), _receiveCts.Token);
        }
        catch (Exception)
        {
            _status = DeviceStatus.Closed;
            _powerManager?.Release();
            _powerManager = null;
            throw;
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_status == DeviceStatus.Closed) return;
        _status = DeviceStatus.Closing;

        if (_receiveCts is not null)
        {
            await _receiveCts.CancelAsync().ConfigureAwait(false);
            _receiveCts.Dispose();
            _receiveCts = null;
        }

        if (_receiveTask is not null)
        {
            try { await _receiveTask.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
            _receiveTask = null;
        }

        try { if (_isOpen) _port.Close(); }
        catch { /* ignore close errors */ }
        _isOpen = false;

        _connection?.Close();
        _connection?.Dispose();
        _connection = null;

        _powerManager?.Release();
        _powerManager = null;

        _status = DeviceStatus.Closed;
        _incomingChannel.Writer.TryComplete();
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        _frameBuffer.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        var buffer = new byte[ReadBufferSize];
        int hardFailures = 0;
        int backoffMs = 50;

        try
        {
            while (!cancellationToken.IsCancellationRequested && _isOpen)
            {
                try
                {
                    DrainFrames();

                    // BulkTransfer (enableAsyncRead=false) respects the timeout.
                    int bytesRead = _port.Read(buffer, ReadTimeoutMs);

                    if (bytesRead > 0)
                    {
                        hardFailures = 0;
                        backoffMs = 50;

                        var dest = _frameBuffer.AppendMemory;
                        var count = Math.Min(bytesRead, dest.Length);
                        buffer.AsSpan(0, count).CopyTo(dest.Span);
                        _frameBuffer.Advance(count);

                        DrainFrames();

                        // If buffer is full despite extraction, invoke compaction
                        // via TryExtractFrame — avoids Reset() which drops pending
                        // response frames (heartbeat responses etc.).
                        if (_frameBuffer.BufferedLength >= _frameBuffer.AppendMemory.Length)
                            _frameBuffer.TryExtractFrame(out _);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Java.IO.IOException ex)
                {
                    if (!IsDeviceStillAttached())
                    {
                        hardFailures++;
                        if (hardFailures >= MaxHardReceiveFailures)
                        {
                            _status = DeviceStatus.Closed;
                            _isOpen = false;
                            _logger.LogError("UsbSerialChannel: device removed — declaring channel dead");
                            break;
                        }
                    }

                    try { await Task.Delay(backoffMs, cancellationToken).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                    backoffMs = Math.Min(backoffMs * 2, 1000);
                }
                catch (Exception)
                {
                    _logger.LogError("UsbSerialChannel: unexpected receive loop error");
                    if (!_isOpen) { _status = DeviceStatus.Closed; break; }
                    try { await Task.Delay(100, cancellationToken).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }
        finally
        {
            _incomingChannel.Writer.TryComplete();
        }
    }

    private void DrainFrames()
    {
        while (_frameBuffer.TryExtractFrame(out var frame))
        {
            if (frame is null) continue;
            _status = DeviceStatus.Connected;
            if (!_incomingChannel.Writer.TryWrite(frame))
                _logger.LogTrace("UsbSerialChannel: incoming channel full — dropping frame");
        }
    }

    private bool IsDeviceStillAttached()
    {
        try
        {
            var device = _port.Driver?.Device;
            if (device == null) return false;
            var devices = _usbManager.DeviceList;
            if (devices == null) return false;
            foreach (var entry in devices)
                if (entry.Value?.DeviceId == device.DeviceId)
                    return true;
            return false;
        }
        catch
        {
            return false;
        }
    }
}
#endif
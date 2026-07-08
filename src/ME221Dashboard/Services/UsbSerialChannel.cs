#if ANDROID
using System.Threading.Channels;
using Android.Hardware.Usb;
using Anotherlab.UsbSerialForAndroid.Driver;
using ME221.Comms.Channels;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using ME221Dashboard.Comms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services;

/// <summary>
/// Android-specific IChannel implementation using UsbSerialForAndroid.
/// Bypasses System.IO.Ports (which doesn't work on Android) by using the
/// Android USB Host API directly via the UsbSerialForAndroid library.
/// </summary>
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
    private readonly int _receiveTimeoutMs = options.ReceiveTimeoutMs;

    /// <summary>
    /// Serializes all access to the USB port (read AND write).
    ///
    /// The CdcAcmSerialPort uses mEnableAsyncReads (RequestWait) for reads and
    /// BulkTransfer for writes, both on the same UsbDeviceConnection. Android's
    /// UsbDeviceConnection is NOT thread-safe for concurrent RequestWait +
    /// BulkTransfer — this is a known cause of Java.IO.IOException.
    ///
    /// This lock ensures only one thread touches the port at any time,
    /// preventing the read/write collision that triggers connection loss.
    /// </summary>
    private readonly SemaphoreSlim _portLock = new(1, 1);

    /// <summary>
    /// Number of transient IOException retries before declaring the channel dead.
    /// </summary>
    private const int MaxSendRetries = 2;
    private const int RetryDelayMs = 150;

    /// <summary>
    /// Number of transient receive-loop IO failures before declaring the channel
    /// dead. The CDC-ACM async read path (RequestWait) can return null on USB
    /// interrupts (e.g. ECU brownout). Multiple transient failures point to a
    /// real disconnect.
    /// </summary>
    private const int MaxReceiveFailures = 5;

    private readonly System.Threading.Channels.Channel<MessageFrame> _incomingChannel =
        System.Threading.Channels.Channel.CreateBounded<MessageFrame>(new BoundedChannelOptions(200)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private readonly FrameBuffer _frameBuffer = new(bufferSize: 16384);
    private UsbDeviceConnection? _connection;
    private Task? _receiveTask;
    private CancellationTokenSource? _receiveCts;
    private volatile DeviceStatus _status = DeviceStatus.Closed;
    private volatile bool _isOpen;

    public bool IsOpen => _status is DeviceStatus.Opened or DeviceStatus.Connected;
    public DeviceStatus Status => _status;
    public IAsyncEnumerable<MessageFrame> IncomingFrames => _incomingChannel.Reader.ReadAllAsync();

    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        if (_port == null || _connection == null)
            throw new InvalidOperationException("USB serial port is not open");

        await _portLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var bytes = frame.ToArray();

            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    _port.Write(bytes, _sendTimeoutMs);
                    return;
                }
                catch (Java.IO.IOException) when (attempt < MaxSendRetries)
                {
                    _logger.LogWarning("UsbSerialChannel: write failed on {DeviceName} (attempt {Attempt}/{Max}), retrying in {Delay}ms",
                        _port.Driver?.Device?.DeviceName, attempt + 1, MaxSendRetries + 1, RetryDelayMs);
                    await Task.Delay(RetryDelayMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _isOpen = false;
            _status = DeviceStatus.Closed;
            _logger.LogError(ex, "UsbSerialChannel: send failed on {DeviceName}",
                _port.Driver?.Device?.DeviceName);
            throw;
        }
        finally
        {
            _portLock.Release();
        }
    }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        if (_connection != null) return;

        _status = DeviceStatus.Opening;
        try
        {
            var device = _port.Driver?.Device;
            if (device == null)
                throw new InvalidOperationException("No USB device associated with this port");

            _connection = _usbManager.OpenDevice(device);
            if (_connection == null)
                throw new InvalidOperationException($"Failed to open USB device {device.DeviceName}. Check USB permission.");

            var stopBits = _stopBits switch
            {
                2 => StopBits.Two,
                _ => StopBits.One
            };

            var parity = _parity switch
            {
                1 => Parity.Odd,
                2 => Parity.Even,
                _ => Parity.None
            };

            // Open the port with a timeout — port.Open() can block on unsupported devices
            using var openCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            openCts.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                await Task.Run(() => _port.Open(_connection), openCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"USB port open timed out after 5s — device may not be supported");
            }

            // CDC ACM sends control transfers while applying parameters, so the
            // library port must be opened before SetParameters is called.
            _port.SetParameters(_baudRate, _dataBits, stopBits, parity);

            _isOpen = true;
            _status = DeviceStatus.Opened;
            _logger.LogInformation("UsbSerialChannel: opened {DeviceName} at {BaudRate} baud",
                device.DeviceName, _baudRate);

            _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _receiveTask = Task.Run(() => ReceiveLoop(_receiveCts.Token), _receiveCts.Token);
        }
        catch (Exception ex)
        {
            _status = DeviceStatus.Closed;
            _logger.LogError(ex, "UsbSerialChannel: failed to open {DeviceName}",
                _port.Driver?.Device?.DeviceName);
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

        try
        {
            if (_isOpen)
                _port.Close();
        }
        catch { /* ignore close errors */ }

        _isOpen = false;

        _connection?.Close();
        _connection?.Dispose();
        _connection = null;

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
        var buffer = new byte[4096];
        int consecutiveReadFailures = 0;
        while (!cancellationToken.IsCancellationRequested && _isOpen)
        {
            try
            {
                // Extract completed frames first — frees buffer space before we
                // decide whether to reset. Avoids the old Reset() that dropped
                // partial frames and their remaining useful bytes.
                while (_frameBuffer.TryExtractFrame(out var readyFrame))
                {
                    if (readyFrame is not null)
                    {
                        _status = DeviceStatus.Connected;
                        await _incomingChannel.Writer.WriteAsync(readyFrame, cancellationToken).ConfigureAwait(false);
                    }
                }

                // Now acquire the port lock and read
                await _portLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                int bytesRead;
                try
                {
                    bytesRead = _port.Read(buffer, _receiveTimeoutMs);
                }
                finally
                {
                    _portLock.Release();
                }

                if (bytesRead > 0)
                {
                    consecutiveReadFailures = 0;

                    // Copy into frame buffer
                    var dest = _frameBuffer.AppendMemory;
                    var count = Math.Min(bytesRead, dest.Length);
                    buffer.AsSpan(0, count).CopyTo(dest.Span);
                    _frameBuffer.Advance(count);

                    // Extract any complete frames that resulted from the new data
                    while (_frameBuffer.TryExtractFrame(out var frame))
                    {
                        if (frame is not null)
                        {
                            _status = DeviceStatus.Connected;
                            await _incomingChannel.Writer.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    // If buffer is full and we couldn't extract any frames,
                    // compact by shifting unread bytes to the front
                    if (_frameBuffer.BufferedLength >= _frameBuffer.AppendMemory.Length && _frameBuffer.BufferedLength > 0)
                        _frameBuffer.Reset();
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Java.IO.IOException ex)
            {
                consecutiveReadFailures++;
                _logger.LogError(ex, "UsbSerialChannel: receive IO error #{FailureCount} for {DeviceName}",
                    consecutiveReadFailures, _port.Driver?.Device?.DeviceName);
                if (consecutiveReadFailures >= MaxReceiveFailures)
                {
                    _logger.LogError("UsbSerialChannel: too many consecutive receive failures, declaring channel dead");
                    _status = DeviceStatus.Closed;
                    break;
                }
                await Task.Delay(RetryDelayMs, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UsbSerialChannel: receive loop error for {DeviceName}",
                    _port.Driver?.Device?.DeviceName);
                if (!_isOpen)
                {
                    _status = DeviceStatus.Closed;
                    break;
                }
            }
        }
    }
}
#endif

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

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private const int ReadTimeoutMs = 350;
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

    // ── SEND: No retries. IOException = dead endpoint. Fail immediately. ──
    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        if (!_isOpen || _connection == null)
            throw new InvalidOperationException("USB serial port is not open");

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var bytes = frame.ToArray();
            _port.Write(bytes, _sendTimeoutMs);
        }
        catch (Java.IO.IOException ex)
        {
            // Write failure = USB endpoint is DEAD at the kernel level.
            // Retrying won't help — the endpoint won't recover without
            // a full close+reopen cycle. Fail immediately so the connection
            // can be torn down and rebuilt.
            _logger.LogError("UsbSerialChannel: write failed — USB endpoint is dead: {Message}", ex.Message);
            _isOpen = false;
            _status = DeviceStatus.Closed;
            throw;
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

            // ── CRITICAL: Verify the USB endpoint actually works ──
            // After a phone call or USB bus suspension, Open() and SetParameters()
            // succeed at the Java level, but the kernel USB endpoint is broken.
            // Bulk transfers (Write/Read) will fail with IOException.
            // This control transfer (SET_CONTROL_LINE_STATE / DTR|RTS) verifies
            // the endpoint is actually responsive. If it fails, the device needs
            // to be unplugged and replugged.
            int ctrlResult = _connection.ControlTransfer(
                (UsbAddressing)0x21,  // Host→Device | Class | Interface
                0x22,                 // SET_CONTROL_LINE_STATE
                0x03,                 // DTR=1 | RTS=1
                0,                    // Control interface index
                null, 0, 1000);

            if (ctrlResult < 0)
            {
                _logger.LogError("UsbSerialChannel: DTR/RTS verification FAILED (result={Result}) — "
                    + "USB endpoint is dead after bus suspension. Device needs replug.", ctrlResult);
                try { _port.Close(); } catch { }
                try { _connection.Close(); } catch { }
                _connection?.Dispose();
                _connection = null;
                _powerManager?.Release();
                _powerManager = null;
                _status = DeviceStatus.Closed;
                throw new IOException("USB device is not responding. Unplug the ECU and plug it back in.");
            }

            _isOpen = true;
            _status = DeviceStatus.Opened;
            _logger.LogInformation("UsbSerialChannel: opened {DeviceName} at {BaudRate} baud "
                + "(DTR/RTS verified, WakeLock held)", device.DeviceName, _baudRate);

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

    // ── CLOSE: Force-release ALL interfaces before closing connection ──
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_status == DeviceStatus.Closed) return;
        _status = DeviceStatus.Closing;
        _isOpen = false; // Signal receive loop to exit on next iteration

        // Cancel and wait for receive loop
        if (_receiveCts is not null)
        {
            await _receiveCts.CancelAsync().ConfigureAwait(false);
            _receiveCts.Dispose();
            _receiveCts = null;
        }

        if (_receiveTask is not null)
        {
            try { await _receiveTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
            _receiveTask = null;
        }

        // ── CRITICAL: Force-release ALL USB interfaces ──
        // CdcAcmSerialPort.Close() only releases interfaces it knows about.
        // If Open() partially failed (e.g. after bus suspension), some interfaces
        // may be claimed but not tracked by the port. We force-release ALL
        // interfaces on the device to prevent stale claims from poisoning
        // the next connection attempt.
        try
        {
            var device = _port.Driver?.Device;
            if (device != null && _connection != null)
            {
                for (int i = 0; i < device.InterfaceCount; i++)
                {
                    try
                    {
                        var iface = device.GetInterface(i);
                        if (iface != null)
                            _connection.ReleaseInterface(iface);
                    }
                    catch { /* best effort — some interfaces may not be claimed */ }
                }
            }
        }
        catch { /* best effort */ }

        // Close the port
        try { _port.Close(); }
        catch (Exception ex)
        {
            _logger.LogDebug("UsbSerialChannel: port.Close() error during cleanup: {Message}", ex.Message);
        }

        // Close and dispose the connection — order matters!
        try { _connection?.Close(); }
        catch (Exception ex)
        {
            _logger.LogDebug("UsbSerialChannel: connection.Close() error during cleanup: {Message}", ex.Message);
        }
        try { _connection?.Dispose(); }
        catch { /* best effort */ }
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

        try
        {
            while (!cancellationToken.IsCancellationRequested && _isOpen)
            {
                try
                {
                    DrainFrames();

                    int bytesRead = _port.Read(buffer, ReadTimeoutMs);

                    if (bytesRead > 0)
                    {
                        var dest = _frameBuffer.AppendMemory;
                        var count = Math.Min(bytesRead, dest.Length);
                        buffer.AsSpan(0, count).CopyTo(dest.Span);
                        _frameBuffer.Advance(count);

                        DrainFrames();

                        if (_frameBuffer.BufferedLength >= _frameBuffer.AppendMemory.Length)
                            _frameBuffer.TryExtractFrame(out _);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Java.IO.IOException ex)
                {
                    // ── BREAK IMMEDIATELY on IOException ──
                    // The old code retried with backoff (up to 1s) indefinitely
                    // when the device was "still attached" (Java object exists).
                    // This held the port open and prevented clean shutdown.
                    // When the endpoint is dead, retrying reads is futile —
                    // the connection must be torn down and rebuilt.
                    _logger.LogWarning("UsbSerialChannel: receive loop IOException — "
                        + "endpoint is dead, shutting down: {Message}", ex.Message);
                    _status = DeviceStatus.Closed;
                    _isOpen = false;
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("UsbSerialChannel: unexpected receive loop error: {Message}", ex.Message);
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
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

    private readonly System.Threading.Channels.Channel<MessageFrame> _incomingChannel =
        System.Threading.Channels.Channel.CreateBounded<MessageFrame>(new BoundedChannelOptions(100)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    private readonly FrameBuffer _frameBuffer = new();
    private UsbDeviceConnection? _connection;
    private Task? _receiveTask;
    private CancellationTokenSource? _receiveCts;
    private volatile DeviceStatus _status = DeviceStatus.Closed;
    private volatile bool _isOpen;

    public bool IsOpen => _status is DeviceStatus.Opened or DeviceStatus.Connected;
    public DeviceStatus Status => _status;
    public IAsyncEnumerable<MessageFrame> IncomingFrames => _incomingChannel.Reader.ReadAllAsync();

    public Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        if (_port == null || _connection == null)
            throw new InvalidOperationException("USB serial port is not open");

        try
        {
            var bytes = frame.ToArray();
            _port.Write(bytes, _sendTimeoutMs);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _isOpen = false;
            _status = DeviceStatus.Closed;
            _logger.LogError(ex, "UsbSerialChannel: send failed on {DeviceName}",
                _port.Driver?.Device?.DeviceName);
            throw;
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
        while (!cancellationToken.IsCancellationRequested && _isOpen)
        {
            try
            {
                if (_frameBuffer.BufferedLength >= _frameBuffer.AppendSpan.Length)
                    _frameBuffer.Reset();

                var bytesRead = _port.Read(buffer, _receiveTimeoutMs);
                if (bytesRead > 0)
                {
                    // Copy received data into frame buffer's AppendMemory, then advance
                    var dest = _frameBuffer.AppendMemory;
                    var count = Math.Min(bytesRead, dest.Length);
                    buffer.AsSpan(0, count).CopyTo(dest.Span);
                    _frameBuffer.Advance(count);

                    while (_frameBuffer.TryExtractFrame(out var frame))
                    {
                        if (frame is not null)
                        {
                            _status = DeviceStatus.Connected;
                            await _incomingChannel.Writer.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Java.IO.IOException ex)
            {
                _logger.LogError(ex, "UsbSerialChannel: receive loop IO error for {DeviceName}",
                    _port.Driver?.Device?.DeviceName);
                _status = DeviceStatus.Closed;
                break;
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

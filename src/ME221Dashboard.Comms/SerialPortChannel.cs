using System.IO.Ports;
using System.Threading.Channels;
using ME221.Comms.Channels;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Comms;

public sealed class SerialPortChannel : IChannel
{
    private readonly ILogger<SerialPortChannel> _logger;
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly int _dataBits;
    private readonly int _parity;
    private readonly int _stopBits;
    private readonly int _sendTimeoutMs;
    private readonly int _receiveTimeoutMs;
    private readonly bool _handshake;

    private readonly Channel<MessageFrame> _incomingChannel = Channel.CreateBounded<MessageFrame>(new BoundedChannelOptions(100)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait
    });

    private readonly FrameBuffer _frameBuffer = new();

    private SerialPort? _serialPort;
    private Task? _receiveTask;
    private CancellationTokenSource? _receiveCts;
    private volatile DeviceStatus _status = DeviceStatus.Closed;

    public SerialPortChannel(ChannelOptions options, ILogger<SerialPortChannel>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? NullLogger<SerialPortChannel>.Instance;
        _portName = options.PortName ?? throw new ArgumentException("PortName is required", nameof(options));
        _baudRate = options.BaudRate;
        _dataBits = options.DataBits;
        _parity = options.Parity;
        _stopBits = options.StopBits;
        _sendTimeoutMs = options.SendTimeoutMs;
        _receiveTimeoutMs = options.ReceiveTimeoutMs;
        _handshake = options.Handshake;
    }

    public bool IsOpen => _status is DeviceStatus.Opened or DeviceStatus.Connected;
    public DeviceStatus Status => _status;
    public IAsyncEnumerable<MessageFrame> IncomingFrames => _incomingChannel.Reader.ReadAllAsync();

    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        if (_serialPort is null || !_serialPort.IsOpen)
            throw new InvalidOperationException("Serial port is not open");

        try
        {
            await _serialPort.BaseStream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "SerialPortChannel: send failed on {PortName}", _portName);
            throw;
        }
    }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        if (_serialPort?.IsOpen == true) return;

        _status = DeviceStatus.Opening;
        try
        {
            _serialPort = new SerialPort(_portName, _baudRate, (Parity)_parity, _dataBits, (StopBits)_stopBits)
            {
                ReadTimeout = _receiveTimeoutMs,
                WriteTimeout = _sendTimeoutMs,
                Handshake = _handshake ? Handshake.RequestToSend : Handshake.None,
            };

            _serialPort.Open();
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();
            _status = DeviceStatus.Opened;
            _logger.LogDebug("SerialPortChannel: opened {PortName} at {BaudRate} baud", _portName, _baudRate);

            _receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _receiveTask = ReceiveLoop(_receiveCts.Token);
        }
        catch (Exception ex)
        {
            _status = DeviceStatus.Closed;
            _logger.LogError(ex, "SerialPortChannel: failed to open {PortName}", _portName);
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

        _serialPort?.Close();
        _serialPort?.Dispose();
        _serialPort = null;

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
        while (!cancellationToken.IsCancellationRequested && _serialPort?.IsOpen == true)
        {
            try
            {
                if (_frameBuffer.BufferedLength >= _frameBuffer.AppendSpan.Length)
                    _frameBuffer.Reset();

                var bytesRead = await _serialPort.BaseStream.ReadAsync(_frameBuffer.AppendMemory, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0) continue;

                _frameBuffer.Advance(bytesRead);

                while (_frameBuffer.TryExtractFrame(out var frame))
                {
                    if (frame is not null)
                    {
                        _status = DeviceStatus.Connected;
                        await _incomingChannel.Writer.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (TimeoutException) { }
            catch (IOException ex)
            {
                _logger.LogError(ex, "SerialPortChannel: receive loop error for {PortName}", _portName);
                if (_serialPort?.IsOpen != true) { _status = DeviceStatus.Closed; break; }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "SerialPortChannel: receive loop error for {PortName}", _portName);
                if (_serialPort?.IsOpen != true) { _status = DeviceStatus.Closed; break; }
            }
        }
    }
}

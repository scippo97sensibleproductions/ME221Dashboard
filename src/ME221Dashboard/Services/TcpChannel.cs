using System.Net.Sockets;
using System.Threading.Channels;
using ME221.Comms.Channels;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services;

public class TcpChannel(string host, int port, ILogger<TcpChannel>? logger = null) : IChannel
{
    private readonly ILogger<TcpChannel> _logger = logger ?? NullLogger<TcpChannel>.Instance;

    private readonly Channel<MessageFrame> _incomingChannel = Channel.CreateBounded<MessageFrame>(new BoundedChannelOptions(100)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });

    private readonly FrameBuffer _frameBuffer = new(4096);

    private TcpClient? _tcpClient;
    private Task? _receiveTask;
    private CancellationTokenSource? _receiveCts;
    private volatile DeviceStatus _status = DeviceStatus.Closed;

    public bool IsOpen => _status is DeviceStatus.Opened or DeviceStatus.Connected;
    public DeviceStatus Status => _status;
    public IAsyncEnumerable<MessageFrame> IncomingFrames => _incomingChannel.Reader.ReadAllAsync();

    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        var client = _tcpClient;
        if (client is null || !client.Connected)
            throw new InvalidOperationException("TcpChannel is not connected");

        try
        {
            await client.GetStream().WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _status = DeviceStatus.Closed;
            _logger.LogError(ex, "TcpChannel: send failed on {Host}:{Port}", host, port);
            throw;
        }
    }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        _status = DeviceStatus.Opening;
        _logger.LogDebug("TcpChannel: connecting to {Host}:{Port}", host, port);

        try
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            _status = DeviceStatus.Opened;
            _logger.LogDebug("TcpChannel: connected to {Host}:{Port}", host, port);

            _receiveCts = new CancellationTokenSource();
            _receiveTask = ReceiveLoop(_receiveCts.Token);
        }
        catch (Exception ex)
        {
            _status = DeviceStatus.Closed;
            _logger.LogError(ex, "TcpChannel: connect failed on {Host}:{Port}", host, port);
            throw;
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _status = DeviceStatus.Closing;

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

        _tcpClient?.Dispose();
        _tcpClient = null;

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
        try
        {
            var stream = _tcpClient?.GetStream();
            if (stream is null) return;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (_frameBuffer.BufferedLength >= _frameBuffer.AppendSpan.Length)
                    _frameBuffer.Reset();

                var bytesRead = await stream.ReadAsync(_frameBuffer.AppendMemory, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    _status = DeviceStatus.Closed;
                    break;
                }

                _frameBuffer.Advance(bytesRead);

                while (_frameBuffer.TryExtractFrame(out var frame))
                {
                    if (frame is not null)
                        await _incomingChannel.Writer.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (IOException ex)
        {
            _logger.LogError(ex, "TcpChannel: receive loop error on {Host}:{Port}", host, port);
            _status = DeviceStatus.Closed;
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "TcpChannel: receive loop error on {Host}:{Port}", host, port);
            _status = DeviceStatus.Closed;
        }
        finally
        {
            _incomingChannel.Writer.TryComplete();
        }
    }
}

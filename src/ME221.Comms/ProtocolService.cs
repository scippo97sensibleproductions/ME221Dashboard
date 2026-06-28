using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using ME221.Comms.Channels;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using Microsoft.Extensions.Logging;
using static ME221.Comms.ProtocolServiceLog;

namespace ME221.Comms;

public sealed class ProtocolService : IAsyncDisposable
{
    private readonly IChannel _channel;
    private readonly ILogger<ProtocolService>? _logger;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly RequestCorrelator _correlator;
    private readonly Task _receiveLoop;
    private readonly Channel<MessageFrame> _uncorrelatedFrames;
    private readonly Timer _heartbeatTimer;
    private volatile bool _isReportingActive;
    private int _consecutiveHeartbeatFailures;

    /// <summary>
    /// Number of consecutive heartbeat failures before declaring connection lost.
    /// Prevents false disconnects during brief I/O interruptions (e.g. Android file picker).
    /// At 1.5s interval × 3 failures = 4.5s tolerance.
    /// </summary>
    private const int MaxConsecutiveHeartbeatFailures = 3;

    public event EventHandler? HeartbeatFailed;

    public ProtocolService(IChannel channel, ILogger<ProtocolService>? logger = null, ILoggerFactory? loggerFactory = null)
    {
        _channel = channel;
        _logger = logger;
        _correlator = new(loggerFactory?.CreateLogger<RequestCorrelator>() ?? logger as ILogger<RequestCorrelator>);

        _uncorrelatedFrames = Channel.CreateBounded<MessageFrame>(new BoundedChannelOptions(256)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _heartbeatTimer = new Timer(OnHeartbeat, null, Timeout.Infinite, 1500);

        _receiveLoop = Task.Run(async () =>
        {
            try
            {
                await foreach (var frame in _channel.IncomingFrames
                    .WithCancellation(_disposeCts.Token)
                    .ConfigureAwait(false))
                {
                    MessageFrame captured = frame;
                    if (_logger is not null)
                        LogFrameArrived(_logger, captured.Type, captured.Class, captured.Command);

                    if (captured is Response response)
                    {
                        if (_logger is not null)
                            LogFrameCorrelationResult(_logger, true, false);

                        var correlated = _correlator.TryCorrelate(response);
                        if (_logger is not null)
                            LogFrameCorrelationResult(_logger, true, correlated);

                        if (!correlated)
                        {
                            _uncorrelatedFrames.Writer.TryWrite(captured);
                        }
                    }
                    else
                    {
                        _uncorrelatedFrames.Writer.TryWrite(captured);
                        if (_logger is not null)
                            LogFrameCorrelationResult(_logger, false, false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (_logger is not null)
                    LogReceiveLoopCancelled(_logger);
            }
            catch (Exception ex)
            {
                if (_logger is not null)
                    LogReceiveLoopError(_logger, ex, ex.Message);
            }
        }, _disposeCts.Token);
    }

    private async void OnHeartbeat(object? state)
    {
        if (!IsOpen || !_isReportingActive)
            return;

        try
        {
            await SendAsync(SendAckRequest.Instance, _disposeCts.Token).ConfigureAwait(false);
            Interlocked.Exchange(ref _consecutiveHeartbeatFailures, 0);
        }
        catch (Exception ex)
        {
            var failures = Interlocked.Increment(ref _consecutiveHeartbeatFailures);
            if (_logger is not null)
                LogHeartbeatFailed(_logger, ex.Message);
            if (failures >= MaxConsecutiveHeartbeatFailures)
            {
                HeartbeatFailed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsOpen => _channel.IsOpen;
    public DeviceStatus Status => _channel.Status;

    /// <summary>
    /// Pause heartbeat timer to prevent false disconnects during brief I/O interruptions
    /// (e.g. Android file picker activity). Call ResumeHeartbeat() when I/O is safe again.
    /// </summary>
    public void PauseHeartbeat()
    {
        _heartbeatTimer.Change(Timeout.Infinite, 1500);
    }

    /// <summary>
    /// Resume heartbeat timer after a PauseHeartbeat() call.
    /// </summary>
    public void ResumeHeartbeat()
    {
        if (_isReportingActive)
            _heartbeatTimer.Change(1500, 1500);
    }

    public async IAsyncEnumerable<MessageFrame> UncorrelatedFrames([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var frame in _uncorrelatedFrames.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return frame;
        }
    }

    public async Task<TResponse> SendAsync<TResponse>(Request request, CancellationToken cancellationToken = default)
        where TResponse : Response
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request is SetStateRequest { Enabled: false })
        {
            _isReportingActive = false;
            _heartbeatTimer.Change(Timeout.Infinite, 1500);
        }

        if (_logger is not null)
            LogSendingRequest(_logger, request.Type, request.Class, request.Command);

        TaskCompletionSource<Response> tcs = _correlator.Register<TResponse>(request);

        try
        {
            using var pooled = FrameBuilder.Build(request);
            await _channel.SendAsync(pooled.Memory, cancellationToken).ConfigureAwait(false);

            Response response = await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (_logger is not null)
                LogReceivedResponse(_logger, response.Type, response.Class, response.Command);

            if (request is SetStateRequest { Enabled: true })
            {
                _isReportingActive = true;
                _heartbeatTimer.Change(1500, 1500);
            }

            return (TResponse)response;
        }
        catch (OperationCanceledException)
        {
            _correlator.RemovePending(request);
            if (_logger is not null)
                LogTimeoutWaitingForResponse(_logger, request.Type, request.Class, request.Command);
            throw;
        }
        catch (Exception)
        {
            _correlator.RemovePending(request);
            if (_logger is not null)
                LogTimeoutWaitingForResponse(_logger, request.Type, request.Class, request.Command);
            throw;
        }
    }

    public async Task SendAsync(MessageFrame frame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        using var pooledFf = FrameBuilder.Build(frame);
        await _channel.SendAsync(pooledFf.Memory, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendBatchAsync(IEnumerable<MessageFrame> frames, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frames);
        foreach (var frame in frames)
            await SendAsync(frame, cancellationToken).ConfigureAwait(false);
    }

    public Task OpenAsync(CancellationToken cancellationToken = default) => _channel.OpenAsync(cancellationToken);

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _isReportingActive = false;
        Interlocked.Exchange(ref _consecutiveHeartbeatFailures, 0);
        _heartbeatTimer.Change(Timeout.Infinite, 1500);
        await _channel.CloseAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_logger is not null) LogDisposing(_logger);
        _isReportingActive = false;
        await _heartbeatTimer.DisposeAsync().ConfigureAwait(false);
        await _disposeCts.CancelAsync().ConfigureAwait(false);
        _correlator.CancelAll();
        _uncorrelatedFrames.Writer.TryComplete();
        await _channel.CloseAsync().ConfigureAwait(false);
        await _receiveLoop.ConfigureAwait(false);
        _disposeCts.Dispose();
        GC.SuppressFinalize(this);
    }
}

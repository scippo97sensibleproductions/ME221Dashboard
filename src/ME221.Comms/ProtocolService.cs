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
    private long _lastFrameReceivedTick;
    private int _heartbeatInFlight;

    /// <summary>
    /// Default per-request timeout when the caller doesn't supply a cancelable token.
    /// Prevents hung requests from blocking indefinitely when a response is lost
    /// (e.g. FrameBuffer overflow, USB stutter).
    /// </summary>
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(2.5);

    /// <summary>
    /// Number of consecutive heartbeat write failures before declaring connection lost.
    /// At 1.5s interval × 3 failures = 4.5s tolerance.
    /// </summary>
    private const int MaxConsecutiveHeartbeatFailures = 3;

    /// <summary>
    /// Watchdog: if no frames arrive for this long during active reporting,
    /// declare the connection dead. The ECU sends at ~10Hz, so 5s of total
    /// silence is a definitive signal. This is a secondary check — the
    /// primary liveness signal is successful heartbeat writes.
    /// </summary>
    private const int ReportingDeadIntervalMs = 5000;

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

        _lastFrameReceivedTick = Environment.TickCount64;
        _heartbeatTimer = new Timer(OnHeartbeat, null, Timeout.Infinite, 1500);

        _receiveLoop = Task.Run(async () =>
        {
            try
            {
                await foreach (var frame in _channel.IncomingFrames
                    .WithCancellation(_disposeCts.Token)
                    .ConfigureAwait(false))
                {
                    Interlocked.Exchange(ref _lastFrameReceivedTick, Environment.TickCount64);

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

    /// <summary>
    /// Heartbeat: fire-and-forget SendAck to keep the ECU in reporting mode.
    ///
    /// MEITE sends SendAckResponse (a Response-type frame) every 1.5s with no
    /// expected response — pure write, no await, no correlation, no TCS.
    /// We do the same here via SendAsync(MessageFrame) which just writes bytes.
    ///
    /// Previously we used SendAsync<SendAckResponse>(SendAckRequest) which
    /// registered a TCS and awaited a response with _disposeCts.Token — a token
    /// that never cancels until dispose. If the response was lost (e.g. buffer
    /// overflow dropped it), the await hung forever, and the _heartbeatInFlight
    /// guard blocked all future heartbeats → silent connection death.
    ///
    /// As a secondary liveness check, we also verify data activity: if reporting
    /// is active but no frames arrive for 5s, declare dead.
    /// </summary>
    private async void OnHeartbeat(object? state)
    {
        if (!IsOpen || !_isReportingActive)
            return;

        if (Interlocked.Exchange(ref _heartbeatInFlight, 1) == 1)
            return;

        try
        {
            // Primary check: data-activity watchdog. If no frames arrived for 5s
            // during active reporting, the channel is dead — the ECU sends ~10Hz.
            var silenceMs = Environment.TickCount64 - Interlocked.Read(ref _lastFrameReceivedTick);
            if (silenceMs > ReportingDeadIntervalMs)
            {
                _logger?.LogWarning("ProtocolService: no frames for {Ms}ms during reporting — connection dead", silenceMs);
                HeartbeatFailed?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Send heartbeat: fire-and-forget, no response awaited.
            // Use SendAckResponse (Response type) — matches MEITE's behaviour.
            // SendAsync(MessageFrame) just writes bytes via the channel; no TCS.
            await SendAsync(SendAckResponse.Instance, _disposeCts.Token).ConfigureAwait(false);
            if (_logger is not null)
                LogSentFireAndForget(_logger, SendAckResponse.Instance.Type, SendAckResponse.Instance.Class, SendAckResponse.Instance.Command);
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
        finally
        {
            Interlocked.Exchange(ref _heartbeatInFlight, 0);
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

    /// <summary>
    /// Send a request and await its response. A default 2.5s timeout is applied
    /// when the caller doesn't supply a cancelable token, preventing infinite hangs
    /// when a response is lost.
    /// </summary>
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

        // Apply default timeout when caller didn't provide a cancelable token.
        // Callers with their own CancellationToken are expected to manage their own timeout.
        CancellationTokenSource? linkedCts = null;
        var effectiveToken = cancellationToken;
        if (cancellationToken.CanBeCanceled == false)
        {
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
            linkedCts.CancelAfter(DefaultRequestTimeout);
            effectiveToken = linkedCts.Token;
        }

        TaskCompletionSource<Response> tcs = _correlator.Register<TResponse>(request);

        try
        {
            using var pooled = FrameBuilder.Build(request);
            await _channel.SendAsync(pooled.Memory, effectiveToken).ConfigureAwait(false);

            Response response = await tcs.Task.WaitAsync(effectiveToken).ConfigureAwait(false);
            if (_logger is not null)
                LogReceivedResponse(_logger, response.Type, response.Class, response.Command);

            if (request is SetStateRequest { Enabled: true })
            {
                _isReportingActive = true;
                Interlocked.Exchange(ref _lastFrameReceivedTick, Environment.TickCount64);
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
        finally
        {
            linkedCts?.Dispose();
        }
    }

    /// <summary>
    /// Fire-and-forget send: writes a frame without awaiting any response.
    /// Used for heartbeats (SendAckResponse) and other one-way messages.
    /// </summary>
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
        var sw = System.Diagnostics.Stopwatch.StartNew();
        if (_logger is not null) LogDisposing(_logger);
        _isReportingActive = false;
        if (_logger is not null) _logger.LogCritical("SHUTDOWN: ProtocolService — dispose heartbeat timer");
        await _heartbeatTimer.DisposeAsync().ConfigureAwait(false);
        if (_logger is not null) _logger.LogCritical("SHUTDOWN: ProtocolService — cancel CTS ({Elapsed}ms)", sw.ElapsedMilliseconds);
        await _disposeCts.CancelAsync().ConfigureAwait(false);
        _correlator.CancelAll();
        _uncorrelatedFrames.Writer.TryComplete();
        if (_logger is not null) _logger.LogCritical("SHUTDOWN: ProtocolService — close channel ({Elapsed}ms)", sw.ElapsedMilliseconds);
        await _channel.CloseAsync().ConfigureAwait(false);
        if (_logger is not null) _logger.LogCritical("SHUTDOWN: ProtocolService — await receive loop ({Elapsed}ms)", sw.ElapsedMilliseconds);
        await _receiveLoop.ConfigureAwait(false);
        _disposeCts.Dispose();
        if (_logger is not null) _logger.LogCritical("SHUTDOWN: ProtocolService — DONE ({Elapsed}ms)", sw.ElapsedMilliseconds);
        GC.SuppressFinalize(this);
    }
}

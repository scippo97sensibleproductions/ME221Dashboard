using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services.Mirror;

/// <summary>
/// A-side per-viewer streaming (U3, KTD-2/KTD-9). Subscribes to the same sources the
/// bridge uses — live entity updates, the GPS/odometer event channel, and captured
/// logs — and fans them out per approved viewer: chunked snapshot with per-chunk
/// acks and whole-snapshot retry, latest-wins live frames (R11, R12), a never-skipped
/// recording feed with monotonic sequence and A timestamps (R18), and relayed events
/// in A's exact shapes (R17, KTD-7). Snapshot-scoped writes end sessions via
/// configUpdate (R19, KTD-6).
/// </summary>
public sealed class MirrorStreamService : IDisposable
{
    private sealed class ViewerState(MirrorServerSession session)
    {
        public MirrorServerSession Session { get; } = session;
        public long SnapshotSeq { get; set; }
        public long NextFrameSeq;
        public long NextRecordSeq;
        public bool SnapshotComplete { get; set; }
        public bool SendingFrame { get; set; }
        public Dictionary<string, float?>? PendingFrame;
        public readonly Queue<Dictionary<string, float?>> RecordQueue = new();
        public CancellationTokenSource? SnapshotCts;
        public readonly HashSet<int> AckedChunks = [];
    }

    private readonly MirrorServerService _server;
    private readonly ILiveDataService _liveData;
    private readonly LogCapture _logCapture;
    private readonly MirrorSnapshotSerializer _serializer;
    private readonly ILogger<MirrorStreamService> _logger;
    private readonly TimeSpan _snapshotAckTimeout;
    private readonly Dictionary<MirrorServerSession, ViewerState> _viewers = new();
    private readonly Lock _lock = new();
    private long _nextSnapshotSeq;
    private long _lastFrameTs;
    private volatile bool _disposed;

    public MirrorStreamService(
        MirrorServerService server,
        ILiveDataService liveData,
        LogCapture logCapture,
        MirrorSnapshotSerializer serializer,
        HybridBridgeService bridge,
        TimeSpan? snapshotAckTimeout = null,
        ILogger<MirrorStreamService>? logger = null)
    {
        _server = server;
        _liveData = liveData;
        _logCapture = logCapture;
        _serializer = serializer;
        _snapshotAckTimeout = snapshotAckTimeout ?? MirrorProtocol.SnapshotAckTimeout;
        _logger = logger ?? NullLogger<MirrorStreamService>.Instance;

        _server.SessionApproved += OnSessionApproved;
        _server.SessionClosed += OnSessionClosed;
        _server.MessageReceived += OnMessageReceived;
        _liveData.EntitiesUpdated += OnEntitiesUpdated;
        _logCapture.EntryCaptured += OnLogEntryCaptured;
        bridge.EventEmitted += OnBridgeEventEmitted;
    }

    /// <summary>R19 hooks (U5) call this: end every viewer session with a configUpdate notice.</summary>
    public Task NotifyConfigUpdateAsync(string reason) => _server.NotifyConfigUpdateAsync(reason);

    public void Dispose()
    {
        _disposed = true;
        _server.SessionApproved -= OnSessionApproved;
        _server.SessionClosed -= OnSessionClosed;
        _server.MessageReceived -= OnMessageReceived;
        _liveData.EntitiesUpdated -= OnEntitiesUpdated;
        _logCapture.EntryCaptured -= OnLogEntryCaptured;
    }

    // ── Session lifecycle ────────────────────────────────────────────────────

    private async void OnSessionApproved(MirrorServerSession session)
    {
        ViewerState state;
        lock (_lock)
        {
            state = new ViewerState(session);
            _viewers[session] = state;
        }
        try
        {
            await SendSnapshotAsync(state).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // async void entry point: an unhandled exception here crashes the process.
            _logger.LogError(ex, "Snapshot send failed for {Device}", session.DeviceName);
            try
            {
                await session.SendAsync(new CloseMessage { Reason = "snapshot failed" }).ConfigureAwait(false);
            }
            catch { /* best effort */ }
            session.Close();
        }
    }

    private void OnSessionClosed(MirrorServerSession session)
    {
        ViewerState? state;
        lock (_lock)
        {
            state = _viewers.Remove(session, out var s) ? s : null;
        }
        if (state is not null)
        {
            state.SnapshotCts?.Cancel();
            state.SnapshotCts?.Dispose();
            state.SnapshotCts = null;
        }
    }

    private void OnMessageReceived(MirrorServerSession session, object message)
    {
        if (message is not SnapshotAckMessage ack)
            return;

        ViewerState? state;
        lock (_lock)
            _viewers.TryGetValue(session, out state);
        if (state is null)
            return;

        lock (_lock)
            state.AckedChunks.Add(ack.Index);
    }

    // ── Snapshot send (KTD-2) ────────────────────────────────────────────────

    private async Task SendSnapshotAsync(ViewerState state)
    {
        state.SnapshotCts = new CancellationTokenSource();

        MirrorSnapshot snapshot;
        try
        {
            snapshot = await _serializer.BuildAsync(0).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Snapshot build failed for {Device}", state.Session.DeviceName);
            await state.Session.SendAsync(new CloseMessage { Reason = "snapshot failed" }).ConfigureAwait(false);
            state.Session.Close();
            return;
        }

        var json = MirrorProtocol.Serialize(snapshot);
        var attempts = new SnapshotAttemptTracker();
        var snapshotSeq = 0L;

        while (!_disposed && state.SnapshotCts?.IsCancellationRequested != true)
        {
            // Every attempt is a fresh sequence: a receiver that partially assembled
            // the previous attempt must see a different seq and restart its assembly
            // (KTD-2 whole-snapshot retry). The same payload is re-framed per attempt.
            snapshotSeq = Interlocked.Increment(ref _nextSnapshotSeq);
            state.SnapshotSeq = snapshotSeq;
            var chunks = MirrorChunkFramer.CreateChunks(json, snapshotSeq);

            lock (_lock)
                state.AckedChunks.Clear();
            var delivered = await SendChunksWithAckAsync(state, chunks, snapshotSeq).ConfigureAwait(false);
            if (delivered)
            {
                await state.Session.SendAsync(new SnapshotDoneMessage { Seq = snapshotSeq }).ConfigureAwait(false);
                lock (_lock)
                {
                    state.SnapshotComplete = true;
                    state.NextFrameSeq = snapshotSeq;
                    state.NextRecordSeq = snapshotSeq;
                }
                _logger.LogInformation("Snapshot sent to {Device} (seq {Seq}, {Chunks} chunks)",
                    state.Session.DeviceName, snapshotSeq, chunks.Count);
                return;
            }

            if (!attempts.RecordFailure())
            {
                _logger.LogWarning("Snapshot delivery failed after {Attempts} attempts for {Device}",
                    attempts.Attempts, state.Session.DeviceName);
                await state.Session.SendAsync(new CloseMessage { Reason = "snapshot failed" }).ConfigureAwait(false);
                state.Session.Close();
                return;
            }
            _logger.LogWarning("Snapshot attempt {Attempt} failed for {Device}; retrying",
                attempts.Attempts, state.Session.DeviceName);
        }
    }

    private async Task<bool> SendChunksWithAckAsync(ViewerState state, List<SnapshotChunkMessage> chunks, long seq)
    {
        foreach (var chunk in chunks)
        {
            if (_disposed || state.SnapshotCts?.IsCancellationRequested == true)
                return false;

            await state.Session.SendAsync(chunk).ConfigureAwait(false);

            var deadline = DateTime.UtcNow + _snapshotAckTimeout;
            while (!_disposed && state.SnapshotCts?.IsCancellationRequested != true)
            {
                lock (_lock)
                {
                    if (state.AckedChunks.Contains(chunk.Index))
                        break;
                }
                if (DateTime.UtcNow >= deadline)
                    return false; // this chunk never acked → whole-snapshot retry
                await Task.Delay(5).ConfigureAwait(false);
            }
        }
        return true;
    }

    // ── Live streaming (R10-R12) ─────────────────────────────────────────────

    private void OnEntitiesUpdated(object? sender, EntitiesUpdatedEventArgs e)
    {
        if (_disposed) return;

        var values = new Dictionary<string, float?>(e.Count);
        var ids = e.EntityIds.Span;
        for (var i = 0; i < e.Count; i++)
        {
            var val = _liveData[ids[i]];
            values[ids[i].ToString()] = val.HasValue && !float.IsNaN(val.Value) ? val : null;
        }

        List<ViewerState> targets;
        lock (_lock)
        {
            targets = _viewers.Values.Where(v => v.SnapshotComplete).ToList();
            _lastFrameTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        foreach (var state in targets)
        {
            // Recording feed: never skipped (KTD-9) — every update is queued even if
            // the viewer loop is mid-send. Frame: latest-wins per viewer.
            lock (_lock)
            {
                state.RecordQueue.Enqueue(values);
                state.PendingFrame = values;
                if (state.SendingFrame)
                    continue; // a send is in flight; the newest frame replaces the pending one
                state.SendingFrame = true;
            }
            _ = SendViewerLoopAsync(state);
        }
    }

    private async Task SendViewerLoopAsync(ViewerState state)
    {
        try
        {
            while (!_disposed && state.SnapshotCts?.IsCancellationRequested != true)
            {
                Dictionary<string, float?>? record;
                Dictionary<string, float?>? frame;
                long ts;
                lock (_lock)
                {
                    record = state.RecordQueue.Count > 0 ? state.RecordQueue.Dequeue() : null;
                    frame = state.PendingFrame;
                    state.PendingFrame = null;
                    if (frame is null && record is null)
                    {
                        state.SendingFrame = false;
                        return;
                    }
                    ts = _lastFrameTs;
                }

                if (record is not null)
                {
                    var recordSeq = Interlocked.Increment(ref state.NextRecordSeq);
                    await state.Session.SendAsync(new RecordFeedMessage
                    {
                        Seq = recordSeq,
                        Ts = ts,
                        Values = record,
                    }).ConfigureAwait(false);
                }

                if (frame is not null)
                {
                    var frameSeq = Interlocked.Increment(ref state.NextFrameSeq);
                    await state.Session.SendAsync(new FrameMessage
                    {
                        Seq = frameSeq,
                        Ts = ts,
                        Values = frame,
                    }).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Viewer stream loop ended for {Device}", state.Session.DeviceName);
        }
        finally
        {
            lock (_lock)
                state.SendingFrame = false;
        }
    }

    // ── Event relay (R17, KTD-7) ─────────────────────────────────────────────

    private void OnBridgeEventEmitted(string channel, string payload)
    {
        if (_disposed) return;
        RelayEventAsync(channel, payload).ConfigureAwait(false);
    }

    private async Task RelayEventAsync(string channel, string payload)
    {
        List<MirrorServerSession> targets;
        lock (_lock)
            targets = _viewers.Values.Where(v => v.SnapshotComplete).Select(v => v.Session).ToList();

        foreach (var session in targets)
        {
            try
            {
                await session.SendAsync(new EventMessage { Channel = channel, Payload = payload }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Event relay to {Device} failed", session.DeviceName);
            }
        }
    }

    private void OnLogEntryCaptured(LogCapture.LogEntry entry)
    {
        if (_disposed || !ShouldRelayLog(entry)) return;

        // Rebuild the exact logEntry shape A's UI consumes (KTD-7).
        var payload = JsonSerializer.Serialize(new
        {
            @event = "logEntry",
            timestamp = entry.Timestamp.ToString("HH:mm:ss.fff"),
            level = entry.Level,
            category = entry.Category,
            message = entry.Message,
            exception = entry.Exception,
        });
        RelayEventAsync("log", payload).ConfigureAwait(false);
    }

    /// <summary>Log entries containing credential material are never relayed (KTD-7).</summary>
    internal static bool ShouldRelayLog(LogCapture.LogEntry entry)
    {
        var text = entry.Message + (entry.Exception ?? "");
        if (text.Length == 0) return true;

        return !text.Contains("companion-cert", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("companion-viewers", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("token", StringComparison.OrdinalIgnoreCase)
            && !text.Contains(".pfx", StringComparison.OrdinalIgnoreCase);
    }
}
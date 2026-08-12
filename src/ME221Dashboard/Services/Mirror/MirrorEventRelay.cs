using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services.Mirror;

/// <summary>
/// B-side event ingestion (U8, KTD-2/KTD-7). Subscribes to the mirror client and:
/// reassembles snapshot chunks with per-chunk acks, applies the snapshot into the
/// MirrorSnapshotStore and the bridge's companion-mode read branch (U7), relays the
/// applied snapshot to Svelte (chunked SendRawMessage), gates frames on the snapshot
/// sequence (R11), and re-emits live/GPS/odometer/log events in A's exact shapes.
/// Session state surfaces to Svelte as mirrorSessionState (never the ECU event).
/// </summary>
public sealed class MirrorEventRelay : IDisposable
{
    private readonly MirrorClientService _client;
    private readonly MirrorSnapshotStore _store;
    private readonly HybridBridgeService _bridge;
    private readonly ILogger<MirrorEventRelay> _logger;
    private MirrorChunkReassembler? _reassembler;
    private long _snapshotSeq;
    private bool _gateOpen;

    public MirrorEventRelay(
        MirrorClientService client,
        MirrorSnapshotStore store,
        HybridBridgeService bridge,
        ILogger<MirrorEventRelay>? logger = null)
    {
        _client = client;
        _store = store;
        _bridge = bridge;
        _logger = logger ?? NullLogger<MirrorEventRelay>.Instance;

        _client.MessageReceived += OnMessage;
        _client.StateChanged += OnStateChanged;
        _client.Wire(); // bind the state machine's transitions to StateChanged
        _bridge.EnterMirrorMode(store);
    }

    public long SnapshotSeq => _snapshotSeq;
    public bool SnapshotApplied => _gateOpen;

    /// <summary>Test seam: feed a parsed wire message as if it arrived from A.</summary>
    internal void InjectMessage(object message) => OnMessage(message);

    /// <summary>Test seam: complete the snapshot assembly.</summary>
    internal Task FinalizeForTest(long seq) => FinalizeSnapshotAsync(seq);

    public void Dispose()
    {
        _client.MessageReceived -= OnMessage;
        _client.StateChanged -= OnStateChanged;
        _bridge.ExitMirrorMode();
    }

    // ── Snapshot transport (KTD-2) ───────────────────────────────────────────

    private void OnMessage(object message)
    {
        switch (message)
        {
            case SnapshotChunkMessage chunk:
                HandleChunk(chunk);
                break;
            case SnapshotDoneMessage done:
                _ = FinalizeSnapshotAsync(done.Seq);
                break;
            case FrameMessage frame:
                HandleFrame(frame);
                break;
            case RecordFeedMessage record:
                RelayToSvelte("mirrorRecordFeed", record);
                break;
            case EventMessage evt:
                HandleEvent(evt);
                break;
        }
    }

    private void HandleChunk(SnapshotChunkMessage chunk)
    {
        _reassembler ??= new MirrorChunkReassembler(chunk.Seq);
        if (_reassembler.TryAdd(chunk))
        {
            // Ack every accepted chunk; out-of-order chunks are rejected and the
            // whole snapshot retries (KTD-2).
            SendToServer(new SnapshotAckMessage { Index = chunk.Index });
        }
        else
        {
            _logger.LogWarning("Rejected snapshot chunk {Index}/{Total} (seq {Seq})", chunk.Index, chunk.Total, chunk.Seq);
        }
    }

    private async Task FinalizeSnapshotAsync(long seq)
    {
        var json = _reassembler?.Reassemble();
        _reassembler = null;
        if (string.IsNullOrEmpty(json))
        {
            _logger.LogWarning("Snapshot finalize with no reassembled payload");
            return;
        }

        MirrorSnapshot? snapshot = null;
        try
        {
            snapshot = MirrorProtocol.DeserializeSnapshot(json);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Snapshot payload failed to deserialize");
        }

        if (snapshot is null)
        {
            await SendNoticeAsync("snapshotFailed").ConfigureAwait(false);
            return;
        }

        _snapshotSeq = seq;
        _store.Apply(snapshot);

        // Relay the applied snapshot to Svelte in chunks so MirrorStore applies it
        // (KTD-2: all mirror traffic rides SendRawMessage, never InvokeDotNet).
        var chunks = MirrorChunkFramer.CreateChunks(json!, seq, chunkSize: 64 * 1024);
        foreach (var chunk in chunks)
            RelayToSvelte("mirrorSnapshotChunk", chunk);
        RelayToSvelte("mirrorSnapshotDone", new SnapshotDoneMessage { Seq = seq });

        _gateOpen = true;
        _logger.LogInformation("Snapshot applied (seq {Seq}, {Chunks} chunks)", seq, chunks.Count);
    }

    // ── Frames and events (R11, R17, KTD-7) ──────────────────────────────────

    private void HandleFrame(FrameMessage frame)
    {
        if (!_gateOpen)
        {
            _logger.LogDebug("Frame seq {Seq} before snapshot gate; discarded", frame.Seq);
            return;
        }
        if (!MirrorSequencing.IsFrameAfterSnapshot(frame.Seq, _snapshotSeq))
        {
            _logger.LogDebug("Frame seq {Seq} older than snapshot {Snapshot}; discarded", frame.Seq, _snapshotSeq);
            return;
        }

        // Re-emit in the exact liveDataUpdate shape A's UI consumes (flat event).
        RelayRawToSvelte(JsonSerializer.Serialize(new { @event = "liveDataUpdate", values = frame.Values }, HybridBridgeService.SJsonOptions));
    }

    private void HandleEvent(EventMessage evt)
    {
        switch (evt.Channel)
        {
            case "gps":
            case "odometer":
                // Payloads are already A's exact shapes; relay verbatim (KTD-7).
                RelayRawToSvelte(evt.Payload);
                if (evt.Channel == "odometer")
                    TryUpdateStoreOdometer(evt.Payload);
                break;
            case "log":
                RelayRawToSvelte(evt.Payload);
                _store.AppendLog(evt.Payload);
                break;
        }
    }

    private void TryUpdateStoreOdometer(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.TryGetProperty("odometer", out var value) && value.ValueKind == JsonValueKind.Number)
                _store.UpdateOdometer(value.GetDouble(), root.TryGetProperty("odometerUnit", out var unit) && unit.GetString() == "mi" ? false : true);
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Odometer relay payload unparsable");
        }
    }

    // ── Session state → Svelte (KTD-7) ───────────────────────────────────────

    private void OnStateChanged(MirrorSessionState state)
    {
        // Flat event shape: MirrorStore.svelte.ts reads sessionState/error at the top
        // level (KTD-7). Chunk/done events stay enveloped because the store reads
        // them via .payload.
        var json = JsonSerializer.Serialize(new
        {
            @event = "mirrorSessionState",
            state = state.State.ToString(),
            error = state.Error,
            token = state.Token,
            displayName = state.ServerDisplayName,
        }, HybridBridgeService.SJsonOptions);
        SvelteMessageEmitted?.Invoke(json);
        _bridge.SendRawMirrorJson(json);
    }

    // ── Transport helpers ────────────────────────────────────────────────────

    private void SendToServer(object message)
    {
        _ = _client.SendAsync(message);
    }

    private async Task SendNoticeAsync(string kind)
    {
        RelayToSvelte("mirrorSessionState", new
        {
            @event = "mirrorSessionState",
            state = "Disconnected",
            error = kind switch
            {
                "snapshotFailed" => "snapshot failed to apply",
                _ => kind,
            },
        });
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>Mirror-specific events ride a {event, payload} envelope; wire events stay flat.</summary>
    private void RelayToSvelte(string eventName, object payload)
    {
        var json = JsonSerializer.Serialize(new { @event = eventName, payload }, HybridBridgeService.SJsonOptions);
        SvelteMessageEmitted?.Invoke(json);
        _bridge.SendRawMirrorJson(json);
    }

    /// <summary>Test observer for the relay's Svelte output channel (no webview in unit tests).</summary>
    internal event Action<string>? SvelteMessageEmitted;

    private void RelayRawToSvelte(string json)
    {
        SvelteMessageEmitted?.Invoke(json);
        _bridge.SendRawMirrorJson(json);
    }
}
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ME221Dashboard.Services.Mirror;

/// <summary>
/// Wire protocol for the companion mirror channel (KTD-2). All messages are JSON
/// objects carrying a lowercase <c>msg</c> discriminator; the snapshot travels as
/// ordered base64 chunks with per-chunk acks and whole-snapshot retry.
/// </summary>
public static class MirrorProtocol
{
    /// <summary>UDP discovery responder port (KTD-8); the WSS channel listens on this + 1.</summary>
    public const int DiscoveryPort = 22100;
    public const int WssPort = 22101;

    /// <summary>UTF-8 byte slice per snapshot chunk (base64 on the wire).</summary>
    public const int ChunkSize = 32 * 1024;

    /// <summary>Whole-snapshot retry budget (R6: 3 attempts).</summary>
    public const int MaxSnapshotAttempts = 3;

    /// <summary>A snapshot chunk missing its ack within this window fails the attempt.</summary>
    public const int SnapshotAckTimeoutMs = 3000;

    /// <summary>Approved/streaming viewer sessions per A (R5).</summary>
    public const int ConnectionCap = 4;

    /// <summary>Pre-approval pending handshakes per A, closed by the 60 s auto-deny.</summary>
    public const int PendingCap = 2;

    /// <summary>Approval prompt auto-deny timeout (R2).</summary>
    public const int ApprovalTimeoutSeconds = 60;

    /// <summary>Unapproved connection attempts per source IP per window (R2, KTD-3).</summary>
    public const int PerIpAttemptLimit = 3;
    public const int PerIpWindowSeconds = 60;

    /// <summary>Raw connections that never send a hello: cap per A and expiry window.</summary>
    public const int PreHelloCap = 16;
    public static readonly TimeSpan PreHelloTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Stale-data threshold for the live feed (R11: 50 frame cadences at 40 ms).</summary>
    public const int StaleAfterMs = 2000;

    public static readonly TimeSpan SnapshotAckTimeout = TimeSpan.FromMilliseconds(SnapshotAckTimeoutMs);
    public static readonly TimeSpan ApprovalTimeout = TimeSpan.FromSeconds(ApprovalTimeoutSeconds);
    public static readonly TimeSpan PerIpWindow = TimeSpan.FromSeconds(PerIpWindowSeconds);

    public static string Serialize(MirrorSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, V2JsonContext.Default.MirrorSnapshot);

    public static MirrorSnapshot? DeserializeSnapshot(string json) =>
        JsonSerializer.Deserialize(json, V2JsonContext.Default.MirrorSnapshot);

    public static string Serialize(object message) => message switch
    {
        HelloMessage m => JsonSerializer.Serialize(m, V2JsonContext.Default.HelloMessage),
        HelloAckMessage m => JsonSerializer.Serialize(m, V2JsonContext.Default.HelloAckMessage),
        ApprovalDecisionMessage m => JsonSerializer.Serialize(m, V2JsonContext.Default.ApprovalDecisionMessage),
        SnapshotChunkMessage m => JsonSerializer.Serialize(m, V2JsonContext.Default.SnapshotChunkMessage),
        SnapshotDoneMessage m => JsonSerializer.Serialize(m, V2JsonContext.Default.SnapshotDoneMessage),
        SnapshotAckMessage m => JsonSerializer.Serialize(m, V2JsonContext.Default.SnapshotAckMessage),
        FrameMessage m => JsonSerializer.Serialize(m, V2JsonContext.Default.FrameMessage),
        RecordFeedMessage m => JsonSerializer.Serialize(m, V2JsonContext.Default.RecordFeedMessage),
        EventMessage m => JsonSerializer.Serialize(m, V2JsonContext.Default.EventMessage),
        ConfigUpdateMessage m => JsonSerializer.Serialize(m, V2JsonContext.Default.ConfigUpdateMessage),
        RevokedMessage m => JsonSerializer.Serialize(m, V2JsonContext.Default.RevokedMessage),
        CloseMessage m => JsonSerializer.Serialize(m, V2JsonContext.Default.CloseMessage),
        _ => throw new ArgumentException($"Unknown mirror message type {message.GetType().Name}", nameof(message)),
    };

    public static object? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var msg = doc.RootElement.TryGetProperty("msg", out var p) ? p.GetString() : null;
        return msg switch
        {
            "hello" => JsonSerializer.Deserialize(json, V2JsonContext.Default.HelloMessage),
            "helloAck" => JsonSerializer.Deserialize(json, V2JsonContext.Default.HelloAckMessage),
            "approvalDecision" => JsonSerializer.Deserialize(json, V2JsonContext.Default.ApprovalDecisionMessage),
            "snapshotChunk" => JsonSerializer.Deserialize(json, V2JsonContext.Default.SnapshotChunkMessage),
            "snapshotDone" => JsonSerializer.Deserialize(json, V2JsonContext.Default.SnapshotDoneMessage),
            "snapshotAck" => JsonSerializer.Deserialize(json, V2JsonContext.Default.SnapshotAckMessage),
            "frame" => JsonSerializer.Deserialize(json, V2JsonContext.Default.FrameMessage),
            "recordFeed" => JsonSerializer.Deserialize(json, V2JsonContext.Default.RecordFeedMessage),
            "event" => JsonSerializer.Deserialize(json, V2JsonContext.Default.EventMessage),
            "configUpdate" => JsonSerializer.Deserialize(json, V2JsonContext.Default.ConfigUpdateMessage),
            "revoke" => JsonSerializer.Deserialize(json, V2JsonContext.Default.RevokedMessage),
            "close" => JsonSerializer.Deserialize(json, V2JsonContext.Default.CloseMessage),
            _ => null,
        };
    }
}

/// <summary>
/// Chunk framing for the snapshot transport (KTD-2): the serialized snapshot JSON is
/// split into fixed-size UTF-8 byte slices, each carried base64 in a snapshotChunk
/// message with a monotonic sequence base and a 0-based index. Reassembly is strictly
/// ordered; anything else is rejected.
/// </summary>
public static class MirrorChunkFramer
{
    public static List<SnapshotChunkMessage> CreateChunks(string json, long seq, int chunkSize = MirrorProtocol.ChunkSize)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var chunks = new List<SnapshotChunkMessage>();
        var total = (bytes.Length + chunkSize - 1) / chunkSize;
        for (var i = 0; i < total; i++)
        {
            var slice = bytes.AsSpan(i * chunkSize, Math.Min(chunkSize, bytes.Length - i * chunkSize)).ToArray();
            chunks.Add(new SnapshotChunkMessage
            {
                Seq = seq,
                Index = i,
                Total = total,
                Payload = Convert.ToBase64String(slice),
            });
        }
        return chunks;
    }
}

/// <summary>
/// Ordered snapshot chunk collector on the consumer side. Accepts chunks strictly in
/// index order for one sequence; a chunk out of order, a duplicate, or a mismatched
/// sequence is rejected and the whole attempt must be retried (KTD-2, R6).
/// </summary>
public sealed class MirrorChunkReassembler
{
    /// <summary>Safety cap: a snapshot larger than this is rejected as hostile.</summary>
    public const int MaxSnapshotBytes = 64 * 1024 * 1024;

    private readonly long _seq;
    private readonly List<byte> _bytes = [];
    private int _expectedIndex;

    public MirrorChunkReassembler(long seq) => _seq = seq;

    public long Seq => _seq;
    public int ExpectedIndex => _expectedIndex;
    public bool IsComplete { get; private set; }
    public int TotalChunks { get; private set; }

    /// <summary>True when the chunk was accepted; false when rejected (out of order, duplicate, wrong seq, already complete, or over the size cap).</summary>
    public bool TryAdd(SnapshotChunkMessage chunk)
    {
        if (IsComplete || chunk.Seq != _seq || chunk.Total <= 0)
            return false;
        if (chunk.Index != _expectedIndex)
            return false;
        if (_bytes.Count + chunk.Payload.Length > MaxSnapshotBytes)
            return false;
        TotalChunks = chunk.Total;
        var payload = Convert.FromBase64String(chunk.Payload);
        _bytes.AddRange(payload);
        _expectedIndex++;
        IsComplete = _expectedIndex == chunk.Total;
        return true;
    }

    /// <summary>Reassembled JSON string, or null while the snapshot is incomplete.</summary>
    public string? Reassemble() => IsComplete ? Encoding.UTF8.GetString(_bytes.ToArray()) : null;
}

/// <summary>
/// Whole-snapshot retry budget on the sender side (R6, KTD-2): a lost or rejected chunk
/// consumes an attempt; after <see cref="MirrorProtocol.MaxSnapshotAttempts"/> the
/// snapshot is failed and the receiver sees the visible error state.
/// </summary>
public sealed class SnapshotAttemptTracker
{
    private int _attempts;
    private readonly int _maxAttempts;

    public SnapshotAttemptTracker(int maxAttempts = MirrorProtocol.MaxSnapshotAttempts) => _maxAttempts = maxAttempts;

    public int Attempts => _attempts;
    public bool Exhausted => _attempts >= _maxAttempts;

    /// <summary>Registers a failed attempt; true while retries remain, false when exhausted.</summary>
    public bool RecordFailure() => ++_attempts < _maxAttempts;
    public void Reset() => _attempts = 0;
}

/// <summary>Monotonic sequencing across snapshot and live frames (R11): snapshot seq N, first frame seq N+1.</summary>
public static class MirrorSequencing
{
    public static long NextFrameSequence(long snapshotSeq) => snapshotSeq + 1;
    public static bool IsFrameAfterSnapshot(long frameSeq, long snapshotSeq) => frameSeq > snapshotSeq;
}
using System.Text.Json.Serialization;
using ME221.Data.Models;

namespace ME221Dashboard.Services.Mirror;

/// <summary>Hello handshake, B to A (wire table).</summary>
public sealed class HelloMessage
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "hello";
    public string AppVersion { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string? Token { get; set; }
    public string ClientId { get; set; } = "";
}

/// <summary>Hello response, A to B. Status: approved / pending / refused / versionMismatch / revoked.</summary>
public sealed class HelloAckMessage
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "helloAck";
    public string Status { get; set; } = "";
}

/// <summary>Approval decision, A to B (R2). Carries the per-device token on approval (R16, KTD-3).</summary>
public sealed class ApprovalDecisionMessage
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "approvalDecision";
    public bool Approved { get; set; }
    public string? Token { get; set; }
}

/// <summary>One ordered snapshot chunk, A to B (KTD-2). Payload is base64 UTF-8.</summary>
public sealed class SnapshotChunkMessage
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "snapshotChunk";
    public long Seq { get; set; }
    public int Index { get; set; }
    public int Total { get; set; }
    public string Payload { get; set; } = "";
}

/// <summary>Last chunk sent, A to B; seq is the monotonic base for live frames.</summary>
public sealed class SnapshotDoneMessage
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "snapshotDone";
    public long Seq { get; set; }
}

/// <summary>Per-chunk ack, B to A (KTD-2).</summary>
public sealed class SnapshotAckMessage
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "snapshotAck";
    public int Index { get; set; }
}

/// <summary>Live frame, A to B (R10, R11). Values keyed by entity id as in liveDataUpdate.</summary>
public sealed class FrameMessage
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "frame";
    public long Seq { get; set; }
    public long Ts { get; set; }
    public Dictionary<string, float?> Values { get; set; } = [];
}

/// <summary>Full-cadence recording feed frame, A to B (R18, KTD-9); never skipped.</summary>
public sealed class RecordFeedMessage
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "recordFeed";
    public long Seq { get; set; }
    public long Ts { get; set; }
    public Dictionary<string, float?> Values { get; set; } = [];
}

/// <summary>Relayed event, A to B (R17, KTD-7). Channel: gps / odometer / log.</summary>
public sealed class EventMessage
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "event";
    public string Channel { get; set; } = "";
    public string Payload { get; set; } = "";
}

/// <summary>Snapshot-scoped config changed on A (R19, KTD-6).</summary>
public sealed class ConfigUpdateMessage
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "configUpdate";
    public string Reason { get; set; } = "";
}

/// <summary>Viewer revoked, A to B (R3).</summary>
public sealed class RevokedMessage
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "revoke";
    public string Reason { get; set; } = "";
}

/// <summary>Server-initiated close, A to B.</summary>
public sealed class CloseMessage
{
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "close";
    public string Reason { get; set; } = "";
}
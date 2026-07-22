using System.Text.Json.Serialization;

namespace ME221Dashboard.Services.Sessions;

public sealed class RecordedSession
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("startTime")]
    public string StartTime { get; init; } = "";

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; init; }

    [JsonPropertyName("sensorIds")]
    public int[] SensorIds { get; init; } = [];

    [JsonPropertyName("sensorNames")]
    public Dictionary<string, string> SensorNames { get; init; } = new();

    [JsonPropertyName("data")]
    public Dictionary<string, List<SamplePoint>> Data { get; init; } = new();

    [JsonPropertyName("freezeFrames")]
    public List<FreezeFrame> FreezeFrames { get; init; } = new();
}

public sealed class SamplePoint
{
    [JsonPropertyName("t")]
    public double T { get; init; }

    [JsonPropertyName("v")]
    public double V { get; init; }
}

public sealed class FreezeFrame
{
    [JsonPropertyName("timeMs")]
    public double TimeMs { get; init; }

    [JsonPropertyName("label")]
    public string Label { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = "";
}

public sealed class SessionSummary
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("startTime")]
    public string StartTime { get; init; } = "";

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; init; }

    [JsonPropertyName("sensorCount")]
    public int SensorCount { get; init; }
}

public sealed class SessionManifest
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; init; } = "";

    [JsonPropertyName("exportDate")]
    public string ExportDate { get; init; } = "";

    [JsonPropertyName("sessionCount")]
    public int SessionCount { get; init; }
}

public sealed class ImportSessionResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public List<RecordedSession> Sessions { get; init; } = new();
}

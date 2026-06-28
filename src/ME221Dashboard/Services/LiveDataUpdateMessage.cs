using System.Text.Json.Serialization;

namespace ME221Dashboard.Services;

public sealed class LiveDataUpdateMessage
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = "liveDataUpdate";

    [JsonPropertyName("values")]
    public Dictionary<string, float?> Values { get; set; } = new();
}

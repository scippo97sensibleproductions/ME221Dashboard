using System.Text.Json.Serialization;

namespace ME221.Data.Models;

[JsonConverter(typeof(JsonStringEnumConverter<DataLinkFeedbackSeverity>))]
public enum DataLinkFeedbackSeverity
{
    Ok,
    Warning,
    Alarm
}

public sealed class DataLinkFeedback
{
    public DataLinkFeedbackSeverity Severity { get; init; }
    public float? MinValue { get; init; }
    public float? MaxValue { get; init; }
    public bool Flashing { get; init; }
}

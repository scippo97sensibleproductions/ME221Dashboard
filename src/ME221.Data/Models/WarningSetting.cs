using System.Text.Json.Serialization;

namespace ME221.Data.Models;

[JsonConverter(typeof(JsonStringEnumConverter<WarningSettingStatus>))]
public enum WarningSettingStatus
{
    Typical,
    Custom,
    Disabled
}

public sealed class DataLinkWarningSetting
{
    public int DataId { get; set; }
    public bool Enabled { get; set; }
    public float? MinWarning { get; set; }
    public float? MaxWarning { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public WarningSettingStatus Status { get; set; } = WarningSettingStatus.Typical;
}

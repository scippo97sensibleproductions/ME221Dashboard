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
    public List<WarningLevel> Levels { get; set; } = [];
    public List<WarningPoint> Points { get; set; } = [];
    public string? MigratedBoundsMarkerLevelId { get; set; }
    public bool MigratedBoundsMarkerSet { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public WarningSettingStatus Status { get; set; } = WarningSettingStatus.Typical;

    [Obsolete("Migrated to Levels/Points. Kept for deserialization of legacy configs.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("minWarning")]
    public float? MinWarning { get; set; }

    [Obsolete("Migrated to Levels/Points. Kept for deserialization of legacy configs.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("maxWarning")]
    public float? MaxWarning { get; set; }
}

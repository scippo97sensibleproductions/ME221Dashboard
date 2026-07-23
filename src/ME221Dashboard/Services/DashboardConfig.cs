using System.Collections.ObjectModel;
using ME221.Data.Models;

namespace ME221Dashboard.Services;

public sealed class DashboardConfig
{
    public string ActiveDashboard { get; set; } = "default";
    public int GridRows { get; set; } = 4;
    public int GridColumns { get; set; } = 7;
    public Dictionary<string, DashboardDefinition> Dashboards { get; init; } = new()
    {
        ["default"] = new()
    };
    public VehicleConfig? Vehicle { get; set; }
    public UserPreferences? Preferences { get; set; }
    public List<DataLinkWarningSetting> WarningSettings { get; set; } = [];
    public int WarningDelayMs { get; set; } = 500;
    public List<MonitoringPreset> MonitoringPresets { get; set; } = [];
}

public sealed class UserPreferences
{
    public ConnectionPreference? LastConnection { get; set; }
    public List<int> FavoriteTableIds { get; set; } = [];
    public List<int> RecentTableIds { get; set; } = [];
    public List<int> FavoriteDriverIds { get; set; } = [];
    public List<int> RecentDriverIds { get; set; } = [];
    public Dictionary<int, string> TableNotes { get; set; } = [];
    public bool UseLambdaMode { get; set; }
    public double StoichAfr { get; set; } = 14.7;
}

public sealed class ConnectionPreference
{
    public string Type { get; set; } = "tcp";
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? SerialPort { get; set; }
}

public sealed class DashboardDefinition
{
    public Collection<GaugeConfigEntry> Gauges { get; init; } = [];
    public Collection<DashboardTableEntry> Tables { get; set; } = [];
    public Dictionary<int, SensorCustomization> Customizations { get; init; } = [];
    public string? BackgroundImagePath { get; set; }
    public Dictionary<string, double>? ExtraData { get; init; }
    public bool HeaderVisible { get; set; } = true;
    public OdometerConfig? Odometer { get; set; }
    public VehicleConfig? Vehicle { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("WarningSettings")]
    [System.Obsolete("Migrated to DashboardConfig.WarningSettings. Kept for deserialization of old configs.")]
    public List<DataLinkWarningSetting>? LegacyWarningSettings { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("WarningDelayMs")]
    [System.Obsolete("Migrated to DashboardConfig.WarningDelayMs. Kept for deserialization of old configs.")]
    public int? LegacyWarningDelayMs { get; set; }
    public List<WarningHistoryEntry> WarningHistory { get; set; } = [];
}

public sealed class WarningHistoryEntry
{
    public int Id { get; set; }
    public int DataId { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Category { get; set; } = "";
    public double Value { get; set; }
    public string Severity { get; set; } = "warning";
    public double Threshold { get; set; }
    public string ThresholdType { get; set; } = "max";
    public long TriggeredAt { get; set; }
    public long? ClearedAt { get; set; }
}

public sealed class DashboardTableEntry
{
    public int TableId { get; set; }
    public double FractionX { get; set; } = 0.025;
    public double FractionY { get; set; } = 0.075;
    public double WidthFraction { get; set; } = 0.40;
    public double HeightFraction { get; set; } = 0.40;
    public int ZIndex { get; set; }
    public string? ColorScheme { get; set; }
    public bool? ShowLabels { get; set; }
    public bool? ShowDimensionBadge { get; set; }
    public int? TraceXLink { get; set; }
    public int? TraceYLink { get; set; }
}

public enum OdometerSpeedSource { Gps = 0, Vss = 1 }

public sealed class OdometerConfig
{
    public double CurrentValue { get; set; }
    public bool UseKilometers { get; set; } = true;
    public OdometerSpeedSource SpeedSource { get; set; } = OdometerSpeedSource.Gps;
    public bool VssSpeedInMph { get; set; } = false;
}

public sealed class VehicleConfig
{
    public bool Enabled { get; set; } = true;
    public double TireDiameterInches { get; set; } = 23;
    public double FinalDriveRatio { get; set; } = 4.3;
    public double[] GearRatios { get; set; } = [3.6, 2.2, 1.5, 1.1, 0.85, 0.7];
    public double WheelSlipPercent { get; set; } = 3;
    public int? RpmEntityId { get; set; }
    public int? VssSpeedEntityId { get; set; }
    public int? MapEntityId { get; set; }
    public int? BaroEntityId { get; set; }
    public int? GearEntityId { get; set; }
}

public sealed class MonitoringPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public List<int> DatalinkIds { get; set; } = [];
}

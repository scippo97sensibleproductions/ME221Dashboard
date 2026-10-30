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
    [System.Obsolete("Migrated to per-dashboard Vehicle + VehicleSeedTemplate. Kept for deserialization of old configs.")]
    public VehicleConfig? Vehicle { get; set; }
    public VehicleConfig? VehicleSeedTemplate { get; set; }
    public UserPreferences? Preferences { get; set; }
    public List<DataLinkWarningSetting> WarningSettings { get; set; } = [];
    public int WarningDelayMs { get; set; } = 500;
    public List<MonitoringPreset> MonitoringPresets { get; set; } = [];
    public List<BatchLedgerEntry> BatchLedger { get; set; } = [];
    public List<QueuedBanner> QueuedBanners { get; set; } = [];
    public List<UndoExpiryNotice> QueuedUndoExpiryNotices { get; set; } = [];
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
    public bool SidebarVisible { get; set; } = true;
    public bool LayoutLocked { get; set; }
    public OdometerConfig? Odometer { get; set; }
    public VehicleConfig? Vehicle { get; set; }
    public ShifterConfig? ShifterConfig { get; set; }
    /// <summary>Accumulated distance/fuel for the derived average-efficiency entity.</summary>
    public FuelTripConfig? FuelTrip { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("WarningSettings")]
    [System.Obsolete("Migrated to DashboardConfig.WarningSettings. Kept for deserialization of old configs.")]
    public List<DataLinkWarningSetting>? LegacyWarningSettings { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("WarningDelayMs")]
    [System.Obsolete("Migrated to DashboardConfig.WarningDelayMs. Kept for deserialization of old configs.")]
    public double? LegacyWarningDelayMs { get; set; }
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
    public int? SpeedEntityId { get; set; }
    public string SpeedUnit { get; set; } = "km/h";
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
    /// <summary>Fuel flow channel (L/hr) feeding the derived fuel efficiency, e.g. "Fuelfow Est." (ID 35).</summary>
    public int? FuelFlowEntityId { get; set; }
    /// <summary>Injector duty channel (%) used to calculate flow when no flow channel is mapped.</summary>
    public int? InjectorDutyEntityId { get; set; }
    /// <summary>Per-injector nominal flow (cc/min); zero disables the duty-based calculation.</summary>
    public double InjectorFlowCcPerMin { get; set; }
    /// <summary>Cylinder count used with injector duty to calculate fuel flow.</summary>
    public int CylinderCount { get; set; } = 4;
    /// <summary>Output unit of the derived fuel efficiency: "l100km", "kmL" or "mpg".</summary>
    public string FuelEfficiencyUnit { get; set; } = "kmL";
    /// <summary>Speed source for the fuel efficiency: "auto" (VSS→GPS), "gps" or "trueSpeed".</summary>
    public string FuelSpeedSource { get; set; } = "auto";
    /// <summary>ECU clock/date source mapping and display options.</summary>
    public RtcConfig? Rtc { get; set; }
}

/// <summary>
/// Source mapping for the ECU real-time clock, which the firmware exposes as
/// separate datalinks (status, day, month, year, hour, minutes, seconds).
/// </summary>
public sealed class RtcConfig
{
    /// <summary>Clock status channel; 0 is the firmware's "Err - Not Init" and withholds the clock.</summary>
    public int? StatusEntityId { get; set; }
    public int? DayEntityId { get; set; }
    public int? MonthEntityId { get; set; }
    public int? YearEntityId { get; set; }
    public int? HourEntityId { get; set; }
    public int? MinuteEntityId { get; set; }
    public int? SecondEntityId { get; set; }
    /// <summary>Render the clock as 12-hour with an AM/PM suffix.</summary>
    public bool Hour12 { get; set; }
    /// <summary>Include seconds in the clock text.</summary>
    public bool ShowSeconds { get; set; } = true;

    public RtcConfig Clone() => (RtcConfig)MemberwiseClone();
}

/// <summary>
/// Persisted trip totals behind the average fuel-efficiency entity. Distance is
/// in kilometers and fuel in liters regardless of the display unit, so changing
/// the unit never invalidates an in-progress trip.
/// </summary>
public sealed class FuelTripConfig
{
    public double DistanceKm { get; set; }
    public double FuelLiters { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

public sealed class ShifterConfig
{
    public double ShiftPointRpm { get; set; }
    public double DownshiftFloorRpm { get; set; }
}

public sealed class MonitoringPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public List<int> DatalinkIds { get; set; } = [];
}

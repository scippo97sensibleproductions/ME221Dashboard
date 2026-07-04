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

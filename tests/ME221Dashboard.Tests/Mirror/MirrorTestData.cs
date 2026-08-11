using ME221.Data.Models;
using ME221Dashboard.Services;
using ME221Dashboard.Services.Mirror;

namespace ME221Dashboard.Tests.Mirror;

/// <summary>Shared deterministic snapshot content for the golden fixture and protocol tests.</summary>
public static class MirrorTestData
{
    public static MirrorSnapshot SampleSnapshot(long seq = 42)
    {
        return new MirrorSnapshot
        {
            Seq = seq,
            AppVersion = "1.2.3",
            EcuConnected = true,
            EcuInfo = new DeviceInfoMetadata { ProductName = "ME221", ModelName = "MX5", Version = "2.0" },
            Calibration = new CalibrationData
            {
                Metadata = new DeviceInfoMetadata { ProductName = "ME221", ModelName = "MX5", Version = "2.0" },
                DataLinks =
                [
                    new DataLinkDefinition { Id = 1, Name = "RPM", Category = "Engine", ViewInTree = true, StandardLogging = true, MeasureUnit = "rpm", MinValue = 0, MaxValue = 9000 },
                ],
                Tables =
                [
                    new TableDefinition { Id = 10, Name = "Ignition", Category = "Timing", ViewInTree = true, Enabled = true, TableType = "2D", Cols = 1, Rows = 4, Input0LinkId = 2, OutputLinkId = 10, OutputName = "deg", IncrementValue = 1, Input0 = [1000, 2000, 3000, 4000], Output = [10, 12, 15, 18] },
                ],
                Drivers =
                [
                    new DriverDefinition { Id = 20, Name = "Test Driver", Category = "Outputs", ViewInTree = true, NumberOfConfigs = 1, Configs = [new DriverParamDefinition { Name = "P1", ParamType = "float", Value = 0.5f, Min = 0, Max = 1, CheckRange = true }] },
                ],
            },
            DashboardConfig = new DashboardConfig
            {
                ActiveDashboard = "default",
                GridRows = 4,
                GridColumns = 7,
                Preferences = PreferenceDefaults(),
                WarningSettings = [],
                Dashboards = new Dictionary<string, DashboardDefinition>
                {
                    ["default"] = new()
                    {
                        Gauges = [new GaugeConfigEntry { Id = 3, ShapeCategory = 0, DisplayType = 0, GridRow = 0, GridColumn = 0, ShowName = true }],
                        Tables = [new DashboardTableEntry { TableId = 10, FractionX = 0.025, FractionY = 0.075 }],
                        HeaderVisible = true,
                        SidebarVisible = true,
                        WarningHistory = [],
                    },
                },
            },
            Images = new Dictionary<string, string>
            {
                ["C:\\me221\\images\\bg.png"] = "aGVsbG8=",
            },
        };
    }

    private static UserPreferences PreferenceDefaults() =>
        new() { LastConnection = new ConnectionPreference { Type = "tcp", Host = "192.168.1.5", Port = 22100 } };
}
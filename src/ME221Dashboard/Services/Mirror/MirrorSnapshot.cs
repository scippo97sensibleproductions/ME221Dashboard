using ME221.Data.Models;

namespace ME221Dashboard.Services.Mirror;

/// <summary>
/// Full A-to-B snapshot (R6, KTD-4). Carries the complete calibration state — data
/// links, all table and driver data — plus dashboard definitions (active dashboard,
/// warning configuration and history, preferences, per-dashboard odometer), ECU info,
/// and embedded image bytes so B never touches its own filesystem for mirrored reads.
/// </summary>
public sealed class MirrorSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public long Seq { get; set; }
    public string AppVersion { get; set; } = "";
    public bool EcuConnected { get; set; }
    public DeviceInfoMetadata? EcuInfo { get; set; }
    public CalibrationData Calibration { get; set; } = new();
    public DashboardConfig DashboardConfig { get; set; } = new();
    /// <summary>Image path (as referenced by dashboards/gauges) → base64 bytes.</summary>
    public Dictionary<string, string> Images { get; set; } = [];
}
using ME221.Data.Models;

namespace ME221Dashboard.Services.Mirror;

/// <summary>
/// B's applied-snapshot store (U7/U8). Holds the full MirrorSnapshot once applied and
/// answers the mirror-served read surface in companion mode (R9). Odometer and recent
/// log tails update from relayed values; nothing here is ever persisted on B.
/// </summary>
public sealed class MirrorSnapshotStore
{
    private MirrorSnapshot? _snapshot;
    private double _odometerValue;
    private bool _odometerKm = true;
    private readonly List<string> _recentLogs = new(200);

    public bool HasSnapshot => _snapshot is not null;
    public long SnapshotSeq => _snapshot?.Seq ?? 0;

    public void Apply(MirrorSnapshot snapshot)
    {
        _snapshot = snapshot;
        var active = snapshot.DashboardConfig.Dashboards.GetValueOrDefault(snapshot.DashboardConfig.ActiveDashboard);
        _odometerValue = active?.Odometer?.CurrentValue ?? 0;
        _odometerKm = active?.Odometer?.UseKilometers ?? true;
    }

    public void Clear()
    {
        _snapshot = null;
        _recentLogs.Clear();
    }

    public void UpdateOdometer(double value, bool useKilometers)
    {
        _odometerValue = value;
        _odometerKm = useKilometers;
    }

    public (double Value, bool UseKilometers) Odometer => (_odometerValue, _odometerKm);

    public void AppendLog(string json)
    {
        _recentLogs.Add(json);
        while (_recentLogs.Count > 200)
            _recentLogs.RemoveAt(0);
    }

    public IReadOnlyList<string> RecentLogs => _recentLogs;

    // ── Mirror-served reads ──────────────────────────────────────────────────

    public CalibrationData? Calibration => _snapshot?.Calibration;
    public DashboardConfig? DashboardConfig => _snapshot?.DashboardConfig;

    public List<TableDefinition>? Tables => _snapshot?.Calibration.Tables;
    public List<DriverDefinition>? Drivers => _snapshot?.Calibration.Drivers;
    public List<DataLinkDefinition>? DataLinks => _snapshot?.Calibration.DataLinks;

    public TableDefinition? GetTable(ushort id) =>
        _snapshot?.Calibration.Tables.FirstOrDefault(t => t.Id == id);

    public DriverDefinition? GetDriver(ushort id) =>
        _snapshot?.Calibration.Drivers.FirstOrDefault(d => d.Id == id);

    public DashboardDefinition? GetDashboard(string name) =>
        _snapshot?.DashboardConfig.Dashboards.GetValueOrDefault(name);

    public IEnumerable<string> DashboardNames =>
        _snapshot is not null ? _snapshot.DashboardConfig.Dashboards.Keys : [];

    public VehicleConfig? GetVehicleConfig(string dashboardName) =>
        _snapshot?.DashboardConfig.Dashboards.GetValueOrDefault(dashboardName)?.Vehicle
        ?? _snapshot?.DashboardConfig.VehicleSeedTemplate;

    public List<DataLinkWarningSetting> WarningSettings =>
        _snapshot?.DashboardConfig.WarningSettings ?? [];

    public List<WarningHistoryEntry> WarningHistory(string dashboardName) =>
        _snapshot?.DashboardConfig.Dashboards.GetValueOrDefault(dashboardName)?.WarningHistory ?? [];

    public List<QueuedBanner> QueuedNotices =>
        _snapshot?.DashboardConfig.QueuedBanners ?? [];

    /// <summary>Resolve an image by its dashboard-referenced path (never B's filesystem, R9).</summary>
    public string? GetImageBase64(string path) =>
        _snapshot?.Images.TryGetValue(path, out var b64) == true ? b64 : null;
}
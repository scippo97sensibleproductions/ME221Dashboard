using ME221.Data.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services.Mirror;

/// <summary>
/// Builds the full A-to-B snapshot (U3, KTD-4): calibration (all table and driver
/// data), dashboard definitions (active dashboard, warning config/history,
/// preferences, odometer), ECU info, and embedded image bytes read from the paths
/// referenced by dashboards and gauges (the .mez precedent).
/// </summary>
public sealed class MirrorSnapshotSerializer(
    ICalibrationService calibration,
    IEcuConnectionService connection,
    Func<string>? appVersion = null,
    ILogger<MirrorSnapshotSerializer>? logger = null)
{
    private readonly ILogger<MirrorSnapshotSerializer> _logger = logger ?? NullLogger<MirrorSnapshotSerializer>.Instance;

    public async Task<MirrorSnapshot> BuildAsync(long seq)
    {
        CalibrationData? calibrationData = null;
        DashboardConfig? dashboardConfig = null;
        try
        {
            var cal = await calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            if (cal.Type == CalibrationResultType.Found)
                calibrationData = cal.Data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load calibration for the mirror snapshot");
        }

        try
        {
            dashboardConfig = await calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load dashboard config for the mirror snapshot");
        }

        var ecuInfo = connection.ProtocolInfo is { } info && !string.IsNullOrEmpty(info.ProductName)
            ? new DeviceInfoMetadata { ProductName = info.ProductName, ModelName = info.ModelName, Version = info.FirmwareVersion }
            : null;

        var images = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (dashboardConfig is not null)
            CollectImages(dashboardConfig, images);

        return new MirrorSnapshot
        {
            Seq = seq,
            AppVersion = appVersion?.Invoke() ?? AppInfo.Current.VersionString,
            EcuConnected = connection.State == ConnectionState.Connected,
            EcuInfo = ecuInfo,
            Calibration = calibrationData ?? new CalibrationData(),
            DashboardConfig = dashboardConfig ?? new DashboardConfig(),
            Images = images,
        };
    }

    private void CollectImages(DashboardConfig config, Dictionary<string, string> images)
    {
        foreach (var dashboard in config.Dashboards.Values)
        {
            TryAddImage(dashboard.BackgroundImagePath, images);
            foreach (var gauge in dashboard.Gauges)
                TryAddImage(gauge.TexturePath, images);
        }
    }

    private void TryAddImage(string? path, Dictionary<string, string> images)
    {
        if (string.IsNullOrEmpty(path) || images.ContainsKey(path))
            return;
        try
        {
            if (!File.Exists(path))
                return;
            images[path] = Convert.ToBase64String(File.ReadAllBytes(path));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to embed image '{Path}' into the mirror snapshot", path);
        }
    }
}
using ME221.Comms;
using ME221.Data.Models;
using ME221Dashboard.Services.Mirror;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services;

/// <summary>
/// Companion-mode calibration source (U7, R9): while <see cref="MirrorSnapshotStore"/>
/// holds an applied snapshot, reads answer from it and saves are dropped — B's local
/// data ground is never touched. All existing read methods keep their exact JSON
/// shapes because they read through the same ICalibrationService seam.
/// </summary>
internal sealed class MirrorCalibrationSource(
    ICalibrationService inner,
    MirrorSnapshotStore store,
    ILogger<HybridBridgeService> logger)
    : ICalibrationService
{
    public Task<CalibrationData> LoadAndParseAsync(Stream mefwStream) =>
        inner.LoadAndParseAsync(mefwStream);

    public Task<CalibrationResult> GetPersistedCalibrationAsync() =>
        Task.FromResult(new CalibrationResult(
            store.HasSnapshot ? CalibrationResultType.Found : CalibrationResultType.NotFound,
            store.Calibration));

    public Task<DashboardConfig?> GetPersistedDashboardConfigAsync() =>
        Task.FromResult(store.HasSnapshot ? store.DashboardConfig : null);

    public Task SaveCalibrationAsync(CalibrationData calibration) =>
        inner.SaveCalibrationAsync(calibration);

    public Task SaveDashboardConfigAsync(DashboardConfig config) =>
        inner.SaveDashboardConfigAsync(config);

    public Task<(string Product, string Model, string Version)> GetEcuInfoAsync(ProtocolService protocol) =>
        inner.GetEcuInfoAsync(protocol);

    public bool MatchesEcu(CalibrationData calibration, string product, string model, string version) =>
        inner.MatchesEcu(calibration, product, model, version);
}
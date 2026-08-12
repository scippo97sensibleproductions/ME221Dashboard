using ME221.Comms;
using ME221.Data.Models;
using ME221Dashboard.Services.Mirror;

namespace ME221Dashboard.Services;

/// <summary>
/// Companion-mode calibration source (U7, R9): while <see cref="MirrorSnapshotStore"/>
/// holds an applied snapshot, reads answer from it. Writes delegate to the real
/// service — the bridge's GateWrite blocks gated writes before they reach here, and
/// B-local exception writes must persist on B (KTD-5).
/// </summary>
internal sealed class MirrorCalibrationSource(
    ICalibrationService inner,
    MirrorSnapshotStore store)
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
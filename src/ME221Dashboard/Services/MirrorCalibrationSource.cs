using ME221.Comms;
using ME221.Data.Models;
using ME221Dashboard.Services.Mirror;

namespace ME221Dashboard.Services;

/// <summary>
/// Companion-mode calibration source (U7, R9): while <see cref="MirrorSnapshotStore"/>
/// holds an applied snapshot, reads answer from it. Gated writes never reach this
/// source, B-local exception writes persist via <c>_realCalibration</c>, and any
/// dashboard-config write that does land here throws (defense in depth — the mirror
/// must never write A's snapshot onto B's disk, KTD-5).
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

    /// <summary>
    /// Writes through the mirror are a bug: gated writes return before reaching
    /// here, and B-local exception writes route through _realCalibration. Throw
    /// loudly rather than persisting A's snapshot over B's config.
    /// </summary>
    public Task SaveDashboardConfigAsync(DashboardConfig config) =>
        throw new InvalidOperationException(
            "Dashboard config write reached the mirror source — companion-mode writes must " +
            "be gated or routed to the local store.");

    public Task<(string Product, string Model, string Version)> GetEcuInfoAsync(ProtocolService protocol) =>
        inner.GetEcuInfoAsync(protocol);

    public bool MatchesEcu(CalibrationData calibration, string product, string model, string version) =>
        inner.MatchesEcu(calibration, product, model, version);
}
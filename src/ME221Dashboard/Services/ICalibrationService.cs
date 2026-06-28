using ME221.Comms;
using ME221.Data.Models;

namespace ME221Dashboard.Services;

public interface ICalibrationService
{
    Task<CalibrationData> LoadAndParseAsync(Stream mefwStream);
    Task<CalibrationResult> GetPersistedCalibrationAsync();
    Task<DashboardConfig?> GetPersistedDashboardConfigAsync();
    Task SaveCalibrationAsync(CalibrationData calibration);
    Task SaveDashboardConfigAsync(DashboardConfig config);
    Task<(string Product, string Model, string Version)> GetEcuInfoAsync(ProtocolService protocol);
    bool MatchesEcu(CalibrationData calibration, string product, string model, string version);
}

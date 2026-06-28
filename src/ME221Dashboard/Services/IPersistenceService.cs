using ME221.Data.Models;

namespace ME221Dashboard.Services;

public enum CalibrationResultType
{
    Found,
    NotFound,
    Corrupt
}

public record CalibrationResult(CalibrationResultType Type, CalibrationData? Data = null, string? BackupPath = null);

public interface IPersistenceService
{
    Task<CalibrationResult> LoadCalibrationAsync();
    Task SaveCalibrationAsync(CalibrationData calibration);
    Task<DashboardConfig?> LoadDashboardConfigAsync();
    Task SaveDashboardConfigAsync(DashboardConfig config);
}

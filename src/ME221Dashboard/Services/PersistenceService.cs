using System.Text.Json;
using ME221.Data.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services;

public sealed class PersistenceService(ILogger<PersistenceService>? logger = null) : IPersistenceService
{
    private readonly ILogger<PersistenceService> _logger = logger ?? NullLogger<PersistenceService>.Instance;

    private static string BasePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ME221");

    private static string CalibrationPath => Path.Combine(BasePath, "calibration.json");
    private static string ConfigPath => Path.Combine(BasePath, "dashboard-config.json");

    public async Task<CalibrationResult> LoadCalibrationAsync()
    {
        try
        {
            if (!File.Exists(CalibrationPath))
                return new CalibrationResult(CalibrationResultType.NotFound);

            var json = await File.ReadAllTextAsync(CalibrationPath).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize(json, V2JsonContext.Default.CalibrationData);

            if (data?.Metadata == null)
            {
                _logger.LogWarning("Calibration file found but Metadata was null.");
                return new CalibrationResult(CalibrationResultType.Corrupt, BackupPath: CreateBackup());
            }

            return new CalibrationResult(CalibrationResultType.Found, data);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize calibration.json");
            return new CalibrationResult(CalibrationResultType.Corrupt, BackupPath: CreateBackup());
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to read calibration.json");
            return new CalibrationResult(CalibrationResultType.Corrupt, BackupPath: CreateBackup());
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied reading calibration.json");
            return new CalibrationResult(CalibrationResultType.Corrupt, BackupPath: CreateBackup());
        }
    }

    public async Task SaveCalibrationAsync(CalibrationData calibration)
    {
        try
        {
            Directory.CreateDirectory(BasePath);
            string json = JsonSerializer.Serialize(calibration, V2JsonContext.Default.CalibrationData);
            var tmpPath = CalibrationPath + ".tmp";
            await File.WriteAllTextAsync(tmpPath, json).ConfigureAwait(false);
            File.Move(tmpPath, CalibrationPath, overwrite: true);
            _logger.LogDebug("Saved calibration successfully.");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "SaveCalibrationAsync failed.");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "SaveCalibrationAsync failed.");
            throw;
        }
    }

    public async Task<DashboardConfig?> LoadDashboardConfigAsync()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                _logger.LogInformation("[PERSIST] LoadDashboardConfigAsync: file not found at {Path}", ConfigPath);
                return null;
            }
            var json = await File.ReadAllTextAsync(ConfigPath).ConfigureAwait(false);
            _logger.LogInformation("[PERSIST] LoadDashboardConfigAsync: read {Length} bytes from {Path}", json.Length, ConfigPath);
            var config = JsonSerializer.Deserialize(json, V2JsonContext.Default.DashboardConfig);
            _logger.LogInformation("[PERSIST] LoadDashboardConfigAsync: deserialized ok, dashboards={DashCount}", config?.Dashboards?.Count ?? -1);
            if (config?.Dashboards != null)
            {
                foreach (var kv in config.Dashboards)
                {
                    _logger.LogInformation("[PERSIST] LoadDashboardConfigAsync: dashboard '{Name}' has Vehicle={HasVehicle}", kv.Key, kv.Value.Vehicle == null ? "NULL" : "HAS VALUE");
                }
            }
            return config;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "LoadDashboardConfigAsync failed.");
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "LoadDashboardConfigAsync failed.");
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "LoadDashboardConfigAsync failed.");
            return null;
        }
    }

    private static readonly SemaphoreSlim _saveLock = new(1, 1);

    public async Task SaveDashboardConfigAsync(DashboardConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(BasePath);
            string json = JsonSerializer.Serialize(config, V2JsonContext.Default.DashboardConfig);
            _logger.LogInformation("[PERSIST] SaveDashboardConfigAsync: serializing {Length} bytes", json.Length);
            if (config.Dashboards != null)
            {
                foreach (var kv in config.Dashboards)
                {
                    _logger.LogInformation("[PERSIST] SaveDashboardConfigAsync: dashboard '{Name}' has Vehicle={HasVehicle}", kv.Key, kv.Value.Vehicle == null ? "NULL" : "HAS VALUE");
                }
            }
            var tmpPath = ConfigPath + $".tmp.{Environment.ProcessId}.{DateTime.UtcNow.Ticks}";
            await File.WriteAllTextAsync(tmpPath, json).ConfigureAwait(false);
            File.Move(tmpPath, ConfigPath, overwrite: true);
            _logger.LogInformation("[PERSIST] SaveDashboardConfigAsync: wrote to {Path}", ConfigPath);
        }
        catch (IOException ex) { _logger.LogError(ex, "SaveDashboardConfigAsync: IOException"); throw; }
        catch (UnauthorizedAccessException ex) { _logger.LogError(ex, "SaveDashboardConfigAsync: UnauthorizedAccess"); throw; }
        finally { _saveLock.Release(); }
    }

    private static string CreateBackup()
    {
        if (!File.Exists(CalibrationPath)) return string.Empty;
        string backupPath = CalibrationPath + $".corrupt.{DateTime.Now:yyyyMMdd-HHmmss}";
        try { File.Move(CalibrationPath, backupPath); return backupPath; }
        catch (IOException) { return string.Empty; }
    }
}

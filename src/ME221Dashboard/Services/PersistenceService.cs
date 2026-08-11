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
            // Open with FILE_SHARE_DELETE so a concurrent save's atomic tmp+move replace
            // never fails on a reader holding the old handle (and never tears our read —
            // the OS keeps the old file alive until this handle closes).
            using (var fs = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            using (var reader = new StreamReader(fs))
            {
                var json = await reader.ReadToEndAsync().ConfigureAwait(false);
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
        => await SaveDashboardConfigAsync(config, ConfigPath).ConfigureAwait(false);

    internal async Task SaveDashboardConfigAsync(DashboardConfig config, string configPath)
    {
        ArgumentNullException.ThrowIfNull(config);
        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(BasePath);

            // Clobber guard (KTD 2026-08-10): a transient load failure makes callers
            // fall back to `new DashboardConfig()`, which contains only the untouched
            // 'default' dashboard. Saving that copy over a config with real dashboards
            // silently erases the user's work — observed live at 23:58:36 where a
            // SaveWarningHistory save landed 8 ms after a layout save and nuked
            // 'Normal Dash'. Refuse the write; the config survives and the next save
            // carries the real content again.
            if (IsFreshDefaultOnly(config) && HasRealDashboards(configPath))
            {
                _logger.LogWarning(
                    "[PERSIST] SaveDashboardConfigAsync BLOCKED: refusing to overwrite a config with real dashboards using a fresh default-only copy (load-failure clobber guard). Active={Active}, dashboards={Count}",
                    config.ActiveDashboard, config.Dashboards.Count);
                return;
            }

            string json = JsonSerializer.Serialize(config, V2JsonContext.Default.DashboardConfig);
            _logger.LogInformation("[PERSIST] SaveDashboardConfigAsync: serializing {Length} bytes", json.Length);
            if (config.Dashboards != null)
            {
                foreach (var kv in config.Dashboards)
                {
                    _logger.LogInformation("[PERSIST] SaveDashboardConfigAsync: dashboard '{Name}' has Vehicle={HasVehicle}", kv.Key, kv.Value.Vehicle == null ? "NULL" : "HAS VALUE");
                }
            }
            var tmpPath = configPath + $".tmp.{Environment.ProcessId}.{DateTime.UtcNow.Ticks}";
            await File.WriteAllTextAsync(tmpPath, json).ConfigureAwait(false);
            File.Move(tmpPath, configPath, overwrite: true);
            _logger.LogInformation("[PERSIST] SaveDashboardConfigAsync: wrote to {Path}", configPath);
        }
        catch (IOException ex) { _logger.LogError(ex, "SaveDashboardConfigAsync: IOException"); throw; }
        catch (UnauthorizedAccessException ex) { _logger.LogError(ex, "SaveDashboardConfigAsync: UnauthorizedAccess"); throw; }
        finally { _saveLock.Release(); }
    }

    /// <summary>
    /// True when the config has the untouched "fresh install" shape: a single empty
    /// 'default' dashboard and nothing else. Only <c>new DashboardConfig()</c> (i.e. a
    /// caller that failed to load) produces this shape — real user configs always carry
    /// content on their dashboard.
    /// </summary>
    internal static bool IsFreshDefaultOnly(DashboardConfig config)
    {
        if (!string.Equals(config.ActiveDashboard, "default", StringComparison.Ordinal))
            return false;
        if (config.Dashboards.Count != 1 || !config.Dashboards.TryGetValue("default", out var def))
            return false;
        return def.Gauges.Count == 0
            && def.Tables.Count == 0
            && def.Customizations.Count == 0
            && def.Vehicle is null
            && def.Odometer is null
            && def.ShifterConfig is null
            && def.WarningHistory.Count == 0
            && string.IsNullOrEmpty(def.BackgroundImagePath);
    }

    internal static bool HasRealDashboards(string configPath)
    {
        if (!File.Exists(configPath)) return false;
        try
        {
            var json = File.ReadAllText(configPath);
            var existing = JsonSerializer.Deserialize(json, V2JsonContext.Default.DashboardConfig);
            return existing?.Dashboards?.Values.Any(d => d.Gauges is { Count: > 0 } || d.Tables is { Count: > 0 }) ?? false;
        }
        catch
        {
            return false;
        }
    }

    private static string CreateBackup()
    {
        if (!File.Exists(CalibrationPath)) return string.Empty;
        string backupPath = CalibrationPath + $".corrupt.{DateTime.Now:yyyyMMdd-HHmmss}";
        try { File.Move(CalibrationPath, backupPath); return backupPath; }
        catch (IOException) { return string.Empty; }
    }
}

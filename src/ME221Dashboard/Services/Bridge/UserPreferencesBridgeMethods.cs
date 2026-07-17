using System.Text.Json;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── User Preferences ──────────────────────────────────────────────────

    private async Task<UserPreferences> GetPreferencesAsync()
    {
        var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
        return config?.Preferences ?? new UserPreferences();
    }

    private async Task SavePreferencesAsync(UserPreferences prefs)
    {
        var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();
        config.Preferences = prefs;
        await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
    }

    /// <summary>
    /// Get last connection parameters for auto-reconnect.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetLastConnection')
    /// </summary>
    public async Task<string> GetLastConnection()
    {
        try
        {
            var prefs = await GetPreferencesAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(prefs.LastConnection, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREFS] GetLastConnection FAILED");
            return JsonSerializer.Serialize(new ConnectionPreference());
        }
    }

    /// <summary>
    /// Save last connection parameters.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveLastConnection', [json])
    /// </summary>
    public async Task<string> SaveLastConnection(string jsonPayload)
    {
        try
        {
            var conn = JsonSerializer.Deserialize<ConnectionPreference>(jsonPayload, SJsonOptions);
            var prefs = await GetPreferencesAsync().ConfigureAwait(false);
            prefs.LastConnection = conn;
            await SavePreferencesAsync(prefs).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREFS] SaveLastConnection FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get favorite table IDs.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetFavoriteTables')
    /// </summary>
    public async Task<string> GetFavoriteTables()
    {
        try
        {
            var prefs = await GetPreferencesAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(prefs.FavoriteTableIds, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREFS] GetFavoriteTables FAILED");
            return JsonSerializer.Serialize(Array.Empty<int>());
        }
    }

    /// <summary>
    /// Save favorite table IDs.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveFavoriteTables', [json])
    /// </summary>
    public async Task<string> SaveFavoriteTables(string jsonPayload)
    {
        try
        {
            var ids = JsonSerializer.Deserialize<List<int>>(jsonPayload, SJsonOptions) ?? [];
            var prefs = await GetPreferencesAsync().ConfigureAwait(false);
            prefs.FavoriteTableIds = ids;
            await SavePreferencesAsync(prefs).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREFS] SaveFavoriteTables FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get recent table IDs.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetRecentTables')
    /// </summary>
    public async Task<string> GetRecentTables()
    {
        try
        {
            var prefs = await GetPreferencesAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(prefs.RecentTableIds, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREFS] GetRecentTables FAILED");
            return JsonSerializer.Serialize(Array.Empty<int>());
        }
    }

    /// <summary>
    /// Save recent table IDs.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveRecentTables', [json])
    /// </summary>
    public async Task<string> SaveRecentTables(string jsonPayload)
    {
        try
        {
            var ids = JsonSerializer.Deserialize<List<int>>(jsonPayload, SJsonOptions) ?? [];
            var prefs = await GetPreferencesAsync().ConfigureAwait(false);
            prefs.RecentTableIds = ids;
            await SavePreferencesAsync(prefs).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREFS] SaveRecentTables FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get favorite driver IDs.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetFavoriteDrivers')
    /// </summary>
    public async Task<string> GetFavoriteDrivers()
    {
        try
        {
            var prefs = await GetPreferencesAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(prefs.FavoriteDriverIds, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREFS] GetFavoriteDrivers FAILED");
            return JsonSerializer.Serialize(Array.Empty<int>());
        }
    }

    /// <summary>
    /// Save favorite driver IDs.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveFavoriteDrivers', [json])
    /// </summary>
    public async Task<string> SaveFavoriteDrivers(string jsonPayload)
    {
        try
        {
            var ids = JsonSerializer.Deserialize<List<int>>(jsonPayload, SJsonOptions) ?? [];
            var prefs = await GetPreferencesAsync().ConfigureAwait(false);
            prefs.FavoriteDriverIds = ids;
            await SavePreferencesAsync(prefs).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREFS] SaveFavoriteDrivers FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get recent driver IDs.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetRecentDrivers')
    /// </summary>
    public async Task<string> GetRecentDrivers()
    {
        try
        {
            var prefs = await GetPreferencesAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(prefs.RecentDriverIds, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREFS] GetRecentDrivers FAILED");
            return JsonSerializer.Serialize(Array.Empty<int>());
        }
    }

    /// <summary>
    /// Save recent driver IDs.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveRecentDrivers', [json])
    /// </summary>
    public async Task<string> SaveRecentDrivers(string jsonPayload)
    {
        try
        {
            var ids = JsonSerializer.Deserialize<List<int>>(jsonPayload, SJsonOptions) ?? [];
            var prefs = await GetPreferencesAsync().ConfigureAwait(false);
            prefs.RecentDriverIds = ids;
            await SavePreferencesAsync(prefs).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREFS] SaveRecentDrivers FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    // ─── Lambda / AFR Mode ────────────────────────────────────────────────

    /// <summary>
    /// Get AFR/Lambda display mode settings.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetLambdaSettings')
    /// </summary>
    public async Task<string> GetLambdaSettings()
    {
        try
        {
            var prefs = await GetPreferencesAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(new { useLambdaMode = prefs.UseLambdaMode, stoichAfr = prefs.StoichAfr }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREFS] GetLambdaSettings FAILED");
            return JsonSerializer.Serialize(new { useLambdaMode = false, stoichAfr = 14.7 });
        }
    }

    /// <summary>
    /// Save AFR/Lambda display mode settings.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveLambdaSettings', [json])
    /// </summary>
    public async Task<string> SaveLambdaSettings(string jsonPayload)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<LambdaSettings>(jsonPayload, SJsonOptions) ?? new();
            var prefs = await GetPreferencesAsync().ConfigureAwait(false);
            prefs.UseLambdaMode = settings.UseLambdaMode;
            prefs.StoichAfr = settings.StoichAfr > 0 ? settings.StoichAfr : 14.7;
            await SavePreferencesAsync(prefs).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREFS] SaveLambdaSettings FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private sealed class LambdaSettings
    {
        public bool UseLambdaMode { get; set; }
        public double StoichAfr { get; set; } = 14.7;
    }

    /// <summary>
    /// Get all table notes.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetTableNotes')
    /// </summary>
    public async Task<string> GetTableNotes()
    {
        try
        {
            var prefs = await GetPreferencesAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(prefs.TableNotes, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREFS] GetTableNotes FAILED");
            return JsonSerializer.Serialize(new Dictionary<int, string>());
        }
    }

    /// <summary>
    /// Save all table notes (replaces entire dictionary).
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveTableNotes', [json])
    /// </summary>
    public async Task<string> SaveTableNotes(string jsonPayload)
    {
        try
        {
            var notes = JsonSerializer.Deserialize<Dictionary<int, string>>(jsonPayload, SJsonOptions) ?? [];
            var prefs = await GetPreferencesAsync().ConfigureAwait(false);
            prefs.TableNotes = notes;
            await SavePreferencesAsync(prefs).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PREFS] SaveTableNotes FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}

using System.Text.Json;
using ME221.Data.Models;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── Warning Centre ────────────────────────────────────────────────────

    /// <summary>
    /// Get warning settings for the active dashboard, merged with DEF XML feedback defaults.
    /// Persisted user settings take precedence over DEF XML defaults.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetWarningSettings')
    /// </summary>
    public async Task<string> GetWarningSettings()
    {
        try
        {
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);

            // Migrate: pull per-dashboard warnings into global config on first read
            if (config != null && config.WarningSettings.Count == 0)
            {
                foreach (var dashboard in config.Dashboards.Values)
                {
                    if (dashboard.LegacyWarningSettings is { Count: > 0 })
                    {
                        config.WarningSettings = dashboard.LegacyWarningSettings;
                        dashboard.LegacyWarningSettings = null;
                        await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
                        break;
                    }
                }
            }

            var persisted = config?.WarningSettings ?? [];

            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            var dataLinks = calResult.Data?.DataLinks ?? [];

            var persistedById = persisted.ToDictionary(s => s.DataId);
            var result = new List<DataLinkWarningSetting>();

            foreach (var dl in dataLinks)
            {
                if (persistedById.TryGetValue((int)dl.Id, out var existing))
                {
                    result.Add(existing);
                    continue;
                }

                if (dl.Feedbacks is { Count: > 0 })
                {
                    var warning = dl.Feedbacks
                        .Where(f => f.Severity == DataLinkFeedbackSeverity.Warning || f.Severity == DataLinkFeedbackSeverity.Alarm)
                        .OrderBy(f => f.Severity)
                        .FirstOrDefault();

                    if (warning is not null)
                    {
                        result.Add(new DataLinkWarningSetting
                        {
                            DataId = (int)dl.Id,
                            Enabled = true,
                            MinWarning = warning.MinValue,
                            MaxWarning = warning.MaxValue,
                            Name = dl.Name,
                            Unit = dl.MeasureUnit,
                            Category = dl.Category,
                            Status = WarningSettingStatus.Typical,
                        });
                    }
                }
            }

            return JsonSerializer.Serialize(result, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WARN] GetWarningSettings FAILED");
            return JsonSerializer.Serialize(Array.Empty<DataLinkWarningSetting>());
        }
    }

    /// <summary>
    /// Save warning settings to the active dashboard.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveWarningSettings', [json])
    /// </summary>
    public async Task<string> SaveWarningSettings(string settingsJson)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<List<DataLinkWarningSetting>>(settingsJson, SJsonOptions) ?? [];
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();

            config.WarningSettings = settings;
            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            _logger.LogInformation("[WARN] SaveWarningSettings: saved {Count} settings", settings.Count);

            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WARN] SaveWarningSettings FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get warning preset defaults from DEF XML feedback thresholds.
    /// Returns empty list if no calibration is loaded or no feedbacks are defined.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetDefXmlDefaults')
    /// </summary>
    public async Task<string> GetDefXmlDefaults()
    {
        try
        {
            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            var dataLinks = calResult.Data?.DataLinks ?? [];
            var defaults = new List<DataLinkWarningSetting>();

            foreach (var dl in dataLinks)
            {
                if (dl.Feedbacks is not { Count: > 0 }) continue;

                var warning = dl.Feedbacks
                    .Where(f => f.Severity == DataLinkFeedbackSeverity.Warning || f.Severity == DataLinkFeedbackSeverity.Alarm)
                    .OrderBy(f => f.Severity)
                    .FirstOrDefault();

                if (warning is null) continue;

                defaults.Add(new DataLinkWarningSetting
                {
                    DataId = (int)dl.Id,
                    Enabled = true,
                    MinWarning = warning.MinValue,
                    MaxWarning = warning.MaxValue,
                    Name = dl.Name,
                    Unit = dl.MeasureUnit,
                    Category = dl.Category,
                    Status = WarningSettingStatus.Typical,
                });
            }

            return JsonSerializer.Serialize(defaults, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WARN] GetDefXmlDefaults FAILED");
            return JsonSerializer.Serialize(Array.Empty<DataLinkWarningSetting>());
        }
    }

    /// <summary>
    /// Get warning history for the active dashboard.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetWarningHistory')
    /// </summary>
    public async Task<string> GetWarningHistory()
    {
        try
        {
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            var def = config?.Dashboards.GetValueOrDefault(config?.ActiveDashboard ?? "default");
            var history = def?.WarningHistory ?? [];
            return JsonSerializer.Serialize(history, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WARN] GetWarningHistory FAILED");
            return JsonSerializer.Serialize(Array.Empty<WarningHistoryEntry>());
        }
    }

    /// <summary>
    /// Save warning history to the active dashboard. Replaces entire history.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveWarningHistory', [json])
    /// </summary>
    public async Task<string> SaveWarningHistory(string historyJson)
    {
        try
        {
            var history = JsonSerializer.Deserialize<List<WarningHistoryEntry>>(historyJson, SJsonOptions) ?? [];
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();
            var activeName = config.ActiveDashboard ?? "default";

            if (!config.Dashboards.ContainsKey(activeName))
                config.Dashboards[activeName] = new DashboardDefinition();

            config.Dashboards[activeName].WarningHistory = history;
            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            _logger.LogInformation("[WARN] SaveWarningHistory: saved {Count} entries", history.Count);

            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WARN] SaveWarningHistory FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}

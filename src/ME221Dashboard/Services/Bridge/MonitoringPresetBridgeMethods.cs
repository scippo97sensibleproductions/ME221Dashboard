using System.Text.Json;
using System.Text.Json.Nodes;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    private const int MaxPresetsPerInstallation = 100;
    private const int MaxPresetNameLength = 50;

    // ─── Monitoring Preset CRUD ────────────────────────────────────────────

    /// <summary>
    /// Get all monitoring presets.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetMonitoringPresets')
    /// </summary>
    public async Task<string> GetMonitoringPresets()
    {
        try
        {
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            var presets = config?.MonitoringPresets ?? [];
            return JsonSerializer.Serialize(new { success = true, presets }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMonitoringPresets failed");
            return JsonSerializer.Serialize(new { success = false, error = "Failed to load presets", presets = Array.Empty<MonitoringPreset>() }, SJsonOptions);
        }
    }

    /// <summary>
    /// Create a new monitoring preset.
    /// Called from JS: window.HybridWebView.InvokeDotNet('CreateMonitoringPreset', [jsonPayload])
    /// Payload: { name: string, datalinkIds: number[] }
    /// </summary>
    public async Task<string> CreateMonitoringPreset(string jsonPayload)
    {
        try
        {
            var data = JsonNode.Parse(jsonPayload)!;
            var name = data["name"]?.GetValue<string>()?.Trim() ?? "";
            var datalinkIds = data["datalinkIds"]?.AsArray()?.Select(n => (int)n!).ToList() ?? [];

            if (string.IsNullOrWhiteSpace(name))
                return JsonSerializer.Serialize(new { success = false, error = "Preset name is required" }, SJsonOptions);

            if (name.Length > MaxPresetNameLength)
                return JsonSerializer.Serialize(new { success = false, error = $"Preset name must be {MaxPresetNameLength} characters or fewer" }, SJsonOptions);

            if (datalinkIds.Count == 0)
                return JsonSerializer.Serialize(new { success = false, error = "At least one sensor must be selected" }, SJsonOptions);

            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();
            config.MonitoringPresets ??= [];

            if (config.MonitoringPresets.Count >= MaxPresetsPerInstallation)
                return JsonSerializer.Serialize(new { success = false, error = $"Maximum of {MaxPresetsPerInstallation} presets reached" }, SJsonOptions);

            if (config.MonitoringPresets.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                return JsonSerializer.Serialize(new { success = false, error = "A preset with this name already exists" }, SJsonOptions);

            var preset = new MonitoringPreset
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                DatalinkIds = datalinkIds,
            };

            config.MonitoringPresets.Add(preset);
            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);

            return JsonSerializer.Serialize(new { success = true, preset }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateMonitoringPreset failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SJsonOptions);
        }
    }

    /// <summary>
    /// Update an existing monitoring preset.
    /// Called from JS: window.HybridWebView.InvokeDotNet('UpdateMonitoringPreset', [jsonPayload])
    /// Payload: { id: string, name: string, datalinkIds: number[] }
    /// </summary>
    public async Task<string> UpdateMonitoringPreset(string jsonPayload)
    {
        try
        {
            var data = JsonNode.Parse(jsonPayload)!;
            var id = data["id"]?.GetValue<string>() ?? "";
            var name = data["name"]?.GetValue<string>()?.Trim() ?? "";
            var datalinkIds = data["datalinkIds"]?.AsArray()?.Select(n => (int)n!).ToList() ?? [];

            if (string.IsNullOrWhiteSpace(id))
                return JsonSerializer.Serialize(new { success = false, error = "Preset ID is required" }, SJsonOptions);

            if (string.IsNullOrWhiteSpace(name))
                return JsonSerializer.Serialize(new { success = false, error = "Preset name is required" }, SJsonOptions);

            if (name.Length > MaxPresetNameLength)
                return JsonSerializer.Serialize(new { success = false, error = $"Preset name must be {MaxPresetNameLength} characters or fewer" }, SJsonOptions);

            if (datalinkIds.Count == 0)
                return JsonSerializer.Serialize(new { success = false, error = "At least one sensor must be selected" }, SJsonOptions);

            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();
            config.MonitoringPresets ??= [];

            var existing = config.MonitoringPresets.FirstOrDefault(p => p.Id == id);
            if (existing == null)
                return JsonSerializer.Serialize(new { success = false, error = "Preset not found" }, SJsonOptions);

            if (config.MonitoringPresets.Any(p => p.Id != id && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                return JsonSerializer.Serialize(new { success = false, error = "A preset with this name already exists" }, SJsonOptions);

            existing.Name = name;
            existing.DatalinkIds = datalinkIds;

            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);

            return JsonSerializer.Serialize(new { success = true, preset = existing }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateMonitoringPreset failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SJsonOptions);
        }
    }

    /// <summary>
    /// Delete a monitoring preset.
    /// Called from JS: window.HybridWebView.InvokeDotNet('DeleteMonitoringPreset', [id])
    /// </summary>
    public async Task<string> DeleteMonitoringPreset(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
                return JsonSerializer.Serialize(new { success = false, error = "Preset ID is required" }, SJsonOptions);

            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            if (config?.MonitoringPresets == null)
                return JsonSerializer.Serialize(new { success = false, error = "No presets found" }, SJsonOptions);

            var removed = config.MonitoringPresets.RemoveAll(p => p.Id == id);
            if (removed == 0)
                return JsonSerializer.Serialize(new { success = false, error = "Preset not found" }, SJsonOptions);

            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteMonitoringPreset failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, SJsonOptions);
        }
    }
}

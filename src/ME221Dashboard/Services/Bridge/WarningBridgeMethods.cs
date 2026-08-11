using System.Text.Json;
using ME221.Data.Infrastructure;
using ME221.Data.Models;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── Warning Centre ────────────────────────────────────────────────────

    // In-memory R19 load-time removal notice ledger, keyed per calibration load (KTD6).
    private readonly List<int> _pendingLoadNotices = [];
    private string _lastCalibrationKey = "";

    /// <summary>
    /// Get warning settings for the active dashboard with R18 migration on first read
    /// and R7/R19 read-time defaults. Returns { settings: [...], delayMs: number }.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetWarningSettings')
    /// </summary>
    public async Task<string> GetWarningSettings()
    {
        try
        {
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();
            var anyMigration = false;

            // Migrate: pull per-dashboard warnings into global config on first read
            if (config.WarningSettings.Count == 0)
            {
                foreach (var dashboard in config.Dashboards.Values)
                {
#pragma warning disable CS0618 // Intentional: migrating from obsolete per-dashboard warnings
                    if (dashboard.LegacyWarningSettings is { Count: > 0 })
                    {
                        config.WarningSettings = dashboard.LegacyWarningSettings;
                        dashboard.LegacyWarningSettings = null;
                        anyMigration = true;
                        break;
                    }
#pragma warning restore CS0618
                }
            }

            // Migrate: legacy delay on the active dashboard definition
            var activeName = config.ActiveDashboard ?? "default";
            if (config.Dashboards.TryGetValue(activeName, out var activeDef))
            {
#pragma warning disable CS0618 // Intentional: migrating from the obsolete legacy delay field
                if (activeDef.LegacyWarningDelayMs is not null)
                {
                    config.WarningDelayMs = (int)WarningMigration.MigrateDelay(activeDef.LegacyWarningDelayMs);
                    activeDef.LegacyWarningDelayMs = null;
                    anyMigration = true;
                }
#pragma warning restore CS0618
            }

            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            var dataLinks = calResult.Data?.DataLinks ?? [];

            var calKey = string.Join(",", dataLinks.Select(dl => dl.Id));
            if (calKey != _lastCalibrationKey)
            {
                _lastCalibrationKey = calKey;
                _pendingLoadNotices.Clear();
            }

            var persistedById = config.WarningSettings.ToDictionary(s => s.DataId);
            var result = new List<DataLinkWarningSetting>();
            var anyDatalinkMigration = false;

            foreach (var dl in dataLinks)
            {
                var dataId = (int)dl.Id;
                DataLinkWarningSetting record;

                if (persistedById.TryGetValue(dataId, out var existing))
                {
                    record = existing;
                }
                else
                {
                    record = new DataLinkWarningSetting
                    {
                        DataId = dataId,
                        Enabled = true,
                        Name = dl.Name,
                        Unit = dl.MeasureUnit,
                        Category = dl.Category,
                        Status = WarningSettingStatus.Typical,
                    };
                }

                // R18: persist legacy min/max migration on first read
                if (WarningMigration.HasLegacyBounds(record))
                {
                    WarningMigration.MigrateSetting(record);
                    anyDatalinkMigration = true;
                }

                // R19: read-time per-severity defaults for Typical datalinks
                var defaultsApplied = false;
                if (record.Status == WarningSettingStatus.Typical)
                {
                    var defaults = WarningDefaults.BuildDefaults(dataId, dl, record.MigratedBoundsMarkerSet);
                    if (defaults is not null)
                    {
                        WarningDefaults.ApplyDefaults(record, defaults, out var pointsRemoved);
                        if (pointsRemoved)
                            _pendingLoadNotices.Add(dataId);
                        defaultsApplied = true;
                    }
                }

                // R7: read-time "warning" default level for Typical datalinks with none
                if (!defaultsApplied && record.Status == WarningSettingStatus.Typical && record.Levels.Count == 0)
                {
                    var level = WarningDefaults.BuildR7Default(dataId, autolog: record.MigratedBoundsMarkerSet);
                    record.Levels.Add(level);
                    if (record.MigratedBoundsMarkerSet)
                        record.MigratedBoundsMarkerLevelId = level.Id;
                }

                record.Name = dl.Name;
                record.Unit = dl.MeasureUnit;
                record.Category = dl.Category;

                result.Add(record);
            }

            if (anyMigration || anyDatalinkMigration)
                await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);

            // R19 load-time removal notices: in-memory per calibration load (KTD6),
            // surfaced once to JS and cleared on read.
            var removedIds = _pendingLoadNotices.ToArray();
            _pendingLoadNotices.Clear();

            _logger.LogInformation("[WARN] GetWarningSettings: returned {Count} settings, delay {DelayMs} ms", result.Count, config.WarningDelayMs);

            return JsonSerializer.Serialize(new { settings = result, delayMs = config.WarningDelayMs, pointsRemovedIds = removedIds }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WARN] GetWarningSettings FAILED");
            return JsonSerializer.Serialize(new { settings = Array.Empty<DataLinkWarningSetting>(), delayMs = 500, pointsRemovedIds = Array.Empty<int>() }, SJsonOptions);
        }
    }

    /// <summary>
    /// Save warning settings to the active dashboard. Merges by DataId — never prunes
    /// datalinks not present in the incoming list (R7).
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveWarningSettings', [json])
    /// </summary>
    public async Task<string> SaveWarningSettings(string settingsJson)
    {
        try
        {
            var incoming = JsonSerializer.Deserialize<List<DataLinkWarningSetting>>(settingsJson, SJsonOptions) ?? [];
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();

            config.WarningSettings = WarningMerge.MergeSave(config.WarningSettings, incoming);
            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            _logger.LogInformation("[WARN] SaveWarningSettings: merged {Count} settings", incoming.Count);

            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WARN] SaveWarningSettings FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get per-severity R19 warning preset defaults from DEF XML feedback thresholds
    /// for every datalink with at least one Warning/Alarm feedback.
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
                var set = WarningDefaults.BuildDefaults((int)dl.Id, dl, markerSet: false);
                if (set is null) continue;

                var record = new DataLinkWarningSetting
                {
                    DataId = (int)dl.Id,
                    Enabled = true,
                    Name = dl.Name,
                    Unit = dl.MeasureUnit,
                    Category = dl.Category,
                    Status = WarningSettingStatus.Typical,
                };
                WarningDefaults.ApplyDefaults(record, set, out _);
                defaults.Add(record);
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
    /// Per-datalink partial update with C# as the validation authority (KTD4):
    /// duplicate-tuple rejection, clamp application, and the R4 status flip are
    /// classified explicitly via writeKind. Returns
    /// { success, rejected?, snapshot?, clamps?, error? }.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveWarningDatalink', [json])
    /// </summary>
    public async Task<string> SaveWarningDatalink(string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<WarningDatalinkPayload>(json, SJsonOptions);
            if (payload is null)
                return JsonSerializer.Serialize(new { success = false, rejected = "invalid", error = "Payload could not be parsed" });

            var levels = payload.Levels ?? [];
            var points = payload.Points ?? [];

            var calResult = await _calibration.GetPersistedCalibrationAsync().ConfigureAwait(false);
            var dl = calResult.Data?.DataLinks.FirstOrDefault(d => d.Id == payload.DataId);
            if (dl is null)
                return JsonSerializer.Serialize(new { success = false, rejected = "invalid", error = $"Datalink {payload.DataId} not found in calibration" });

            // KTD1: C# generates level/point ids at insert
            foreach (var level in levels)
            {
                if (string.IsNullOrWhiteSpace(level.Id))
                    level.Id = Guid.NewGuid().ToString("N");
            }
            foreach (var point in points)
            {
                if (string.IsNullOrWhiteSpace(point.Id))
                    point.Id = Guid.NewGuid().ToString("N");
            }

            // Duplicate-tuple check across points: (value, direction, levelId)
            var seen = new HashSet<(float Value, string Direction, string LevelId)>();
            foreach (var point in points)
            {
                if (!seen.Add((point.Value, point.Direction, point.LevelId)))
                    return JsonSerializer.Serialize(new { success = false, rejected = "duplicate" });
            }

            // Every point must reference a level present in the payload
            var levelIds = new HashSet<string>(levels.Select(l => l.Id));
            foreach (var point in points)
            {
                if (!levelIds.Contains(point.LevelId))
                    return JsonSerializer.Serialize(new { success = false, rejected = "invalid", error = $"Point {point.Id} references unknown level {point.LevelId}" });
            }

            // Clamp point values into the datalink domain; collect clamped point ids
            var clamps = new List<string>();
            if (dl.MaxValue > dl.MinValue)
            {
                foreach (var point in points)
                {
                    var clamped = Math.Clamp(point.Value, dl.MinValue, dl.MaxValue);
                    if (clamped != point.Value)
                    {
                        point.Value = clamped;
                        clamps.Add(point.Id);
                    }
                }
            }

            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();
            var record = config.WarningSettings.FirstOrDefault(s => s.DataId == payload.DataId);
            if (record is null)
            {
                record = new DataLinkWarningSetting
                {
                    DataId = payload.DataId,
                    Name = dl.Name,
                    Unit = dl.MeasureUnit,
                    Category = dl.Category,
                    Status = WarningSettingStatus.Typical,
                };
                config.WarningSettings.Add(record);
            }

            record.Levels = levels;
            record.Points = points;
            record.Enabled = payload.Enabled;

            switch (payload.WriteKind)
            {
                case "enable-toggle":
                    // R3: no status change
                    break;
                case "points-levels-edit":
                    // R4: any user edit to points/levels flips to Custom
                    record.Status = WarningSettingStatus.Custom;
                    break;
                case "preset-restore":
                    // R15: replacement resets to Typical; marker re-initialization per R18
                    record.Status = WarningSettingStatus.Typical;
                    if (payload.MarkerSet)
                    {
                        var warningLevel = levels.FirstOrDefault(l => WarningMigration.ResolveRole(l) == WarningDefaults.WarningRole);
                        record.MigratedBoundsMarkerSet = true;
                        record.MigratedBoundsMarkerLevelId = warningLevel?.Id;
                        if (warningLevel is not null)
                            warningLevel.Autolog = true;
                    }
                    break;
                case "undo-restore":
                    // R9/KTD4: restore the recorded pre-delete status; intervening edits keep Custom
                    record.Status = payload.InterveningEdit
                        ? WarningSettingStatus.Custom
                        : ParseWarningStatus(payload.PreDeleteStatus) ?? record.Status;
                    if (payload.MarkerSet && payload.MarkerLevelName is not null)
                    {
                        var restored = levels.FirstOrDefault(l =>
                            string.Equals(l.Name.Trim(), payload.MarkerLevelName, StringComparison.OrdinalIgnoreCase));
                        if (restored is not null)
                        {
                            record.MigratedBoundsMarkerSet = true;
                            record.MigratedBoundsMarkerLevelId = restored.Id;
                        }
                    }
                    break;
            }

            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            _logger.LogInformation("[WARN] SaveWarningDatalink: saved datalink {DataId} (writeKind={WriteKind}, {PointCount} points)", payload.DataId, payload.WriteKind, points.Count);

            return JsonSerializer.Serialize(new { success = true, snapshot = record, clamps }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WARN] SaveWarningDatalink FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Persist the global warning delay, clamped to [0, 60000].
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveWarningDelay', [json])
    /// </summary>
    public async Task<string> SaveWarningDelay(string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<WarningDelayPayload>(json, SJsonOptions);
            if (payload is null)
                return JsonSerializer.Serialize(new { success = false, error = "Payload could not be parsed" });

            var delayMs = Math.Clamp(payload.DelayMs, 0, 60000);
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();
            config.WarningDelayMs = (int)Math.Round(delayMs);
            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            _logger.LogInformation("[WARN] SaveWarningDelay: saved {DelayMs} ms", config.WarningDelayMs);

            return JsonSerializer.Serialize(new { success = true, delayMs = config.WarningDelayMs });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WARN] SaveWarningDelay FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Persist the R15 batch ledger (datalink id + per-datalink outcome).
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveBatchLedger', [json])
    /// </summary>
    public async Task<string> SaveBatchLedger(string ledgerJson)
    {
        try
        {
            var ledger = JsonSerializer.Deserialize<List<BatchLedgerEntry>>(ledgerJson, SJsonOptions) ?? [];
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();
            config.BatchLedger = ledger;
            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            _logger.LogInformation("[WARN] SaveBatchLedger: saved {Count} entries", ledger.Count);

            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WARN] SaveBatchLedger FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get the persisted R15 queued notices (batch banners + undo-expiry notices)
    /// so partial failures and expired undo windows survive process death.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetWarningQueuedNotices')
    /// </summary>
    public async Task<string> GetWarningQueuedNotices()
    {
        try
        {
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();
            return JsonSerializer.Serialize(new { banners = config.QueuedBanners, undoExpiryNotices = config.QueuedUndoExpiryNotices }, SJsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WARN] GetWarningQueuedNotices FAILED");
            return JsonSerializer.Serialize(new { banners = Array.Empty<QueuedBanner>(), undoExpiryNotices = Array.Empty<UndoExpiryNotice>() }, SJsonOptions);
        }
    }

    /// <summary>
    /// Persist the R15 queued notices. Called from JS:
    /// window.HybridWebView.InvokeDotNet('SaveWarningQueuedNotices', [json])
    /// </summary>
    public async Task<string> SaveWarningQueuedNotices(string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<QueuedNoticesPayload>(json, SJsonOptions);
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();
            config.QueuedBanners = payload?.Banners ?? [];
            config.QueuedUndoExpiryNotices = payload?.UndoExpiryNotices ?? [];
            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WARN] SaveWarningQueuedNotices FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
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

    private static WarningSettingStatus? ParseWarningStatus(string? value)
        => value is not null && Enum.TryParse<WarningSettingStatus>(value, out var status) ? status : null;

private sealed class WarningDatalinkPayload
{
    public int DataId { get; set; }
    public List<WarningLevel> Levels { get; set; } = [];
    public List<WarningPoint> Points { get; set; } = [];
    public bool Enabled { get; set; }
    public string WriteKind { get; set; } = "";
    public bool MarkerSet { get; set; }
    public string? PreDeleteStatus { get; set; }
    public bool InterveningEdit { get; set; }
    public string? MarkerLevelName { get; set; }
}

private sealed class QueuedNoticesPayload
{
    public List<QueuedBanner> Banners { get; set; } = [];
    public List<UndoExpiryNotice> UndoExpiryNotices { get; set; } = [];
}

    private sealed class WarningDelayPayload
    {
        public double DelayMs { get; set; }
    }
}

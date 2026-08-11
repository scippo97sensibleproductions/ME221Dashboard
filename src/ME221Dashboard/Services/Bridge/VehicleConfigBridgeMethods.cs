using System.Text.Json;
using System.Text.Json.Nodes;
using ME221.Data.Models;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── Vehicle Config (per-dashboard — flipped from global, KTD3) ─────────

    /// <summary>
    /// Resolve the effective vehicle config for a dashboard: the dashboard's own Vehicle,
    /// else the seed template, else fresh defaults. Reads never write back to the global slot.
    /// </summary>
    private static VehicleConfig ResolveVehicle(DashboardConfig config, string dashboardName)
    {
        if (config.Dashboards?.TryGetValue(dashboardName, out var def) == true && def?.Vehicle != null)
            return def.Vehicle;
        return config.VehicleSeedTemplate ?? new VehicleConfig();
    }

    /// <summary>Active dashboard name: the in-process authority when it still exists in
    /// the config, else the persisted one when it exists, else the first dashboard,
    /// else "default". Never a stale/nonexistent name — a missing dashboard would make
    /// reads silently fall back to defaults.</summary>
    private string ResolveActiveDashboardName(DashboardConfig? config)
    {
        if (_activeDashboardName is { Length: > 0 } session
            && (config?.Dashboards is null || config.Dashboards.ContainsKey(session)))
            return session;
        return ResolveEffectiveActiveDashboard(config);
    }

    /// <summary>
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetVehicleConfig', [])
    /// Reads the ACTIVE dashboard's vehicle + shifter config (falling back to the seed).
    /// </summary>
    public async Task<string> GetVehicleConfig()
    {
        try
        {
            await EnsureVehicleMigrationAsync().ConfigureAwait(false);
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            var name = ResolveActiveDashboardName(config);
            if (config == null)
            {
                _logger.LogWarning("[VEHCFG] GetVehicleConfig: no config on disk, returning defaults");
                return SerializeVehicleConfig(new VehicleConfig(), new ShifterConfig());
            }

            var vc = ResolveVehicle(config, name);
            var shifter = config.Dashboards?.TryGetValue(name, out var activeDef) == true
                ? activeDef?.ShifterConfig
                : null;
            _logger.LogInformation("[VEHCFG] GetVehicleConfig: dashboard={Dash}, fd={FD}, tire={Tire}, rpm={RPM}, vss={VSS}, shift={Shift}, floor={Floor}",
                name, vc.FinalDriveRatio, vc.TireDiameterInches, vc.RpmEntityId, vc.VssSpeedEntityId,
                shifter?.ShiftPointRpm, shifter?.DownshiftFloorRpm);

            return SerializeVehicleConfig(vc, shifter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VEHCFG] GetVehicleConfig FAILED");
            return SerializeVehicleConfig(new VehicleConfig(), new ShifterConfig());
        }
    }

    /// <summary>
    /// Called from JS: window.HybridWebView.InvokeDotNet('SetVehicleConfig', [json])
    /// Writes the ACTIVE dashboard's vehicle + shifter config.
    /// Partial-payload contract: a shifter block absent from the payload preserves the
    /// existing per-dashboard ShifterConfig (never defaults to zero), so the modal and the
    /// shifter-section Save cannot clobber each other regardless of save ordering.
    /// An explicit "autoDetect": true flag excludes the save from the seed refresh gate (R8).
    /// Trusted-client contract: the WebView UI is the only caller; shifter bounds (200–9000
    /// shift point, floor ≥ FLOOR_MIN) are enforced in the TS config surface, not re-validated
    /// here. Malformed/out-of-range fields persist as-is (fail-open by design for a local app).
    /// </summary>
    public async Task<string> SetVehicleConfig(string json)
    {
        _logger.LogInformation("[VEHCFG] SetVehicleConfig: {Json}", json);
        try
        {
            await EnsureVehicleMigrationAsync().ConfigureAwait(false);
            var node = JsonNode.Parse(json);
            if (node == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Invalid JSON" });
            }

            var vc = new VehicleConfig
            {
                Enabled = node["enabled"]?.GetValue<bool>() ?? true,
                TireDiameterInches = node["tireDiameterInches"]?.GetValue<double>() ?? 23,
                FinalDriveRatio = node["finalDriveRatio"]?.GetValue<double>() ?? 4.3,
                GearRatios = node["gearRatios"]?.AsArray()?.Select(x => x?.GetValue<double>() ?? 0).Where(x => x > 0).ToArray() ?? [3.6, 2.2, 1.5, 1.1, 0.85, 0.7],
                WheelSlipPercent = node["wheelSlipPercent"]?.GetValue<double>() ?? 3,
                RpmEntityId = node["rpmEntityId"]?.GetValue<int>(),
                VssSpeedEntityId = node["vssSpeedEntityId"]?.GetValue<int>(),
                MapEntityId = node["mapEntityId"]?.GetValue<int>(),
                BaroEntityId = node["baroEntityId"]?.GetValue<int>(),
                GearEntityId = node["gearEntityId"]?.GetValue<int>(),
            };

            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            if (config == null)
            {
                _logger.LogWarning("[VEHCFG] SetVehicleConfig: no config on disk, creating new");
                config = new DashboardConfig();
            }

            var name = ResolveActiveDashboardName(config);
            if (!config.Dashboards.ContainsKey(name))
                config.Dashboards[name] = new DashboardDefinition();

            var def = config.Dashboards[name];
            def.Vehicle = vc;

            // Partial-payload contract: absent shifter block preserves the existing value.
            if (node["shifter"] is JsonObject shifterObj)
            {
                def.ShifterConfig = new ShifterConfig
                {
                    ShiftPointRpm = shifterObj["shiftPointRpm"]?.GetValue<double>() ?? 0,
                    DownshiftFloorRpm = shifterObj["downshiftFloorRpm"]?.GetValue<double>() ?? 0,
                };
            }

            // Seed refresh: per-dashboard save path only, gated on completeness and on
            // differing from the current seed; auto-detect writes never refresh it (R8).
            var autoDetect = node["autoDetect"]?.GetValue<bool>() ?? false;
            if (!autoDetect && VehicleConfigMigration.ShouldRefreshSeed(config.VehicleSeedTemplate, vc))
            {
                config.VehicleSeedTemplate = VehicleConfigMigration.Clone(vc);
                _logger.LogInformation("[VEHCFG] seed template refreshed from dashboard '{Dash}'", name);
            }

            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            _activeDashboardName = name;
            _logger.LogInformation("[VEHCFG] SetVehicleConfig: saved per-dashboard, fd={FD}", vc.FinalDriveRatio);

            // Write shared file for emulator (tmp + move so a crash cannot tear it)
            await WriteEmulatorVehicleFileAsync(config, name).ConfigureAwait(false);

            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VEHCFG] SetVehicleConfig FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test seam: overrides the shared emulator-file directory so tests never
    /// touch the real user profile. Null in production.
    /// </summary>
    internal static string? SharedDirOverride;

    /// <summary>
    /// Write the shared emulator config file from a dashboard's effective vehicle config.
    /// Also carries the dashboard's shifter shift-up point (null when not configured, so the
    /// emulator falls back to its own default) — the simulated driver then shifts exactly
    /// where the dashboard's shift light is set. The shift-light downshift floor is NOT
    /// written: it is a display threshold, and using it as the emulator's coast downshift
    /// floor would ping-pong gears on wide ratio spreads.
    /// Non-fatal on failure; tmp + move so a crash mid-write cannot leave a truncated file.
    /// </summary>
    internal async Task WriteEmulatorVehicleFileAsync(DashboardConfig config, string dashboardName)
    {
        string? tmpPath = null;
        try
        {
            var vc = ResolveVehicle(config, dashboardName);
            var def = config.Dashboards.TryGetValue(dashboardName, out var dashboardDef) ? dashboardDef : null;
            var sharedDir = SharedDirOverride ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".me221");
            Directory.CreateDirectory(sharedDir);
            var sharedPath = Path.Combine(sharedDir, "vehicle-config.json");
            tmpPath = sharedPath + $".tmp.{Environment.ProcessId}.{DateTime.UtcNow.Ticks}";
            var sharedJson = JsonSerializer.Serialize(new
            {
                enabled = vc.Enabled,
                tireDiameterInches = vc.TireDiameterInches,
                finalDriveRatio = vc.FinalDriveRatio,
                gearRatios = vc.GearRatios,
                wheelSlipPercent = vc.WheelSlipPercent,
                shiftUpRpm = def?.ShifterConfig?.ShiftPointRpm,
            });
            await File.WriteAllTextAsync(tmpPath, sharedJson).ConfigureAwait(false);
            File.Move(tmpPath, sharedPath, overwrite: true);
            tmpPath = null;
        }
        catch (Exception ex)
        {
            // Best-effort tmp cleanup so a crash between write and move cannot
            // leave orphaned .tmp.* files accumulating in the shared dir.
            if (tmpPath != null)
            {
                try { File.Delete(tmpPath); }
                catch { /* non-fatal */ }
            }
            _logger.LogWarning(ex, "[VEHCFG] shared config write failed (non-fatal)");
        }
    }

    private static string SerializeVehicleConfig(VehicleConfig vc, ShifterConfig? shifter)
    {
        return JsonSerializer.Serialize(new
        {
            enabled = vc.Enabled,
            tireDiameterInches = vc.TireDiameterInches,
            finalDriveRatio = vc.FinalDriveRatio,
            gearRatios = vc.GearRatios,
            wheelSlipPercent = vc.WheelSlipPercent,
            rpmEntityId = vc.RpmEntityId,
            vssSpeedEntityId = vc.VssSpeedEntityId,
            mapEntityId = vc.MapEntityId,
            baroEntityId = vc.BaroEntityId,
            gearEntityId = vc.GearEntityId,
            shifter = new
            {
                shiftPointRpm = shifter?.ShiftPointRpm ?? 0,
                downshiftFloorRpm = shifter?.DownshiftFloorRpm ?? 0,
            },
        });
    }
}

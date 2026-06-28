using System.Text.Json;
using System.Text.Json.Nodes;
using ME221.Data.Models;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── Vehicle Config (global — not per-dashboard) ─────────────────────

    /// Called from JS: window.HybridWebView.InvokeDotNet('GetVehicleConfig', [])
    public async Task<string> GetVehicleConfig()
    {
        try
        {
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            if (config == null)
            {
                _logger.LogWarning("[VEHCFG] GetVehicleConfig: no config on disk, returning defaults");
                return SerializeVehicleConfig(new VehicleConfig());
            }

            // Migration: if global is null, check per-dashboard for backwards compat
            var vc = config.Vehicle;
            if (vc == null && config.Dashboards != null)
            {
                foreach (var def in config.Dashboards.Values)
                {
                    if (def?.Vehicle != null)
                    {
                        vc = def.Vehicle;
                        _logger.LogInformation("[VEHCFG] GetVehicleConfig: migrated from per-dashboard Vehicle");
                        // Persist to global level
                        config.Vehicle = vc;
                        await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
                        break;
                    }
                }
            }

            vc ??= new VehicleConfig();
            _logger.LogInformation("[VEHCFG] GetVehicleConfig: fd={FD}, tire={Tire}, rpm={RPM}, vss={VSS}",
                vc.FinalDriveRatio, vc.TireDiameterInches, vc.RpmEntityId, vc.VssSpeedEntityId);

            return SerializeVehicleConfig(vc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VEHCFG] GetVehicleConfig FAILED");
            return SerializeVehicleConfig(new VehicleConfig());
        }
    }

    /// Called from JS: window.HybridWebView.InvokeDotNet('SetVehicleConfig', [json])
    public async Task<string> SetVehicleConfig(string json)
    {
        _logger.LogInformation("[VEHCFG] SetVehicleConfig: {Json}", json);
        try
        {
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

            // Write to global level
            config.Vehicle = vc;

            // Also write to all per-dashboard for backwards compat
            if (config.Dashboards != null)
            {
                foreach (var def in config.Dashboards.Values)
                {
                    def.Vehicle = vc;
                }
            }

            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);
            _logger.LogInformation("[VEHCFG] SetVehicleConfig: saved to global level, fd={FD}", vc.FinalDriveRatio);

            // Verify: re-read from disk
            var verifyConfig = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            if (verifyConfig?.Vehicle != null)
            {
                _logger.LogInformation("[VEHCFG] VERIFY: fd={FD}, tire={Tire}",
                    verifyConfig.Vehicle.FinalDriveRatio, verifyConfig.Vehicle.TireDiameterInches);
            }
            else
            {
                _logger.LogWarning("[VEHCFG] VERIFY: Vehicle is NULL after save!");
            }

            // Write shared file for emulator
            try
            {
                var sharedDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".me221");
                Directory.CreateDirectory(sharedDir);
                var sharedPath = Path.Combine(sharedDir, "vehicle-config.json");
                var sharedJson = JsonSerializer.Serialize(vc, V2JsonContext.Default.VehicleConfig);
                await File.WriteAllTextAsync(sharedPath, sharedJson).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[VEHCFG] shared config write failed (non-fatal)");
            }

            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VEHCFG] SetVehicleConfig FAILED");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private static string SerializeVehicleConfig(VehicleConfig vc)
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
        });
    }
}

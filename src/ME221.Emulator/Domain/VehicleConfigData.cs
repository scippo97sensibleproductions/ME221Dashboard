using System.Text.Json;
using ME221.Data.Models;
using Microsoft.Extensions.Logging;

namespace ME221.Emulator.Domain;

public sealed class VehicleConfigData
{
    public double TireDiameterInches { get; set; } = 23;
    public double FinalDriveRatio { get; set; } = 4.3;
    public double[] GearRatios { get; set; } = [3.6, 2.2, 1.5, 1.1, 0.85, 0.7];
}

public static class VehicleConfigLoader
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static DateTime s_lastWriteUtc = DateTime.MinValue;
    private static VehicleConfigData? s_cached;

    public static VehicleConfigData Load(string? vehicleConfigPath, CalibrationData? calibration, ILogger? logger = null)
    {
        // 1. Try shared VehicleConfig file
        if (vehicleConfigPath is not null && File.Exists(vehicleConfigPath))
        {
            try
            {
                var json = File.ReadAllText(vehicleConfigPath);
                var config = JsonSerializer.Deserialize<VehicleConfigData>(json, s_jsonOptions);
                if (config is not null && config.GearRatios.Length > 0)
                {
                    logger?.LogInformation("Loaded vehicle config from {Path}: {Gears} gears, final={Final:F2}, tire={Tire:F1}\"",
                        vehicleConfigPath, config.GearRatios.Length, config.FinalDriveRatio, config.TireDiameterInches);
                    s_cached = config;
                    s_lastWriteUtc = File.GetLastWriteTimeUtc(vehicleConfigPath);
                    return config;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to load vehicle config from {Path}, falling back", vehicleConfigPath);
            }
        }

        // 2. Try calibration.json "VSS Gear Settings" driver (id 8210)
        if (calibration is not null)
        {
            var vssDriver = calibration.Drivers.FirstOrDefault(d => d.Id == 8210);
            if (vssDriver is not null)
            {
                var ratios = new List<double>();
                double finalRatio = 0;

                foreach (var param in vssDriver.Configs)
                {
                    if (param.Name.StartsWith("Gear ", StringComparison.OrdinalIgnoreCase) && param.Name.EndsWith(" Ratio", StringComparison.OrdinalIgnoreCase))
                    {
                        if (param.Value > 0)
                            ratios.Add(param.Value);
                    }
                    else if (param.Name.Equals("Final Ratio", StringComparison.OrdinalIgnoreCase))
                    {
                        if (param.Value > 0)
                            finalRatio = param.Value;
                    }
                }

                if (ratios.Count > 0 && finalRatio > 0)
                {
                    logger?.LogInformation("Loaded gear ratios from calibration VSS Gear Settings: {Gears} gears, final={Final:F2}",
                        ratios.Count, finalRatio);
                    return new VehicleConfigData
                    {
                        GearRatios = ratios.ToArray(),
                        FinalDriveRatio = finalRatio,
                        TireDiameterInches = 23, // default, not in calibration
                    };
                }
            }
        }

        // 3. Hardcoded defaults
        logger?.LogInformation("Using default gear ratios: 6-speed, final=4.3, tire=23\"");
        return new VehicleConfigData();
    }

    /// <summary>
    /// Re-reads the shared config file if it has changed since last load.
    /// Returns the current config (cached or freshly loaded).
    /// Returns null if the file doesn't exist or hasn't changed.
    /// </summary>
    public static VehicleConfigData? ReloadIfChanged(string? sharedPath, CalibrationData? calibration, ILogger? logger = null)
    {
        if (sharedPath is null || !File.Exists(sharedPath))
            return null;

        try
        {
            var lastWrite = File.GetLastWriteTimeUtc(sharedPath);
            if (lastWrite <= s_lastWriteUtc && s_cached is not null)
                return null;

            var json = File.ReadAllText(sharedPath);
            var config = JsonSerializer.Deserialize<VehicleConfigData>(json, s_jsonOptions);
            if (config is not null && config.GearRatios.Length > 0)
            {
                s_cached = config;
                s_lastWriteUtc = lastWrite;
                logger?.LogInformation("Hot-reloaded vehicle config: {Gears} gears, final={Final:F2}, tire={Tire:F1}\"",
                    config.GearRatios.Length, config.FinalDriveRatio, config.TireDiameterInches);
                return config;
            }
        }
        catch
        {
            // non-fatal, return null
        }

        return null;
    }
}

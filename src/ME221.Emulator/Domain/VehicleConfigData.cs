using System.Text.Json;
using ME221.Data.Models;
using Microsoft.Extensions.Logging;

namespace ME221.Emulator.Domain;

public sealed class VehicleConfigData
{
    public double TireDiameterInches { get; set; } = 23;
    public double FinalDriveRatio { get; set; } = 4.3;
    public double[] GearRatios { get; set; } = [3.6, 2.2, 1.5, 1.1, 0.85, 0.7];

    /// <summary>Shift-up point (RPM) the simulated driver shifts at. Null/absent → 7000.
    /// The dashboard writes this from its shifter config so the emulator shifts where the
    /// shift light is set.</summary>
    public double? ShiftUpRpm { get; set; }

    /// <summary>Coast downshift floor (RPM) — the simulated driver engine-brakes down
    /// through the gears as RPM falls to this floor. Manual config only; the dashboard
    /// deliberately does NOT write its shift-light floor here (a high floor with wide
    /// ratio spreads would ping-pong gears). Default 2200.</summary>
    public double? ShiftDownRpm { get; set; }

    /// <summary>Power at the wheels (hp) for the acceleration model. Null/absent → 110.</summary>
    public double? WheelHorsepower { get; set; }

    /// <summary>Vehicle mass (kg) for the acceleration model. Null/absent → 1000.</summary>
    public double? MassKg { get; set; }
}

public static class VehicleConfigLoader
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Dictionary<string, DateTime> s_lastWriteUtc = new(StringComparer.OrdinalIgnoreCase);
    private static VehicleConfigData? s_cached;
    private static string? s_explicitPath;

    public static string DashboardConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ME221", "dashboard-config.json");

    public static string SharedConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".me221", "vehicle-config.json");

    /// <summary>Set at startup (Program.cs) from the --vehicle-config arg. Null = not set.</summary>
    public static void Configure(string? explicitPath) => s_explicitPath = explicitPath;

    /// <summary>
    /// Resolve the vehicle/gearing config. Priority:
    ///   1. explicit --vehicle-config file (manual setups / tests)
    ///   2. the dashboard's own dashboard-config.json — the FIRST dashboard (file order)
    ///      that carries gears wins, with that dashboard's shifter shift point. The
    ///      active dashboard is not necessarily the one with gears, so the file is
    ///      scanned, not the active dashboard.
    ///   3. legacy shared ~/.me221/vehicle-config.json (older dashboard relay)
    ///   4. calibration.json "VSS Gear Settings" driver (id 8210)
    ///   5. hardcoded defaults
    /// </summary>
    public static VehicleConfigData Load(CalibrationData? calibration, ILogger? logger = null)
    {
        // 1. Explicit --vehicle-config file
        if (s_explicitPath is not null && File.Exists(s_explicitPath))
        {
            var loaded = TryReadFile(s_explicitPath, logger, $"vehicle config '{s_explicitPath}'");
            if (loaded is not null)
            {
                RecordWatch(s_explicitPath);
                return loaded;
            }
        }

        // 2. Dashboard's own config — first dashboard with gears (only when no explicit file)
        if (s_explicitPath is null)
        {
            var fromDashboards = LoadFirstDashboardWithGears(logger);
            if (fromDashboards is not null)
                return fromDashboards;

            // 3. Legacy shared relay file
            if (File.Exists(SharedConfigPath))
            {
                var loaded = TryReadFile(SharedConfigPath, logger, "shared vehicle config");
                if (loaded is not null)
                {
                    RecordWatch(SharedConfigPath);
                    return loaded;
                }
            }
        }

        // 4. calibration.json "VSS Gear Settings" driver (id 8210)
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

        // 5. Hardcoded defaults
        logger?.LogInformation("Using default gear ratios: 6-speed, final=4.3, tire=23\"");
        return new VehicleConfigData();
    }

    /// <summary>
    /// Re-reads the watched config sources (explicit file, dashboard config, shared file)
    /// if any of them changed since last load. Returns the fresh config when the effective
    /// values differ, otherwise null (and the watch times are refreshed).
    /// </summary>
    public static VehicleConfigData? ReloadIfChanged(CalibrationData? calibration, ILogger? logger = null)
    {
        // A previously watched source that vanished counts as a change so we can fall back.
        foreach (var path in s_lastWriteUtc.Keys.ToList())
        {
            if (!File.Exists(path))
            {
                s_lastWriteUtc.Remove(path);
                s_cached = null;
                logger?.LogInformation("Vehicle config source '{Path}' disappeared — reloading", path);
            }
        }

        var watched = new List<string>();
        if (s_explicitPath is not null)
        {
            if (File.Exists(s_explicitPath)) watched.Add(s_explicitPath);
        }
        else
        {
            if (File.Exists(DashboardConfigPath)) watched.Add(DashboardConfigPath);
            if (File.Exists(SharedConfigPath)) watched.Add(SharedConfigPath);
        }

        var changed = false;
        foreach (var path in watched)
        {
            var mtime = File.GetLastWriteTimeUtc(path);
            if (!s_lastWriteUtc.TryGetValue(path, out var prev) || mtime > prev)
                changed = true;
        }
        if (!changed)
            return null;

        var fresh = Load(calibration, logger);
        foreach (var path in watched)
            s_lastWriteUtc[path] = File.GetLastWriteTimeUtc(path);

        if (s_cached is not null && ConfigsEqual(s_cached, fresh))
            return null;
        s_cached = fresh;
        return fresh;
    }

    private static VehicleConfigData? LoadFirstDashboardWithGears(ILogger? logger)
    {
        if (!File.Exists(DashboardConfigPath))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(DashboardConfigPath));
            var root = doc.RootElement;

            // Per-dashboard: the first dashboard (file order) whose vehicle carries gears.
            if (root.TryGetProperty("dashboards", out var dashboards) && dashboards.ValueKind == JsonValueKind.Object)
            {
                foreach (var dashboard in dashboards.EnumerateObject())
                {
                    var vehicle = GetPropertyOrNull(dashboard.Value, "vehicle");
                    if (vehicle is not { } veh || veh.ValueKind != JsonValueKind.Object)
                        continue;

                    var gears = ReadGearRatios(veh);
                    if (gears is null)
                        continue;

                    var config = new VehicleConfigData
                    {
                        GearRatios = gears,
                        FinalDriveRatio = ReadDouble(veh, "finalDriveRatio", 4.3),
                        TireDiameterInches = ReadDouble(veh, "tireDiameterInches", 23),
                        WheelHorsepower = ReadDouble(veh, "wheelHorsepower", 0) is var hp && hp > 10 ? hp : null,
                        MassKg = ReadDouble(veh, "massKg", 0) is var m && m > 100 ? m : null,
                    };

                    var shifter = GetPropertyOrNull(dashboard.Value, "shifterConfig");
                    var shiftUp = shifter is not null ? ReadDouble(shifter.Value, "shiftPointRpm", 0) : 0;
                    if (shiftUp > 500)
                        config.ShiftUpRpm = shiftUp;

                    RecordWatch(DashboardConfigPath);
                    logger?.LogInformation(
                        "Loaded vehicle config from dashboard '{Dashboard}' (first dashboard with gears): {Gears} gears, final={Final:F2}, tire={Tire:F1}, shiftUp={ShiftUp:F0}",
                        dashboard.Name, gears.Length, config.FinalDriveRatio, config.TireDiameterInches, config.ShiftUpRpm ?? 7000);
                    return config;
                }
            }

            // Legacy global vehicle slot (pre-migration configs).
            if (root.TryGetProperty("vehicle", out var legacy) && legacy.ValueKind == JsonValueKind.Object)
            {
                var gears = ReadGearRatios(legacy);
                if (gears is not null)
                {
                    RecordWatch(DashboardConfigPath);
                    logger?.LogInformation("Loaded vehicle config from legacy global vehicle slot: {Gears} gears, final={Final:F2}",
                        gears.Length, ReadDouble(legacy, "finalDriveRatio", 4.3));
                    return new VehicleConfigData
                    {
                        GearRatios = gears,
                        FinalDriveRatio = ReadDouble(legacy, "finalDriveRatio", 4.3),
                        TireDiameterInches = ReadDouble(legacy, "tireDiameterInches", 23),
                    };
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to read dashboard config from {Path}, falling back", DashboardConfigPath);
        }

        return null;
    }

    private static VehicleConfigData? TryReadFile(string path, ILogger? logger, string description)
    {
        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<VehicleConfigData>(json, s_jsonOptions);
            if (config is not null && config.GearRatios.Length > 0)
            {
                logger?.LogInformation("Loaded {Description}: {Gears} gears, final={Final:F2}, tire={Tire:F1}\"",
                    description, config.GearRatios.Length, config.FinalDriveRatio, config.TireDiameterInches);
                return config;
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to load {Description}, falling back", description);
        }
        return null;
    }

    private static double[]? ReadGearRatios(JsonElement vehicle)
    {
        if (!vehicle.TryGetProperty("gearRatios", out var ratios) || ratios.ValueKind != JsonValueKind.Array)
            return null;

        var list = new List<double>();
        foreach (var item in ratios.EnumerateArray())
        {
            if (item.TryGetDouble(out var value) && value > 0)
                list.Add(value);
        }
        return list.Count > 0 ? list.ToArray() : null;
    }

    private static JsonElement? GetPropertyOrNull(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) ? value : null;
    }

    private static double ReadDouble(JsonElement element, string name, double fallback)
    {
        return element.TryGetProperty(name, out var value) && value.TryGetDouble(out var parsed) ? parsed : fallback;
    }

    private static void RecordWatch(string path)
    {
        if (File.Exists(path))
            s_lastWriteUtc[path] = File.GetLastWriteTimeUtc(path);
    }

    private static bool ConfigsEqual(VehicleConfigData a, VehicleConfigData b)
    {
        return a.TireDiameterInches == b.TireDiameterInches
            && a.FinalDriveRatio == b.FinalDriveRatio
            && a.ShiftUpRpm == b.ShiftUpRpm
            && a.ShiftDownRpm == b.ShiftDownRpm
            && a.GearRatios.SequenceEqual(b.GearRatios);
    }
}

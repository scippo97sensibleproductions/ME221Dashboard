using ME221.Data.Models;
using Microsoft.Extensions.Logging;

namespace ME221.Emulator.Domain;

public sealed class SensorSimulator
{
    private readonly EntityStore _entityStore;
    private readonly ILogger<SensorSimulator> _logger;
    private readonly Random _random = new();
    private readonly Dictionary<string, ushort> _dataKeyToId;
    private readonly Dictionary<string, (ushort Id, Func<SimContext, float> Sim)> _nameSim;
    private VehicleConfigData _vehicleConfig;
    private double _tireCircumferenceMeters;
    private string? _sharedConfigPath;
    private CalibrationData? _calibration;
    private DateTime _lastConfigReload = DateTime.MinValue;
    private double _simulatedTime;
    private int _tickCount;

    // ── Gear state ──
    private int _currentGear = 1;
    private const float ShiftUpRpm = 6000f;
    private const float ShiftDownRpm = 2000f;

    // ── RPM sweep mode state machine ──
    private enum DrivingMode { Idle, RpmSweepUp, RpmHoldHigh, RpmSweepDown, RpmHoldLow }
    private DrivingMode _currentMode = DrivingMode.Idle;
    private double _modeStartTime;
    private const double IdleDuration = 5.0;
    private const double SweepUpDuration = 60.0;
    private const double HoldHighDuration = 3.0;
    private const double SweepDownDuration = 60.0;
    private const double HoldLowDuration = 3.0;

    // Smoothed values for interconnected simulation
    private float _smoothRpm;
    private float _smoothMap;
    private float _smoothTps;
    private float _smoothBatteryV;
    private float _smoothCoolantTemp;
    private float _prevTps;

    // ── Computed speed from gear model ──
    private float _computedSpeed;

    public SensorSimulator(CalibrationData calibration, EntityStore entityStore, ILogger<SensorSimulator> logger, VehicleConfigData? vehicleConfig = null)
    {
        _entityStore = entityStore;
        _logger = logger;
        _vehicleConfig = vehicleConfig ?? new VehicleConfigData();
        _tireCircumferenceMeters = Math.PI * _vehicleConfig.TireDiameterInches * 0.0254;
        _calibration = calibration;

        _sharedConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".me221", "vehicle-config.json");

        _dataKeyToId = calibration.DataLinks
            .Where(dl => dl.DataKey is not null)
            .ToDictionary(dl => dl.DataKey!, dl => dl.Id);

        _smoothRpm = 800f;
        _smoothMap = 35f;
        _smoothTps = 0f;
        _smoothBatteryV = 13.2f;
        _smoothCoolantTemp = 82f;
        _modeStartTime = 0;

        _nameSim = BuildNameSimulation(calibration.DataLinks);

        _logger.LogInformation("SensorSimulator: initialized — {DataKeyCount} data-key links, {NameCount} name-based sims, {Gears} gears, final={Final:F2}, tire={Tire:F1}\", configPath={ConfigPath}",
            _dataKeyToId.Count, _nameSim.Count, _vehicleConfig.GearRatios.Length, _vehicleConfig.FinalDriveRatio, _vehicleConfig.TireDiameterInches, _sharedConfigPath);
    }

    public void Tick()
    {
        _tickCount++;
        _simulatedTime += 0.1; // ~10Hz tick rate

        // Hot-reload vehicle config from dashboard every ~5 seconds
        if (_tickCount % 50 == 0)
        {
            var reloaded = VehicleConfigLoader.ReloadIfChanged(_sharedConfigPath, _calibration, _logger);
            if (reloaded is not null)
            {
                _logger.LogWarning(
                    "Vehicle config reloaded: gears=[{Gears}], final={Final:F2}, tire={Tire:F1}\"",
                    string.Join(",", reloaded.GearRatios.Select(g => g.ToString("F2"))),
                    reloaded.FinalDriveRatio, reloaded.TireDiameterInches);
                _vehicleConfig = reloaded;
                _tireCircumferenceMeters = Math.PI * _vehicleConfig.TireDiameterInches * 0.0254;
            }
        }

        var now = DateTime.UtcNow;
        var elapsed = _simulatedTime;

        // ── Advance driving mode state machine ──
        AdvanceDrivingMode(elapsed);

        // ── Compute smoothed primary values ──
        var (targetRpm, targetMap, targetTps) = GetModeTargets(elapsed);
        var dt = 0.1f; // ~10Hz tick rate

        _smoothRpm += (targetRpm - _smoothRpm) * Math.Clamp(dt * 1.5f, 0.01f, 0.3f);
        _smoothMap += (targetMap - _smoothMap) * Math.Clamp(dt * 1.2f, 0.01f, 0.25f);
        _smoothTps += (targetTps - _smoothTps) * Math.Clamp(dt * 2.0f, 0.01f, 0.4f);
        _smoothBatteryV += (ComputeBatteryVoltage() - _smoothBatteryV) * 0.02f;
        _smoothCoolantTemp += (85f - _smoothCoolantTemp) * 0.0005f; // slow warmup toward 85°C

        // ── Add natural variation (idle hunt, road load, etc.) ──
        var rpmVariation = ComputeRpmVariation(elapsed);
        var rpm = _smoothRpm + rpmVariation;

        var mapNoise = (float)(_random.NextDouble() - 0.5f) * 1.5f;
        var map = _smoothMap + mapNoise;

        var tpsNoise = (float)(_random.NextDouble() - 0.5f) * 0.3f;
        var tps = Math.Clamp(_smoothTps + tpsNoise, 0f, 100f);

        // ── Gear logic and speed from RPM ──
        UpdateGear(rpm);
        _computedSpeed = RpmToSpeedKmh(rpm, _currentGear);
        var speed = Math.Max(0f, _computedSpeed + (float)(_random.NextDouble() - 0.5f) * 0.5f);
        var batteryV = _smoothBatteryV + (float)(_random.NextDouble() - 0.5f) * 0.05f;
        var coolantTemp = _smoothCoolantTemp + (float)(_random.NextDouble() - 0.5f) * 0.5f;

        // ── Derived values ──
        var injectorDuty = ComputeInjectorDuty(rpm, map);
        var injectorPw = ComputeInjectorPw(rpm, map);
        var airflow = ComputeAirflow(rpm, map);
        var ignAdvance = ComputeIgnitionAdvance(rpm, map);
        var ve = ComputeVolumetricEfficiency(rpm, map);
        var lambdaAfr = ComputeLambdaAfr(rpm, map, tps);
        var oilPressure = ComputeOilPressure(rpm, coolantTemp);
        var fuelPressure = ComputeFuelPressure(rpm, map);
        var ethanolContent = 10f + 2f * (float)Math.Sin(elapsed * 0.02);
        var idleTargetRpm = ComputeIdleTargetRpm(coolantTemp);
        var idleRpmError = rpm - idleTargetRpm;
        var knockLevel = ComputeKnockLevel(rpm, map, ignAdvance);
        var vvtPosition = ComputeVvtPosition(rpm, map);

        // ── DataKey-based simulations ──
        SetIfPresent("RPM", Math.Max(0f, rpm));
        SetIfPresent("MAP", map);
        SetIfPresent("TPS", tps);
        SetIfPresent("AFR_ERR", (lambdaAfr - 14.7f) * 0.5f);
        SetIfPresent("SYNC_STATUS", rpm > 50f ? 1f : 0f);
        SetIfPresent("INJ_MAX_DUTY_CNT", injectorDuty * 1.2f);
        SetIfPresent("TPS_RAW", tps / 100f * 5f);

        SetIfPresent("DAY", now.Day);
        SetIfPresent("MONTH", now.Month);
        SetIfPresent("YEAR", now.Year);
        SetIfPresent("HOUR", now.Hour);
        SetIfPresent("MIN", now.Minute);
        SetIfPresent("SEC", now.Second);

        SetIfPresent("TC_STATUS", 0f);
        SetIfPresent("LC_STATUS", 0f);
        SetIfPresent("ALS_STATUS", 0f);
        SetIfPresent("CAN_STATUS", 0f);
        SetIfPresent("KNK_STATUS", knockLevel > 0.3f ? 4f : 1f);
        SetIfPresent("BST_STATUS", map > 100f ? 1f : 0f);
        SetIfPresent("FC_STATUS", 0f);
        SetIfPresent("SC_STATUS", rpm > 6500f ? 1f : 0f);

        SetIfPresent("SEL_MAP_BOOST", 0f);
        SetIfPresent("SEL_MAP_IGN", 0f);
        SetIfPresent("SEL_MAP_INJ", 0f);

        var pps = _smoothTps / 100f;
        SetIfPresent("PPS1_RAW", pps * 5f * 0.5f);
        SetIfPresent("PPS2_RAW", pps * 5f * 0.45f);
        SetIfPresent("TPPS1_RAW", pps * 5f * 0.48f);
        SetIfPresent("TPPS2_RAW", pps * 5f * 0.43f);
        SetIfPresent("PPS1_VAL", _smoothTps * 0.5f);
        SetIfPresent("PPS2_VAL", _smoothTps * 0.45f);
        SetIfPresent("TPPS1_VAL", _smoothTps * 0.48f);
        SetIfPresent("TPPS2_VAL", _smoothTps * 0.43f);

        SetIfPresent("DBW_PWM", _smoothTps * 0.9f);
        SetIfPresent("DBW_BATT_V_MULT", batteryV);

        // ── Name-based simulations (links without DataKey) ──
        var ctx = new SimContext
        {
            Elapsed = elapsed,
            Rpm = rpm,
            Map = map,
            Tps = tps,
            Speed = speed,
            Gear = _currentGear,
            BatteryVoltage = batteryV,
            CoolantTemp = coolantTemp,
            InjectorDuty = injectorDuty,
            InjectorPw = injectorPw,
            Airflow = airflow,
            IgnitionAdvance = ignAdvance,
            VolumetricEfficiency = ve,
            LambdaAfr = lambdaAfr,
            OilPressure = oilPressure,
            FuelPressure = fuelPressure,
            EthanolContent = ethanolContent,
            IdleTargetRpm = idleTargetRpm,
            IdleRpmError = idleRpmError,
            KnockLevel = knockLevel,
            VvtPosition = vvtPosition,
            PrevTps = _prevTps,
        };

        foreach (var (_, (id, sim)) in _nameSim)
            _entityStore.SetDataLinkValue(id, sim(ctx));

        _prevTps = tps;

        // Log key sensor values every ~10 ticks
        if (_tickCount % 10 == 0)
        {
            _logger.LogDebug(
                "SensorSimulator tick #{TickCount} [{Mode}]: RPM={RPM:F0} MAP={MAP:F1} TPS={TPS:F1} Speed={Speed:F0} Batt={Batt:F2} CLT={CLT:F1}",
                _tickCount, _currentMode, rpm, map, tps, speed, batteryV, coolantTemp);
        }
    }

    // ── Driving mode state machine ──

    private void AdvanceDrivingMode(double elapsed)
    {
        var modeElapsed = elapsed - _modeStartTime;
        var duration = _currentMode switch
        {
            DrivingMode.Idle => IdleDuration,
            DrivingMode.RpmSweepUp => SweepUpDuration,
            DrivingMode.RpmHoldHigh => HoldHighDuration,
            DrivingMode.RpmSweepDown => SweepDownDuration,
            DrivingMode.RpmHoldLow => HoldLowDuration,
            _ => IdleDuration,
        };

        if (modeElapsed >= duration)
        {
            _currentMode = _currentMode switch
            {
                DrivingMode.Idle => DrivingMode.RpmSweepUp,
                DrivingMode.RpmSweepUp => DrivingMode.RpmHoldHigh,
                DrivingMode.RpmHoldHigh => DrivingMode.RpmSweepDown,
                DrivingMode.RpmSweepDown => DrivingMode.RpmHoldLow,
                DrivingMode.RpmHoldLow => DrivingMode.Idle,
                _ => DrivingMode.Idle,
            };
            _modeStartTime = elapsed;
        }
    }

    private (float rpm, float map, float tps) GetModeTargets(double elapsed)
    {
        var modeElapsed = elapsed - _modeStartTime;
        var sweepT = (float)Math.Clamp(modeElapsed / SweepUpDuration, 0.0, 1.0);

        return _currentMode switch
        {
            DrivingMode.Idle => (800f, 35f, 0f),
            DrivingMode.RpmSweepUp => (
                800f + 9200f * EaseInOut(sweepT),
                35f + 55f * EaseInOut(sweepT),
                40f * EaseInOut(sweepT)
            ),
            DrivingMode.RpmHoldHigh => (10000f, 90f, 0f),
            DrivingMode.RpmSweepDown => (
                10000f - 9200f * EaseInOut(sweepT),
                90f - 55f * EaseInOut(sweepT),
                40f * (1f - EaseInOut(sweepT))
            ),
            DrivingMode.RpmHoldLow => (800f, 35f, 0f),
            _ => (800f, 35f, 0f),
        };
    }

    private void UpdateGear(float rpm)
    {
        var maxGear = _vehicleConfig.GearRatios.Length;
        if (maxGear == 0) return;

        if (rpm > ShiftUpRpm && _currentGear < maxGear)
            _currentGear++;
        else if (rpm < ShiftDownRpm && _currentGear > 1)
            _currentGear--;
    }

    private float RpmToSpeedKmh(float rpm, int gear)
    {
        if (gear < 1 || gear > _vehicleConfig.GearRatios.Length || rpm <= 0)
            return 0f;

        var gearRatio = _vehicleConfig.GearRatios[gear - 1];
        var totalRatio = gearRatio * _vehicleConfig.FinalDriveRatio;
        // speed (km/h) = RPM * tire_circumference(m) * 60(min/h) / (total_ratio * 1000(m/km))
        return (float)(rpm * _tireCircumferenceMeters * 60.0 / (totalRatio * 1000.0));
    }

    private static float EaseInOut(float t) => t * t * (3f - 2f * t);

    private float ComputeRpmVariation(double elapsed)
    {
        // Idle hunt: low-frequency oscillation
        var idleHunt = 30f * (float)Math.Sin(elapsed * 2.5) +
                       15f * (float)Math.Sin(elapsed * 5.3) +
                       8f * (float)Math.Sin(elapsed * 11.7);

        // Road load variation at cruise
        var roadLoad = 50f * (float)Math.Sin(elapsed * 0.8) +
                       25f * (float)Math.Sin(elapsed * 2.1);

        // White noise
        var noise = (float)(_random.NextDouble() - 0.5f) * 10f;

        return _currentMode switch
        {
            DrivingMode.Idle => idleHunt * 0.5f + noise,
            DrivingMode.RpmSweepUp => noise * 0.5f,
            DrivingMode.RpmHoldHigh => roadLoad * 0.3f + noise,
            DrivingMode.RpmSweepDown => noise * 0.5f,
            DrivingMode.RpmHoldLow => idleHunt * 0.3f + noise,
            _ => noise,
        };
    }

    // ── Derived value computations ──

    private float ComputeInjectorDuty(float rpm, float map) =>
        Math.Clamp(3f + map * 0.15f + rpm * 0.005f, 0f, 100f);

    private float ComputeInjectorPw(float rpm, float map) =>
        Math.Clamp(1.5f + map * 0.04f + (rpm > 0 ? 60000f / rpm * 0.3f : 0f), 0f, 20f);

    private float ComputeAirflow(float rpm, float map) =>
        Math.Clamp(map * rpm * 0.00002f, 0f, 500f);

    private float ComputeVolumetricEfficiency(float rpm, float map) =>
        Math.Clamp(50f + 30f * (float)Math.Sin(rpm * 0.001 * Math.PI) + map * 0.1f, 10f, 120f);

    private float ComputeIgnitionAdvance(float rpm, float map) =>
        Math.Clamp(10f + rpm * 0.003f - map * 0.08f + 5f * (float)Math.Sin(rpm * 0.0005), 0f, 45f);

    private float ComputeLambdaAfr(float rpm, float map, float tps)
    {
        var baseAfr = 14.7f;
        if (tps > 30f) baseAfr = 13.5f - (tps - 30f) * 0.02f; // enrichment under load
        if (rpm > 5000f) baseAfr -= 0.5f; // high-rpm enrichment
        return Math.Clamp(baseAfr + (float)(_random.NextDouble() - 0.5f) * 0.2f, 10f, 16f);
    }

    private float ComputeOilPressure(float rpm, float coolantTemp)
    {
        var tempFactor = Math.Clamp((coolantTemp - 20f) / 80f, 0.3f, 1f);
        return Math.Clamp(30f + rpm * 0.03f * tempFactor, 0f, 200f);
    }

    private float ComputeFuelPressure(float rpm, float map) =>
        Math.Clamp(300f + map * 0.5f + rpm * 0.01f, 200f, 500f);

    private float ComputeBatteryVoltage()
    {
        var rpmFactor = Math.Clamp((_smoothRpm - 500f) / 3000f, 0f, 1f);
        return 12.5f + rpmFactor * 1.5f;
    }

    private float ComputeIdleTargetRpm(float coolantTemp)
    {
        if (coolantTemp < 40f) return 1200f - (coolantTemp - 20f) * 10f;
        if (coolantTemp < 80f) return 900f - (coolantTemp - 40f) * 2.5f;
        return 800f;
    }

    private float ComputeKnockLevel(float rpm, float map, float ignAdvance) =>
        Math.Max(0f, (map / 100f) * (ignAdvance / 30f) * 0.3f + (float)(_random.NextDouble() - 0.5f) * 0.1f);

    private float ComputeVvtPosition(float rpm, float map) =>
        Math.Clamp(20f + rpm * 0.005f - map * 0.1f, 0f, 50f);

    // ── Name-based simulation builder ──

    private static Dictionary<string, (ushort Id, Func<SimContext, float> Sim)> BuildNameSimulation(
        IReadOnlyList<DataLinkDefinition> links)
    {
        var map = new Dictionary<string, (ushort, Func<SimContext, float>)>();

        foreach (var link in links)
        {
            if (link.DataKey is not null)
                continue;

            Func<SimContext, float>? sim = link.MeasureUnit switch
            {
                "V" or "Volt" when link.Name.Contains("Battery", StringComparison.OrdinalIgnoreCase)
                    => ctx => ctx.BatteryVoltage,
                "V" or "Volt"
                    => ctx => 5f + 2f * (float)Math.Sin(ctx.Elapsed * 0.1),
                "%" or "Percent" when link.Name.Contains("Duty", StringComparison.OrdinalIgnoreCase)
                    => ctx => ctx.InjectorDuty,
                "%" or "Percent" when link.Name.Contains("VE", StringComparison.OrdinalIgnoreCase)
                    => ctx => ctx.VolumetricEfficiency,
                "%" or "Percent" when link.Name.Contains("TPS", StringComparison.OrdinalIgnoreCase)
                    || link.Name.Contains("Pedal", StringComparison.OrdinalIgnoreCase)
                    || link.Name.Contains("Throttle", StringComparison.OrdinalIgnoreCase)
                    => ctx => ctx.Tps,
                "%" or "Percent"
                    => ctx => 30f + 30f * (float)Math.Sin(ctx.Elapsed * 0.2),
                "C" or "\u00B0C" or "degC"
                    => ctx => ctx.CoolantTemp,
                "F" or "\u00B0F" or "degF"
                    => ctx => 176f + 27f * (float)Math.Sin(ctx.Elapsed * 0.05),
                "kPa" or "KPa" or "bar" or "PSI" or "psi" when link.Name.Contains("Oil", StringComparison.OrdinalIgnoreCase)
                    => ctx => ctx.OilPressure,
                "kPa" or "KPa" or "bar" or "PSI" or "psi" when link.Name.Contains("Fuel", StringComparison.OrdinalIgnoreCase)
                    => ctx => ctx.FuelPressure,
                "kPa" or "KPa" or "bar" or "PSI" or "psi"
                    => ctx => ctx.Map,
                "rpm" or "RPM"
                    => ctx => ctx.Rpm,
                "ms"
                    => ctx => ctx.InjectorPw,
                "deg" or "\u00B0" when link.Name.Contains("Adv", StringComparison.OrdinalIgnoreCase)
                    => ctx => ctx.IgnitionAdvance,
                "deg" or "\u00B0" when link.Name.Contains("VVT", StringComparison.OrdinalIgnoreCase)
                    || link.Name.Contains("Cam", StringComparison.OrdinalIgnoreCase)
                    => ctx => ctx.VvtPosition,
                "deg" or "\u00B0"
                    => ctx => 10f + 20f * (float)Math.Sin(ctx.Elapsed * 0.1),
                "g/s" or "gps"
                    => ctx => ctx.Airflow,
                "L/hr"
                    => ctx => ctx.Airflow * 0.08f,
                "s"
                    => ctx => 10f + 5f * (float)Math.Sin(ctx.Elapsed * 0.05),
                "/sec" or "%/sec"
                    => ctx => 5f + 3f * (float)Math.Sin(ctx.Elapsed * 0.3),
                "" or "bool"
                    => BuildEmptyUnitSim(link),
                _
                    => ctx => (float)(50f * Math.Sin(ctx.Elapsed * 0.1)),
            };

            if (sim is not null)
                map[link.Name] = (link.Id, sim);
        }

        return map;
    }

    private static Func<SimContext, float>? BuildEmptyUnitSim(DataLinkDefinition link)
    {
        var name = link.Name;

        // Status/flag links with TextValues should stay at 0
        if (link.TextValues is { Count: > 0 })
            return null;

        // Raw sensor values — derived from known context values
        if (name.Contains("Raw", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("TPS", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.Tps / 100f * 5f;
            if (name.Contains("MAP", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.Map / 100f * 5f;
            if (name.Contains("Coolant", StringComparison.OrdinalIgnoreCase) || name.Contains("CLT", StringComparison.OrdinalIgnoreCase))
                return ctx => 2f + ctx.CoolantTemp / 100f * 3f;
            if (name.Contains("IAT", StringComparison.OrdinalIgnoreCase) || name.Contains("Intake", StringComparison.OrdinalIgnoreCase))
                return _ => 2f + 30f / 100f * 3f; // IAT ~30°C
            if (name.Contains("Battery", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.BatteryVoltage * 0.95f + 0.5f;
            if (name.Contains("Oil", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.OilPressure * 0.02f;
            if (name.Contains("Fuel", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.FuelPressure * 0.01f;
            if (name.Contains("Analog", StringComparison.OrdinalIgnoreCase))
                return ctx => 2.5f + 1.5f * (float)Math.Sin(ctx.Elapsed * 0.3 + ctx.Rpm * 0.0001f);
            // Generic raw
            return ctx => 2.5f + 1.5f * (float)Math.Sin(ctx.Elapsed * 0.2);
        }

        // Speed — from gear model
        if (name.Contains("Speed", StringComparison.OrdinalIgnoreCase) && !name.Contains("Raw", StringComparison.OrdinalIgnoreCase))
            return ctx => ctx.Speed;

        // Gear — from gear model state
        if (name.Contains("Gear", StringComparison.OrdinalIgnoreCase))
            return ctx => ctx.Gear;

        // RPM-related
        if (name.Contains("RPM", StringComparison.OrdinalIgnoreCase) || name.Contains("Rpm", StringComparison.OrdinalIgnoreCase))
            return ctx => ctx.Rpm;

        // AFR / Lambda
        if (name.Contains("AFR", StringComparison.OrdinalIgnoreCase) && !name.Contains("Err", StringComparison.OrdinalIgnoreCase))
            return ctx => ctx.LambdaAfr;
        if (name.Contains("AFR Err", StringComparison.OrdinalIgnoreCase) || name.Contains("AFR_ERR", StringComparison.OrdinalIgnoreCase))
            return ctx => (ctx.LambdaAfr - 14.7f) * 0.5f;
        if (name.Contains("Lambda", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("AFR", StringComparison.OrdinalIgnoreCase) || name.Contains("Curr", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.LambdaAfr;
            if (name.Contains("Err", StringComparison.OrdinalIgnoreCase))
                return ctx => (ctx.LambdaAfr - 14.7f) * 0.3f;
            if (name.Contains("Trim", StringComparison.OrdinalIgnoreCase))
                return ctx => 1f + 0.5f * (float)Math.Sin(ctx.Elapsed * 0.15);
            if (name.Contains("Conf", StringComparison.OrdinalIgnoreCase))
                return ctx => 50f + 20f * (float)Math.Sin(ctx.Elapsed * 0.1);
            return null; // Lambda Status has TextValues
        }

        // Trim values
        if (name.Contains("Trim", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Cyl", StringComparison.OrdinalIgnoreCase))
                return ctx => (float)((ctx.Rpm * 0.001f + ctx.Map * 0.01f) % 5f - 2.5f);
            if (name.Contains("CLT", StringComparison.OrdinalIgnoreCase))
                return ctx => 5f + 3f * (float)Math.Sin(ctx.Elapsed * 0.08);
            if (name.Contains("IAT", StringComparison.OrdinalIgnoreCase))
                return ctx => 2f + 2f * (float)Math.Sin(ctx.Elapsed * 0.1);
            if (name.Contains("Idle", StringComparison.OrdinalIgnoreCase))
                return ctx => 3f + 2f * (float)Math.Sin(ctx.Elapsed * 0.2);
            if (name.Contains("Sec. Load", StringComparison.OrdinalIgnoreCase))
                return ctx => 1f + 1f * (float)Math.Sin(ctx.Elapsed * 0.15);
            if (name.Contains("Knock", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.KnockLevel * 5f;
            if (name.Contains("Cranking", StringComparison.OrdinalIgnoreCase))
                return ctx => 10f + 5f * (float)Math.Sin(ctx.Elapsed * 0.03);
            if (name.Contains("ASE", StringComparison.OrdinalIgnoreCase))
                return ctx => Math.Max(0f, 15f * (float)Math.Exp(-ctx.Elapsed * 0.01));
            if (name.Contains("Limiter", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.Rpm > 6500f ? 5f : 0f;
            if (name.Contains("Overrun", StringComparison.OrdinalIgnoreCase))
                return _ => 0f;
            if (name.Contains("Lambda", StringComparison.OrdinalIgnoreCase))
                return ctx => 1f + 0.5f * (float)Math.Sin(ctx.Elapsed * 0.15);
            if (name.Contains("AC", StringComparison.OrdinalIgnoreCase))
                return _ => 0.5f;
            if (name.Contains("LC", StringComparison.OrdinalIgnoreCase))
                return _ => 0f;
            // Generic trim
            return ctx => 3f * (float)Math.Sin(ctx.Elapsed * 0.2);
        }

        // PID compensator values (P/I/D Comp)
        if (name.Contains("Comp", StringComparison.OrdinalIgnoreCase) || name.Contains("P Comp", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("P Comp", StringComparison.OrdinalIgnoreCase))
                return ctx => 5f + 3f * (float)Math.Sin(ctx.Elapsed * 0.25);
            if (name.Contains("I Comp", StringComparison.OrdinalIgnoreCase))
                return ctx => 3f + 2f * (float)Math.Sin(ctx.Elapsed * 0.15);
            if (name.Contains("D Comp", StringComparison.OrdinalIgnoreCase))
                return ctx => 1f + 1f * (float)Math.Sin(ctx.Elapsed * 0.35);
            if (name.Contains("Friction", StringComparison.OrdinalIgnoreCase))
                return ctx => 2f + 1f * (float)Math.Sin(ctx.Elapsed * 0.2);
            if (name.Contains("Spring", StringComparison.OrdinalIgnoreCase))
                return ctx => 3f + 1.5f * (float)Math.Sin(ctx.Elapsed * 0.18);
            return ctx => 3f * (float)Math.Sin(ctx.Elapsed * 0.2);
        }

        // Error values
        if (name.Contains("Err", StringComparison.OrdinalIgnoreCase) || name.Contains("Error", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Idle", StringComparison.OrdinalIgnoreCase) || name.Contains("Target RPM", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.IdleRpmError;
            return ctx => 2f * (float)Math.Sin(ctx.Elapsed * 0.3);
        }

        // Target values
        if (name.Contains("Target", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("AFR", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.Tps > 30f ? 13.5f : 14.7f;
            if (name.Contains("MAP", StringComparison.OrdinalIgnoreCase) || name.Contains("Boost", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.Map + 5f;
            if (name.Contains("RPM", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.IdleTargetRpm;
            if (name.Contains("TPPS", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.Tps;
            return ctx => 50f + 30f * (float)Math.Sin(ctx.Elapsed * 0.1);
        }

        // VVT
        if (name.Contains("VVT", StringComparison.OrdinalIgnoreCase) || name.Contains("Cam", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Status", StringComparison.OrdinalIgnoreCase))
                return null; // has TextValues
            if (name.Contains("Duty", StringComparison.OrdinalIgnoreCase))
                return ctx => Math.Clamp(ctx.Rpm * 0.005f + ctx.Map * 0.1f, 0f, 100f);
            if (name.Contains("Adv", StringComparison.OrdinalIgnoreCase) || name.Contains("Curr", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.VvtPosition;
            if (name.Contains("Target", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.VvtPosition + 2f;
            return ctx => ctx.VvtPosition;
        }

        // Idle-related
        if (name.Contains("Idle", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Duty", StringComparison.OrdinalIgnoreCase))
                return ctx => 15f + 10f * (float)Math.Sin(ctx.Elapsed * 0.15);
            if (name.Contains("Target RPM", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.IdleTargetRpm;
            if (name.Contains("Status", StringComparison.OrdinalIgnoreCase))
                return null; // has TextValues
            if (name.Contains("Stepper", StringComparison.OrdinalIgnoreCase))
                return ctx => 30f + 10f * (float)Math.Sin(ctx.Elapsed * 0.1);
            if (name.Contains("Batt", StringComparison.OrdinalIgnoreCase))
                return ctx => 3f + 1f * (float)Math.Sin(ctx.Elapsed * 0.2);
            return ctx => 10f + 8f * (float)Math.Sin(ctx.Elapsed * 0.15);
        }

        // Ignition advance related (remaining ones that aren't deg unit)
        if (name.Contains("Adv", StringComparison.OrdinalIgnoreCase) && !name.Contains("VVT", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Knock", StringComparison.OrdinalIgnoreCase))
                return ctx => -ctx.KnockLevel * 3f;
            if (name.Contains("CLT", StringComparison.OrdinalIgnoreCase))
                return ctx => 2f + 2f * (float)Math.Sin(ctx.Elapsed * 0.08);
            if (name.Contains("IAT", StringComparison.OrdinalIgnoreCase))
                return ctx => 1f + 1f * (float)Math.Sin(ctx.Elapsed * 0.1);
            if (name.Contains("Sec. Load", StringComparison.OrdinalIgnoreCase))
                return ctx => 1f + 1.5f * (float)Math.Sin(ctx.Elapsed * 0.15);
            if (name.Contains("Limiter", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.Rpm > 6500f ? -5f : 0f;
            if (name.Contains("ALS", StringComparison.OrdinalIgnoreCase))
                return _ => 0f;
            if (name.Contains("LC", StringComparison.OrdinalIgnoreCase))
                return _ => 0f;
            if (name.Contains("AC", StringComparison.OrdinalIgnoreCase))
                return _ => 1f;
            if (name.Contains("Spark Scatter", StringComparison.OrdinalIgnoreCase))
                return ctx => 2f * (float)Math.Sin(ctx.Elapsed * 0.5);
            if (name.Contains("Trailing", StringComparison.OrdinalIgnoreCase))
                return ctx => 10f + 5f * (float)Math.Sin(ctx.Elapsed * 0.15);
            return ctx => ctx.IgnitionAdvance;
        }

        // Knock
        if (name.Contains("Knock", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Status", StringComparison.OrdinalIgnoreCase))
                return null; // has TextKeys
            if (name.Contains("Cyl", StringComparison.OrdinalIgnoreCase) && name.Contains("Peak", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.KnockLevel * 20f;
            if (name.Contains("Cyl", StringComparison.OrdinalIgnoreCase) && name.Contains("Cnt", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.KnockLevel > 0.5f ? 1f : 0f;
            if (name.Contains("Acc", StringComparison.OrdinalIgnoreCase) || name.Contains("Lvl", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.KnockLevel * 50f;
            if (name.Contains("Reading", StringComparison.OrdinalIgnoreCase))
            {
                if (name.Contains("Raw", StringComparison.OrdinalIgnoreCase))
                    return ctx => ctx.KnockLevel * 100f;
                if (name.Contains("Peak", StringComparison.OrdinalIgnoreCase))
                    return ctx => ctx.KnockLevel * 80f;
                return ctx => ctx.KnockLevel * 60f;
            }
            if (name.Contains("CLT", StringComparison.OrdinalIgnoreCase))
                return ctx => 0.8f + 0.2f * (float)Math.Sin(ctx.Elapsed * 0.05);
            if (name.Contains("Rel", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.KnockLevel;
            if (name.Contains("Events", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.KnockLevel > 0.4f ? 1f : 0f;
            return ctx => ctx.KnockLevel * 30f;
        }

        // Status values (non-TextValue) — diagnostic states derived from driving
        if (name.Contains("Status", StringComparison.OrdinalIgnoreCase) && link.TextValues is not { Count: > 0 })
        {
            if (name.Contains("Overrun", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.Rpm > 1500f && ctx.Tps < 3f ? 2f : 0f;
            if (name.Contains("Launch", StringComparison.OrdinalIgnoreCase))
                return _ => 0f;
            if (name.Contains("ALS", StringComparison.OrdinalIgnoreCase))
                return _ => 0f;
            if (name.Contains("Water", StringComparison.OrdinalIgnoreCase))
                return _ => 0f;
            if (name.Contains("Alt", StringComparison.OrdinalIgnoreCase))
                return _ => 3f; // Run Mode
            if (name.Contains("Fan", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.CoolantTemp > 90f ? 2f : 0f;
            if (name.Contains("VICS", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.Rpm > 4500f ? 1f : 0f;
            if (name.Contains("EVAP", StringComparison.OrdinalIgnoreCase))
                return _ => 0f;
            if (name.Contains("AC", StringComparison.OrdinalIgnoreCase))
                return _ => 3f; // Active
            return _ => 0f;
        }

        // Boost
        if (name.Contains("Boost", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Duty", StringComparison.OrdinalIgnoreCase))
                return ctx => 30f + 20f * (float)Math.Sin(ctx.Elapsed * 0.2);
            if (name.Contains("Target", StringComparison.OrdinalIgnoreCase) && name.Contains("MAP", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.Map + 5f;
            if (name.Contains("Target", StringComparison.OrdinalIgnoreCase) && name.Contains("Gear", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.Map;
            if (name.Contains("Status", StringComparison.OrdinalIgnoreCase))
                return null; // has TextValues
            return ctx => 40f + 20f * (float)Math.Sin(ctx.Elapsed * 0.2);
        }

        // Alternator control
        if (name.Contains("Alt.", StringComparison.OrdinalIgnoreCase) || name.Contains("Alt Ctrl", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Duty", StringComparison.OrdinalIgnoreCase))
                return ctx => 50f + 20f * (float)Math.Sin(ctx.Elapsed * 0.1);
            return ctx => 5f + 3f * (float)Math.Sin(ctx.Elapsed * 0.2);
        }

        // DBW (Drive-by-Wire) non-unit values
        if (name.Contains("DBW", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Status", StringComparison.OrdinalIgnoreCase))
                return null; // has TextValues
            if (name.Contains("Sens", StringComparison.OrdinalIgnoreCase))
                return _ => 0f;
            if (name.Contains("Batt", StringComparison.OrdinalIgnoreCase))
                return ctx => 2f + 1f * (float)Math.Sin(ctx.Elapsed * 0.2);
            return ctx => 5f + 3f * (float)Math.Sin(ctx.Elapsed * 0.2);
        }

        // Startup/ASE
        if (name.Contains("ASE", StringComparison.OrdinalIgnoreCase) || name.Contains("Cranking", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Decay", StringComparison.OrdinalIgnoreCase))
                return ctx => Math.Max(0f, 30f * (float)Math.Exp(-ctx.Elapsed * 0.005));
            return ctx => Math.Max(0f, 15f * (float)Math.Exp(-ctx.Elapsed * 0.01));
        }

        // Limiter
        if (name.Contains("Lim", StringComparison.OrdinalIgnoreCase) || name.Contains("Limiter", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("RPM", StringComparison.OrdinalIgnoreCase))
                return _ => 7200f;
            return ctx => ctx.Rpm > 6500f ? 10f : 0f;
        }

        // AE (Accel Enrich)
        if (name.Contains("AE", StringComparison.OrdinalIgnoreCase) || name.Contains("Accel", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Delta", StringComparison.OrdinalIgnoreCase))
                return ctx => (ctx.Tps - ctx.PrevTps) * 10f;
            if (name.Contains("MAP", StringComparison.OrdinalIgnoreCase))
                return ctx => (ctx.Map - 35f) * 0.5f;
            if (name.Contains("TPS", StringComparison.OrdinalIgnoreCase))
                return ctx => (ctx.Tps - ctx.PrevTps) * 5f;
            if (name.Contains("Equiv", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.Tps * 0.8f;
            return ctx => 5f + 5f * (float)Math.Sin(ctx.Elapsed * 0.3);
        }

        // Flex Fuel / Ethanol
        if (name.Contains("Flex", StringComparison.OrdinalIgnoreCase) || name.Contains("Ethanol", StringComparison.OrdinalIgnoreCase))
            return ctx => ctx.EthanolContent;

        // Main table lookup outputs (these appear without units)
        if (name.StartsWith("Main - ", StringComparison.OrdinalIgnoreCase))
        {
            var mainName = name[7..];
            if (mainName.Contains("VE", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.VolumetricEfficiency;
            if (mainName.Contains("AFR", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.LambdaAfr;
            if (mainName.Contains("Ign", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.IgnitionAdvance;
            if (mainName.Contains("Boost", StringComparison.OrdinalIgnoreCase))
                return ctx => 30f + 20f * (float)Math.Sin(ctx.Elapsed * 0.2);
            if (mainName.Contains("Idle", StringComparison.OrdinalIgnoreCase))
                return ctx => 15f + 10f * (float)Math.Sin(ctx.Elapsed * 0.15);
            if (mainName.Contains("ASE", StringComparison.OrdinalIgnoreCase))
                return ctx => Math.Max(0f, 15f * (float)Math.Exp(-ctx.Elapsed * 0.01));
            if (mainName.Contains("Cranking", StringComparison.OrdinalIgnoreCase))
                return ctx => 10f + 5f * (float)Math.Sin(ctx.Elapsed * 0.03);
            if (mainName.Contains("Priming", StringComparison.OrdinalIgnoreCase))
                return ctx => 5f + 3f * (float)Math.Sin(ctx.Elapsed * 0.1);
            return ctx => 50f * (float)Math.Sin(ctx.Elapsed * 0.1);
        }

        // Ethanol content
        if (name.Contains("Ethanol", StringComparison.OrdinalIgnoreCase))
            return ctx => ctx.EthanolContent;

        // Generic fallback for empty unit — non-zero sine wave
        return ctx => 25f + 25f * (float)Math.Sin(ctx.Elapsed * 0.15 + ctx.Rpm * 0.0005f);
    }

    private void SetIfPresent(string dataKey, float value)
    {
        if (_dataKeyToId.TryGetValue(dataKey, out var id))
            _entityStore.SetDataLinkValue(id, value);
    }

    private float GetValue(string dataKey)
    {
        if (_dataKeyToId.TryGetValue(dataKey, out var id))
            return _entityStore.GetDataLinkValue(id);
        return 0f;
    }

    public sealed class SimContext
    {
        public double Elapsed { get; init; }
        public float Rpm { get; init; }
        public float Map { get; init; }
        public float Tps { get; init; }
        public float Speed { get; init; }
        public int Gear { get; init; }
        public float BatteryVoltage { get; init; }
        public float CoolantTemp { get; init; }
        public float InjectorDuty { get; init; }
        public float InjectorPw { get; init; }
        public float Airflow { get; init; }
        public float IgnitionAdvance { get; init; }
        public float VolumetricEfficiency { get; init; }
        public float LambdaAfr { get; init; }
        public float OilPressure { get; init; }
        public float FuelPressure { get; init; }
        public float EthanolContent { get; init; }
        public float IdleTargetRpm { get; init; }
        public float IdleRpmError { get; init; }
        public float KnockLevel { get; init; }
        public float VvtPosition { get; init; }
        public float PrevTps { get; init; }
    }
}

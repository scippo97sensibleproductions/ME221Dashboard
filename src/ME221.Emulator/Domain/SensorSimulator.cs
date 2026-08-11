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
    private readonly Dictionary<ushort, List<TableDefinition>> _outputLinkToTables;
    private readonly Dictionary<ushort, float> _overrides = new();
    private readonly object _overridesLock = new();
    private VehicleConfigData _vehicleConfig;
    private double _tireCircumferenceMeters;
    private CalibrationData? _calibration;
    private DateTime _lastConfigReload = DateTime.MinValue;
    private double _simulatedTime;
    private int _tickCount;

    // ── Gear state ──
    private int _currentGear = 1;

    // Shift points — the simulated driver shifts up 1000 RPM past the dashboard's
    // configured shift light limit (so the light flashes through approach + shiftNow
    // before the gear change) and engine-brakes down when RPM falls to the coast
    // floor. The upshift limit follows the dashboard's shifter config (shared
    // vehicle-config.json / dashboard-config.json); the coast floor is a physical
    // constant — it must stay well below the upshift point or wide ratio spreads
    // would bounce gears back up on the downshift bump.
    private const float ShiftUpDefaultRpm = 7000f;
    private const float ShiftUpRpmOverrun = 1000f;
    private const float ShiftDownDefaultRpm = 2200f;
    private float _shiftUpRpm = ShiftUpDefaultRpm;
    private float _shiftDownRpm = ShiftDownDefaultRpm;

    // ── Shift state ──
    // A gear change is a proper lift-shift: the driver closes the throttle for
    // ~1 s (TPS 0%, manifold drops to vacuum) while the engine freewheels with
    // the clutch in, then the gear lands and RPM steps to match the (constant)
    // road speed in the new gear. Virtual Dyno expects exactly this — a WOT
    // pull with clean 0% gaps at the shifts.
    private const float ShiftDurationSeconds = 1.0f;
    private const float ShiftRpmDecayRate = 1200f; // rpm/s freewheel with clutch in
    private const float ShiftMapTarget = 32f;      // kPa with the blade closed
    private bool _isShifting;
    private double _shiftStartTime;
    private int _pendingGearChange;
    private float _shiftHoldSpeedKmh;

    // ── ADC simulation ──
    private const float RawAdcMax = 65535f;
    private const float VoltMax = 5f;
    private static float VoltageToRaw(float voltage) => Math.Clamp(voltage / VoltMax * RawAdcMax, 0f, RawAdcMax);

    // ── Drive-cycle state machine ──
    // A full cycle simulates a real drive: idle at standstill → accelerate
    // through the gears (each shift drops RPM by the gear ratio, speed stays
    // continuous) → cruise at the limit → coast down with engine braking
    // (downshifts bump RPM back up) → back to idle.
    private enum DrivingMode { Idle, Accelerate, CruiseHigh, Coast }
    private DrivingMode _currentMode = DrivingMode.Idle;
    private double _modeStartTime;
    private const float IdleDuration = 5f;
    private const float AccelerateDuration = 36f; // cap — normally exits early at top gear + limit
    private const float CruiseHighDuration = 6f;
    private const float CoastDuration = 14f; // includes ~1 s per downshift lift

    // ── Vehicle physics ──
    // The acceleration model is power-limited (110 whp), traction-capped at launch
    // (~0.35 g), and fights rolling + aero drag. RPM rate in each gear follows
    // PHYSICALLY from the wheel acceleration — no arbitrary sweep rates.
    private const float WheelHorsepowerDefault = 110f;
    private const float VehicleMassKgDefault = 1000f;
    private const float MaxLaunchAccel = 3.4f; // m/s² traction ceiling
    private const float DragArea = 0.67f;      // Cd × frontal area (m²)
    private const float AirDensity = 1.225f;   // kg/m³
    private float _wheelHorsepower = WheelHorsepowerDefault;
    private float _vehicleMassKg = VehicleMassKgDefault;
    private const float IdleRpm = 800f;
    private const float CoastDecayRate = 1300f; // rpm/s while coasting (engine braking)
    private float _rpmState = IdleRpm; // physical engine rpm — integrated, stepped on gear change
    private float _smoothMap;
    private float _smoothTps;
    private float _smoothBatteryV;

    // ── Thermal state ──
    // Temperatures are first-class simulated values so temp gauges visibly
    // swing: cold start warms up, coolant follows load with fan hysteresis,
    // IAT reacts to boost heat vs. ram airflow, oil lags coolant, EGT tracks
    // rpm × load. All °C/°F links are routed per-channel (no shared pin).
    private float _smoothCoolantTemp;
    private float _smoothIat;
    private float _smoothOilTemp;
    private float _smoothFuelTemp;
    private float _egtState;
    private float _prevTps;

    // ── Computed speed from gear model ──
    private float _computedSpeed;

    public SensorSimulator(CalibrationData calibration, EntityStore entityStore, ILogger<SensorSimulator> logger, VehicleConfigData? vehicleConfig = null)
    {
        _entityStore = entityStore;
        _logger = logger;
        _vehicleConfig = vehicleConfig ?? new VehicleConfigData();
        _tireCircumferenceMeters = Math.PI * _vehicleConfig.TireDiameterInches * 0.0254;
        _shiftUpRpm = NormalizeShiftUpRpm(_vehicleConfig.ShiftUpRpm) + ShiftUpRpmOverrun;
        _shiftDownRpm = NormalizeShiftDownRpm(_vehicleConfig.ShiftDownRpm, _shiftUpRpm);
        _wheelHorsepower = _vehicleConfig.WheelHorsepower is > 10f ? (float)_vehicleConfig.WheelHorsepower.Value : WheelHorsepowerDefault;
        _vehicleMassKg = _vehicleConfig.MassKg is > 100f ? (float)_vehicleConfig.MassKg.Value : VehicleMassKgDefault;
        _calibration = calibration;

        _dataKeyToId = calibration.DataLinks
            .Where(dl => dl.DataKey is not null)
            .ToDictionary(dl => dl.DataKey!, dl => dl.Id);

        _rpmState = IdleRpm;
        _smoothMap = 35f;
        _smoothTps = 0f;
        _smoothBatteryV = 13.2f;
        _smoothCoolantTemp = 20f; // cold start — warms up through the drive cycle
        _smoothIat = 24f;
        _smoothOilTemp = 20f;
        _smoothFuelTemp = 22f;
        _egtState = 150f;
        _modeStartTime = 0;

        _nameSim = BuildNameSimulation(calibration.DataLinks);

        // Build lookup: output data link ID → tables that produce it
        _outputLinkToTables = new Dictionary<ushort, List<TableDefinition>>();
        foreach (var table in calibration.Tables)
        {
            if (table.OutputLinkId == 0) continue;
            if (!_outputLinkToTables.TryGetValue(table.OutputLinkId, out var list))
            {
                list = [];
                _outputLinkToTables[table.OutputLinkId] = list;
            }
            list.Add(table);
        }

        _logger.LogInformation("SensorSimulator: initialized — {DataKeyCount} data-key links, {NameCount} name-based sims, {TableOutputCount} table output links, {Gears} gears, final={Final:F2}, tire={Tire:F1}, shiftUp={ShiftUp:F0}",
            _dataKeyToId.Count, _nameSim.Count, _outputLinkToTables.Count, _vehicleConfig.GearRatios.Length, _vehicleConfig.FinalDriveRatio, _vehicleConfig.TireDiameterInches, _shiftUpRpm);
    }

    public void Tick()
    {
        _tickCount++;
        _simulatedTime += 0.1; // ~10Hz tick rate

        // Hot-reload vehicle config from the dashboard every ~5 seconds
        if (_tickCount % 50 == 0)
        {
            var reloaded = VehicleConfigLoader.ReloadIfChanged(_calibration, _logger);
            if (reloaded is not null)
            {
                _logger.LogWarning(
                    "Vehicle config reloaded: gears=[{Gears}], final={Final:F2}, tire={Tire:F1}\"",
                    string.Join(",", reloaded.GearRatios.Select(g => g.ToString("F2"))),
                    reloaded.FinalDriveRatio, reloaded.TireDiameterInches);
                _vehicleConfig = reloaded;
                _tireCircumferenceMeters = Math.PI * _vehicleConfig.TireDiameterInches * 0.0254;
                _shiftUpRpm = NormalizeShiftUpRpm(reloaded.ShiftUpRpm) + ShiftUpRpmOverrun;
                _shiftDownRpm = NormalizeShiftDownRpm(reloaded.ShiftDownRpm, _shiftUpRpm);
                _wheelHorsepower = reloaded.WheelHorsepower is > 10f ? (float)reloaded.WheelHorsepower.Value : WheelHorsepowerDefault;
                _vehicleMassKg = reloaded.MassKg is > 100f ? (float)reloaded.MassKg.Value : VehicleMassKgDefault;
                if (_currentGear > reloaded.GearRatios.Length)
                    _currentGear = Math.Max(1, reloaded.GearRatios.Length);
            }
        }

        var now = DateTime.UtcNow;
        var elapsed = _simulatedTime;

        // ── Advance driving mode (conditional transitions) ──
        AdvanceDrivingMode(elapsed);

        // ── Physics: integrate engine RPM under the current mode ──
        // RPM is the physical state of the simulated engine: it climbs under
        // throttle, holds at cruise, decays while coasting, and on a gear change
        // it steps by the gear ratio (speed stays continuous) — exactly like a
        // real car accelerating through the gears.
        var dt = 0.1f; // ~10Hz tick rate
        var (rpmRate, targetMap, targetTps) = GetDriveTargets(elapsed);
        if (!DrivingModeHold)
        {
            _rpmState = Math.Max(IdleRpm, _rpmState + rpmRate * dt);
            UpdateGear(elapsed);
        }

        _smoothMap += (targetMap - _smoothMap) * Math.Clamp(dt * 1.2f, 0.01f, 0.25f);
        // TPS responds fast in a real car: WOT is stabbed in a few ticks and the
        // shift lift closes the blade fully — slow smoothing would smear the 0%
        // shift gap and hide the WOT pull from dyno tools.
        var tpsLerp = _isShifting || _currentMode == DrivingMode.Accelerate ? 0.7f : 0.25f;
        _smoothTps += (targetTps - _smoothTps) * tpsLerp;
        _smoothBatteryV += (ComputeBatteryVoltage() - _smoothBatteryV) * 0.02f;

        // ── Add natural variation (idle hunt, road load, etc.) ──
        var rpmVariation = ComputeRpmVariation(elapsed);
        var rpm = _rpmState + rpmVariation;

        var mapNoise = (float)(_random.NextDouble() - 0.5f) * 1.5f;
        var map = _smoothMap + mapNoise;

        var tpsNoise = (float)(_random.NextDouble() - 0.5f) * 0.3f;
        var tps = Math.Clamp(_smoothTps + tpsNoise, 0f, 100f);

        // ── Speed from the gear model ──
        // During a shift the clutch is in: road speed holds while the engine
        // freewheels, then RPM steps to match that same speed in the new gear.
        _computedSpeed = _isShifting
            ? _shiftHoldSpeedKmh
            : _rpmState > 1000f ? RpmToSpeedKmh(_rpmState, _currentGear) : 0f;
        var speed = Math.Max(0f, _computedSpeed + (float)(_random.NextDouble() - 0.5f) * 0.5f);
        var batteryV = _smoothBatteryV + (float)(_random.NextDouble() - 0.5f) * 0.05f;

        // ── Thermal model ──
        // Coolant climbs toward a load-dependent operating point (84 idle …
        // 96 full load) and the fan drags it back down above 93 — hysteresis
        // makes it rock a few degrees even at steady cruise. IAT heats up with
        // load (bay + charge heat) and cools with ram airflow at speed. Oil
        // lags coolant, fuel temp hugs ambient + load, EGT tracks rpm × load.
        var loadFrac = Math.Clamp((map - 30f) / 60f, 0f, 1f);
        var speedFactor = Math.Clamp(speed / 140f, 0f, 1f);

        var coolantTarget = 84f + 12f * loadFrac;
        if (_smoothCoolantTemp > 93f) coolantTarget -= 6f; // fan on
        var coolantWarmRate = 0.4f + 1.1f * loadFrac;      // °C/s toward target
        _smoothCoolantTemp += Math.Clamp(coolantTarget - _smoothCoolantTemp, -0.8f * dt, coolantWarmRate * dt);

        var iatTarget = 24f + 14f * loadFrac - 6f * speedFactor + 2f * (float)Math.Sin(elapsed * 0.15);
        _smoothIat += (iatTarget - _smoothIat) * 0.2f;

        var oilTarget = Math.Max(_smoothCoolantTemp, 88f) + 6f;
        _smoothOilTemp += Math.Clamp(oilTarget - _smoothOilTemp, -0.3f * dt, 0.35f * dt);

        var fuelTarget = 22f + 8f * loadFrac + 2f * (float)Math.Sin(elapsed * 0.05);
        _smoothFuelTemp += (fuelTarget - _smoothFuelTemp) * 0.1f;

        var egtTarget = 150f + 700f * Math.Clamp((rpm - IdleRpm) / 5700f, 0f, 1f) * (0.3f + 0.7f * loadFrac);
        _egtState += (egtTarget - _egtState) * Math.Clamp(dt * 2.5f, 0f, 1f);

        var coolantTemp = _smoothCoolantTemp + (float)(_random.NextDouble() - 0.5f) * 0.5f;
        var intakeAirTemp = _smoothIat + (float)(_random.NextDouble() - 0.5f) * 0.4f;
        var oilTemp = _smoothOilTemp + (float)(_random.NextDouble() - 0.5f) * 0.5f;
        var fuelTemp = _smoothFuelTemp + (float)(_random.NextDouble() - 0.5f) * 0.3f;
        var egtTemp = _egtState + (float)(_random.NextDouble() - 0.5f) * 4f;

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
        SetIfPresent("TPS_RAW", tps / 100f * 65535f);

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
        SetIfPresent("PPS1_RAW", pps * 65535f * 0.5f);
        SetIfPresent("PPS2_RAW", pps * 65535f * 0.45f);
        SetIfPresent("TPPS1_RAW", pps * 65535f * 0.48f);
        SetIfPresent("TPPS2_RAW", pps * 65535f * 0.43f);
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
            IntakeAirTemp = intakeAirTemp,
            OilTemp = oilTemp,
            FuelTemp = fuelTemp,
            EgtTemp = egtTemp,
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

        // ── Table output interpolation ──
        // For each table, look up current input data link values and interpolate the table
        // to produce the actual output data link value. This ensures table outputs are
        // consistent with the table data, not just hardcoded formulas.
        foreach (var (outputLinkId, tables) in _outputLinkToTables)
        {
            // First enabled table that produces this output (usually there's only
            // one). Disabled tables are OFF in a real ECU — they must not drive
            // outputs, and placeholder tables (no real data) interpolate to null.
            var table = tables.FirstOrDefault(t => _entityStore.IsTableEnabled(t.Id));
            if (table is null) continue;
            var input0Value = _entityStore.GetDataLinkValue(table.Input0LinkId);
            var input1Value = _entityStore.GetDataLinkValue(table.Input1LinkId);

            var interpolated = _entityStore.InterpolateTable(table.Id, input0Value, input1Value);
            if (interpolated.HasValue && !float.IsNaN(interpolated.Value))
                _entityStore.SetDataLinkValue(outputLinkId, interpolated.Value);
        }

        _prevTps = tps;

        // Log key sensor values every ~10 ticks (visible heartbeat — lets you watch
        // the gear model work: RPM climbs, drops on each shift, speed stays smooth)
        if (_tickCount % 10 == 0)
        {
            _logger.LogInformation(
                "SensorSimulator tick #{TickCount} [{Mode}]: RPM={RPM:F0} MAP={MAP:F1} TPS={TPS:F1} Speed={Speed:F0} Gear={Gear} Batt={Batt:F2} CLT={CLT:F1} IAT={IAT:F1} OILT={OILT:F1}",
                _tickCount, _currentMode, rpm, map, tps, speed, _currentGear, batteryV, coolantTemp, intakeAirTemp, oilTemp);
        }
    }

    // ── Scripted override injection ──

    public bool DrivingModeHold { get; private set; }

    public void SetDrivingModeHold(bool hold)
    {
        DrivingModeHold = hold;
    }

    public void SetOverride(ushort linkId, float value)
    {
        lock (_overridesLock)
            _overrides[linkId] = value;
    }

    public void ClearOverride(ushort linkId)
    {
        lock (_overridesLock)
            _overrides.Remove(linkId);
    }

    public void ApplyOverrides()
    {
        lock (_overridesLock)
        {
            foreach (var (id, value) in _overrides)
                _entityStore.SetDataLinkValue(id, value);
        }
    }

    // ── Drive-cycle state machine ──

    private void AdvanceDrivingMode(double elapsed)
    {
        if (DrivingModeHold) return;

        var modeElapsed = elapsed - _modeStartTime;
        var maxGear = _vehicleConfig.GearRatios.Length;
        var done = _currentMode switch
        {
            DrivingMode.Idle => modeElapsed >= IdleDuration,
            // Accelerate until we hit the limit in the top gear (or the cap — e.g. a
            // single-gear config never reaches "top gear at the limit" twice).
            DrivingMode.Accelerate => modeElapsed >= AccelerateDuration
                || (_currentGear >= maxGear && _rpmState >= _shiftUpRpm),
            DrivingMode.CruiseHigh => modeElapsed >= CruiseHighDuration,
            // Coast until the engine settles back to idle at a standstill.
            DrivingMode.Coast => modeElapsed >= CoastDuration
                || (_currentGear == 1 && _rpmState <= IdleRpm + 50f),
            _ => modeElapsed >= IdleDuration,
        };

        if (done)
        {
            _currentMode = _currentMode switch
            {
                DrivingMode.Idle => DrivingMode.Accelerate,
                DrivingMode.Accelerate => DrivingMode.CruiseHigh,
                DrivingMode.CruiseHigh => DrivingMode.Coast,
                DrivingMode.Coast => DrivingMode.Idle,
                _ => DrivingMode.Idle,
            };
            _modeStartTime = elapsed;
        }
    }

    /// <summary>
    /// Per-mode RPM rate (rpm/s — positive under throttle, negative while coasting)
    /// plus the MAP/TPS targets for the mode. RPM itself is integrated from this
    /// rate; the gear model then steps it on gear changes.
    /// </summary>
    private (float rpmRate, float map, float tps) GetDriveTargets(double elapsed)
    {
        // While shifting, the driver is off the throttle: TPS 0%, manifold drops
        // to vacuum, and the decoupled engine freewheels down before the clutch-out
        // step lands the new gear (speed is held constant in the meantime).
        if (_isShifting)
            return (-ShiftRpmDecayRate, ShiftMapTarget, 0f);

        return _currentMode switch
        {
            // Exponential settle toward idle — handles both the normal idle phase
            // and the rare "mode changed while RPM was still high" case.
            DrivingMode.Idle => ((IdleRpm - _rpmState) * 0.5f, 35f, 0f),
            // Full-throttle pull: TPS pins at WOT (100%) so dyno tools recognize
            // a real pull, MAP rises to atmospheric as the engine revs out.
            DrivingMode.Accelerate => (
                ComputeAccelRpmRate(),
                35f + 65f * Math.Clamp((_rpmState - IdleRpm) / (_shiftUpRpm - IdleRpm), 0f, 1f),
                100f
            ),
            DrivingMode.CruiseHigh => (0f, 50f, 18f),
            DrivingMode.Coast => (-CoastDecayRate, 30f, 0f),
            _ => (0f, 35f, 0f),
        };
    }

    /// <summary>
    /// Physical RPM climb rate (rpm/s) for the current gear under full throttle,
    /// derived from a power-limited drivetrain model (110 whp default):
    ///   wheel force = min(power / speed, traction ceiling)
    ///   acceleration = (force − rolling − aero drag) / mass
    ///   RPM rate = acceleration × 60 × totalRatio / tireCircumference
    /// Low gears pull hard (traction-capped), high gears fade as drag eats the
    /// power — exactly like accelerating a real car.
    /// </summary>
    private float ComputeAccelRpmRate()
    {
        if (_currentGear < 1 || _currentGear > _vehicleConfig.GearRatios.Length)
            return 0f;

        var totalRatio = _vehicleConfig.GearRatios[_currentGear - 1] * _vehicleConfig.FinalDriveRatio;
        if (totalRatio <= 0 || _tireCircumferenceMeters <= 0)
            return 0f;

        var speedMs = _rpmState * (float)_tireCircumferenceMeters / (60f * (float)totalRatio);
        var speedClamped = Math.Max(speedMs, 2f); // avoid the P/v launch singularity

        var powerW = _wheelHorsepower * 745.7f;
        var forceResist = 0.013f * _vehicleMassKg * 9.81f + 0.5f * AirDensity * DragArea * speedMs * speedMs;
        var forceTraction = Math.Min(powerW / speedClamped, MaxLaunchAccel * _vehicleMassKg + forceResist);
        var accel = Math.Max(0f, (forceTraction - forceResist) / _vehicleMassKg);

        return accel * 60f * (float)totalRatio / (float)_tireCircumferenceMeters;
    }

    /// <summary>
    /// Time-based gear changes. When a threshold is crossed the shift starts
    /// (throttle closed — GetDriveTargets returns the shift targets) and the
    /// gear lands ShiftDurationSeconds later: RPM steps so the held road speed
    /// stays continuous (on an upshift RPM drops to the new-gear speed match,
    /// on a downshift it jumps to it), exactly like a lift-shift in a real car.
    /// </summary>
    private void UpdateGear(double elapsed)
    {
        var maxGear = _vehicleConfig.GearRatios.Length;
        if (maxGear == 0) return;

        if (_isShifting)
        {
            if (elapsed - _shiftStartTime >= ShiftDurationSeconds)
            {
                _currentGear += _pendingGearChange;
                _rpmState = SpeedToRpm(_shiftHoldSpeedKmh, _currentGear);
                _isShifting = false;
                _pendingGearChange = 0;
                _logger.LogDebug("Shift complete to gear {Gear}: RPM {NewRpm:F0}, speed held {Speed:F0} km/h",
                    _currentGear, _rpmState, _shiftHoldSpeedKmh);
            }
            return;
        }

        if (_rpmState >= _shiftUpRpm && _currentGear < maxGear)
        {
            _isShifting = true;
            _pendingGearChange = 1;
            _shiftStartTime = elapsed;
            _shiftHoldSpeedKmh = RpmToSpeedKmh(_rpmState, _currentGear);
            _logger.LogDebug("Shift UP started at RPM {Rpm:F0} — throttle closed for {Seconds:F0} s",
                _rpmState, ShiftDurationSeconds);
        }
        else if (_rpmState <= _shiftDownRpm && _currentGear > 1)
        {
            _isShifting = true;
            _pendingGearChange = -1;
            _shiftStartTime = elapsed;
            _shiftHoldSpeedKmh = RpmToSpeedKmh(_rpmState, _currentGear);
            _logger.LogDebug("Shift DOWN started at RPM {Rpm:F0} — throttle closed for {Seconds:F0} s",
                _rpmState, ShiftDurationSeconds);
        }
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

    private float SpeedToRpm(float speedKmh, int gear)
    {
        if (gear < 1 || gear > _vehicleConfig.GearRatios.Length)
            return _rpmState;

        var gearRatio = _vehicleConfig.GearRatios[gear - 1];
        var totalRatio = gearRatio * _vehicleConfig.FinalDriveRatio;
        return speedKmh * (float)totalRatio * 1000f / ((float)_tireCircumferenceMeters * 60f);
    }

    private static float NormalizeShiftUpRpm(double? shiftUpRpm)
    {
        if (shiftUpRpm is > 500f)
            return Math.Min((float)shiftUpRpm.Value, 15000f);
        return ShiftUpDefaultRpm;
    }

    /// <summary>
    /// Coast downshift floor. Never allows a downshift whose ratio bump could push RPM
    /// back past the upshift point: the floor is capped at half the shift-up RPM.
    /// </summary>
    private static float NormalizeShiftDownRpm(double? shiftDownRpm, float shiftUpRpm)
    {
        var floor = shiftDownRpm is > 500f ? (float)shiftDownRpm.Value : ShiftDownDefaultRpm;
        return Math.Min(floor, shiftUpRpm * 0.5f);
    }

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
            DrivingMode.Accelerate => noise * 0.5f,
            DrivingMode.CruiseHigh => roadLoad * 0.3f + noise,
            DrivingMode.Coast => noise * 0.5f,
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
        var rpmFactor = Math.Clamp((_rpmState - 500f) / 3000f, 0f, 1f);
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
                "C" or "\u00B0C" or "degC" when link.Name.Contains("Intake", StringComparison.OrdinalIgnoreCase)
                    || link.Name.Contains("IAT", StringComparison.OrdinalIgnoreCase)
                    || link.Name.Contains("Inlet", StringComparison.OrdinalIgnoreCase)
                    => ctx => ctx.IntakeAirTemp,
                "C" or "\u00B0C" or "degC" when link.Name.Contains("Oil", StringComparison.OrdinalIgnoreCase)
                    => ctx => ctx.OilTemp,
                "C" or "\u00B0C" or "degC" when link.Name.Contains("Fuel", StringComparison.OrdinalIgnoreCase)
                    => ctx => ctx.FuelTemp,
                "C" or "\u00B0C" or "degC" when link.Name.Contains("EG", StringComparison.OrdinalIgnoreCase)
                    || link.Name.Contains("Exhaust", StringComparison.OrdinalIgnoreCase)
                    => ctx => ctx.EgtTemp,
                "C" or "\u00B0C" or "degC"
                    => ctx => ctx.CoolantTemp,
                "F" or "\u00B0F" or "degF" when link.Name.Contains("Intake", StringComparison.OrdinalIgnoreCase)
                    || link.Name.Contains("IAT", StringComparison.OrdinalIgnoreCase)
                    => ctx => ctx.IntakeAirTemp * 1.8f + 32f,
                "F" or "\u00B0F" or "degF"
                    => ctx => ctx.CoolantTemp * 1.8f + 32f,
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

        // Raw sensor values — simulate actual ADC readings (0–65535 for 16-bit)
        if (name.Contains("Raw", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("TPS", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.Tps / 100f * RawAdcMax;
            if (name.Contains("MAP", StringComparison.OrdinalIgnoreCase))
                return ctx => ctx.Map / 250f * RawAdcMax;
            if (name.Contains("Coolant", StringComparison.OrdinalIgnoreCase) || name.Contains("CLT", StringComparison.OrdinalIgnoreCase))
                return ctx => VoltageToRaw(2f + ctx.CoolantTemp / 100f * 3f);
            if (name.Contains("IAT", StringComparison.OrdinalIgnoreCase) || name.Contains("Intake", StringComparison.OrdinalIgnoreCase))
                return ctx => VoltageToRaw(2f + ctx.IntakeAirTemp / 100f * 3f);
            if (name.Contains("Battery", StringComparison.OrdinalIgnoreCase))
                return ctx => VoltageToRaw(ctx.BatteryVoltage * 0.95f + 0.5f);
            if (name.Contains("Oil Temp", StringComparison.OrdinalIgnoreCase))
                return ctx => VoltageToRaw(2f + ctx.OilTemp / 100f * 3f);
            if (name.Contains("Oil", StringComparison.OrdinalIgnoreCase))
                return ctx => VoltageToRaw(ctx.OilPressure * 0.02f);
            if (name.Contains("Fuel", StringComparison.OrdinalIgnoreCase))
                return ctx => VoltageToRaw(ctx.FuelPressure * 0.01f);
            if (name.Contains("Analog", StringComparison.OrdinalIgnoreCase))
                return ctx => VoltageToRaw(2.5f + 1.5f * (float)Math.Sin(ctx.Elapsed * 0.3 + ctx.Rpm * 0.0001f));
            // Generic raw
            return ctx => VoltageToRaw(2.5f + 1.5f * (float)Math.Sin(ctx.Elapsed * 0.2));
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
        public float IntakeAirTemp { get; init; }
        public float OilTemp { get; init; }
        public float FuelTemp { get; init; }
        public float EgtTemp { get; init; }
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

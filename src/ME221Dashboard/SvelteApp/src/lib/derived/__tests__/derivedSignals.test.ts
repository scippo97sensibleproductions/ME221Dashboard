import { describe, it, expect } from 'vitest';
import { computeDerived, formatDerivedValue } from '../compute';
import { autoDetectMapping } from '../autoDetect';
import { DerivedEntityId, defaultDerivedConfig, defaultRtcConfig } from '../types';
import type { VehicleConfig as BridgeVehicleConfig } from '../../HybridBridgeTypes';
import type { VehicleConfig as DerivedVehicleConfig } from '../types';

const cfg = {
  ...defaultDerivedConfig(),
  rpmEntityId: 1,
  vssSpeedEntityId: 2,
  mapEntityId: 3,
  baroEntityId: 4,
  gearEntityId: 5,
};

const val = (v: Record<string, number>) => Object.fromEntries(
  Object.entries(v).map(([k, n]) => [k, n as number | null]),
);

describe('computeDerived — gear', () => {
  it('prefers the raw gear entity', () => {
    const r = computeDerived({ entityValues: val({ '5': 3 }), config: cfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.Gear]).toBe(3);
  });

  it('rejects out-of-range raw gear values', () => {
    const r = computeDerived({ entityValues: val({ '5': 99 }), config: cfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.Gear]).toBeNull();
  });

  it('rejects zero raw gear', () => {
    const r = computeDerived({ entityValues: val({ '5': 0 }), config: cfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.Gear]).toBeNull();
  });

  it('predicts gear from the RPM/speed ratio within 15%', () => {
    // tire 23", final 4.3, ratios [3.6, 2.2, 1.5, 1.1, 0.85, 0.7]
    // expected ratio for gear 3: 1.5*4.3*1000/(circ*60) ≈ 58.6; 3000rpm @ 50km/h → 60
    const r = computeDerived({ entityValues: val({ '1': 3000, '2': 50 }), config: cfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.Gear]).toBe(3);
  });

  it('returns null gear when no ratio matches within 15%', () => {
    const r = computeDerived({ entityValues: val({ '1': 3000, '2': 15 }), config: cfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.Gear]).toBeNull();
  });

  it('detects neutral at standstill with rpm above idle', () => {
    const r = computeDerived({ entityValues: val({ '1': 1000, '2': 0.2 }), config: cfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.Gear]).toBe(0);
  });

  it('does not flag neutral when rpm is low', () => {
    const r = computeDerived({ entityValues: val({ '1': 600, '2': 0.2 }), config: cfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.Gear]).toBeNull();
  });

  it('uses GPS speed as fallback for gear prediction', () => {
    const r = computeDerived({ entityValues: val({ '1': 3000 }), config: cfg, gpsSpeedKmh: 50, gpsValid: true });
    expect(r[DerivedEntityId.Gear]).toBe(3);
  });
});

describe('computeDerived — true speed', () => {
  it('computes speed from rpm and predicted gear', () => {
    // 3000rpm, gear 3 (1.5), tire circ = 23*0.0254*π = 1.8357m
    const r = computeDerived({ entityValues: val({ '1': 3000, '5': 3 }), config: cfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.TrueSpeed]).toBeCloseTo(51.23, 1);
  });

  it('returns null true speed without rpm', () => {
    const r = computeDerived({ entityValues: val({ '5': 3 }), config: cfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.TrueSpeed]).toBeNull();
  });
});

describe('computeDerived — boost', () => {
  it('subtracts baro when available', () => {
    const r = computeDerived({ entityValues: val({ '3': 150, '4': 98 }), config: cfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.Boost]).toBe(52);
  });

  it('falls back to 100 kPa reference without baro', () => {
    const r = computeDerived({ entityValues: val({ '3': 150 }), config: cfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.Boost]).toBe(50);
  });

  it('returns null without map', () => {
    const r = computeDerived({ entityValues: val({}), config: cfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.Boost]).toBeNull();
  });
});

describe('computeDerived — speed error', () => {  it('compares GPS against VSS when both present', () => {
    const r = computeDerived({ entityValues: val({ '2': 55 }), config: cfg, gpsSpeedKmh: 60, gpsValid: true });
    expect(r[DerivedEntityId.SpeedError]).toBe(5);
  });

  it('falls back to GPS vs true speed without VSS', () => {
    const r = computeDerived({ entityValues: val({ '1': 3000, '5': 3 }), config: cfg, gpsSpeedKmh: 60, gpsValid: true });
    expect(r[DerivedEntityId.SpeedError]).toBeCloseTo(60 - 51.23, 1);
  });

  it('returns null when GPS is invalid', () => {
    const r = computeDerived({ entityValues: val({ '2': 55 }), config: cfg, gpsSpeedKmh: 60, gpsValid: false });
    expect(r[DerivedEntityId.SpeedError]).toBeNull();
  });

  it('handles missing sensors as null', () => {
    const r = computeDerived({ entityValues: val({}), config: cfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r).toEqual({
      [DerivedEntityId.Gear]: null,
      [DerivedEntityId.TrueSpeed]: null,
      [DerivedEntityId.Boost]: null,
      [DerivedEntityId.SpeedError]: null,
      [DerivedEntityId.RpmToShift]: null,
      [DerivedEntityId.FuelEfficiency]: null,
      [DerivedEntityId.EcuClock]: null,
      [DerivedEntityId.EcuDate]: null,
    });
  });
});

describe('computeDerived — fuel efficiency', () => {
  const fuelCfg = {
    ...cfg,
    fuelFlowEntityId: 35,
    injectorDutyEntityId: 4 as number | null,
    injectorFlowCcPerMin: 440,
    cylinderCount: 4,
  };

  it('km/L = speed ÷ flow', () => {
    const r = computeDerived({
      entityValues: val({ '2': 100, '35': 10 }),
      config: { ...fuelCfg, fuelEfficiencyUnit: 'kmL' as const },
      gpsSpeedKmh: null,
      gpsValid: false,
    });
    expect(r[DerivedEntityId.FuelEfficiency]).toBeCloseTo(10, 5);
  });

  it('L/100km inverts to flow ÷ speed × 100', () => {
    const r = computeDerived({
      entityValues: val({ '2': 100, '35': 10 }),
      config: { ...fuelCfg, fuelEfficiencyUnit: 'l100km' as const },
      gpsSpeedKmh: null,
      gpsValid: false,
    });
    expect(r[DerivedEntityId.FuelEfficiency]).toBeCloseTo(10, 5);
  });

  it('MPG uses US gallons (100 km/h at 10 L/hr ≈ 23.5 mpg)', () => {
    const r = computeDerived({
      entityValues: val({ '2': 100, '35': 10 }),
      config: { ...fuelCfg, fuelEfficiencyUnit: 'mpg' as const },
      gpsSpeedKmh: null,
      gpsValid: false,
    });
    expect(r[DerivedEntityId.FuelEfficiency]).toBeCloseTo(23.52, 1);
  });

  it('falls back to injector duty × injector flow × cylinders when no flow channel', () => {
    // 20 % of 4 × 440 cc/min = 352 cc/min = 21.12 L/hr at 100 km/h → 4.73 km/L
    const r = computeDerived({
      entityValues: val({ '2': 100, '4': 20 }),
      config: { ...fuelCfg, fuelFlowEntityId: null, fuelEfficiencyUnit: 'kmL' as const },
      gpsSpeedKmh: null,
      gpsValid: false,
    });
    expect(r[DerivedEntityId.FuelEfficiency]).toBeCloseTo(100 / 21.12, 3);
  });

  it('uses GPS speed when the speed source says so', () => {
    const r = computeDerived({
      entityValues: val({ '2': 50, '35': 10 }),
      config: { ...fuelCfg, fuelSpeedSource: 'gps' as const, fuelEfficiencyUnit: 'kmL' as const },
      gpsSpeedKmh: 100,
      gpsValid: true,
    });
    expect(r[DerivedEntityId.FuelEfficiency]).toBeCloseTo(10, 5);
  });

  it('uses the derived True Speed chain when configured', () => {
    // 3000 rpm, gear 3 → 51.23 km/h from the geometry chain; 10 L/hr → 5.12 km/L
    const r = computeDerived({
      entityValues: val({ '1': 3000, '5': 3, '35': 10 }),
      config: { ...fuelCfg, fuelSpeedSource: 'trueSpeed' as const, fuelEfficiencyUnit: 'kmL' as const },
      gpsSpeedKmh: 200,
      gpsValid: true,
    });
    expect(r[DerivedEntityId.FuelEfficiency]).toBeCloseTo(51.23 / 10, 1);
  });

  it('is null while stationary and without any fuel source', () => {
    const stationary = computeDerived({
      entityValues: val({ '2': 0, '35': 10 }),
      config: cfg,
      gpsSpeedKmh: null,
      gpsValid: false,
    });
    expect(stationary[DerivedEntityId.FuelEfficiency]).toBeNull();

    const noFuel = computeDerived({
      entityValues: val({ '2': 100 }),
      config: cfg,
      gpsSpeedKmh: null,
      gpsValid: false,
    });
    expect(noFuel[DerivedEntityId.FuelEfficiency]).toBeNull();
  });
});

describe('computeDerived — RPM to shift countdown (R2, R14)', () => {
  const shiftCfg = { ...cfg, shifter: { shiftPointRpm: 7000, downshiftFloorRpm: 5000 } };

  it('countdown = shiftPoint − rpm', () => {
    const r = computeDerived({ entityValues: val({ '1': 6000 }), config: shiftCfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.RpmToShift]).toBe(1000);
  });

  it('clamps at 0 at/above the shift point', () => {
    const r = computeDerived({ entityValues: val({ '1': 7500 }), config: shiftCfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.RpmToShift]).toBe(0);
  });

  it('null when the shift point is unset or zero', () => {
    const noShift = computeDerived({ entityValues: val({ '1': 6000 }), config: cfg, gpsSpeedKmh: null, gpsValid: false });
    expect(noShift[DerivedEntityId.RpmToShift]).toBeNull();
  });

  it('null when RPM is null', () => {
    const r = computeDerived({ entityValues: val({}), config: shiftCfg, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.RpmToShift]).toBeNull();
  });

  it('null when no RPM datalink is configured', () => {
    const noRpm = { ...shiftCfg, rpmEntityId: null };
    const r = computeDerived({ entityValues: val({ '1': 6000 }), config: noRpm, gpsSpeedKmh: null, gpsValid: false });
    expect(r[DerivedEntityId.RpmToShift]).toBeNull();
  });
});

describe('formatDerivedValue — shift state labels', () => {
  it('maps −1 to DOWN, 0 to CRUISE, +1 to SHIFT', () => {
    expect(formatDerivedValue(DerivedEntityId.ShiftState, -1)).toBe('DOWN');
    expect(formatDerivedValue(DerivedEntityId.ShiftState, 0)).toBe('CRUISE');
    expect(formatDerivedValue(DerivedEntityId.ShiftState, 1)).toBe('SHIFT');
  });

  it('formats null as ---', () => {
    expect(formatDerivedValue(DerivedEntityId.ShiftState, null)).toBe('---');
  });
});

describe('computeDerived — ECU clock and date', () => {
  const rtcCfg = {
    ...defaultDerivedConfig(),
    rtc: {
      ...defaultRtcConfig(),
      hourEntityId: 2224,
      minuteEntityId: 2225,
      secondEntityId: 2226,
      dayEntityId: 2221,
      monthEntityId: 2222,
      yearEntityId: 2223,
    },
  };

  it('folds the mapped clock channels into the derived entities', () => {
    const r = computeDerived({
      entityValues: { '2221': 30, '2222': 10, '2223': 2026, '2224': 14, '2225': 32, '2226': 9 },
      config: rtcCfg,
      gpsSpeedKmh: null,
      gpsValid: false,
    });

    expect(r[DerivedEntityId.EcuClock]).toBe(14 * 3600 + 32 * 60 + 9);
    expect(r[DerivedEntityId.EcuDate]).toBe(20261030);
  });

  it('leaves both entities inert when the clock is unmapped', () => {
    const r = computeDerived({
      entityValues: {},
      config: defaultDerivedConfig(),
      gpsSpeedKmh: null,
      gpsValid: false,
    });

    expect(r[DerivedEntityId.EcuClock]).toBeNull();
    expect(r[DerivedEntityId.EcuDate]).toBeNull();
  });
});

describe('vehicle-config shape parity (single type home)', () => {
  // Compile-time structural-equality assertion: both files must stay mutually
  // assignable, so HybridBridgeTypes never drifts from derived/types.
  const bridgeToDerived = (_v: BridgeVehicleConfig): DerivedVehicleConfig => _v;
  const derivedToBridge = (_v: DerivedVehicleConfig): BridgeVehicleConfig => _v;

  it('shapes are mutually assignable and carry the shifter block', () => {
    const sample: BridgeVehicleConfig = { ...defaultDerivedConfig(), shifter: { shiftPointRpm: 7000, downshiftFloorRpm: 5000 } };
    const asDerived = bridgeToDerived(sample);
    expect(asDerived.shifter?.shiftPointRpm).toBe(7000);
    const back = derivedToBridge(asDerived);
    expect(back.shifter?.downshiftFloorRpm).toBe(5000);
  });
});

describe('formatDerivedValue', () => {
  it('formats null as ---', () => {
    expect(formatDerivedValue(DerivedEntityId.Gear, null)).toBe('---');
  });

  it('formats gear numbers, neutral and reverse', () => {
    expect(formatDerivedValue(DerivedEntityId.Gear, 4)).toBe('4');
    expect(formatDerivedValue(DerivedEntityId.Gear, 0)).toBe('N');
    expect(formatDerivedValue(DerivedEntityId.Gear, -1)).toBe('R');
  });

  it('formats boost with sign and one decimal', () => {
    expect(formatDerivedValue(DerivedEntityId.Boost, 12.34)).toBe('+12.3');
    expect(formatDerivedValue(DerivedEntityId.Boost, -1.5)).toBe('-1.5');
  });

  it('passes through other values as strings', () => {
    expect(formatDerivedValue(DerivedEntityId.TrueSpeed, 51.2)).toBe('51.2');
  });
});

describe('autoDetectMapping', () => {
  const sensors = [
    { id: 1, name: 'Engine RPM' },
    { id: 2, name: 'Vehicle Speed' },
    { id: 3, name: 'MAP' },
    { id: 4, name: 'BARO' },
    { id: 5, name: 'Selected Gear' },
  ];

  it('detects all five channels by name pattern', () => {
    const m = autoDetectMapping(sensors);
    expect(m.rpmEntityId).toBe(1);
    expect(m.vssSpeedEntityId).toBe(2);
    expect(m.mapEntityId).toBe(3);
    expect(m.baroEntityId).toBe(4);
    expect(m.gearEntityId).toBe(5);
  });

  it('matching is case-insensitive and trims whitespace', () => {
    const m = autoDetectMapping([
      { id: 10, name: '  rpm  ' },
      { id: 11, name: 'vss speed' },
    ]);
    expect(m.rpmEntityId).toBe(10);
    expect(m.vssSpeedEntityId).toBe(11);
  });

  it('returns the first sensor matching each pattern', () => {
    const m = autoDetectMapping([
      { id: 1, name: 'RPM' },
      { id: 2, name: 'Engine Speed' },
    ]);
    expect(m.rpmEntityId).toBe(1);
  });

  it('leaves unmapped channels null', () => {
    const m = autoDetectMapping([{ id: 1, name: 'RPM' }]);
    expect(m.baroEntityId).toBeNull();
    expect(m.gearEntityId).toBeNull();
  });

  it('returns all-null for empty sensor list', () => {
    const m = autoDetectMapping([]);
    expect(m).toEqual({
      rpmEntityId: null,
      vssSpeedEntityId: null,
      mapEntityId: null,
      baroEntityId: null,
      gearEntityId: null,
      fuelFlowEntityId: null,
      injectorDutyEntityId: null,
      rtc: {
        statusEntityId: null,
        dayEntityId: null,
        monthEntityId: null,
        yearEntityId: null,
        hourEntityId: null,
        minuteEntityId: null,
        secondEntityId: null,
      },
    });
  });

  it('detects the firmware fuel channels', () => {
    const m = autoDetectMapping([
      { id: 35, name: 'Fuelfow Est.' },
      { id: 4, name: 'Injector duty' },
    ]);
    expect(m.fuelFlowEntityId).toBe(35);
    expect(m.injectorDutyEntityId).toBe(4);
  });
});

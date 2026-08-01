import { describe, it, expect } from 'vitest';
import { computeDerived, formatDerivedValue } from '../compute';
import { autoDetectMapping } from '../autoDetect';
import { DerivedEntityId, defaultDerivedConfig } from '../types';

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

describe('computeDerived — speed error', () => {
  it('compares GPS against VSS when both present', () => {
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
    });
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
    });
  });
});

import { describe, it, expect } from 'vitest';
import { searchSensors } from '../sensorSearch';
import type { AvailableSensor } from '../HybridBridgeTypes';

function sensor(id: number, name: string, category: string, unit: string): AvailableSensor {
  return { id, name, category, unit, minValue: 0, maxValue: 100, inEntityMap: true, isSelected: false, customization: null };
}

const SENSORS: AvailableSensor[] = [
  sensor(1, 'Engine Speed', 'Engine', 'rpm'),
  sensor(2, 'RPM', 'Engine', 'rpm'),
  sensor(3, 'Idle Target RPM', 'Engine', 'rpm'),
  sensor(4, 'MAP', 'Intake', 'kPa'),
  sensor(5, 'Boost', 'Boost', 'kPa'),
  sensor(6, 'AFR', 'Fuel', 'lambda'),
  // The derived block is appended LAST by C# GetAvailableSensors — the case
  // the old 30-item cap in array order starved.
  sensor(-3005, 'RPM to Shift', 'Derived', 'rpm'),
  sensor(-3006, 'Shift State', 'Derived', ''),
];

describe('searchSensors (shared add-gauge / chart-overlay search)', () => {
  it('surfaces "RPM to Shift" when searching "rpm" despite the links-first payload order', () => {
    const result = searchSensors(SENSORS, 'rpm');
    const ids = result.map(s => s.id);
    expect(ids).toContain(-3005);
  });

  it('surfaces "Shift State" when searching "shift" (ranked above "RPM to Shift")', () => {
    const result = searchSensors(SENSORS, 'shift');
    expect(result.map(s => s.id)).toEqual([-3006, -3005]);
  });

  it('ranks name matches above category/unit/id matches', () => {
    const result = searchSensors(SENSORS, 'rpm');
    // "RPM" and "RPM to Shift" are name matches and must rank above the
    // category/unit matches that dominate the array order.
    expect(result[0].name).toBe('RPM');
    expect(result[1].name).toBe('RPM to Shift');
  });

  it('returns the full list for an empty query (no cap)', () => {
    expect(searchSensors(SENSORS, '')).toHaveLength(SENSORS.length);
  });

  it('matches custom names when set', () => {
    const custom = sensor(9, 'RPM', 'Engine', 'rpm');
    custom.customization = {
      customName: 'My Tach',
      customUnit: null,
      minRange: null,
      maxRange: null,
      minRangeBypass: false,
      maxRangeBypass: false,
    };
    const result = searchSensors([custom], 'tach');
    expect(result.map(s => s.id)).toEqual([9]);
  });

  it('matches numeric ids', () => {
    const result = searchSensors(SENSORS, '-3006');
    expect(result.map(s => s.id)).toEqual([-3006]);
  });
});

import { describe, it, expect } from 'vitest';
import { formatEntityValue } from '../formatting';
import { DerivedEntityId } from '../../derived/types';

describe('formatEntityValue', () => {
  it('renders the clock with the configured clock options', () => {
    const value = 14 * 3600 + 5 * 60 + 3;
    expect(formatEntityValue(DerivedEntityId.EcuClock, value, 'ECU Clock', ''))
      .toBe('14:05:03');
    expect(formatEntityValue(DerivedEntityId.EcuClock, value, 'ECU Clock', '', {
      clock: { hour12: true, showSeconds: false },
    })).toBe('02:05 PM');
  });

  it('renders the date day-first', () => {
    expect(formatEntityValue(DerivedEntityId.EcuDate, 20261030, 'ECU Date', ''))
      .toBe('30/10/2026');
  });

  it('renders word-valued derived entities', () => {
    expect(formatEntityValue(DerivedEntityId.Gear, 0, 'Gear', '')).toBe('N');
    expect(formatEntityValue(DerivedEntityId.ShiftState, 1, 'Shift State', '')).toBe('SHIFT');
    expect(formatEntityValue(DerivedEntityId.Boost, 12.34, 'Boost', 'kPa')).toBe('+12.3');
  });

  it('renders fuel figures with one decimal', () => {
    expect(formatEntityValue(DerivedEntityId.FuelEfficiency, 14.26, 'Fuel Efficiency', 'km/L'))
      .toBe('14.3');
    expect(formatEntityValue(DerivedEntityId.AverageFuelEfficiency, 9.94, 'Avg Fuel Efficiency', 'km/L'))
      .toBe('9.9');
  });

  it('falls back to the numeric format for ordinary datalinks', () => {
    expect(formatEntityValue(940, 3456.7, 'RPM', 'rpm')).toBe('3457');
    expect(formatEntityValue(42, 14.74, 'AFR', 'AFR')).toBe('14.74');
    expect(formatEntityValue(15, 92.7, 'Coolant Temp', '°C')).toBe('92.7');
  });

  it('shows --- for an inert derived entity and 0 for a missing datalink', () => {
    expect(formatEntityValue(DerivedEntityId.EcuClock, null, 'ECU Clock', '')).toBe('---');
    expect(formatEntityValue(940, null, 'RPM', 'rpm')).toBe('0');
    expect(formatEntityValue(null, 12.3, 'Anything', '')).toBe('12.3');
  });
});

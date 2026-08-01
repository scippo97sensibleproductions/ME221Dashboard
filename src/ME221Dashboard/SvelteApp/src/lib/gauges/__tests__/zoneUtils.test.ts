import { describe, it, expect } from 'vitest';
import { clampZoneThresholds, zoneColorAt } from '../zoneUtils';

describe('clampZoneThresholds', () => {
  it('clamps to [0,1]', () => {
    expect(clampZoneThresholds(-0.5, 1.5)).toEqual({ amber: 0, red: 1 });
  });

  it('enforces amber <= red (AE2)', () => {
    const t = clampZoneThresholds(0.9, 0.7);
    expect(t.amber).toBe(0.7);
    expect(t.red).toBe(0.7);
  });

  it('passes valid thresholds through unchanged', () => {
    expect(clampZoneThresholds(0.7, 0.85)).toEqual({ amber: 0.7, red: 0.85 });
  });

  it('identical thresholds produce no inverted range', () => {
    const t = clampZoneThresholds(0.5, 0.5);
    expect(t.amber).toBe(0.5);
    expect(t.red).toBe(0.5);
  });
});

describe('zoneColorAt', () => {
  const amber = 0.7;
  const red = 0.85;

  it('below amber uses the LUT color', () => {
    expect(zoneColorAt(0.3, amber, red, '#0078D7')).toBe('#0078D7');
  });

  it('between amber and red uses amber color', () => {
    expect(zoneColorAt(0.75, amber, red, '#0078D7')).toBe('#F59F00');
  });

  it('at or above red uses red color', () => {
    expect(zoneColorAt(0.85, amber, red, '#0078D7')).toBe('#E03131');
    expect(zoneColorAt(0.99, amber, red, '#0078D7')).toBe('#E03131');
  });

  it('boundary at amber exactly uses amber color', () => {
    expect(zoneColorAt(0.7, amber, red, '#0078D7')).toBe('#F59F00');
  });
});

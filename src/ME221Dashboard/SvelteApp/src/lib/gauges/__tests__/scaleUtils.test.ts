import { describe, it, expect } from 'vitest';
import { buildScaleTicks } from '../scaleUtils';

describe('buildScaleTicks', () => {
  it('count 0 yields no ticks', () => {
    expect(buildScaleTicks(0, 100, 0, false, 1)).toEqual([]);
  });

  it('count 20 yields 21 fractions across [0,1]', () => {
    const ticks = buildScaleTicks(0, 100, 20, false, 1);
    expect(ticks).toHaveLength(21);
    expect(ticks[0].fraction).toBe(0);
    expect(ticks[20].fraction).toBe(1);
    expect(ticks[10].fraction).toBeCloseTo(0.5);
  });

  it('default count 3 yields 4 ticks (matches legacy Arc rendering)', () => {
    const ticks = buildScaleTicks(0, 100, 3, false, 1);
    expect(ticks.map((t) => t.fraction)).toEqual([0, 1 / 3, 2 / 3, 1]);
  });

  it('labels off yields null labels', () => {
    const ticks = buildScaleTicks(0, 100, 5, false, 1);
    expect(ticks.every((t) => t.label === null)).toBe(true);
  });

  it('labels every 2nd tick with everyN 2', () => {
    const ticks = buildScaleTicks(0, 100, 4, true, 2);
    expect(ticks[0].label).not.toBeNull();
    expect(ticks[1].label).toBeNull();
    expect(ticks[2].label).not.toBeNull();
    expect(ticks[4].label).not.toBeNull();
  });

  it('everyN 0 behaves like 1', () => {
    const ticks = buildScaleTicks(0, 100, 3, true, 0);
    expect(ticks.every((t) => t.label !== null)).toBe(true);
  });

  it('labels reflect min/max range values', () => {
    const ticks = buildScaleTicks(0, 10000, 2, true, 1);
    expect(ticks[0].label).toBe('0');
    expect(ticks[1].label).toBe('5,000');
    expect(ticks[2].label).toBe('10,000');
  });

  it('clamps count beyond 20 and below 0', () => {
    expect(buildScaleTicks(0, 1, 99, false, 1)).toHaveLength(21);
    expect(buildScaleTicks(0, 1, -5, false, 1)).toEqual([]);
  });

  it('non-finite range values produce no NaN labels', () => {
    const ticks = buildScaleTicks(NaN, 100, 3, true, 1);
    expect(ticks.every((t) => t.label === null || t.label === '')).toBe(true);
  });
});

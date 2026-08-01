import { describe, it, expect } from 'vitest';
import {
  pushSample,
  computeAutoRange,
  lineDashFor,
  type ChartSample,
} from '../chartDataUtils';

const s = (t: number, v: number): ChartSample => ({ t, v });

describe('pushSample', () => {
  it('appends the sample and returns the same buffer instance', () => {
    const buf: ChartSample[] = [];
    const out = pushSample(buf, s(1000, 5), 10000, 100);
    expect(out).toBe(buf);
    expect(buf).toEqual([s(1000, 5)]);
  });

  it('evicts samples outside the time window from the front', () => {
    const buf = [s(0, 1), s(500, 2), s(1000, 3), s(1500, 4)];
    pushSample(buf, s(2000, 5), 1000, 100);
    expect(buf).toEqual([s(1000, 3), s(1500, 4), s(2000, 5)]);
  });

  it('keeps samples exactly at the window cutoff', () => {
    const buf = [s(500, 1), s(1000, 2)];
    pushSample(buf, s(1500, 3), 1000, 100);
    expect(buf).toEqual([s(500, 1), s(1000, 2), s(1500, 3)]);
  });

  it('caps total length at maxSamples after time eviction', () => {
    const buf = [s(100, 1), s(200, 2), s(300, 3), s(400, 4)];
    pushSample(buf, s(500, 5), 100000, 3);
    expect(buf).toEqual([s(300, 3), s(400, 4), s(500, 5)]);
  });

  it('windowMs <= 0 disables time eviction but still caps length', () => {
    const buf = [s(100, 1), s(200, 2), s(300, 3)];
    pushSample(buf, s(400, 4), 0, 3);
    expect(buf).toEqual([s(200, 2), s(300, 3), s(400, 4)]);
  });

  it('does not evict when nothing exceeds the window or cap', () => {
    const buf = [s(1000, 1)];
    pushSample(buf, s(2000, 2), 100000, 100);
    expect(buf).toEqual([s(1000, 1), s(2000, 2)]);
  });
});

describe('computeAutoRange', () => {
  it('returns null for no series', () => {
    expect(computeAutoRange([])).toBeNull();
  });

  it('returns null when all series are empty', () => {
    expect(computeAutoRange([[], []])).toBeNull();
  });

  it('spans multiple series with 10% padding on each side', () => {
    const r = computeAutoRange([
      [{ v: 10 }, { v: 20 }],
      [{ v: -5 }, { v: 50 }],
    ]);
    expect(r).toEqual({ min: -5 - 5.5, max: 50 + 5.5 });
  });

  it('single value pads by 1 each side', () => {
    expect(computeAutoRange([[{ v: 42 }]])).toEqual({ min: 41, max: 43 });
  });

  it('ignores empty series between populated ones', () => {
    const r = computeAutoRange([[{ v: 100 }], [], [{ v: 200 }]]);
    expect(r).toEqual({ min: 90, max: 210 });
  });
});

describe('lineDashFor', () => {
  it('returns empty dash for solid', () => {
    expect(lineDashFor(0)).toEqual([]);
  });

  it('returns [8,4] for dashed', () => {
    expect(lineDashFor(1)).toEqual([8, 4]);
  });

  it('returns [2,3] for dotted', () => {
    expect(lineDashFor(2)).toEqual([2, 3]);
  });

  it('unknown styles degrade to solid', () => {
    expect(lineDashFor(-1)).toEqual([]);
    expect(lineDashFor(99)).toEqual([]);
  });
});

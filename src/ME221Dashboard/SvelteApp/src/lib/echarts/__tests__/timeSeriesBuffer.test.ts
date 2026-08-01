import { describe, it, expect } from 'vitest';
import { TimeSeriesBuffer, WindowedSeriesCache } from '../TimeSeriesBuffer';

describe('TimeSeriesBuffer', () => {
  it('returns empty for unknown series', () => {
    const b = new TimeSeriesBuffer();
    expect(b.getVisiblePoints('rpm', 1000, 500)).toEqual([]);
    expect(b.getSeriesIds()).toEqual([]);
  });

  it('push creates a series and trims beyond the max point count', () => {
    const b = new TimeSeriesBuffer(3);
    for (let i = 0; i < 10; i++) b.push('rpm', i * 10, i);
    expect(b.getSeriesIds()).toEqual(['rpm']);
    expect(b.getVisiblePoints('rpm', 1000, 10000)).toHaveLength(3);
    expect(b.getVisiblePoints('rpm', 1000, 10000)[0]).toEqual({ t: 70, v: 7 });
  });

  it('getVisiblePoints returns points within the window (cutoff inclusive)', () => {
    const b = new TimeSeriesBuffer();
    for (let t = 0; t <= 100; t += 10) b.push('s', t, t);
    // now=100, window=50 → cutoff 50 → t >= 50
    const pts = b.getVisiblePoints('s', 100, 50);
    expect(pts[0]).toEqual({ t: 50, v: 50 });
    expect(pts).toHaveLength(6);
  });

  it('zero window returns only points at or after now', () => {
    const b = new TimeSeriesBuffer();
    for (let t = 0; t <= 100; t += 10) b.push('s', t, t);
    expect(b.getVisiblePoints('s', 60, 0)).toEqual([{ t: 60, v: 60 }, { t: 70, v: 70 }, { t: 80, v: 80 }, { t: 90, v: 90 }, { t: 100, v: 100 }]);
  });

  it('cutoff before the first point returns everything, after the last returns nothing', () => {
    const b = new TimeSeriesBuffer();
    for (let t = 10; t <= 40; t += 10) b.push('s', t, t);
    expect(b.getVisiblePoints('s', 20, 1000)).toHaveLength(4); // cutoff = -980
    expect(b.getVisiblePoints('s', 20, 1)).toHaveLength(3); // cutoff = 19 → t ≥ 20
    expect(b.getVisiblePoints('s', 200, 100)).toHaveLength(0); // cutoff = 100 > last
  });

  it('removeSeries deletes a single series', () => {
    const b = new TimeSeriesBuffer();
    b.push('a', 0, 1);
    b.push('b', 0, 1);
    b.removeSeries('a');
    expect(b.getSeriesIds()).toEqual(['b']);
  });

  it('clear empties all series', () => {
    const b = new TimeSeriesBuffer();
    b.push('a', 0, 1);
    b.push('b', 0, 1);
    b.clear();
    expect(b.getSeriesIds()).toEqual([]);
  });

  it('getAllSeries exposes the underlying map for iteration', () => {
    const b = new TimeSeriesBuffer();
    b.push('a', 0, 42);
    const all = b.getAllSeries();
    expect(all.get('a')![0]).toEqual({ t: 0, v: 42 });
    expect(all).toBeInstanceOf(Map);
  });

  it('get returns the live array for an existing series', () => {
    const b = new TimeSeriesBuffer();
    expect(b.get('rpm')).toBeUndefined();
    b.push('rpm', 10, 100);
    expect(b.get('rpm')).toHaveLength(1);
    expect(b.get('rpm')![0]).toEqual({ t: 10, v: 100 });
  });
});

describe('WindowedSeriesCache', () => {
  const b = () => {
    const buf = new TimeSeriesBuffer();
    for (let t = 0; t <= 1000; t += 100) buf.push('rpm', t, t);
    return buf;
  };

  it('seed builds windowed arrays filtered by cutoff', () => {
    const buf = b();
    const cache = new WindowedSeriesCache();
    cache.seed(['rpm'], (id) => buf.get(id), 500);
    const out = cache.tick(['rpm'], (id) => buf.get(id), 500);
    expect(out).toHaveLength(1);
    expect(out[0].data[0]).toEqual([500, 500]);
    expect(out[0].data).toHaveLength(6);
  });

  it('tick appends only points pushed since the last call', () => {
    const buf = b();
    const cache = new WindowedSeriesCache();
    cache.seed(['rpm'], (id) => buf.get(id), 0);
    buf.push('rpm', 1100, 1100);
    buf.push('rpm', 1200, 1200);
    const out = cache.tick(['rpm'], (id) => buf.get(id), 0);
    expect(out[0].data).toHaveLength(13);
    expect(out[0].data[12]).toEqual([1200, 1200]);
    // No new points → same content, no duplicates
    const out2 = cache.tick(['rpm'], (id) => buf.get(id), 0);
    expect(out2[0].data).toHaveLength(13);
  });

  it('tick trims points older than the cutoff', () => {
    const buf = b();
    const cache = new WindowedSeriesCache();
    cache.seed(['rpm'], (id) => buf.get(id), 0);
    buf.push('rpm', 1100, 1100);
    const out = cache.tick(['rpm'], (id) => buf.get(id), 900);
    expect(out[0].data[0]).toEqual([900, 900]);
    expect(out[0].data).toHaveLength(3);
  });

  it('tick passes through the same array instances for echarts length diff', () => {
    const buf = b();
    const cache = new WindowedSeriesCache();
    cache.seed(['rpm'], (id) => buf.get(id), 0);
    const first = cache.tick(['rpm'], (id) => buf.get(id), 0);
    buf.push('rpm', 1100, 1100);
    const second = cache.tick(['rpm'], (id) => buf.get(id), 0);
    expect(second[0].data).toBe(first[0].data);
  });

  it('tick skips series with no cached data', () => {
    const cache = new WindowedSeriesCache();
    expect(cache.tick(['nope'], () => undefined, 0)).toEqual([]);
  });
});

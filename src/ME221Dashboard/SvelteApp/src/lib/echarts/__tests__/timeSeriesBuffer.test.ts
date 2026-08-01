import { describe, it, expect } from 'vitest';
import { TimeSeriesBuffer } from '../TimeSeriesBuffer';

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
});

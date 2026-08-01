import { describe, it, expect } from 'vitest';
import {
  buildPlaybackColumns,
  buildTooltipRows,
  formatChartValue,
  LiveColumns,
  relativeTimeLabels,
  type ChartSeries,
} from '../chartUtils';

const series: ChartSeries[] = [
  { id: '1', name: 'RPM', color: '#0ea5e9' },
  { id: '2', name: 'AFR', color: '#f97316' },
];

describe('buildPlaybackColumns', () => {
  it('aligns series with identical timestamps into columns', () => {
    const data = new Map<string, Array<{ t: number; v: number }>>([
      ['1', [{ t: 100, v: 10 }, { t: 200, v: 20 }]],
      ['2', [{ t: 100, v: 1 }, { t: 200, v: 2 }]],
    ]);
    const cols = buildPlaybackColumns(series, data);
    expect(cols).toEqual([
      [100, 200],
      [10, 20],
      [1, 2],
    ]);
  });

  it('unions timestamps and fills missing samples with null', () => {
    const data = new Map<string, Array<{ t: number; v: number }>>([
      ['1', [{ t: 100, v: 10 }, { t: 300, v: 30 }]],
      ['2', [{ t: 200, v: 2 }]],
    ]);
    const cols = buildPlaybackColumns(series, data);
    expect(cols).toEqual([
      [100, 200, 300],
      [10, null, 30],
      [null, 2, null],
    ]);
  });

  it('sorts timestamps ascending regardless of insertion order', () => {
    const data = new Map<string, Array<{ t: number; v: number }>>([
      ['1', [{ t: 300, v: 3 }, { t: 100, v: 1 }, { t: 200, v: 2 }]],
    ]);
    const cols = buildPlaybackColumns(series, data);
    expect(cols[0]).toEqual([100, 200, 300]);
    expect(cols[1]).toEqual([1, 2, 3]);
  });

  it('appends overlay columns after main series columns', () => {
    const data = new Map<string, Array<{ t: number; v: number }>>([
      ['1', [{ t: 100, v: 10 }]],
    ]);
    const cols = buildPlaybackColumns(series, data, [
      { name: 'run1', color: '#fff', data: new Map([
        ['1', [{ t: 100, v: 99 }, { t: 200, v: 88 }]],
        ['2', [{ t: 100, v: 77 }]],
      ]) },
    ]);
    expect(cols).toEqual([
      [100, 200],
      [10, null],
      [null, null],
      [99, 88],
      [77, null],
    ]);
  });

  it('returns an all-null column for a series with no data', () => {
    const cols = buildPlaybackColumns(series, new Map([['1', [{ t: 5, v: 1 }]]]));
    expect(cols[2]).toEqual([null]);
  });
});

describe('LiveColumns', () => {
  it('keeps columns aligned across pushes', () => {
    const lc = new LiveColumns(2);
    lc.push(100, [10, 1]);
    lc.push(200, [null, 2]);
    expect(lc.data()).toEqual([
      [100, 200],
      [10, null],
      [1, 2],
    ]);
    expect(lc.length).toBe(2);
  });

  it('treats missing values as null', () => {
    const lc = new LiveColumns(2);
    lc.push(100, [5]);
    expect(lc.data()[2]).toEqual([null]);
  });

  it('drops the oldest rows once the cap is exceeded by the trim margin', () => {
    const lc = new LiveColumns(2, 10);
    for (let i = 0; i < 300; i++) lc.push(i, [i, -i]);
    // Trims to cap when the margin is exceeded, then grows again: 10 + (300 - 267)
    expect(lc.length).toBe(43);
    expect(lc.data()[0][0]).toBe(257);
    expect(lc.data()[0][42]).toBe(299);
    expect(lc.data()[1][42]).toBe(299);
    expect(lc.data()[2][42]).toBe(-299);
  });
});

describe('relativeTimeLabels', () => {
  const now = 100_000;
  const labels = relativeTimeLabels(() => now);

  it('formats relative offsets', () => {
    expect(labels(null as unknown as undefined, [100_000, 99_500, 99_000, 95_000, 60_000, 0])).toEqual([
      'now',
      'now',
      '-1s',
      '-5s',
      '-40s',
      '-2m',
    ]);
  });

  it('uses hours for large offsets', () => {
    expect(labels(null as unknown as undefined, [100_000 - 2 * 3600_000])).toEqual(['-2h']);
  });
});

describe('formatChartValue', () => {
  it('formats by magnitude like the old gauge formatting', () => {
    expect(formatChartValue(1234.56)).toBe('1235');
    expect(formatChartValue(123.456)).toBe('123.5');
    expect(formatChartValue(12.3456)).toBe('12.35');
    expect(formatChartValue(-1234.56)).toBe('-1235');
  });

  it('renders missing values as placeholder', () => {
    expect(formatChartValue(null)).toBe('--');
    expect(formatChartValue(undefined)).toBe('--');
    expect(formatChartValue(Number.NaN)).toBe('--');
  });
});

describe('buildTooltipRows', () => {
  it('reads values at the hovered index for each main series', () => {
    const rows = buildTooltipRows(series, [[0, 1], [10, 20], [null, 2.5]], 1);
    expect(rows).toEqual([
      { name: 'RPM', color: '#0ea5e9', value: '20.00' },
      { name: 'AFR', color: '#f97316', value: '2.50' },
    ]);
  });

  it('marks missing values with a placeholder', () => {
    const rows = buildTooltipRows(series, [[0], [10], [null]], 0);
    expect(rows[1].value).toBe('--');
  });
});

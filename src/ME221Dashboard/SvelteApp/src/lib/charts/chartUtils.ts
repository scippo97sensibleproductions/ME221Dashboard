export interface ChartSeries {
  id: string;
  name: string;
  color: string;
}

export interface OverlaySession {
  name: string;
  color: string;
  data: Map<string, Array<{ t: number; v: number }>>;
}

export interface Sample {
  t: number;
  v: number;
}

const DEFAULT_CAP = 18_000;
const TRIM_MARGIN = 256;

/**
 * Aligned columnar data for uPlot: `data[0]` is the x column, `data[i + 1]`
 * is the value column for `series[i]` (overlay columns appended after).
 * Missing samples are `null` so uPlot breaks the line at the gap.
 */
export type ChartColumns = (number | null)[][];

function columnFor(
  times: number[],
  pts: Array<{ t: number; v: number }> | undefined,
): (number | null)[] {
  if (!pts || pts.length === 0) return times.map(() => null);
  const byT = new Map<number, number>();
  for (const p of pts) byT.set(p.t, p.v);
  return times.map((t) => byT.get(t) ?? null);
}

/**
 * Build aligned columns for playback/overlay rendering from per-series
 * point lists. Timestamps may differ between series; the union is used and
 * missing values become `null`.
 */
export function buildPlaybackColumns(
  series: ChartSeries[],
  playbackData: Map<string, Array<{ t: number; v: number }>>,
  overlaySessions: OverlaySession[] = [],
): ChartColumns {
  const tsSet = new Set<number>();
  for (const s of series) {
    const pts = playbackData.get(s.id);
    if (pts) for (const p of pts) tsSet.add(p.t);
  }
  for (const ov of overlaySessions) {
    for (const pts of ov.data.values()) {
      for (const p of pts) tsSet.add(p.t);
    }
  }
  const times = Array.from(tsSet).sort((a, b) => a - b);

  const cols: ChartColumns = [times];
  for (const s of series) cols.push(columnFor(times, playbackData.get(s.id)));
  for (const ov of overlaySessions) {
    for (const s of series) cols.push(columnFor(times, ov.data.get(s.id)));
  }
  return cols;
}

/**
 * Live mode keeps a single x column (one push per frame) with one aligned
 * value column per series, so columns never diverge. Rows older than the
 * retention cap are dropped in batches (O(1) amortized).
 */
export class LiveColumns {
  readonly x: number[] = [];
  readonly cols: (number | null)[][] = [];
  #cap: number;

  constructor(seriesCount: number, cap: number = DEFAULT_CAP) {
    this.#cap = cap;
    for (let i = 0; i < seriesCount; i++) this.cols.push([]);
  }

  push(now: number, values: (number | null)[]): void {
    this.x.push(now);
    for (let i = 0; i < this.cols.length; i++) {
      this.cols[i].push(values[i] ?? null);
    }
    this.trim();
  }

  trim(): void {
    const excess = this.x.length - this.#cap;
    if (excess > TRIM_MARGIN) {
      this.x.splice(0, excess);
      for (const c of this.cols) c.splice(0, excess);
    }
  }

  data(): ChartColumns {
    return [this.x, ...this.cols];
  }

  get length(): number {
    return this.x.length;
  }
}

/** uPlot x-axis values relative to `getNow()`, like the ECharts formatter. */
export function relativeTimeLabels(getNow: () => number) {
  return (_u: unknown, ticks: number[]): string[] =>
    ticks.map((t) => {
      const diff = (getNow() - t) / 1000;
      if (diff < 1) return 'now';
      if (diff < 60) return `-${Math.round(diff)}s`;
      if (diff < 3600) return `-${Math.round(diff / 60)}m`;
      return `-${Math.round(diff / 3600)}h`;
    });
}

export function formatChartValue(v: number | null | undefined): string {
  if (v == null || Number.isNaN(v)) return '--';
  const a = Math.abs(v);
  if (a >= 1000) return v.toFixed(0);
  if (a >= 100) return v.toFixed(1);
  return v.toFixed(2);
}

export interface TooltipRow {
  name: string;
  color: string;
  value: string;
}

/** Values of all main series at the hovered column index. */
export function buildTooltipRows(
  series: ChartSeries[],
  data: ChartColumns,
  idx: number,
): TooltipRow[] {
  const rows: TooltipRow[] = [];
  for (let i = 0; i < series.length; i++) {
    const col = data[i + 1];
    rows.push({
      name: series[i].name,
      color: series[i].color,
      value: formatChartValue(col ? col[idx] : null),
    });
  }
  return rows;
}

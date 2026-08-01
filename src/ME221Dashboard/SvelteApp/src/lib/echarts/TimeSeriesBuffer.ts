interface Pt {
  t: number;
  v: number;
}

const MAX_AGE_MS = 30 * 60 * 1000; // 30 minutes

function bisect(buf: Pt[], cutoff: number): number {
  let lo = 0, hi = buf.length;
  while (lo < hi) {
    const m = (lo + hi) >> 1;
    buf[m].t < cutoff ? (lo = m + 1) : (hi = m);
  }
  return lo;
}

export class TimeSeriesBuffer {
  #series = new Map<string, Pt[]>();
  #maxPointsPerSeries: number;

  constructor(maxPointsPerSeries = 18_000) {
    this.#maxPointsPerSeries = maxPointsPerSeries;
  }

  push(seriesId: string, time: number, value: number): void {
    let buf = this.#series.get(seriesId);
    if (!buf) {
      buf = [];
      this.#series.set(seriesId, buf);
    }
    buf.push({ t: time, v: value });
    if (buf.length > this.#maxPointsPerSeries) {
      buf.splice(0, buf.length - this.#maxPointsPerSeries);
    }
  }

  getVisiblePoints(seriesId: string, now: number, windowMs: number): Pt[] {
    const buf = this.#series.get(seriesId);
    if (!buf || buf.length === 0) return [];
    const cutoff = now - windowMs;
    const start = bisect(buf, cutoff);
    return buf.slice(start);
  }

  get(seriesId: string): Pt[] | undefined {
    return this.#series.get(seriesId);
  }

  getAllSeries(): Map<string, Pt[]> {
    return this.#series;
  }

  getSeriesIds(): string[] {
    return Array.from(this.#series.keys());
  }

  clear(): void {
    this.#series.clear();
  }

  removeSeries(seriesId: string): void {
    this.#series.delete(seriesId);
  }
}

/**
 * Tracks how many buffer points per series have been handed to echarts via
 * `chart.appendData`, so live frames only append the delta (O(new points))
 * instead of re-sending the full window. `seed` resets the baseline on every
 * full rebuild; `tick` returns the newly arrived points, filtered to the
 * current window.
 */
export class WindowedSeriesCache {
  #counts = new Map<string, number>();

  reset(): void {
    this.#counts.clear();
  }

  /** Reset the consumption baseline to the current buffer contents. */
  seed(seriesIds: string[], getPts: (id: string) => Pt[] | undefined): void {
    this.reset();
    for (const id of seriesIds) {
      const pts = getPts(id);
      this.#counts.set(id, pts?.length ?? 0);
    }
  }

  /**
   * Return points pushed since the last call, excluding points older than
   * the cutoff. The result is ready for `chart.appendData({ seriesIndex, data })`.
   */
  tick(
    seriesIds: string[],
    getPts: (id: string) => Pt[] | undefined,
    cutoff: number,
  ): Array<{ id: string; data: number[][] }> {
    const out: Array<{ id: string; data: number[][] }> = [];
    for (const id of seriesIds) {
      const pts = getPts(id);
      if (!pts) continue;
      const start = this.#counts.get(id);
      if (start == null) {
        this.#counts.set(id, pts.length);
        continue;
      }
      if (start >= pts.length) continue;
      const delta: number[][] = [];
      for (let i = start; i < pts.length; i++) {
        if (pts[i].t >= cutoff) delta.push([pts[i].t, pts[i].v]);
      }
      this.#counts.set(id, pts.length);
      if (delta.length > 0) out.push({ id, data: delta });
    }
    return out;
  }
}

export type { Pt };

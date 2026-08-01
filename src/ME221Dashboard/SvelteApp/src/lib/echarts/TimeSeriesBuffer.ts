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
 * Maintains per-series windowed `[t, v]` arrays for echarts' dynamic-data
 * update pattern: the full array is passed to setOption every frame and
 * echarts appends only the new tail. New points are appended incrementally
 * and points older than the window are trimmed, so per-frame work is O(1)
 * amortized instead of O(n) filter/map over the whole buffer.
 */
export class WindowedSeriesCache {
  #data = new Map<string, number[][]>();
  #counts = new Map<string, number>();

  reset(): void {
    this.#data.clear();
    this.#counts.clear();
  }

  /** Rebuild the cache from the current buffer contents (config changes). */
  seed(
    seriesIds: string[],
    getPts: (id: string) => Pt[] | undefined,
    cutoff: number,
  ): void {
    this.reset();
    for (const id of seriesIds) {
      const pts = getPts(id);
      const arr: number[][] = [];
      if (pts) {
        for (const p of pts) {
          if (p.t >= cutoff) arr.push([p.t, p.v]);
        }
        this.#counts.set(id, pts.length);
      }
      this.#data.set(id, arr);
    }
  }

  /**
   * Append points pushed since the last call, trim points older than the
   * cutoff, and return per-series data ready for `setOption({ series })`.
   */
  tick(
    seriesIds: string[],
    getPts: (id: string) => Pt[] | undefined,
    cutoff: number,
  ): Array<{ id: string; data: number[][] }> {
    const out: Array<{ id: string; data: number[][] }> = [];
    for (const id of seriesIds) {
      const pts = getPts(id);
      const arr = this.#data.get(id);
      if (!pts || !arr) continue;
      const start = this.#counts.get(id) ?? pts.length;
      if (start < pts.length) {
        for (let i = start; i < pts.length; i++) {
          if (pts[i].t >= cutoff) arr.push([pts[i].t, pts[i].v]);
        }
        this.#counts.set(id, pts.length);
      }
      while (arr.length > 0 && arr[0][0] < cutoff) arr.shift();
      out.push({ id, data: arr });
    }
    return out;
  }
}

export type { Pt };

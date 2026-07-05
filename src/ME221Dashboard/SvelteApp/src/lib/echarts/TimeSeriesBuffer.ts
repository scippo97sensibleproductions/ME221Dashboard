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

export type { Pt };

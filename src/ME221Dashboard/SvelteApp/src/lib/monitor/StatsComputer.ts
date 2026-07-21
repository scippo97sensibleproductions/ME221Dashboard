export interface RangeStats {
  min: number;
  max: number;
  avg: number;
  delta: number;
  rateOfChange: number;
  count: number;
  durationMs: number;
}

export function computeRangeStats(
  data: Array<{ t: number; v: number }>,
): RangeStats | null {
  if (data.length === 0) return null;

  let min = Infinity;
  let max = -Infinity;
  let sum = 0;

  for (const s of data) {
    if (s.v < min) min = s.v;
    if (s.v > max) max = s.v;
    sum += s.v;
  }

  const avg = sum / data.length;
  const durationMs = data[data.length - 1].t - data[0].t;
  const delta = data[data.length - 1].v - data[0].v;
  const rateOfChange = durationMs > 0 ? (delta / durationMs) * 1000 : 0;

  return {
    min,
    max,
    avg,
    delta,
    rateOfChange,
    count: data.length,
    durationMs,
  };
}

export function computeRangeStatsBetween(
  data: Array<{ t: number; v: number }>,
  startMs: number,
  endMs: number,
): RangeStats | null {
  const filtered = data.filter(s => s.t >= startMs && s.t <= endMs);
  return computeRangeStats(filtered);
}

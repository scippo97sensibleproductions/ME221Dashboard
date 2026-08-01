// ── Chart data helpers (R22-R24) ─────────────────────────────────────────────
// Pure, allocation-friendly helpers shared between DashboardPage (history
// writers) and ChartGauge (overlay rendering). Negative entity ids (GPS,
// odometer, derived) are valid overlay targets — they are never excluded here.

export interface ChartSample {
  t: number;
  v: number;
}

// Dash arrays are module-level singletons so the draw path never allocates.
// Callers must NOT mutate the returned array (canvas setLineDash copies it).
const DASH_SOLID: number[] = [];
const DASH_DASHED: number[] = [8, 4];
const DASH_DOTTED: number[] = [2, 3];

/**
 * Line style → canvas dash pattern.
 * 0 = solid, 1 = dashed [8,4], 2 = dotted [2,3]; anything else degrades to solid.
 */
export function lineDashFor(style: number): number[] {
  switch (style) {
    case 1: return DASH_DASHED;
    case 2: return DASH_DOTTED;
    default: return DASH_SOLID;
  }
}

/**
 * Append a sample to a time-windowed ring buffer (mutates in place, no copy).
 * Evicts samples older than `windowMs` from the front and caps total length at
 * `maxSamples`. Returns the same buffer instance for chaining.
 */
export function pushSample(
  buffer: ChartSample[],
  sample: ChartSample,
  windowMs: number,
  maxSamples: number
): ChartSample[] {
  buffer.push(sample);
  if (windowMs > 0) {
    const cutoff = sample.t - windowMs;
    let evict = 0;
    const len = buffer.length;
    while (evict < len && buffer[evict].t < cutoff) evict++;
    if (evict > 0) buffer.splice(0, evict);
  }
  const over = buffer.length - maxSamples;
  if (over > 0) buffer.splice(0, over);
  return buffer;
}

/**
 * Auto Y-range across all visible series (primary + overlays) with 10% padding
 * on each side, mirroring the existing chart behavior. Returns null when no
 * series contains any values.
 */
export function computeAutoRange(series: { v: number }[][]): { min: number; max: number } | null {
  let min = Infinity;
  let max = -Infinity;
  for (let s = 0; s < series.length; s++) {
    const pts = series[s];
    for (let i = 0; i < pts.length; i++) {
      const v = pts[i].v;
      if (v < min) min = v;
      if (v > max) max = v;
    }
  }
  if (min === Infinity) return null;
  const m = (max - min) * 0.1 || 1;
  return { min: min - m, max: max + m };
}

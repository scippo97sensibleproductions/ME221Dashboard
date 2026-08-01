export interface ScaleTick {
  /** 0..1 position along the scale (start → end). */
  fraction: number;
  /** Formatted value label, or null when labels are off for this tick. */
  label: string | null;
}

/**
 * Generate evenly spaced scale ticks for a min/max range.
 * count is clamped to 0..20; count 0 yields no ticks.
 * Labels appear only when labelsOn is true, at every `everyN`-th tick.
 */
export function buildScaleTicks(
  min: number,
  max: number,
  count: number,
  labelsOn: boolean,
  everyN: number
): ScaleTick[] {
  const n = Math.max(0, Math.min(20, Math.round(count)));
  if (n === 0) return [];
  const every = Math.max(1, Math.round(everyN || 1));
  const range = max - min;
  const ticks: ScaleTick[] = [];
  for (let i = 0; i <= n; i++) {
    const fraction = i / n;
    const label = labelsOn && i % every === 0 ? formatTickLabel(min + fraction * range) : null;
    ticks.push({ fraction, label });
  }
  return ticks;
}

function formatTickLabel(value: number): string {
  if (!Number.isFinite(value)) return '';
  if (Math.abs(value) >= 100 || Number.isInteger(value)) {
    return Math.round(value).toLocaleString('en-US');
  }
  return String(Math.round(value * 10) / 10);
}

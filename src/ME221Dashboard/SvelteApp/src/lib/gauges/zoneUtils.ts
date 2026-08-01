export interface ZoneThresholds {
  amber: number;
  red: number;
}

/** Clamp both thresholds to [0,1] and enforce amber <= red (AE2). */
export function clampZoneThresholds(amber: number, red: number): ZoneThresholds {
  const a = Math.max(0, Math.min(1, amber));
  const r = Math.max(0, Math.min(1, red));
  return { amber: Math.min(a, r), red: r };
}

/**
 * Resolve the color for a value fraction given zone thresholds.
 * Below amber uses the LUT color; between amber and red uses the amber color;
 * at or above red uses the red color. Identical thresholds produce no inverted range.
 */
export function zoneColorAt(
  fraction: number,
  amber: number,
  red: number,
  lutColor: string,
  amberColor = '#F59F00',
  redColor = '#E03131'
): string {
  if (fraction >= red) return redColor;
  if (fraction >= amber) return amberColor;
  return lutColor;
}

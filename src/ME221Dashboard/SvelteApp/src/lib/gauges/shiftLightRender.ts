import { parseHex, isCvdConfusable } from '../warnings/colorWarnings';

// ─── Zone palette (KTD6) ─────────────────────────────────────────────────────
// Green → amber → red, left to right. Reuses the Warning Centre's CVD-calibrated
// palette guidance: the zone hues must not be confusable under deuteranopia/
// protanopia (asserted in tests via the colorWarnings confusability check).
export const SHIFT_ZONE_COLORS = {
  green: '#2EA043',
  amber: '#F59F00',
  red: '#E81123',
} as const;

// ─── State texts (announced via the sr-only live region, U5) ───────────────
export const SHIFT_STATE_TEXTS = {
  shiftNow: 'Shift now',
  downshift: 'Downshift suggested',
  belowRange: 'Below shift range',
  approaching: 'Approaching shift point',
  notConfigured: 'Shift light not configured',
  dataStale: 'Shift light data stale',
} as const;

export type ShiftLightRenderMode =
  | 'inert'      // dim segments, no lit segments, no arrows (R14)
  | 'cold'       // below the ramp band: 1–2 segments lit at full intensity
  | 'progressive' // inside the ramp band: fill grows with RPM (R11)
  | 'shiftNow'   // at/above the shift point: full bar + ▲ flash (R12)
  | 'downshift'; // hold: frozen entry pattern, ▼, low-segment flash (R12)

export interface ShiftSegment {
  /** 0..1 intensity (1 = full, 0.5 = frozen/reduced, 0.27 = inert dim). */
  intensity: number;
  color: string;
}

export interface ShiftLightRenderInput {
  /** −3005 countdown entity value (null = inert). */
  countdown: number | null;
  /** −3006 shift-state entity value (−1/0/1, null = inert). */
  shiftState: number | null;
  /** Raw RPM (null within a sub-tick stale window → inert). */
  rpm: number | null;
  /** Frames stale (component computes from LiveDataStore STALE_MS). */
  stale: boolean;
  /** Per-dashboard shift point (enriched by DashboardPage; null = unset). */
  shiftPoint: number | null;
  /** Gauge-local ramp width (R13); clamped so shiftPoint − ramp ≥ 0. */
  rampWidthRpm: number;
  /** Flash phase (component-driven from `flashPhase`); ignored when steady. */
  flashOn: boolean;
  /** Steady rendering (settings preview): no flash, full intensity. */
  steady?: boolean;
  /** Segment count (default 16, clamped 4..32). */
  segmentCount?: number;
  /** Color-zone count (default 3, clamped 1..3). */
  zoneCount?: number;
}

export interface ShiftLightRenderResult {
  mode: ShiftLightRenderMode;
  segments: ShiftSegment[];
  showUp: boolean;
  showDown: boolean;
  /** Flash schedule active (shiftNow 4 Hz bar/▲, downshift 2 Hz low segments). */
  flash: { rateHz: number; on: boolean } | null;
  /** Effective ramp: min(rampWidthRpm, shiftPoint) — never a below-zero band. */
  effectiveRamp: number;
  /** Announcement text for the current state (null = nothing to announce). */
  announcement: string | null;
  /** True when the bar is frozen on the downshift-entry pattern. */
  frozen: boolean;
}

// ─── Flash schedules (KTD6) ────────────────────────────────────────────────
export const FLASH_RATES = {
  shiftNow: 4,
  downshiftLow: 2,
} as const;

/** 50% duty square wave; `phase` is the caller's wall-clock in ms. */
export function flashPhase(nowMs: number, rateHz: number): boolean {
  const period = 1000 / rateHz;
  return (nowMs % period) / period < 0.5;
}

export const INERT_DIM_OPACITY = 0.27;
export const FROZEN_INTENSITY = 0.55;

export const DEFAULT_SEGMENT_COUNT = 16;
export const MIN_SEGMENT_COUNT = 3;
export const MAX_SEGMENT_COUNT = 48;
const COLD_LIT_SEGMENTS = 2;

export function clampSegmentCount(value: number | undefined): number {
  return Math.max(MIN_SEGMENT_COUNT, Math.min(MAX_SEGMENT_COUNT, Math.round(value ?? DEFAULT_SEGMENT_COUNT)));
}

export function clampZoneCount(value: number | undefined): number {
  return Math.max(1, Math.min(3, Math.round(value ?? 3)));
}

/**
 * Zone hues for a segment fraction, for the configured color count. Zones
 * divide the bar EVENLY so every selected color is always visible regardless
 * of segment count: 1 → all red; 2 → green left half, red right half;
 * 3 → green/amber/red in thirds.
 */
function zoneColor(fraction: number, zoneCount: number): string {
  if (zoneCount <= 1) return SHIFT_ZONE_COLORS.red;
  if (zoneCount === 2) return fraction < 0.5 ? SHIFT_ZONE_COLORS.green : SHIFT_ZONE_COLORS.red;
  if (fraction < 1 / 3) return SHIFT_ZONE_COLORS.green;
  if (fraction < 2 / 3) return SHIFT_ZONE_COLORS.amber;
  return SHIFT_ZONE_COLORS.red;
}

/** Low (left) segments that flash red during a downshift hold: ~19% of the bar. */
function lowSegmentsFor(segCount: number): number {
  return Math.min(3, Math.max(1, Math.round(segCount * 0.1875)));
}

export interface ShiftLightRenderer {
  render(input: ShiftLightRenderInput): ShiftLightRenderResult;
  /** Clear the session-scoped freeze memo (staleness, dashboard switch, mount). */
  reset(): void;
}

/**
 * Pure renderer. Session-scoped freeze memo, keyed by entity pair (−3005 ⇔ −3006,
 * KTD5): the frozen entry pattern survives tick re-emissions but is cleared
 * whenever the render function observes null inputs (staleness nulls, dashboard-
 * switch slot clears, mount). The entry pattern is the LAST ramp pattern seen
 * before the crossing — the hold render's rpm already sits below the floor.
 */
export function createShiftLightRenderer(): ShiftLightRenderer {
  let freezePattern: number[] | null = null;
  let frozenPairKey: string | null = null;
  let lastRampPattern: number[] | null = null;

  function pairKey(input: ShiftLightRenderInput): string {
    return `${input.countdown ?? 'n'}:${input.shiftState ?? 'n'}`;
  }

  function render(input: ShiftLightRenderInput): ShiftLightRenderResult {
    const key = pairKey(input);
    const segCount = clampSegmentCount(input.segmentCount);
    const zoneCount = clampZoneCount(input.zoneCount);
    const hasNull = input.countdown === null || input.shiftState === null || input.rpm === null;
    if (hasNull) {
      freezePattern = null;
      frozenPairKey = null;
      lastRampPattern = null;
      // Inert-cause split (U5): not configured (no shift point / no RPM datalink)
      // vs stale frames (config present, lastUpdateAt older than STALE_MS) vs
      // sample-null with fresh frames — the transient sub-tick cause announces
      // nothing.
      const unconfigured = input.shiftPoint == null || input.shiftPoint <= 0;
      const announcement = unconfigured
        ? SHIFT_STATE_TEXTS.notConfigured
        : input.stale
          ? SHIFT_STATE_TEXTS.dataStale
          : null;
      const segments = Array.from({ length: segCount }, () => ({
        intensity: INERT_DIM_OPACITY,
        color: SHIFT_ZONE_COLORS.green,
      }));
      return {
        mode: 'inert',
        segments,
        showUp: false,
        showDown: false,
        flash: null,
        effectiveRamp: 0,
        announcement,
        frozen: false,
      };
    }

    const shiftPoint = input.shiftPoint ?? 0;
    const effectiveRamp = shiftPoint > 0 ? Math.min(Math.max(0, input.rampWidthRpm), shiftPoint) : 0;
    const bandStart = Math.max(0, shiftPoint - effectiveRamp);
    const rpm: number = input.rpm as number; // narrowed by the hasNull early-return above

    if (input.shiftState === -1) {
      // Downshift hold: freeze the entry pattern (memo), ▼ steady, low-segment
      // flash. The countdown is ignored during the hold (R12). Frozen segments
      // render at 50–60% intensity (KTD6), distinct from inert (27%) and cold.
      // The Warning Centre never influences the shift light (its flash must
      // always run) — dimming is Warning-Centre-only by design.
      if (frozenPairKey !== key || freezePattern === null || freezePattern.length !== segCount) {
        freezePattern = lastRampPattern ?? capturePattern(rpm, effectiveRamp, bandStart, segCount);
        frozenPairKey = key;
      }
      const lowCount = lowSegmentsFor(segCount);
      const frozen = freezePattern;
      const segments = Array.from({ length: segCount }, (_, i) => {
        if (i < lowCount) {
          const on = input.flashOn;
          const intensity = on ? 1 : FROZEN_INTENSITY * 0.5;
          return { intensity, color: SHIFT_ZONE_COLORS.red };
        }
        const base = frozen[i] === 1 ? FROZEN_INTENSITY : 0.4;
        return { intensity: base, color: frozenColor(i, frozen.length, zoneCount) };
      });
      return {
        mode: 'downshift',
        segments,
        showUp: false,
        showDown: true,
        flash: { rateHz: FLASH_RATES.downshiftLow, on: input.flashOn },
        effectiveRamp,
        announcement: SHIFT_STATE_TEXTS.downshift,
        frozen: true,
      };
    }

    freezePattern = null;
    frozenPairKey = null;

    if (input.shiftState === 1) {
      lastRampPattern = Array.from({ length: segCount }, () => 1);
      // Strong 4 Hz blink: the off phase drops to 30% so the flash is clearly
      // visible (a subtle nudge reads as "never blinks"). The Warning Centre
      // never dims the shift light — the flash must always run. Steady (settings
      // preview) renders the full bar without a flash schedule.
      const segments = Array.from({ length: segCount }, (_, i) => ({
        intensity: input.steady ? 1 : input.flashOn ? 1 : 0.3,
        color: zoneColor(i / segCount, zoneCount),
      }));
      return {
        mode: 'shiftNow',
        segments,
        showUp: true,
        showDown: false,
        flash: input.steady ? null : { rateHz: FLASH_RATES.shiftNow, on: input.flashOn },
        effectiveRamp,
        announcement: SHIFT_STATE_TEXTS.shiftNow,
        frozen: false,
      };
    }

    return renderRamp(rpm, shiftPoint, effectiveRamp, bandStart, segCount, zoneCount);
  }

  function renderRamp(
    rpm: number,
    shiftPoint: number,
    effectiveRamp: number,
    bandStart: number,
    segCount: number,
    zoneCount: number,
  ): ShiftLightRenderResult {
    if (rpm < bandStart || effectiveRamp <= 0) {
      // Cold: below the ramp band, 1–2 segments at full intensity (R14).
      const segments = Array.from({ length: segCount }, (_, i) => ({
        intensity: i < Math.min(COLD_LIT_SEGMENTS, segCount) ? 1 : INERT_DIM_OPACITY,
        color: zoneColor(i / segCount, zoneCount),
      }));
      lastRampPattern = segments.map(s => (s.intensity === 1 ? 1 : 0.4));
      return {
        mode: 'cold',
        segments,
        showUp: false,
        showDown: false,
        flash: null,
        effectiveRamp,
        announcement: SHIFT_STATE_TEXTS.belowRange,
        frozen: false,
      };
    }

    const position = Math.min(1, (rpm - bandStart) / effectiveRamp);
    const lit = Math.round(position * segCount);
    const segments = Array.from({ length: segCount }, (_, i) => ({
      intensity: i < lit ? 1 : INERT_DIM_OPACITY,
      color: zoneColor(i / segCount, zoneCount),
    }));
    lastRampPattern = segments.map(s => (s.intensity === 1 ? 1 : 0.4));
    return {
      mode: 'progressive',
      segments,
      showUp: false,
      showDown: false,
      flash: null,
      effectiveRamp,
      announcement: SHIFT_STATE_TEXTS.approaching,
      frozen: false,
    };
  }

  return {
    render,
    reset: () => {
      freezePattern = null;
      frozenPairKey = null;
      lastRampPattern = null;
    },
  };
}

function capturePattern(rpm: number, effectiveRamp: number, bandStart: number, segCount: number): number[] {
  if (effectiveRamp <= 0) {
    return Array.from({ length: segCount }, () => 0.4);
  }
  const position = Math.min(1, (rpm - bandStart) / effectiveRamp);
  const lit = Math.round(position * segCount);
  return Array.from({ length: segCount }, (_, i) => (i < lit ? 1 : 0.4));
}

function frozenColor(index: number, count: number, zoneCount: number): string {
  return zoneColor(index / Math.max(1, count), zoneCount);
}

// ─── Singleton for the live gauge ──────────────────────────────────────────
export const shiftLightRenderer: ShiftLightRenderer = createShiftLightRenderer();

// ─── Zone-color CVD sanity (exported for the render tests) ─────────────────
export function zoneColorsCvdSafe(): boolean {
  const pairs: Array<[string, string]> = [
    [SHIFT_ZONE_COLORS.green, SHIFT_ZONE_COLORS.amber],
    [SHIFT_ZONE_COLORS.amber, SHIFT_ZONE_COLORS.red],
  ];
  for (const [a, b] of pairs) {
    if (parseHex(a) === null || parseHex(b) === null) return false;
    if (isCvdConfusable(a, b)) return false;
  }
  return true;
}

// ─── Shifter config-surface pure helpers (R19) ─────────────────────────────

/** Shift-point bounds (R19): the 200 minimum keeps FLOOR_MIN strictly below any
 *  valid shift point, aligned with the countdown entity's nominal 0–9000 range. */
export const SHIFT_POINT_MIN = 200;
export const SHIFT_POINT_MAX = 9000;
/** Downshift-floor minimum (R19). */
export const FLOOR_MIN = 100;
/** Floor at/below which advice is effectively inactive (the hidden band). */
export const IDLE_FLOOR = 200;
/** Live-reject error auto-hide timer (U6). */
export const LIVE_REJECT_AUTO_HIDE_MS = 3000;
/** Default dip spacing used when no establishment or persisted spacing exists. */
export const DEFAULT_DIP_RPM = 1500;

export interface ShifterSessionValues {
  shiftPointRpm: number;
  downshiftFloorRpm: number;
}

/** Shift point valid when within [SHIFT_POINT_MIN, SHIFT_POINT_MAX] (R19). */
export function isShiftPointValid(value: number): boolean {
  return value >= SHIFT_POINT_MIN && value <= SHIFT_POINT_MAX;
}

/** The floor field is disabled while the shift point is unset or zero. */
export function isFloorFieldDisabled(shiftPointRpm: number | null): boolean {
  return shiftPointRpm == null || shiftPointRpm <= 0;
}

/**
 * Direct floor edit validation in the NON-held state (R19): an input at or above
 * the shift point, or below FLOOR_MIN, reverts to the last valid value.
 * While held (U8), the at-or-above check is skipped — only FLOOR_MIN applies.
 */
export function isFloorEditValid(
  value: number,
  shiftPointRpm: number | null,
  held: boolean,
): boolean {
  if (value < FLOOR_MIN) return false;
  if (held) return true;
  if (shiftPointRpm == null || shiftPointRpm <= 0) return false;
  return value < shiftPointRpm;
}

// ─── R19 lifecycle helpers (U8) ─────────────────────────────────────────────

/**
 * Dip-preserving clamp (R19): `newFloor = min(max(FLOOR_MIN, newShiftPoint − dip),
 * newShiftPoint)`. The dip is frozen at establishment and never re-captured by
 * in-hold edits; fall back to the persisted spacing or DEFAULT_DIP_RPM.
 */
export function computeClampedFloor(newShiftPoint: number, dip: number): number {
  return Math.min(Math.max(FLOOR_MIN, newShiftPoint - dip), newShiftPoint);
}

/**
 * Resolve the clamp dip: the session-establishment spacing first (frozen at
 * establishment, never evaluated at clamp time), else the persisted positive
 * spacing, else the 1500 default. A non-positive persisted spacing (legacy
 * floor ≥ shift point) falls back to the default and caps at newShiftPoint.
 */
export function resolveClampDip(sessionDip: number | null, persistedSpacing: number | null): number {
  if (sessionDip !== null && sessionDip > 0) return sessionDip;
  if (persistedSpacing !== null && persistedSpacing > 0) return persistedSpacing;
  return DEFAULT_DIP_RPM;
}

/** The floor is held when the shift point sits at or below an existing floor. */
export function isFloorHeld(shiftPointRpm: number, downshiftFloorRpm: number): boolean {
  return downshiftFloorRpm > 0 && shiftPointRpm > 0 && shiftPointRpm <= downshiftFloorRpm;
}

/**
 * The re-derive suggestion (R19): a floor re-derived from an unset state appears
 * as a confirmation-required suggestion and does not arm ▼ until confirmed.
 * Hidden when shiftPoint − 1500 ≤ IDLE_FLOOR (i.e. shift point ≤ 1700) — the
 * formula needs no clamp and no dead-advice zone exists.
 */
export function isSuggestionVisible(shiftPointRpm: number): boolean {
  return shiftPointRpm - DEFAULT_DIP_RPM > IDLE_FLOOR;
}

/** The suggested floor value: shift point minus the default dip. */
export function suggestedFloor(shiftPointRpm: number): number {
  return shiftPointRpm - DEFAULT_DIP_RPM;
}

/**
 * Clamp-to-minimum detection: a stored floor at or below IDLE_FLOOR (zero
 * excluded — zero means "floor not set") renders the effectively-inactive
 * notice + "Clear floor" on load (the conflation with a deliberate user-set
 * low floor is by design, R19).
 */
export function isEffectivelyInactiveFloor(downshiftFloorRpm: number): boolean {
  return downshiftFloorRpm > 0 && downshiftFloorRpm <= IDLE_FLOOR;
}
export const SHIFTER_COPY = {
  floorFieldHint: 'Set a shift point first',
  floorUnset: '▼ disabled — floor not set',
  adviceUnavailable: 'Downshift advice unavailable at this shift point',
  effectivelyInactive: 'Downshift floor is at the minimum — advice is effectively inactive',
  clearFloor: 'Clear floor',
  valueOutOfRange: 'Value out of range',
  /** U5-owned ramp-clamp copy, canonical here (pinned by the copy-parity test). */
  rampClamped: 'Ramp clamped to {value}',
  /** R19 on-save clamp of the downshift FLOOR while held (distinct from ramp). */
  floorClamped: 'Downshift floor clamped to {value}',
  suggestionBody: 'Set the downshift floor to {value}?',
  dirtyTitle: 'Discard unsaved changes?',
  dirtyBody: 'Your shifter settings have unsaved changes.',
  dirtyStay: 'Stay',
  dirtyDiscard: 'Discard',
  dirtySaveAndLeave: 'Save and leave',
  dirtyDelete: 'Discard and delete',
} as const;

/** Format a template containing a single {value} placeholder. */
export function formatShifterCopy(template: string, value: number): string {
  return template.replace('{value}', String(Math.round(value)));
}

// ─── Canonical config-surface copy (U6/U8) ─────────────────────────────────
// The copy strings here are the canonical constants the dialog, hints, and
// notices must never drift from (pinned by the copy-parity tests).

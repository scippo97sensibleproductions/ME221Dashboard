import { describe, it, expect } from 'vitest';
import {
  SHIFT_POINT_MIN,
  SHIFT_POINT_MAX,
  FLOOR_MIN,
  IDLE_FLOOR,
  isShiftPointValid,
  isFloorFieldDisabled,
  isFloorEditValid,
  isFloorHeld,
  isSuggestionVisible,
  suggestedFloor,
  isEffectivelyInactiveFloor,
  computeClampedFloor,
  resolveClampDip,
  DEFAULT_DIP_RPM,
  SHIFTER_COPY,
  formatShifterCopy,
} from '../shifterConfig';

describe('shifterConfig — shift-point bounds (R19)', () => {
  it('accepts the full valid range 200–9000', () => {
    expect(isShiftPointValid(SHIFT_POINT_MIN)).toBe(true);
    expect(isShiftPointValid(7000)).toBe(true);
    expect(isShiftPointValid(SHIFT_POINT_MAX)).toBe(true);
  });

  it('rejects below 200 and above 9000', () => {
    expect(isShiftPointValid(199)).toBe(false);
    expect(isShiftPointValid(0)).toBe(false);
    expect(isShiftPointValid(9001)).toBe(false);
    expect(isShiftPointValid(12000)).toBe(false);
  });

  it('keeps FLOOR_MIN strictly below the minimum shift point', () => {
    expect(FLOOR_MIN).toBeLessThan(SHIFT_POINT_MIN);
  });
});

describe('shifterConfig — floor field gating', () => {
  it('disabled while the shift point is unset or zero', () => {
    expect(isFloorFieldDisabled(null)).toBe(true);
    expect(isFloorFieldDisabled(0)).toBe(true);
    expect(isFloorFieldDisabled(-5)).toBe(true);
    expect(isFloorFieldDisabled(7000)).toBe(false);
  });
});

describe('shifterConfig — floor edit validation (R19, non-held)', () => {
  it('rejects values below FLOOR_MIN', () => {
    expect(isFloorEditValid(99, 7000, false)).toBe(false);
    expect(isFloorEditValid(FLOOR_MIN, 7000, false)).toBe(true);
  });

  it('rejects values at or above the shift point', () => {
    expect(isFloorEditValid(7000, 7000, false)).toBe(false);
    expect(isFloorEditValid(7500, 7000, false)).toBe(false);
    expect(isFloorEditValid(6999, 7000, false)).toBe(true);
  });

  it('rejects edits while the shift point is unset', () => {
    expect(isFloorEditValid(5000, null, false)).toBe(false);
    expect(isFloorEditValid(5000, 0, false)).toBe(false);
  });

  it('while held, only the FLOOR_MIN bound applies (at-or-above is accepted)', () => {
    expect(isFloorEditValid(7500, 7000, true)).toBe(true);
    expect(isFloorEditValid(99, 7000, true)).toBe(false);
  });
});

describe('shifterConfig — idle floor band constants', () => {
  it('IDLE_FLOOR sits at 200, equal to the shift-point minimum', () => {
    expect(IDLE_FLOOR).toBe(200);
    expect(IDLE_FLOOR).toBe(SHIFT_POINT_MIN);
  });
});

describe('shifterConfig — copy parity (canonical constants)', () => {
  it('pins the U5 ramp-clamp copy and its {value} formatting', () => {
    expect(SHIFTER_COPY.rampClamped).toBe('Ramp clamped to {value}');
    expect(formatShifterCopy(SHIFTER_COPY.rampClamped, 1500)).toBe('Ramp clamped to 1500');
  });

  it('pins the R19 floor-clamp copy (distinct from the ramp copy)', () => {
    expect(SHIFTER_COPY.floorClamped).toBe('Downshift floor clamped to {value}');
    expect(formatShifterCopy(SHIFTER_COPY.floorClamped, 3000)).toBe('Downshift floor clamped to 3000');
  });

  it('pins the floor-field hint shared by the section and the settings preview', () => {
    expect(SHIFTER_COPY.floorFieldHint).toBe('Set a shift point first');
  });

  it('pins the dirty-dialog copy incl. the delete variant', () => {
    expect(SHIFTER_COPY.dirtyTitle).toBe('Discard unsaved changes?');
    expect(SHIFTER_COPY.dirtyDiscard).toBe('Discard');
    expect(SHIFTER_COPY.dirtySaveAndLeave).toBe('Save and leave');
    expect(SHIFTER_COPY.dirtyDelete).toBe('Discard and delete');
    expect(SHIFTER_COPY.dirtyStay).toBe('Stay');
  });

  it('pins the muted-advice and suggestion copy', () => {
    expect(SHIFTER_COPY.adviceUnavailable).toBe('Downshift advice unavailable at this shift point');
    expect(SHIFTER_COPY.effectivelyInactive).toBe('Downshift floor is at the minimum — advice is effectively inactive');
    expect(SHIFTER_COPY.clearFloor).toBe('Clear floor');
    expect(formatShifterCopy(SHIFTER_COPY.suggestionBody, 5500)).toBe('Set the downshift floor to 5500?');
  });
});

describe('shifterConfig — R19 held state and dip-preserving clamp', () => {
  it('enters the held state when the shift point moves at or below the floor (equality edge)', () => {
    expect(isFloorHeld(7000, 5000)).toBe(false);
    expect(isFloorHeld(5000, 5000)).toBe(true);
    expect(isFloorHeld(3000, 5000)).toBe(true);
    expect(isFloorHeld(0, 5000)).toBe(false, 'unset shift point is never held');
  });

  it('clamp formula: newFloor = min(max(FLOOR_MIN, newShiftPoint − dip), newShiftPoint)', () => {
    expect(computeClampedFloor(3000, 1500)).toBe(1500);
    expect(computeClampedFloor(7000, 1500)).toBe(5500);
    expect(computeClampedFloor(200, 1500)).toBe(FLOOR_MIN);
    expect(computeClampedFloor(300, 1500)).toBe(FLOOR_MIN);
  });

  it('preserves small dips exactly', () => {
    expect(computeClampedFloor(5000, 200)).toBe(4800);
    expect(computeClampedFloor(2100, 100)).toBe(2000);
  });

  it('worked example: accept at 7000 → floor 5500, lower shift point to 3000 → clamped to 1500', () => {
    // Session dip frozen at establishment: 7000 − 5500 = 1500.
    expect(resolveClampDip(1500, null)).toBe(1500);
    expect(computeClampedFloor(3000, resolveClampDip(1500, null))).toBe(1500);
  });

  it('held edits to above the bound never re-capture the dip (frozen establishment spacing)', () => {
    // Session dip 1500 is frozen; an in-hold floor edit to 6000 must not
    // change the clamp outcome — the dip stays 1500 (never shiftPoint − 6000).
    const clampedAtLowering = computeClampedFloor(3000, resolveClampDip(1500, null));
    expect(clampedAtLowering).toBe(1500);
    // The clamp result is identical regardless of the in-hold edited floor.
    expect(computeClampedFloor(3000, resolveClampDip(1500, 6000 - 3000))).toBe(1500);
  });

  it('the capped formula holds for any frozen dip', () => {
    for (const dip of [1, 100, 1500, 5000, 100000]) {
      for (const sp of [200, 1000, 3000, 7000, 9000]) {
        const clamped = computeClampedFloor(sp, dip);
        expect(clamped).toBeLessThanOrEqual(sp);
        expect(clamped).toBeGreaterThanOrEqual(FLOOR_MIN);
      }
    }
  });

  it('persisted-spacing fallback: non-positive spacing (legacy floor ≥ shift point) uses the 1500 offset and caps at newShiftPoint', () => {
    expect(resolveClampDip(null, 5000 - 7000)).toBe(DEFAULT_DIP_RPM);
    expect(resolveClampDip(null, -2000)).toBe(DEFAULT_DIP_RPM);
    expect(resolveClampDip(null, 0)).toBe(DEFAULT_DIP_RPM);
    expect(resolveClampDip(null, 1200)).toBe(1200);
    // Session dip wins over persisted spacing.
    expect(resolveClampDip(900, 1200)).toBe(900);
  });

  it('clamp-to-minimum persists a floor clamped to IDLE_FLOOR or below (never zero)', () => {
    // FLOOR_MIN (100) < IDLE_FLOOR (200) — both are "at the minimum".
    expect(computeClampedFloor(200, 1500)).toBe(FLOOR_MIN);
    expect(isEffectivelyInactiveFloor(computeClampedFloor(200, 1500))).toBe(true);
  });
});

describe('shifterConfig — R19 re-derive suggestion', () => {
  it('the hide rule: shift point − 1500 ≤ IDLE_FLOOR (shift point ≤ 1700) hides the suggestion — no clamp, no dead-advice zone', () => {
    expect(isSuggestionVisible(1700)).toBe(false);
    expect(isSuggestionVisible(1701)).toBe(true);
    expect(isSuggestionVisible(200)).toBe(false);
    expect(isSuggestionVisible(9000)).toBe(true);
  });

  it('the suggestion value is shift point minus the default dip', () => {
    expect(suggestedFloor(7000)).toBe(5500);
    expect(suggestedFloor(1701)).toBe(201);
  });
});

describe('shifterConfig — R19 effectively-inactive detection', () => {
  it('flags stored floors at or below IDLE_FLOOR (zero excluded)', () => {
    expect(isEffectivelyInactiveFloor(IDLE_FLOOR)).toBe(true);
    expect(isEffectivelyInactiveFloor(150)).toBe(true);
    expect(isEffectivelyInactiveFloor(FLOOR_MIN)).toBe(true);
    expect(isEffectivelyInactiveFloor(0)).toBe(false, 'zero = floor not set, not the inactive notice');
    expect(isEffectivelyInactiveFloor(5000)).toBe(false);
  });
});

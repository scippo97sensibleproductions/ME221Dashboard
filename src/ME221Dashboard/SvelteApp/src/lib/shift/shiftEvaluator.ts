import { computeRpmToShift } from '../derived/compute';

export interface ShiftRuntimeConfig {
  shiftPointRpm: number;
  downshiftFloorRpm: number;
  rpmEntityId: number | null;
}

export interface ShiftTickResult {
  /** −1 (downshift held), 0 (cruising), +1 (shift up due), or null when inert. */
  state: number | null;
  /** shiftPoint − rpm, clamped at 0; null whenever the machine is inert. */
  countdown: number | null;
}

export interface ShiftEvaluator {
  /**
   * One fixed-rate tick. `config` is the reactive per-dashboard payload (null until the
   * DashboardPage has loaded it — the evaluator never fetches). `stale` is computed by
   * the caller from LiveDataStore (STALE_MS); when stale the evaluator is the sole
   * staleness authority and emits null for BOTH entities in the same tick (R14/R16).
   */
  step(now: number, values: Record<string, number | null>, config: ShiftRuntimeConfig | null, stale: boolean): ShiftTickResult;
  /** Clear internal state (disconnect, calibration reload, dashboard switch). */
  reset(): void;
}

type MachineState = 'idle' | 'approaching' | 'shiftNow' | 'downshift';

const HOLD_MS = 3000;
/** Machine-internal Idle↔Approaching boundary (KTD6 default ramp). Signal-irrelevant:
 *  both emit 0; the ramp band rendering is gauge-local (R18). */
const MACHINE_RAMP = 1500;

/**
 * Fixed-rate evaluator. Pure: no logging, no timers — the caller drives every
 * tick and consumes the returned (state, countdown) pair.
 */
export function createShiftEvaluator(): ShiftEvaluator {
  let machine: MachineState = 'idle';
  let prevRpm: number | null = null;
  let downshiftEnteredAt: number | null = null;
  let shiftUpEnteredAt: number | null = null;

  function baseState(rpm: number, shiftPoint: number): MachineState {
    if (rpm >= shiftPoint) return 'shiftNow';
    if (rpm >= shiftPoint - MACHINE_RAMP) return 'approaching';
    return 'idle';
  }

  function clear(): void {
    machine = 'idle';
    prevRpm = null;
    downshiftEnteredAt = null;
    shiftUpEnteredAt = null;
  }

  function step(
    stepNow: number,
    values: Record<string, number | null>,
    config: ShiftRuntimeConfig | null,
    stale: boolean,
  ): ShiftTickResult {
    if (config === null || stale) {
      clear();
      return { state: null, countdown: null };
    }

    const { shiftPointRpm, downshiftFloorRpm, rpmEntityId } = config;
    if (shiftPointRpm <= 0 || rpmEntityId === null) {
      clear();
      return { state: null, countdown: null };
    }

    const rpm = values[String(rpmEntityId)] ?? null;
    if (rpm === null) {
      clear();
      return { state: null, countdown: null };
    }

    const countdown = computeRpmToShift(rpm, shiftPointRpm);

    // Edge-triggered floor crossing from any state: previous sample ≥ floor,
    // current sample < floor. A lift-off in any state is captured (R4).
    const floorActive = downshiftFloorRpm > 0;
    const crossedDown = floorActive
      && prevRpm !== null
      && prevRpm >= downshiftFloorRpm
      && rpm < downshiftFloorRpm;
    if (crossedDown) {
      machine = 'downshift';
      downshiftEnteredAt = stepNow;
      shiftUpEnteredAt = null;
      prevRpm = rpm;
      return { state: -1, countdown };
    }

    if (machine === 'downshift' && downshiftEnteredAt !== null) {
      const held = rpm <= downshiftFloorRpm && stepNow - downshiftEnteredAt < HOLD_MS;
      if (held) {
        prevRpm = rpm;
        return { state: -1, countdown };
      }
      machine = baseState(rpm, shiftPointRpm);
      downshiftEnteredAt = null;
      shiftUpEnteredAt = machine === 'shiftNow' ? stepNow : null;
    } else if (machine === 'shiftNow' && shiftUpEnteredAt !== null) {
      // Shift-up hold: once RPM reaches the shift point, the 'shiftNow' state
      // persists for HOLD_MS even if RPM dips back below — the ▲ keeps
      // flashing so a brief sub-limit dip (e.g. the emulator upshifting) does
      // not extinguish it a moment after it lit.
      if (stepNow - shiftUpEnteredAt < HOLD_MS) {
        prevRpm = rpm;
        return { state: 1, countdown };
      }
      shiftUpEnteredAt = null;
      machine = baseState(rpm, shiftPointRpm);
    } else {
      machine = baseState(rpm, shiftPointRpm);
      if (machine === 'shiftNow') shiftUpEnteredAt = stepNow;
    }

    prevRpm = rpm;
    return { state: machine === 'shiftNow' ? 1 : 0, countdown };
  }

  return {
    step,
    reset: () => {
      clear();
    },
  };
}

export const shiftEvaluator: ShiftEvaluator = createShiftEvaluator();

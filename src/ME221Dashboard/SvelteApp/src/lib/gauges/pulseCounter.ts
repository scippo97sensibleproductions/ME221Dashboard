import type { WarningEvaluator } from '../stores/warningEvaluator';

export interface PulseCounter {
  attachEvaluator(evaluator: WarningEvaluator): void;
  /** Total pulse count since reset. */
  count(): number;
  /** Pulses since the given baseline (non-destructive delta read). */
  delta(baseline: number): number;
  /** Instance baseline for a gauge mounting now — never re-pulses stale events. */
  mount(): number;
  reset(): void;
}

/**
 * R11: one pulse per toast-firing activation into a flash-enabled level,
 * never for silent re-arm-window activations. Gauges read deltas against a
 * mount-time baseline, so a gauge mounting after an activation (or a
 * dashboard-switch remount) does not re-pulse.
 */
export function createPulseCounter(): PulseCounter {
  let counter = 0;
  let evaluator: WarningEvaluator | null = null;
  let unsubscribe: (() => void) | null = null;

  function isFlashEnabled(dataId: number, levelId: string): boolean {
    return evaluator?.getLevel(dataId, levelId)?.flash ?? false;
  }

  return {
    attachEvaluator(instance: WarningEvaluator): void {
      unsubscribe?.();
      evaluator = instance;
      unsubscribe = instance.subscribe(events => {
        for (const event of events) {
          if (event.type !== 'activation') continue;
          const a = event.activation;
          if (a.viaRecompute) continue;
          if (!a.toastEligible) continue;
          if (!isFlashEnabled(a.dataId, a.levelId)) continue;
          counter++;
        }
      });
    },

    count(): number {
      return counter;
    },

    delta(baseline: number): number {
      return Math.max(0, counter - baseline);
    },

    mount(): number {
      return counter;
    },

    reset(): void {
      counter = 0;
    },
  };
}

export const pulseCounter: PulseCounter = createPulseCounter();

import { SHIFT_STATE_TEXTS } from './shiftLightRender';
import { warningStore } from '../stores/warningStore.svelte';

export interface ShiftLightAnnouncerDeps {
  now: () => number;
  warningActive: () => boolean;
}

export interface ShiftLightAnnouncer {
  /**
   * Push the renderer's state for one gauge (entity pair). Returns the text to
   * announce (or null when nothing should be announced this tick).
   * - Urgent states announce immediately on entry at assertive politeness,
   *   never dropped — only deferred by the minimum interval between
   *   consecutive announcements (parked in a dedicated slot that no other
   *   state can displace; it fires when its own state persists past the
   *   interval or is dropped once the state leaves).
   * - Calm states use a full entry debounce at polite politeness (guarding
   *   cold ↔ approaching ramp-edge oscillation).
   * - State re-emissions are never re-announced while the state persists; a
   *   state that leaves and re-enters is announced again (the dedupe slot is
   *   released on exit).
   * - All announcements are suppressed while a Warning Centre alert is live
   *   (mirrors the visual border-pulse precedence, R12).
   */
  push(entityPair: string, state: string, urgent: boolean, at?: number): string | null;
  reset(): void;
}

/** Minimum gap between consecutive announcements (any priority). */
export const MIN_INTERVAL_MS = 500;
/** Entry debounce for calm states. */
export const CALM_DEBOUNCE_MS = 500;

const URGENT_STATES: ReadonlySet<string> = new Set([SHIFT_STATE_TEXTS.shiftNow, SHIFT_STATE_TEXTS.downshift]);

export function isUrgentState(state: string): boolean {
  return URGENT_STATES.has(state);
}

export function createShiftLightAnnouncer(deps: ShiftLightAnnouncerDeps): ShiftLightAnnouncer {
  const announced = new Set<string>();
  const lastStateByPair = new Map<string, string>();
  let lastAnnouncedAt = -Infinity;
  let calmCandidate: { key: string; state: string; since: number } | null = null;
  let pendingUrgent: { key: string; state: string; since: number } | null = null;

  function push(entityPair: string, state: string, urgent: boolean, at?: number): string | null {
    const t = at ?? deps.now();
    if (deps.warningActive()) return null;

    const key = `${entityPair}:${state}`;

    // State-exit detection: when the pair left its previous state, release the
    // dedupe slot and any parked announcement — a re-entry must re-announce,
    // and a pending urgent whose state has gone must never fire late.
    const prevState = lastStateByPair.get(entityPair);
    lastStateByPair.set(entityPair, state);
    if (prevState !== undefined && prevState !== state) {
      announced.delete(`${entityPair}:${prevState}`);
      if (pendingUrgent !== null && pendingUrgent.key === `${entityPair}:${prevState}`) {
        pendingUrgent = null;
      }
    }

    if (announced.has(key)) return null;

    if (urgent) {
      calmCandidate = null;
      const deferUntil = lastAnnouncedAt + MIN_INTERVAL_MS;
      if (t < deferUntil) {
        // Deferred, but never dropped: parked in a dedicated slot that only
        // its own state can fire or release (an interleaved calm state must
        // not displace the pending urgent announcement).
        pendingUrgent = { key, state, since: deferUntil };
        return null;
      }
      pendingUrgent = null;
      announced.add(key);
      lastAnnouncedAt = t;
      return state;
    }

    // Calm: entry debounce — announce only if the state persists.
    if (calmCandidate !== null && calmCandidate.key !== key) {
      calmCandidate = null;
    }
    if (calmCandidate === null) {
      calmCandidate = { key, state, since: t };
      return null;
    }
    if (t - calmCandidate.since < CALM_DEBOUNCE_MS) return null;
    calmCandidate = null;
    announced.add(key);
    lastAnnouncedAt = t;
    return state;
  }

  return {
    push,
    reset: () => {
      announced.clear();
      lastStateByPair.clear();
      calmCandidate = null;
      pendingUrgent = null;
      lastAnnouncedAt = -Infinity;
    },
  };
}

// ─── Singleton shared across gauge instances ───────────────────────────────
// A single coordinator so N shift-light gauges never double-announce the same
// state (the dedupe key is the entity pair + state, shared across instances).
export const shiftLightAnnouncer: ShiftLightAnnouncer = createShiftLightAnnouncer({
  now: () => performance.now(),
  warningActive: () => warningStore.activeWarningCount > 0,
});

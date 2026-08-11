import { describe, it, expect } from 'vitest';
import { createShiftLightAnnouncer, MIN_INTERVAL_MS, type ShiftLightAnnouncer } from '../shiftLightAnnouncer';
import { SHIFT_STATE_TEXTS } from '../shiftLightRender';

const PAIR = '-3005:-3006';

interface Clock {
  t: number;
  warningActive: boolean;
}

function make(clock: Clock): ShiftLightAnnouncer {
  return createShiftLightAnnouncer({
    now: () => clock.t,
    warningActive: () => clock.warningActive,
  });
}

describe('shiftLightAnnouncer (U8 — manual clock)', () => {
  it('state-change-only announcements: tick re-emissions never re-announce', () => {
    const clock: Clock = { t: 0, warningActive: false };
    const a = make(clock);
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.shiftNow, true)).toBe(SHIFT_STATE_TEXTS.shiftNow);
    // Re-emission of the same state on every tick → nothing.
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.shiftNow, true)).toBeNull();
    clock.t += 1000;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.shiftNow, true)).toBeNull();
  });

  it('urgent states announce immediately on entry; consecutive urgent announcements defer by the minimum interval', () => {
    const clock: Clock = { t: 0, warningActive: false };
    const a = make(clock);
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.shiftNow, true)).toBe(SHIFT_STATE_TEXTS.shiftNow);
    // New state within the interval → deferred (never dropped), nothing yet.
    clock.t += 100;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.downshift, true)).toBeNull();
    // After the interval passes, the deferred urgent fires.
    clock.t = MIN_INTERVAL_MS + 1;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.downshift, true)).toBe(SHIFT_STATE_TEXTS.downshift);
  });

  it('calm states use the full entry debounce at polite politeness', () => {
    const clock: Clock = { t: 0, warningActive: false };
    const a = make(clock);
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.approaching, false)).toBeNull();
    clock.t += 400;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.approaching, false)).toBeNull();
    clock.t += 200;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.approaching, false)).toBe(SHIFT_STATE_TEXTS.approaching);
  });

  it('calm debounce guards cold ↔ approaching ramp-edge oscillation', () => {
    const clock: Clock = { t: 0, warningActive: false };
    const a = make(clock);
    a.push(PAIR, SHIFT_STATE_TEXTS.belowRange, false);
    clock.t += 200;
    // Oscillates back before the debounce elapses → nothing announced.
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.approaching, false)).toBeNull();
    clock.t += 200;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.belowRange, false)).toBeNull();
    // Stays long enough → announces.
    clock.t += 600;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.belowRange, false)).toBe(SHIFT_STATE_TEXTS.belowRange);
  });

  it('suppresses all announcements while a Warning Centre alert is live', () => {
    const clock: Clock = { t: 0, warningActive: true };
    const a = make(clock);
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.shiftNow, true)).toBeNull();
    clock.t += 1000;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.downshift, true)).toBeNull();
  });

  it('reset clears the announced set (state re-announces after reset)', () => {
    const clock: Clock = { t: 0, warningActive: false };
    const a = make(clock);
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.shiftNow, true)).toBe(SHIFT_STATE_TEXTS.shiftNow);
    a.reset();
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.shiftNow, true)).toBe(SHIFT_STATE_TEXTS.shiftNow);
  });

  it('a deferred urgent is not displaced by an interleaved calm state, and fires when its own state persists', () => {
    const clock: Clock = { t: 0, warningActive: false };
    const a = make(clock);
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.shiftNow, true)).toBe(SHIFT_STATE_TEXTS.shiftNow);
    // New urgent within the interval → parked as pending (never dropped).
    clock.t += 100;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.downshift, true)).toBeNull();
    // An interleaved calm state must not displace the parked urgent…
    clock.t += 100;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.approaching, false)).toBeNull();
    // …and the urgent fires once the interval passes (same state persists).
    clock.t = MIN_INTERVAL_MS + 1;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.downshift, true)).toBe(SHIFT_STATE_TEXTS.downshift);
  });

  it('a parked urgent fires exactly once when the interval passes (no double-fire on repeated pushes)', () => {
    const clock: Clock = { t: 0, warningActive: false };
    const a = make(clock);
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.shiftNow, true)).toBe(SHIFT_STATE_TEXTS.shiftNow);
    // The downshift parks while the interval is still open — repeated pushes of
    // the persisting state must not fire early.
    clock.t += 100;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.downshift, true)).toBeNull();
    clock.t += 100;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.downshift, true)).toBeNull();
    clock.t += 100;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.downshift, true)).toBeNull();
    // Once the interval passes, the parked urgent fires exactly once…
    clock.t = MIN_INTERVAL_MS + 1;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.downshift, true)).toBe(SHIFT_STATE_TEXTS.downshift);
    // …and never again for the persisting state.
    clock.t += 100;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.downshift, true)).toBeNull();
  });

  it('a state that exits and re-enters is announced again (dedupe slot released on exit)', () => {
    const clock: Clock = { t: 0, warningActive: false };
    const a = make(clock);
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.shiftNow, true)).toBe(SHIFT_STATE_TEXTS.shiftNow);
    // State leaves (inert frame with no announcement)…
    clock.t += 1000;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.approaching, false)).toBeNull();
    // …and re-enters — the dedupe slot was released, so it announces again.
    clock.t += 1000;
    expect(a.push(PAIR, SHIFT_STATE_TEXTS.shiftNow, true)).toBe(SHIFT_STATE_TEXTS.shiftNow);
  });
});

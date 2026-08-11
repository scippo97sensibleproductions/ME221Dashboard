import { describe, it, expect } from 'vitest';
import { createShiftEvaluator, type ShiftRuntimeConfig, type ShiftEvaluator } from '../shiftEvaluator';

interface Tick {
  now: number;
  values: Record<string, number | null>;
  config: ShiftRuntimeConfig | null;
  stale?: boolean;
}

const RPM = 940;

function makeConfig(overrides: Partial<ShiftRuntimeConfig> = {}): ShiftRuntimeConfig {
  return {
    shiftPointRpm: 7000,
    downshiftFloorRpm: 5000,
    rpmEntityId: RPM,
    ...overrides,
  };
}

function drive(evaluator: ShiftEvaluator, ticks: Tick[]) {
  return ticks.map(t => evaluator.step(t.now, t.values, t.config, t.stale ?? false));
}

describe('shiftEvaluator — state machine', () => {
  it('cruising below the ramp → state 0', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    const [r] = drive(ev, [{ now: 0, values: { [RPM]: 3000 }, config: cfg }]);
    expect(r.state).toBe(0);
    expect(r.countdown).toBe(4000);
  });

  it('floor crossing from Idle → −1 (edge-triggered, previous ≥ floor, current < floor)', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    drive(ev, [{ now: 0, values: { [RPM]: 6000 }, config: cfg }]);
    const [r] = drive(ev, [{ now: 100, values: { [RPM]: 4500 }, config: cfg }]);
    expect(r.state).toBe(-1);
  });

  it('floor crossing from Approaching → −1', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    drive(ev, [{ now: 0, values: { [RPM]: 6500 }, config: cfg }]);
    const [r] = drive(ev, [{ now: 100, values: { [RPM]: 4500 }, config: cfg }]);
    expect(r.state).toBe(-1);
  });

  it('floor crossing from ShiftNow → −1', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    drive(ev, [{ now: 0, values: { [RPM]: 7200 }, config: cfg }]);
    const [r] = drive(ev, [{ now: 100, values: { [RPM]: 4500 }, config: cfg }]);
    expect(r.state).toBe(-1);
  });

  it('hold: −1 persists at constant RPM below the floor', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    drive(ev, [
      { now: 0, values: { [RPM]: 6000 }, config: cfg },
      { now: 100, values: { [RPM]: 4000 }, config: cfg },
    ]);
    const [r] = drive(ev, [{ now: 1000, values: { [RPM]: 4000 }, config: cfg }]);
    expect(r.state).toBe(-1);
  });

  it('hold clears on step past the 3 s deadline (AE3)', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    drive(ev, [
      { now: 0, values: { [RPM]: 6000 }, config: cfg },
      { now: 100, values: { [RPM]: 4000 }, config: cfg },
    ]);
    const [r] = drive(ev, [{ now: 3300, values: { [RPM]: 4000 }, config: cfg }]);
    expect(r.state).toBe(0);
  });

  it('recovery: RPM above floor → 0', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    drive(ev, [
      { now: 0, values: { [RPM]: 6000 }, config: cfg },
      { now: 100, values: { [RPM]: 4000 }, config: cfg },
    ]);
    const [r] = drive(ev, [{ now: 500, values: { [RPM]: 5500 }, config: cfg }]);
    expect(r.state).toBe(0);
  });

  it('idle below floor without a crossing → stays 0 (AE4)', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    const [r] = drive(ev, [{ now: 0, values: { [RPM]: 900 }, config: cfg }]);
    expect(r.state).toBe(0);
  });

  it('at/above the shift point → +1', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    const [r] = drive(ev, [{ now: 0, values: { [RPM]: 7000 }, config: cfg }]);
    expect(r.state).toBe(1);
  });

  it('resume above the shift point → +1 on the first tick', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    drive(ev, [{ now: 0, values: { [RPM]: 3000 }, config: cfg }]);
    const [r] = drive(ev, [{ now: 100, values: { [RPM]: 7500 }, config: cfg }]);
    expect(r.state).toBe(1);
  });

  it('shift-up hold: +1 persists through a brief dip below the shift point', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    drive(ev, [{ now: 0, values: { [RPM]: 7500 }, config: cfg }]);
    // RPM dips back below the limit (e.g. the emulator upshifts) — the ▲ must
    // keep flashing through the hold window, not extinguish on the first dip.
    const [hold1] = drive(ev, [{ now: 500, values: { [RPM]: 5500 }, config: cfg }]);
    expect(hold1.state).toBe(1);
    const [hold2] = drive(ev, [{ now: 2000, values: { [RPM]: 5500 }, config: cfg }]);
    expect(hold2.state).toBe(1);
  });

  it('shift-up hold clears after the 3 s window when RPM stays below the limit', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    drive(ev, [{ now: 0, values: { [RPM]: 7500 }, config: cfg }]);
    drive(ev, [{ now: 500, values: { [RPM]: 5500 }, config: cfg }]);
    const [r] = drive(ev, [{ now: 3600, values: { [RPM]: 5500 }, config: cfg }]);
    expect(r.state).toBe(0);
  });

  it('shift-up hold re-arms when RPM crosses the limit again after expiry', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    drive(ev, [{ now: 0, values: { [RPM]: 7500 }, config: cfg }]);
    drive(ev, [{ now: 3600, values: { [RPM]: 5500 }, config: cfg }]); // hold expired → 0
    const [r] = drive(ev, [{ now: 3700, values: { [RPM]: 7200 }, config: cfg }]);
    expect(r.state).toBe(1);
  });

  it('unset floor → −1 never fires (AE10)', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig({ downshiftFloorRpm: 0 });
    drive(ev, [{ now: 0, values: { [RPM]: 6000 }, config: cfg }]);
    const [r] = drive(ev, [{ now: 100, values: { [RPM]: 4000 }, config: cfg }]);
    expect(r.state).toBe(0);
  });

  it('null RPM sample → null for both and resets to Idle (R16)', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    drive(ev, [
      { now: 0, values: { [RPM]: 6000 }, config: cfg },
      { now: 100, values: { [RPM]: 4000 }, config: cfg }, // −1 held
    ]);
    const [rNull] = drive(ev, [{ now: 200, values: { [RPM]: null }, config: cfg }]);
    expect(rNull.state).toBeNull();
    expect(rNull.countdown).toBeNull();
    // re-entry: fresh sample re-evaluates from Idle, no stale hold
    const [rRe] = drive(ev, [{ now: 300, values: { [RPM]: 4000 }, config: cfg }]);
    expect(rRe.state).toBe(0);
  });

  it('staleness → null for both in the same tick (AE7)', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    drive(ev, [
      { now: 0, values: { [RPM]: 7200 }, config: cfg }, // +1
    ]);
    const [r] = drive(ev, [{ now: 100, values: { [RPM]: 7200 }, config: cfg, stale: true }]);
    expect(r.state).toBeNull();
    expect(r.countdown).toBeNull();
    // resume: re-evaluates from Idle
    const [r2] = drive(ev, [{ now: 200, values: { [RPM]: 7200 }, config: cfg }]);
    expect(r2.state).toBe(1);
  });

  it('no config payload → null for both, no bridge call implied', () => {
    const ev = createShiftEvaluator();
    const [r] = drive(ev, [{ now: 0, values: { [RPM]: 6000 }, config: null }]);
    expect(r.state).toBeNull();
    expect(r.countdown).toBeNull();
  });

  it('unset shift point → countdown null and machine inert (AE9)', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig({ shiftPointRpm: 0 });
    const [r] = drive(ev, [{ now: 0, values: { [RPM]: 6000 }, config: cfg }]);
    expect(r.state).toBeNull();
    expect(r.countdown).toBeNull();
  });

  it('no RPM datalink configured → null for both (R14, AE5)', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig({ rpmEntityId: null });
    const [r] = drive(ev, [{ now: 0, values: { [RPM]: 6000 }, config: cfg }]);
    expect(r.state).toBeNull();
    expect(r.countdown).toBeNull();
  });
});

describe('shiftEvaluator — countdown', () => {
  it('countdown = shiftPoint − rpm, clamped at 0', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    const [below] = drive(ev, [{ now: 0, values: { [RPM]: 6000 }, config: cfg }]);
    expect(below.countdown).toBe(1000);
    const [above] = drive(ev, [{ now: 100, values: { [RPM]: 7500 }, config: cfg }]);
    expect(above.countdown).toBe(0);
  });

  it('live shift-point edit recomputes on the next tick', () => {
    const ev = createShiftEvaluator();
    drive(ev, [{ now: 0, values: { [RPM]: 6000 }, config: makeConfig({ shiftPointRpm: 7000 }) }]);
    const [r] = drive(ev, [{ now: 100, values: { [RPM]: 6000 }, config: makeConfig({ shiftPointRpm: 6500 }) }]);
    expect(r.countdown).toBe(500);
  });

  it('reset clears the hold so re-entry is edge-triggered afresh', () => {
    const ev = createShiftEvaluator();
    const cfg = makeConfig();
    drive(ev, [
      { now: 0, values: { [RPM]: 6000 }, config: cfg },
      { now: 100, values: { [RPM]: 4000 }, config: cfg }, // hold
    ]);
    ev.reset();
    const [r] = drive(ev, [{ now: 200, values: { [RPM]: 4000 }, config: cfg }]);
    expect(r.state).toBe(0, 'after reset the floor crossing must re-fire to hold');
  });
});

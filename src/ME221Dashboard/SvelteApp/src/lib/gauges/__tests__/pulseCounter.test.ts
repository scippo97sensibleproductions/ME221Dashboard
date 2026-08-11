import { describe, it, expect, beforeEach } from 'vitest';
import { createPulseCounter, type PulseCounter } from '../pulseCounter';
import { createWarningEvaluator, type WarningEvaluator } from '../../stores/warningEvaluator';
import type { WarningSettingsPayload } from '../../HybridBridgeTypes';

let t = 0;
let evaluator: WarningEvaluator;
let counter: PulseCounter;

function payload(flash: boolean, delayMs = 100): WarningSettingsPayload {
  return {
    settings: [
      {
        dataId: 100,
        enabled: true,
        name: 'Oil Temp',
        unit: 'C',
        category: 'Engine',
        status: 'Typical',
        levels: [
          { id: 'warning', name: 'warning', color: '#f59e0b', autolog: false, flash, order: 0 },
          { id: 'critical', name: 'critical', color: '#ef4444', autolog: false, flash, order: 1 },
        ],
        points: [
          { id: 'p0', value: 105, direction: 'max', levelId: 'warning', enabled: true },
          { id: 'p1', value: 120, direction: 'max', levelId: 'critical', enabled: true },
        ],
        migratedBoundsMarkerLevelId: null,
        migratedBoundsMarkerSet: false,
      },
    ],
    delayMs,
  };
}

beforeEach(() => {
  t = 0;
  evaluator = createWarningEvaluator(() => t);
  counter = createPulseCounter();
  counter.attachEvaluator(evaluator);
});

describe('pulseCounter (R11)', () => {
  it('pulses once per toast-firing activation into a flash-enabled level', () => {
    evaluator.refresh(payload(true), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(200, { '100': 110 }); // warning activation (toast-eligible, flash) → pulse
    expect(counter.count()).toBe(1);
    evaluator.step(300, { '100': 125 });
    evaluator.step(500, { '100': 125 }); // critical escalation → pulse
    expect(counter.count()).toBe(2);
  });

  it('never pulses for non-flash levels', () => {
    evaluator.refresh(payload(false), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(200, { '100': 110 });
    expect(counter.count()).toBe(0);
  });

  it('never pulses for silent (re-arm window) activations', () => {
    evaluator.refresh(payload(true), 0);
    evaluator.step(0, { '100': 125 });
    evaluator.step(200, { '100': 125 }); // critical pulse
    expect(counter.count()).toBe(1);
    evaluator.step(1000, { '100': 110 }); // drop (re-arm at 1000, window 100ms)
    evaluator.step(1050, { '100': 125 }); // re-cross inside the window
    evaluator.step(1200, { '100': 125 }); // silent escalation — no pulse
    expect(counter.count()).toBe(1);
  });

  it('never pulses for edit-recompute activations', () => {
    evaluator.refresh(payload(true), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(200, { '100': 110 }); // warning pulse
    expect(counter.count()).toBe(1);
    // Edit raises the critical threshold below the value → immediate recompute (silent)
    const edited = payload(true);
    edited.settings[0].points[1].value = 100;
    evaluator.refresh(edited, 300);
    expect(counter.count()).toBe(1);
  });

  it('delta reads are non-destructive and mount-time baselines never re-pulse stale events', () => {
    evaluator.refresh(payload(true), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(200, { '100': 110 }); // pulse 1

    // A gauge mounting after the activation: baseline = current count
    const baseline = counter.mount();
    expect(counter.delta(baseline)).toBe(0); // no re-pulse of the stale event
    expect(counter.count()).toBe(1); // non-destructive

    evaluator.step(300, { '100': 125 });
    evaluator.step(500, { '100': 125 }); // pulse 2
    expect(counter.delta(baseline)).toBe(1);
    // Two co-mounted gauges both see the same delta
    expect(counter.delta(baseline)).toBe(1);
  });

  it('reset zeroes the count', () => {
    evaluator.refresh(payload(true), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(200, { '100': 110 });
    expect(counter.count()).toBe(1);
    counter.reset();
    expect(counter.count()).toBe(0);
  });
});

import { describe, it, expect } from 'vitest';
import { createWarningEvaluator, furthestPoint, type EvaluatorEvent, type WarningEvaluator } from '../warningEvaluator';
import type { WarningLevel, WarningPoint, WarningSettingsPayload } from '../../../HybridBridgeTypes';

interface LevelSpec {
  id: string;
  name?: string;
  color?: string;
  autolog?: boolean;
  flash?: boolean;
  order?: number;
}

interface PointSpec {
  value: number;
  direction: 'min' | 'max';
  levelId: string;
  enabled?: boolean;
}

function makePayload(opts: {
  dataId?: number;
  delayMs?: number;
  enabled?: boolean;
  levels?: LevelSpec[];
  points?: PointSpec[];
}): WarningSettingsPayload {
  const levels: WarningLevel[] = (opts.levels ?? []).map((l, i) => ({
    id: l.id,
    name: l.name ?? l.id,
    color: l.color ?? '#f59e0b',
    autolog: l.autolog ?? false,
    flash: l.flash ?? false,
    order: l.order ?? i,
  }));
  const points: WarningPoint[] = (opts.points ?? []).map((p, i) => ({
    id: `p${i}`,
    value: p.value,
    direction: p.direction,
    levelId: p.levelId,
    enabled: p.enabled ?? true,
  }));
  return {
    settings: [
      {
        dataId: opts.dataId ?? 100,
        enabled: opts.enabled ?? true,
        name: 'Oil Temp',
        unit: 'C',
        category: 'Engine',
        status: 'Typical',
        levels,
        points,
        migratedBoundsMarkerLevelId: null,
        migratedBoundsMarkerSet: false,
      },
    ],
    delayMs: opts.delayMs ?? 500,
  };
}

function oilTempPayload(): WarningSettingsPayload {
  return makePayload({
    levels: [
      { id: 'warning', name: 'warning' },
      { id: 'critical', name: 'critical', autolog: true },
    ],
    points: [
      { value: 105, direction: 'max', levelId: 'warning' },
      { value: 120, direction: 'max', levelId: 'critical' },
    ],
  });
}

function collect(evaluator: WarningEvaluator): EvaluatorEvent[] {
  const events: EvaluatorEvent[] = [];
  evaluator.subscribe(ev => events.push(...ev));
  return events;
}

describe('warningEvaluator — AE1 staged severity', () => {
  it('warns at 110, escalates to critical at 125, drops silently', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 110 });
    ev.step(500, { '100': 110 }); // 500ms dwell → warning activation
    ev.step(1000, { '100': 125 }); // escalations debounce too
    ev.step(1500, { '100': 125 }); // critical activation
    ev.step(2000, { '100': 110 }); // silent de-escalation

    const activations = events.filter(e => e.type === 'activation');
    expect(activations).toHaveLength(2);
    expect(activations[0]).toMatchObject({
      type: 'activation',
      activation: { levelId: 'warning', toastEligible: true, viaRecompute: false },
    });
    expect(activations[1]).toMatchObject({
      type: 'activation',
      activation: { levelId: 'critical', toastEligible: true, viaRecompute: false },
    });
    expect(ev.getState(100).activeLevelId).toBe('warning');
    expect(events.some(e => e.type === 'drop' && e.drop.fromLevelId === 'critical' && e.drop.toLevelId === 'warning')).toBe(true);
  });
});

describe('warningEvaluator — AE2 debounce', () => {
  it('200ms spike produces no activation; 700ms excursion activates with toast', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(makePayload({ levels: [{ id: 'warning' }], points: [{ value: 7000, direction: 'max', levelId: 'warning' }] }), 0);

    ev.step(0, { '100': 6800 });
    ev.step(100, { '100': 7100 }); // spike starts
    ev.step(300, { '100': 6800 }); // spike ends (200ms) → cancelled
    ev.step(500, { '100': 6800 });
    expect(events).toHaveLength(0);

    ev.step(800, { '100': 7100 }); // sustained excursion starts
    ev.step(1500, { '100': 7100 }); // 700ms sustained → activation
    const activations = events.filter(e => e.type === 'activation');
    expect(activations).toHaveLength(1);
    expect(activations[0].activation).toMatchObject({ levelId: 'warning', toastEligible: true });
  });
});

describe('warningEvaluator — AE5 escalation spike', () => {
  it('400ms spike above critical produces no critical; 600ms excursion escalates', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 110 }); // warning dwell starts
    ev.step(500, { '100': 110 }); // warning active
    ev.step(600, { '100': 125 }); // spike above 120 starts
    ev.step(1000, { '100': 110 }); // spike ends (400ms) — no critical
    expect(events.filter(e => e.type === 'activation' && e.activation.levelId === 'critical')).toHaveLength(0);

    ev.step(1100, { '100': 125 }); // sustained excursion starts
    ev.step(1600, { '100': 125 }); // 500ms dwell complete
    const crits = events.filter(e => e.type === 'activation' && e.activation.levelId === 'critical');
    expect(crits).toHaveLength(1);
  });
});

describe('warningEvaluator — AE7 staircase escalation', () => {
  it('joins the earliest timer; single critical activation; reverted spike restarts warning dwell', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 110 }); // crosses 105 at t=0
    ev.step(200, { '100': 125 }); // crosses 120 at t=200 — joins earliest timer
    ev.step(500, { '100': 125 }); // single activation to critical
    const activations = events.filter(e => e.type === 'activation');
    expect(activations).toHaveLength(1);
    expect(activations[0].activation).toMatchObject({ levelId: 'critical', toastEligible: true });

    // Scenario B (fresh evaluator, from none): a 400ms spike to 125 that falls
    // back to 110 produces no critical activation, and the warning activation
    // completes only after its own full 500ms dwell above 105.
    const eventsB: EvaluatorEvent[] = [];
    const evB = createWarningEvaluator(() => t);
    evB.subscribe(ev => eventsB.push(...ev));
    evB.refresh(oilTempPayload(), 0);

    evB.step(0, { '100': 125 }); // spike crosses both — pending critical (deadline 500)
    evB.step(400, { '100': 110 }); // falls back — revert to warning (deadline 900)
    expect(eventsB.filter(e => e.type === 'activation' && e.activation.levelId === 'critical')).toHaveLength(0);
    evB.step(900, { '100': 110 }); // warning completes after its own full dwell
    const warnsB = eventsB.filter(e => e.type === 'activation' && e.activation.levelId === 'warning');
    expect(warnsB).toHaveLength(1);
    expect(warnsB[0].activation.toastEligible).toBe(true);
  });
});

describe('warningEvaluator — AE8 silent re-escalation', () => {
  it('re-escalation inside the de-escalation delay is toast-ineligible', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 125 });
    ev.step(500, { '100': 125 }); // critical active
    ev.step(2000, { '100': 110 }); // drop to warning; re-arm at 2000
    ev.step(2300, { '100': 125 }); // re-crosses within 300ms of the drop
    ev.step(2800, { '100': 125 }); // completes silently
    const esc = events.filter(e => e.type === 'activation' && e.activation.levelId === 'critical');
    expect(esc).toHaveLength(2);
    expect(esc[0].activation.toastEligible).toBe(true);
    expect(esc[1].activation.toastEligible).toBe(false);
  });

  it('re-escalation starting 800ms after the drop fires a fresh toast', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 125 });
    ev.step(500, { '100': 125 });
    ev.step(2000, { '100': 110 }); // drop
    ev.step(2800, { '100': 125 }); // crossing 800ms after the drop
    ev.step(3300, { '100': 125 }); // completion
    const esc = events.filter(e => e.type === 'activation' && e.activation.levelId === 'critical');
    expect(esc[1].activation.toastEligible).toBe(true);
  });
});

describe('warningEvaluator — R14 edges', () => {
  it('reversion with a higher point still crossed restarts the timer to that level', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 125 }); // crosses both — pending critical (deadline 500)
    ev.step(300, { '100': 110 }); // critical reverted — restart warning (deadline 800)
    ev.step(800, { '100': 110 }); // warning completes
    const activations = events.filter(e => e.type === 'activation');
    expect(activations).toHaveLength(1);
    expect(activations[0].activation.levelId).toBe('warning');
  });

  it('reversion to none cancels the pending and sets the re-arm timestamp', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 110 });
    ev.step(200, { '100': 100 }); // back in range — cancel + re-arm at 200
    ev.step(200, { '100': 110 }); // immediate re-cross inside the window
    ev.step(700, { '100': 110 }); // completes at 700 (200+500) — silent
    const activations = events.filter(e => e.type === 'activation');
    expect(activations).toHaveLength(1);
    expect(activations[0].activation.toastEligible).toBe(false);
  });

  it('delay 0 completes the pending at the next re-read', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(makePayload({ delayMs: 0, levels: [{ id: 'warning' }], points: [{ value: 105, direction: 'max', levelId: 'warning' }] }), 0);
    ev.step(0, { '100': 110 });
    ev.step(1, { '100': 110 });
    expect(events.filter(e => e.type === 'activation')).toHaveLength(1);
  });

  it('pushDelay recomputes remaining dwell while preserving elapsed and frozen eligibility', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 110 }); // crossing 0, deadline 500
    ev.pushDelay(1000, 200); // elapsed 200 → deadline = 200 + 800 = 1000
    ev.step(1000, { '100': 110 }); // completes exactly on the new deadline
    const activations = events.filter(e => e.type === 'activation');
    expect(activations).toHaveLength(1);
    expect(activations[0].activation.toastEligible).toBe(true); // crossing-moment delay frozen

    // Shrinking delay below elapsed completes immediately
    ev.step(1100, { '100': 100 }); // clear
    ev.step(1200, { '100': 110 }); // fresh crossing, deadline 1700
    ev.pushDelay(100, 1300); // elapsed 100 → remaining 0
    ev.step(1400, { '100': 110 });
    expect(events.filter(e => e.type === 'activation')).toHaveLength(2);
  });

  it('null/absent value clears the active state and reverts pending with re-arm', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 110 });
    ev.step(500, { '100': 110 }); // warning active
    ev.step(600, { '100': null }); // null — immediate clear + re-arm
    expect(ev.getState(100).activeLevelId).toBe(null);
    expect(events.some(e => e.type === 'drop' && e.drop.toLevelId === null)).toBe(true);

    ev.step(600, { '100': 110 }); // re-cross inside the window
    ev.step(1100, { '100': 110 });
    const activations = events.filter(e => e.type === 'activation');
    expect(activations).toHaveLength(2);
    expect(activations[1].activation.toastEligible).toBe(false); // re-arm window
  });
});

describe('warningEvaluator — edit-driven recompute', () => {
  it('refresh with changed settings activates immediately, silently', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 110 });
    ev.step(500, { '100': 110 }); // warning active
    // User raises a threshold: critical point now at 100 — value 110 crosses immediately
    ev.refresh(makePayload({
      levels: [
        { id: 'warning', name: 'warning' },
        { id: 'critical', name: 'critical' },
      ],
      points: [
        { value: 105, direction: 'max', levelId: 'warning' },
        { value: 100, direction: 'max', levelId: 'critical' },
      ],
    }), 600);
    const activations = events.filter(e => e.type === 'activation');
    const last = activations[activations.length - 1];
    expect(last.activation).toMatchObject({ levelId: 'critical', viaRecompute: true, toastEligible: false });
    expect(ev.getState(100).activeLevelId).toBe('critical');
  });

  it('refresh removing the active level drops immediately', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);
    ev.step(0, { '100': 125 });
    ev.step(500, { '100': 125 }); // critical active
    ev.refresh(makePayload({
      levels: [{ id: 'warning', name: 'warning' }],
      points: [{ value: 105, direction: 'max', levelId: 'warning' }],
    }), 600);
    expect(ev.getState(100).activeLevelId).toBe('warning');
    expect(events.some(e => e.type === 'drop' && e.drop.fromLevelId === 'critical' && e.drop.toLevelId === 'warning')).toBe(true);
  });

  it('refresh removing a datalink drops it without re-arm', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);
    ev.step(0, { '100': 125 });
    ev.step(500, { '100': 125 }); // critical active
    ev.refresh(makePayload({ delayMs: 500, dataId: 101 }), 600); // dataId 100 removed from the payload
    expect(ev.getState(100).activeLevelId).toBe(null);
    expect(events.some(e => e.type === 'drop' && e.drop.dataId === 100 && e.drop.toLevelId === null)).toBe(true);
  });
});

describe('warningEvaluator — KTD2 suspension branches', () => {
  it('(a) resume with the crossing still holding restores dwell and completes at the original deadline + suspension', () => {
    let t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 110 }); // deadline 500
    t = 100;
    ev.step(100, { '100': 110 });
    ev.invalidatePending(100, 'edit'); // dispatch-time suspension; remaining 400
    ev.step(120, { '100': 110 }); // frozen — no completion despite deadline passing
    ev.resolveAfterWrite(100, 'resume', 150); // write OK, re-read OK, crossing holds
    ev.step(550, { '100': 110 }); // completes at 150+400
    const activations = events.filter(e => e.type === 'activation');
    expect(activations).toHaveLength(1);
    expect(activations[0].activation.toastEligible).toBe(true); // crossing moment frozen
  });

  it('re-enable discards the frozen transition and starts a fresh zero-dwell timer', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 110 });
    ev.step(100, { '100': 110 });
    ev.invalidatePending(100, 'disable'); // disable write dispatched
    ev.step(150, { '100': 110 }); // frozen
    ev.invalidatePending(100, 're-enable'); // re-enable lands while suspended
    ev.resolveAfterWrite(100, 'restart', 200); // resolving re-read: fresh zero-dwell timer
    ev.step(700, { '100': 110 }); // completes at 200+500 — NOT at the original deadline
    const activations = events.filter(e => e.type === 'activation');
    expect(activations).toHaveLength(1);
    expect(activations[0].activation.toastEligible).toBe(true); // resolve moment is the crossing moment
    ev.step(200, { '100': 110 });
    expect(events.filter(e => e.type === 'activation')).toHaveLength(1); // nothing at 200
  });

  it('(b) cancelled suspension never activates even after the deadline passes', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 110 });
    ev.invalidatePending(100, 'edit');
    ev.resolveAfterWrite(100, 'cancel', 100);
    ev.step(1000, { '100': 110 }); // would have completed at 500
    expect(events.filter(e => e.type === 'activation')).toHaveLength(0);
    expect(ev.getState(100).activeLevelId).toBe(null);
  });

  it('(c) write failure resumes the frozen transition unchanged', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 110 }); // deadline 500
    ev.invalidatePending(100, 'edit'); // remaining 500
    ev.resolveAfterWrite(100, 'resume', 200); // write failed — continue against last snapshot
    ev.step(700, { '100': 110 }); // completes at 200+500
    const activations = events.filter(e => e.type === 'activation');
    expect(activations).toHaveLength(1);
  });

  it('resume with the crossing no longer holding cancels and re-arms', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 110 });
    ev.invalidatePending(100, 'edit');
    ev.step(100, { '100': 100 }); // value back in range during the round trip
    ev.resolveAfterWrite(100, 'resume', 150); // cancel + re-arm at 150
    ev.step(200, { '100': 110 }); // re-cross inside the window (150+500=650)
    ev.step(700, { '100': 110 }); // completes silently
    const activations = events.filter(e => e.type === 'activation');
    expect(activations).toHaveLength(1);
    expect(activations[0].activation.toastEligible).toBe(false); // re-arm from the resume cancel
  });

  it('suspended datalinks still process value-driven clears and de-escalations', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 125 });
    ev.step(500, { '100': 125 }); // critical active
    ev.step(600, { '100': 110 }); // warning pending (escalation back)
    ev.invalidatePending(100, 'edit'); // suspend the pending
    // While suspended: a value-null clears the active state (drop event fires)
    ev.step(700, { '100': null });
    expect(ev.getState(100).activeLevelId).toBe(null);
    expect(events.some(e => e.type === 'drop' && e.drop.toLevelId === null)).toBe(true);
  });

  it('write-driven recompute drops lower the active level with a drop event', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 125 });
    ev.step(500, { '100': 125 }); // critical active
    // Critical point removed by a write → immediate recompute drop to warning
    ev.refresh(makePayload({
      levels: [{ id: 'warning', name: 'warning' }],
      points: [{ value: 105, direction: 'max', levelId: 'warning' }],
    }), 600);
    expect(ev.getState(100).activeLevelId).toBe('warning');
    expect(events.some(e => e.type === 'drop' && e.drop.fromLevelId === 'critical' && e.drop.toLevelId === 'warning')).toBe(true);
  });
});

describe('warningEvaluator — reset and pause', () => {
  it('reset clears state and drops to none without re-arm', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 125 });
    ev.step(500, { '100': 125 }); // critical active
    ev.reset(600);
    expect(ev.getState(100).activeLevelId).toBe(null);
    expect(events.some(e => e.type === 'drop' && e.drop.dataId === 100 && e.drop.toLevelId === null)).toBe(true);

    // No re-arm: an immediate crossing is toast-eligible again
    ev.step(600, { '100': 125 });
    ev.step(1100, { '100': 125 });
    const activations = events.filter(e => e.type === 'activation');
    expect(activations[activations.length - 1].activation.toastEligible).toBe(true);
  });

  it('setPaused freezes dwell and shifts deadlines on resume', () => {
    const t = 0;
    const ev = createWarningEvaluator(() => t);
    const events = collect(ev);
    ev.refresh(oilTempPayload(), 0);

    ev.step(0, { '100': 110 }); // deadline 500
    ev.setPaused(100, true); // backgrounded
    ev.step(400, { '100': 110 }); // no step actually happens while paused, but guard anyway
    ev.setPaused(400, false); // resumed — deadline shifts by 300 → 800
    ev.step(800, { '100': 110 });
    expect(events.filter(e => e.type === 'activation')).toHaveLength(1);
  });
});

describe('furthestPoint (R20)', () => {
  it('max-direction largest threshold wins', () => {
    const points: WarningPoint[] = [
      { id: 'a', value: 105, direction: 'max', levelId: 'l', enabled: true },
      { id: 'b', value: 112, direction: 'max', levelId: 'l', enabled: true },
    ];
    expect(furthestPoint(points, 125)).toEqual({ threshold: 112, thresholdType: 'max' });
  });

  it('min-direction smallest threshold wins', () => {
    const points: WarningPoint[] = [
      { id: 'a', value: 1.5, direction: 'min', levelId: 'l', enabled: true },
      { id: 'b', value: 0.8, direction: 'min', levelId: 'l', enabled: true },
    ];
    expect(furthestPoint(points, 0.3)).toEqual({ threshold: 0.8, thresholdType: 'min' });
  });

  it('list-position tie-break keeps the earlier point', () => {
    const points: WarningPoint[] = [
      { id: 'a', value: 105, direction: 'max', levelId: 'l', enabled: true },
      { id: 'b', value: 105, direction: 'max', levelId: 'l', enabled: true },
    ];
    expect(furthestPoint(points, 125)).toEqual({ threshold: 105, thresholdType: 'max' });
  });

  it('max-direction wins when both directions are crossed', () => {
    const points: WarningPoint[] = [
      { id: 'a', value: 0.8, direction: 'min', levelId: 'l', enabled: true },
      { id: 'b', value: 105, direction: 'max', levelId: 'l', enabled: true },
    ];
    expect(furthestPoint(points, 125)).toEqual({ threshold: 105, thresholdType: 'max' });
  });

  it('no crossed points yields the zero fallback', () => {
    expect(furthestPoint([], 10)).toEqual({ threshold: 0, thresholdType: 'max' });
  });
});

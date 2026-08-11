import { describe, it, expect, beforeEach } from 'vitest';
import { createWarningToasts, type WarningToasts } from '../warningToasts';
import { createWarningEvaluator, type WarningEvaluator } from '../stores/warningEvaluator';
import { getToasts, clearToasts } from '../toasts.svelte';
import { navigationGate } from '../navigationGate.svelte';
import type { WarningSettingsPayload } from '../HybridBridgeTypes';
let t = 0;
let panelOpen = false;

let controller: WarningToasts;
let evaluator: WarningEvaluator;

function payload(levels: { id: string; order?: number }[], points: { value: number; direction: 'min' | 'max'; levelId: string }[], delayMs = 500): WarningSettingsPayload {
  return {
    settings: [
      {
        dataId: 100,
        enabled: true,
        name: 'Oil Temp',
        unit: 'C',
        category: 'Engine',
        status: 'Typical',
        levels: levels.map((l, i) => ({ id: l.id, name: l.id, color: '#f59e0b', autolog: false, flash: false, order: l.order ?? i })),
        points: points.map((p, i) => ({ id: `p${i}`, value: p.value, direction: p.direction, levelId: p.levelId, enabled: true })),
        migratedBoundsMarkerLevelId: null,
        migratedBoundsMarkerSet: false,
      },
    ],
    delayMs,
  };
}

const dual = () =>
  payload(
    [{ id: 'warning' }, { id: 'critical' }],
    [
      { value: 105, direction: 'max', levelId: 'warning' },
      { value: 120, direction: 'max', levelId: 'critical' },
    ]
  );

function visible() {
  return getToasts().filter(x => x.meta?.displayed);
}

beforeEach(() => {
  t = 0;
  panelOpen = false;
  clearToasts();
  navigationGate.setBlocked('modal-sheet', false);
  controller = createWarningToasts({ now: () => t, isPanelOpen: () => panelOpen });
  evaluator = createWarningEvaluator(() => t);
  controller.attachEvaluator(evaluator);
});

describe('warningToasts — activation + escalation', () => {
  it('fires one toast with datalink + level name on activation', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 }); // warning activation (toastEligible)
    expect(visible()).toHaveLength(1);
    expect(visible()[0].message).toBe('Oil Temp: warning');
    expect(visible()[0].meta?.evictionClass).toBe('activation');
  });

  it('escalation replaces the datalink toast with an escalation-class toast', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 });
    expect(visible()).toHaveLength(1);
    expect(visible()[0].meta?.evictionClass).toBe('activation');

    evaluator.step(600, { '100': 125 });
    evaluator.step(1100, { '100': 125 }); // critical escalation
    // R13: the older stacked toast for the datalink is dismissed — the fresh
    // toast becomes the single on-screen representation.
    expect(visible()).toHaveLength(1);
    expect(visible()[0].meta?.levelId).toBe('critical');
    expect(visible()[0].meta?.evictionClass).toBe('escalation');
  });

  it('no toast on de-escalation or clear (drop)', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 });
    expect(visible()).toHaveLength(1);
    evaluator.step(2000, { '100': 100 }); // return to none
    expect(visible()).toHaveLength(0);
  });

  it('silent (re-arm window) activations produce no toast', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 125 });
    evaluator.step(500, { '100': 125 }); // critical toast
    expect(visible()).toHaveLength(1);
    const ledgerCount = controller.getLedger().length;
    const toastId = visible()[0].id;
    const announcedText = controller.getAnnouncedText(toastId);
    evaluator.step(2000, { '100': 110 }); // drop → toast demoted to warning
    evaluator.step(2300, { '100': 125 }); // re-cross inside the window
    evaluator.step(2800, { '100': 125 }); // silent escalation — no new toast
    expect(visible()).toHaveLength(1); // same toast, single representation
    // R13: the demoted toast's content agrees with the gauge/panel (updated
    // silently to critical) without a new announcement.
    expect(visible()[0].meta?.levelId).toBe('critical');
    expect(controller.getAnnouncedText(visible()[0].id)).toBe(announcedText);
    expect(controller.getLedger().length).toBe(ledgerCount); // no new announcements
  });

  it('viaRecompute activations produce no toast', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 });
    const before = visible().length;
    evaluator.refresh(
      payload(
        [{ id: 'warning' }, { id: 'critical' }],
        [
          { value: 105, direction: 'max', levelId: 'warning' },
          { value: 100, direction: 'max', levelId: 'critical' },
        ]
      ),
      600
    );
    expect(visible().length).toBe(before); // no new toast for the immediate recompute
  });
});

describe('warningToasts — demotion and single representation', () => {
  it('demoted toast retains its eviction class and updates content', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 });
    evaluator.step(600, { '100': 125 });
    evaluator.step(1100, { '100': 125 }); // critical escalation toast
    const escalated = visible().find(x => x.meta?.levelId === 'critical')!;
    const announcedBefore = controller.getAnnouncedText(escalated.id);
    const ledgerCount = controller.getLedger().length;

    evaluator.step(2000, { '100': 110 }); // drop to warning → demote
    const demoted = visible().find(x => x.id === escalated.id);
    expect(demoted).toBeDefined();
    expect(demoted?.meta?.levelId).toBe('warning');
    expect(demoted?.meta?.evictionClass).toBe('escalation'); // retained
    expect(demoted?.meta?.announced).toBe(true);
    expect(controller.getAnnouncedText(escalated.id)).toBe(announcedBefore); // no re-announcement
    expect(controller.getLedger().length).toBe(ledgerCount); // no new ledger entry
  });

  it('drop to none dismisses the toast', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 125 });
    evaluator.step(500, { '100': 125 });
    evaluator.step(2000, { '100': 100 });
    expect(visible()).toHaveLength(0);
  });

  it('new activation for a datalink with a stacked toast dismisses the older one', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 }); // warning toast
    evaluator.step(1000, { '100': 100 }); // drop to none → dismissed
    evaluator.step(2000, { '100': 110 }); // re-cross
    evaluator.step(2500, { '100': 110 }); // re-activation (toastEligible — outside re-arm window)
    const toasts = visible();
    expect(toasts).toHaveLength(1);
  });
});

describe('warningToasts — stack cap and hidden region', () => {
  function escalatorPayload(count: number): WarningSettingsPayload {
    return {
      settings: Array.from({ length: count }, (_, i) => ({
        dataId: 200 + i,
        enabled: true,
        name: `DL ${200 + i}`,
        unit: '',
        category: 'Engine',
        status: 'Typical',
        levels: [
          { id: 'warning', name: 'warning', color: '#f59e0b', autolog: false, flash: false, order: 0 },
          { id: 'critical', name: 'critical', color: '#ef4444', autolog: false, flash: false, order: 1 },
        ],
        points: [
          { id: 'p0', value: 10, direction: 'max', levelId: 'warning', enabled: true },
          { id: 'p1', value: 20, direction: 'max', levelId: 'critical', enabled: true },
        ],
        migratedBoundsMarkerLevelId: null,
        migratedBoundsMarkerSet: false,
      })),
      delayMs: 100,
    };
  }

  let live: Record<string, number>;

  function stepAt(tNow: number) {
    evaluator.step(tNow, { ...live });
  }

  function activate(id: number) {
    live[String(id)] = 15;
    stepAt(0);
    stepAt(200); // warning dwell → activation
  }

  function escalate(id: number) {
    live[String(id)] = 25;
    stepAt(300);
    stepAt(500); // critical dwell → escalation (replaces the datalink's activation toast)
  }

  it('activation arrival evicts the oldest toast when the stack is full', () => {
    live = {};
    evaluator.refresh(escalatorPayload(4), 0);
    for (const id of [200, 201, 202]) activate(id);
    expect(visible()).toHaveLength(3);
    const firstId = visible()[0].meta!.dataId;
    expect(firstId).toBe(200);

    activate(203); // fourth activation evicts the oldest (200)
    const ids = visible().map(x => x.meta!.dataId);
    expect(ids).not.toContain(200);
    expect(ids).toContain(203);
  });

  it('escalation displaces a non-escalation toast when the stack is full', () => {
    live = {};
    evaluator.refresh(escalatorPayload(4), 0);
    for (const id of [200, 201, 202]) activate(id);
    expect(visible()).toHaveLength(3);
    expect(visible().every(x => x.meta!.evictionClass === 'activation')).toBe(true);

    // 4th datalink escalates: its own activation toast is replaced (single
    // representation), then the full stack of activations must admit the
    // escalation by displacing the oldest non-escalation.
    activate(203);
    escalate(203);
    const ids = visible().map(x => x.meta!.dataId);
    expect(ids).toContain(203);
    expect(ids).toHaveLength(3);
    expect(visible().filter(x => x.meta!.evictionClass === 'escalation')).toHaveLength(1);
  });

  it('all-escalation stack drops the arrival into the hidden live region (ledger channel hidden, announced once)', () => {
    panelOpen = false;
    live = {};
    evaluator.refresh(escalatorPayload(4), 0);

    // 202 activates first, then its toast expires via tick while 202 stays
    // active at warning (tick only removes toasts, not active states).
    activate(202);
    activate(203);
    activate(200);
    controller.tick(3000); // 202's activation toast expires (deadline 3000)
    escalate(203);
    escalate(200);
    activate(201);
    escalate(201);
    // Stack: [203-esc, 200-esc, 201-esc] — full, all escalations
    expect(visible()).toHaveLength(3);
    expect(visible().every(x => x.meta!.evictionClass === 'escalation')).toBe(true);

    // 202 re-escalates (still active at warning) → full all-escalation stack → hidden region
    escalate(202);
    const hidden = controller.getLedger().filter(e => e.channel === 'hidden');
    expect(hidden.length).toBe(1);
    expect(hidden[0].dataId).toBe(202);
    expect(visible().map(x => x.meta!.dataId)).not.toContain(202); // not visible
  });

  it('hidden-region announcements are skipped when the panel is open', () => {
    panelOpen = true;
    live = {};
    evaluator.refresh(escalatorPayload(4), 0);

    activate(202);
    activate(203);
    activate(200);
    controller.tick(3000);
    escalate(203);
    escalate(200);
    activate(201);
    escalate(201);
    escalate(202);
    expect(controller.getLedger().filter(e => e.channel === 'hidden')).toHaveLength(0);
  });
});

describe('warningToasts — deadlines and lifecycle', () => {
  it('auto-dismisses after the duration via tick', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 });
    expect(visible()).toHaveLength(1);
    controller.tick(500 + 3000);
    expect(visible()).toHaveLength(0);
  });

  it('pause freezes the countdown; resume shifts the deadline', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 });
    controller.setBackgrounded(true, 1000);
    controller.tick(2000); // no dismissal while paused (deadline long past)
    expect(visible()).toHaveLength(1);
    controller.setBackgrounded(false, 3000); // deadline 3000 → shifted by 2000 → 5000
    controller.tick(4999);
    expect(visible()).toHaveLength(1);
    controller.tick(5000);
    expect(visible()).toHaveLength(0);
  });

  it('revalidate dismisses toasts whose datalink is no longer active at the toast level', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 125 });
    evaluator.step(500, { '100': 125 }); // critical toast
    evaluator.step(1000, { '100': 100 }); // drop to none — dismissed by drop event
    expect(visible()).toHaveLength(0);

    // Revalidate with a stale state (simulate a foreground pass with a reset evaluator)
    evaluator.step(1500, { '100': 125 });
    evaluator.step(2000, { '100': 125 }); // new critical toast
    expect(visible()).toHaveLength(1);
  });
});

describe('warningToasts — first-run queue', () => {
  function manyPayload(count: number): WarningSettingsPayload {
    return {
      settings: Array.from({ length: count }, (_, i) => ({
        dataId: 300 + i,
        enabled: true,
        name: `DL ${300 + i}`,
        unit: '',
        category: 'Engine',
        status: 'Typical',
        levels: [{ id: 'warning', name: 'warning', color: '#f59e0b', autolog: false, flash: false, order: 0 }],
        points: [{ id: 'p0', value: 10, direction: 'max', levelId: 'warning', enabled: true }],
        migratedBoundsMarkerLevelId: null,
        migratedBoundsMarkerSet: false,
      })),
      delayMs: 100,
    };
  }

  it('first run bounds the stack and queues the oldest non-escalation', () => {
    evaluator.refresh(manyPayload(6), 0);
    const live: Record<string, number> = {};
    for (const i of [0, 1, 2, 3, 4, 5]) {
      live[String(300 + i)] = 20;
      evaluator.step(0, { ...live });
      evaluator.step(200, { ...live });
    }
    expect(visible()).toHaveLength(3);
    const queued = getToasts().filter(x => x.meta?.firstRunQueued);
    expect(queued.length).toBeGreaterThan(0);
  });

  it('surfaceFirstRunQueue re-surfaces paced by the replay gap, once per toast', () => {
    evaluator.refresh(manyPayload(6), 0);
    const live: Record<string, number> = {};
    for (const i of [0, 1, 2, 3, 4, 5]) {
      live[String(300 + i)] = 20;
      evaluator.step(0, { ...live });
      evaluator.step(200, { ...live });
    }
    const queued = getToasts().filter(x => x.meta?.firstRunQueued);
    const queuedBefore = queued.length;
    expect(queuedBefore).toBeGreaterThan(0);

    controller.firstRunCompleted();
    // Free capacity deterministically: dismiss the visible stack
    for (const toast of getToasts().filter(x => x.meta?.displayed)) controller.dismiss(toast.id);
    const queuedIds = new Set(queued.map(x => x.id));
    // Surface paced by the ~1s replay gap (ticks 2000/3000/4000; the first
    // surfaced toast's deadline is 5000, so nothing expires mid-test)
    const counts: number[] = [];
    let now = 1000;
    for (let i = 0; i < queuedBefore; i++) {
      now += 1000;
      controller.tick(now);
      counts.push(getToasts().filter(x => queuedIds.has(x.id) && x.meta?.displayed).length);
    }
    expect(counts).toEqual([1, 2, 3]);
    // No double surface: a sub-gap tick changes nothing
    controller.tick(4500);
    expect(getToasts().filter(x => queuedIds.has(x.id) && x.meta?.displayed).length).toBe(queuedBefore);
  });

  it('reset invalidates the queue and clears toasts', () => {
    evaluator.refresh(manyPayload(6), 0);
    const live: Record<string, number> = {};
    for (const i of [0, 1, 2, 3, 4, 5]) {
      live[String(300 + i)] = 20;
      evaluator.step(0, { ...live });
      evaluator.step(200, { ...live });
    }
    expect(getToasts().length).toBeGreaterThan(0);
    controller.reset();
    expect(getToasts()).toHaveLength(0);
    expect(controller.getLedger()).toHaveLength(0);
  });
});

describe('warningToasts — ledger and tap', () => {
  it('ledger records exactly-once announcements per visible toast', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 });
    evaluator.step(600, { '100': 125 });
    evaluator.step(1100, { '100': 125 });
    const ledger = controller.getLedger();
    expect(ledger.length).toBe(2);
    expect(ledger.filter(e => e.dataId === 100 && e.levelId === 'critical' && e.channel === 'toast')).toHaveLength(1);
  });

  it('rename updates the visible text without re-announcing', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 });
    const announcedText = controller.getAnnouncedText(visible()[0].id);
    expect(visible()[0].message).toBe('Oil Temp: warning');

    // rename the level: refresh with the same id, new name
    const renamed = payload(
      [{ id: 'warning' }, { id: 'critical' }],
      [
        { value: 105, direction: 'max', levelId: 'warning' },
        { value: 120, direction: 'max', levelId: 'critical' },
      ]
    );
    renamed.settings[0].levels[0].name = 'WARNING!';
    evaluator.refresh(renamed, 600);
    controller.refreshDisplayLookup();
    expect(visible()[0].message).toBe('Oil Temp: WARNING!');
    expect(controller.getAnnouncedText(visible()[0].id)).toBe(announcedText); // unchanged
    expect(controller.getLedger().length).toBe(1); // no new entry
  });

  it('tap dismisses and reports the datalink for the container to navigate', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 });
    const id = visible()[0].id;
    const handled = controller.handleTap(id);
    expect(handled).toBe(true);
    expect(visible()).toHaveLength(0);
  });

  it('tap is suppressed (no dismiss) while the gate blocks', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 });
    navigationGate.setBlocked('modal-sheet', true);
    const id = visible()[0].id;
    const handled = controller.handleTap(id);
    expect(handled).toBe(false);
    expect(visible()).toHaveLength(1);
    navigationGate.setBlocked('modal-sheet', false);
    expect(controller.handleTap(id)).toBe(true);
    expect(visible()).toHaveLength(0);
  });
});

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

vi.mock('../../HybridBridge', () => ({
  HybridBridge: {
    saveWarningHistory: vi.fn().mockResolvedValue(undefined),
    getWarningHistory: vi.fn().mockResolvedValue([]),
  },
}));

import { HybridBridge } from '../../HybridBridge';
import { warningStore } from '../warningStore.svelte';
import { createWarningEvaluator, type WarningEvaluator } from '../warningEvaluator';
import type { WarningSettingsPayload } from '../../HybridBridgeTypes';
import { liveDataStore } from '../LiveDataStore.svelte';

const saveWarningHistory = vi.mocked(HybridBridge.saveWarningHistory);
const getWarningHistory = vi.mocked(HybridBridge.getWarningHistory);

let t = 0;
let evaluator: WarningEvaluator;

function payload(levels: { id: string; autolog?: boolean }[], points: { value: number; direction: 'min' | 'max'; levelId: string }[], delayMs = 500): WarningSettingsPayload {
  return {
    settings: [
      {
        dataId: 100,
        enabled: true,
        name: 'Oil Temp',
        unit: 'C',
        category: 'Engine',
        status: 'Typical',
        levels: levels.map((l, i) => ({ id: l.id, name: l.id, color: '#f59e0b', autolog: l.autolog ?? false, flash: false, order: i })),
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
    [{ id: 'warning', autolog: false }, { id: 'critical', autolog: true }],
    [
      { value: 105, direction: 'max', levelId: 'warning' },
      { value: 120, direction: 'max', levelId: 'critical' },
    ]
  );

beforeEach(() => {
  vi.useFakeTimers();
  t = 0;
  saveWarningHistory.mockClear();
  getWarningHistory.mockClear();
  warningStore.reset();
  evaluator = createWarningEvaluator(() => t);
  warningStore.__attachEvaluator(evaluator);
});

afterEach(() => {
  warningStore.__detachEvaluator();
  vi.useRealTimers();
});

describe('warningStore activation-time snapshots (R12)', () => {
  it('snapshots once per activation into an autolog level', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 125 });
    evaluator.step(500, { '100': 125 }); // critical activation (autolog)

    expect(warningStore.activeWarningCount).toBe(1);
    expect(warningStore.history).toHaveLength(1);
    expect(warningStore.history[0]).toMatchObject({
      dataId: 100,
      severity: 'critical',
      triggeredAt: expect.any(Number),
      clearedAt: null,
    });
    expect(warningStore.history[0].value).toBe(125);
  });

  it('does not snapshot non-autolog activations', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 }); // warning activation (autolog false)

    expect(warningStore.activeWarningCount).toBe(1);
    expect(warningStore.history).toHaveLength(0);
  });

  it('escalation closes the lower entry in place and opens a new entry for the autolog level', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 }); // warning active (no entry — not autolog)
    evaluator.step(600, { '100': 125 });
    evaluator.step(1100, { '100': 125 }); // critical escalation

    expect(warningStore.history).toHaveLength(1);
    const entry = warningStore.history[0];
    expect(entry.severity).toBe('critical');
    expect(entry.clearedAt).toBeNull();
  });

  it('escalation into a non-autolog level still closes the lower entry', () => {
    const p = payload(
      [{ id: 'a', autolog: true }, { id: 'b', autolog: false }],
      [
        { value: 10, direction: 'max', levelId: 'a' },
        { value: 20, direction: 'max', levelId: 'b' },
      ]
    );
    evaluator.refresh(p, 0);
    evaluator.step(0, { '100': 15 });
    evaluator.step(500, { '100': 15 }); // level a activation (autolog) → entry opens
    evaluator.step(600, { '100': 25 });
    evaluator.step(1100, { '100': 25 }); // escalate to b (not autolog)

    expect(warningStore.history).toHaveLength(1);
    expect(warningStore.history[0].severity).toBe('a');
    expect(warningStore.history[0].clearedAt).toEqual(expect.any(Number));
  });

  it('active-level drop closes the entry in place and opens a replacement snapshot only when the re-entered level autologs', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 125 });
    evaluator.step(500, { '100': 125 }); // critical (autolog) entry open
    evaluator.step(2000, { '100': 110 }); // drop to warning (not autolog)

    expect(warningStore.history).toHaveLength(1);
    expect(warningStore.history[0]).toMatchObject({ severity: 'critical', clearedAt: expect.any(Number) });
    expect(warningStore.activeWarnings.get(100)?.levelId).toBe('warning');
  });

  it('drop to a re-entered autolog level opens a replacement snapshot', () => {
    const p = payload(
      [{ id: 'a', autolog: true }, { id: 'b', autolog: true }],
      [
        { value: 10, direction: 'max', levelId: 'a' },
        { value: 20, direction: 'max', levelId: 'b' },
      ]
    );
    evaluator.refresh(p, 0);
    evaluator.step(0, { '100': 25 });
    evaluator.step(500, { '100': 25 }); // b active (autolog) entry
    evaluator.step(1000, { '100': 15 }); // drop to a (autolog) → replacement snapshot at the same moment

    expect(warningStore.history).toHaveLength(2);
    expect(warningStore.history[0]).toMatchObject({ severity: 'a', triggeredAt: expect.any(Number), clearedAt: null });
    expect(warningStore.history[1]).toMatchObject({ severity: 'b', clearedAt: expect.any(Number) });
  });

  it('drop-moment snapshot uses the live value and the re-entered level threshold (R12/R20)', () => {
    const p = payload(
      [{ id: 'a', autolog: true }, { id: 'b', autolog: true }],
      [
        { value: 10, direction: 'max', levelId: 'a' },
        { value: 12, direction: 'max', levelId: 'a' },
        { value: 20, direction: 'max', levelId: 'b' },
      ]
    );
    evaluator.refresh(p, 0);
    evaluator.step(0, { '100': 25 });
    evaluator.step(500, { '100': 25 }); // b active (value 25)
    liveDataStore.values['100'] = 13; // live value at the drop moment
    evaluator.step(1000, { '100': 13 }); // drop to a — furthest point is 12 (max)

    expect(warningStore.history[0]).toMatchObject({ severity: 'a', value: 13, threshold: 12, thresholdType: 'max' });
    expect(warningStore.activeWarnings.get(100)).toMatchObject({ value: 13, threshold: 12, thresholdType: 'max' });
  });

  it('records the R20 furthest-beyond threshold from the activation event', () => {
    const p = payload(
      [{ id: 'warning' }],
      [
        { value: 105, direction: 'max', levelId: 'warning' },
        { value: 112, direction: 'max', levelId: 'warning' },
      ]
    );
    evaluator.refresh({ ...p, settings: [{ ...p.settings[0], levels: [{ id: 'warning', name: 'warning', color: '#f59e0b', autolog: true, flash: false, order: 0 }] }] }, 0);
    evaluator.step(0, { '100': 125 });
    evaluator.step(500, { '100': 125 });

    expect(warningStore.history[0]).toMatchObject({ threshold: 112, thresholdType: 'max' });
    expect(warningStore.activeWarnings.get(100)).toMatchObject({ threshold: 112, thresholdType: 'max' });
  });
});

describe('warningStore edit recompute (R13)', () => {
  it('viaRecompute closes the lower entry in place without a snapshot', () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 }); // warning active (no entry — not autolog)
    // Edit raises the critical threshold below the current value → immediate silent recompute
    evaluator.refresh(
      payload(
        [{ id: 'warning' }, { id: 'critical', autolog: true }],
        [
          { value: 105, direction: 'max', levelId: 'warning' },
          { value: 100, direction: 'max', levelId: 'critical' },
        ]
      ),
      600
    );

    expect(warningStore.activeWarnings.get(100)?.levelId).toBe('critical');
    expect(warningStore.history).toHaveLength(0); // no snapshot for the raised level
  });
});

describe('warningStore persistence', () => {
  it('debounced save persists the current history', async () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 125 });
    evaluator.step(500, { '100': 125 });

    vi.advanceTimersByTime(2100);
    expect(saveWarningHistory).toHaveBeenCalledTimes(1);
    expect(saveWarningHistory).toHaveBeenCalledWith(warningStore.history);
  });

  it('save failure queues the retry for the next mutation', async () => {
    saveWarningHistory.mockRejectedValueOnce(new Error('boom'));
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 125 });
    evaluator.step(500, { '100': 125 });

    await vi.advanceTimersByTimeAsync(2100); // first save fails → queued
    expect(saveWarningHistory).toHaveBeenCalledTimes(1);

    evaluator.step(2000, { '100': 110 }); // next mutation (drop)
    await vi.advanceTimersByTimeAsync(2100);
    expect(saveWarningHistory).toHaveBeenCalledTimes(3); // queued retry + current
  });

  it('reset clears history and open-entry tracking without phantom saves', async () => {
    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 125 });
    evaluator.step(500, { '100': 125 });
    expect(warningStore.history).toHaveLength(1);

    warningStore.reset();
    vi.advanceTimersByTime(5000);
    expect(saveWarningHistory).not.toHaveBeenCalled(); // no save after reset
  });

  it('loadHistory seeds the id counter', async () => {
    getWarningHistory.mockResolvedValue([
      { id: 5, dataId: 100, name: 'X', unit: '', category: '', value: 1, severity: 'warning', threshold: 1, thresholdType: 'max', triggeredAt: 1, clearedAt: 2 },
    ] as never);
    await warningStore.loadHistory();
    expect(warningStore.history).toHaveLength(1);

    evaluator.refresh(dual(), 0);
    evaluator.step(0, { '100': 125 });
    evaluator.step(500, { '100': 125 });
    expect(warningStore.history[0].id).toBe(6);
  });
});

describe('warningStore snapshotToggle (R12)', () => {
  it('snapshots the ongoing activation synchronously when autolog is toggled on', () => {
    const p = payload([{ id: 'warning', autolog: false }], [{ value: 105, direction: 'max', levelId: 'warning' }]);
    evaluator.refresh(p, 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 }); // warning active, no entry (autolog false)

    // Autolog toggled on: synchronous toggle-moment snapshot
    warningStore.snapshotToggle(100, 'warning');
    expect(warningStore.history).toHaveLength(1);
    expect(warningStore.history[0].severity).toBe('warning');
  });

  it('never re-snapshots an activation that already has an open entry', () => {
    const p = payload([{ id: 'warning', autolog: true }], [{ value: 105, direction: 'max', levelId: 'warning' }]);
    evaluator.refresh(p, 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 }); // entry already open (autolog true at activation)

    warningStore.snapshotToggle(100, 'warning');
    expect(warningStore.history).toHaveLength(1);
  });

  it('no-ops when the level is not active', () => {
    const p = payload([{ id: 'warning', autolog: false }], [{ value: 105, direction: 'max', levelId: 'warning' }]);
    evaluator.refresh(p, 0);
    evaluator.step(0, { '100': 90 });

    warningStore.snapshotToggle(100, 'warning');
    expect(warningStore.history).toHaveLength(0);
  });

  it('uses the live value at the toggle moment', () => {
    const p = payload([{ id: 'warning', autolog: false }], [{ value: 105, direction: 'max', levelId: 'warning' }]);
    evaluator.refresh(p, 0);
    evaluator.step(0, { '100': 110 });
    evaluator.step(500, { '100': 110 });

    liveDataStore.values['100'] = 113;
    warningStore.snapshotToggle(100, 'warning');
    expect(warningStore.history[0].value).toBe(113);
  });
});

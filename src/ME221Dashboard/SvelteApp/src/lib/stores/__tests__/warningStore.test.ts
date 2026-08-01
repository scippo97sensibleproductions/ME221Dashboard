import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

vi.mock('../../HybridBridge', () => ({
  HybridBridge: {
    saveWarningHistory: vi.fn().mockResolvedValue(undefined),
    getWarningHistory: vi.fn().mockResolvedValue([]),
  },
}));

import { HybridBridge } from '../../HybridBridge';
import { warningStore, type ActiveWarning } from '../warningStore.svelte';
import type { DataLinkWarningSetting } from '../../HybridBridgeTypes';

const saveWarningHistory = vi.mocked(HybridBridge.saveWarningHistory);
const getWarningHistory = vi.mocked(HybridBridge.getWarningHistory);

function settings(overrides: Partial<DataLinkWarningSetting> = {}): DataLinkWarningSetting {
  return {
    dataId: 100,
    enabled: true,
    minWarning: null,
    maxWarning: 9000,
    name: 'RPM',
    unit: 'RPM',
    category: 'Engine',
    status: 'Custom',
    ...overrides,
  };
}

function warningState(w: ActiveWarning) {
  return { dataId: w.dataId, severity: w.severity, value: w.value, threshold: w.threshold, thresholdType: w.thresholdType };
}

beforeEach(() => {
  vi.useFakeTimers();
  saveWarningHistory.mockClear();
  getWarningHistory.mockClear();
  warningStore.reset();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('warningStore.updateWarning', () => {
  it('fires a toast-style add on none → warning transition', () => {
    warningStore.updateWarning(100, 'RPM', 'RPM', 'Engine', 9500, 'warning', [settings()]);

    expect(warningStore.activeWarningCount).toBe(1);
    expect(warningStore.activeWarnings.get(100)).toMatchObject({
      dataId: 100, severity: 'warning', value: 9500, threshold: 9000, thresholdType: 'max',
    });
  });

  it('escalates warning → critical in place', () => {
    warningStore.updateWarning(100, 'RPM', 'RPM', 'Engine', 9500, 'warning', [settings()]);
    warningStore.updateWarning(100, 'RPM', 'RPM', 'Engine', 9600, 'critical', [settings()]);

    expect(warningStore.activeWarningCount).toBe(1);
    expect(warningStore.activeWarnings.get(100)?.severity).toBe('critical');
    expect(warningStore.activeWarnings.get(100)?.value).toBe(9600);
  });

  it('updates value in place without toast on same-state updates', () => {
    warningStore.updateWarning(100, 'RPM', 'RPM', 'Engine', 9500, 'warning', [settings()]);
    warningStore.updateWarning(100, 'RPM', 'RPM', 'Engine', 9800, 'warning', [settings()]);

    expect(warningStore.activeWarningCount).toBe(1);
    expect(warningStore.activeWarnings.get(100)?.value).toBe(9800);
    expect(warningStore.history).toHaveLength(0); // no new history entry
  });

  it('no transition when state stays none', () => {
    warningStore.updateWarning(100, 'RPM', 'RPM', 'Engine', 5000, 'none', [settings()]);

    expect(warningStore.activeWarningCount).toBe(0);
    expect(warningStore.history).toHaveLength(0);
  });

  it('clears an active warning when the value recovers', () => {
    warningStore.updateWarning(100, 'RPM', 'RPM', 'Engine', 9500, 'warning', [settings()]);
    warningStore.updateWarning(100, 'RPM', 'RPM', 'Engine', 5000, 'none', [settings()]);

    expect(warningStore.activeWarningCount).toBe(0);
    expect(warningStore.history).toHaveLength(1); // archived with clearedAt
    expect(warningStore.history[0]).toMatchObject({ dataId: 100, severity: 'warning', clearedAt: expect.any(Number) });
  });

  it('uses minWarning for low-side violations', () => {
    warningStore.updateWarning(100, 'Boost', 'kPa', 'Engine', 50, 'warning',
      [settings({ minWarning: 80, maxWarning: null })]);

    const w = warningStore.activeWarnings.get(100)!;
    expect(warningState(w)).toEqual({
      dataId: 100, severity: 'warning', value: 50, threshold: 80, thresholdType: 'min',
    });
  });

  it('falls back to zero threshold when no matching setting exists', () => {
    warningStore.updateWarning(100, 'RPM', 'RPM', 'Engine', 9500, 'warning', []);

    const w = warningStore.activeWarnings.get(100)!;
    expect(warningState(w)).toEqual({
      dataId: 100, severity: 'warning', value: 9500, threshold: 0, thresholdType: 'max',
    });
  });
});

describe('warningStore.clear', () => {
  it('clearWarning archives and saves history', async () => {
    warningStore.updateWarning(100, 'RPM', 'RPM', 'Engine', 9500, 'warning', [settings()]);
    warningStore.clearWarning(100);

    expect(warningStore.activeWarningCount).toBe(0);
    expect(warningStore.history).toHaveLength(1);

    vi.advanceTimersByTime(2100);
    expect(saveWarningHistory).toHaveBeenCalledTimes(1);
    expect(saveWarningHistory).toHaveBeenCalledWith(warningStore.history);
  });

  it('clearAllWarnings archives every active warning', () => {
    warningStore.updateWarning(100, 'RPM', 'RPM', 'Engine', 9500, 'warning', [settings()]);
    warningStore.updateWarning(200, 'Temp', '°C', 'Engine', 120, 'critical',
      [settings({ dataId: 200, name: 'Temp', unit: '°C', maxWarning: 110 })]);

    warningStore.clearAllWarnings();

    expect(warningStore.activeWarningCount).toBe(0);
    expect(warningStore.history).toHaveLength(2);
  });

  it('reset clears state and history and allows reload', () => {
    warningStore.updateWarning(100, 'RPM', 'RPM', 'Engine', 9500, 'warning', [settings()]);
    warningStore.reset();

    expect(warningStore.activeWarningCount).toBe(0);
    expect(warningStore.history).toHaveLength(0);
  });
});

describe('warningStore.loadHistory', () => {
  it('loads persisted history and seeds the id counter', async () => {
    getWarningHistory.mockResolvedValue([
      { id: 5, dataId: 100, name: 'RPM', unit: 'RPM', category: 'Engine', value: 9500, severity: 'warning', threshold: 9000, thresholdType: 'max', triggeredAt: 1, clearedAt: 2 },
    ] as never);

    await warningStore.loadHistory();

    expect(warningStore.history).toHaveLength(1);
    expect(warningStore.history[0].id).toBe(5);

    // next archived entry continues from the loaded max id
    warningStore.updateWarning(100, 'RPM', 'RPM', 'Engine', 9500, 'warning', [settings()]);
    warningStore.clearWarning(100);
    expect(warningStore.history[0].id).toBe(6);
  });

  it('loadHistory is a no-op once loaded', async () => {
    getWarningHistory.mockResolvedValue([{ id: 1, dataId: 1 } as never]);

    await warningStore.loadHistory();
    getWarningHistory.mockClear();
    await warningStore.loadHistory();

    expect(getWarningHistory).not.toHaveBeenCalled();
  });
});

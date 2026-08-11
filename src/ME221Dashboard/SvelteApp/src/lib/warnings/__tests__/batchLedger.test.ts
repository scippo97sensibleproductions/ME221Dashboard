import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createBatchLedger, type BatchLedger } from '../batchLedger';

let persist: ReturnType<typeof vi.fn>;
let ledger: BatchLedger;

beforeEach(() => {
  persist = vi.fn().mockResolvedValue(undefined);
  ledger = createBatchLedger({ now: () => 123, persist });
});

describe('batchLedger — outcomes and aggregate', () => {
  it('all-success batch produces no banner and a determinate toggle', () => {
    ledger.begin();
    ledger.recordOutcome(1, 'success');
    ledger.recordOutcome(2, 'success');
    ledger.complete();
    expect(ledger.getState().bannerVisible).toBe(false);
    expect(ledger.indeterminate()).toBe(false);
  });

  it('partial failure yields the indeterminate aggregate and the banner', () => {
    ledger.begin();
    ledger.recordOutcome(1, 'success');
    ledger.recordOutcome(2, 'failed');
    ledger.complete();
    expect(ledger.getState().bannerVisible).toBe(true);
    expect(ledger.indeterminate()).toBe(true);
    expect(ledger.failedDatalinks()).toEqual([2]);
  });

  it('full failure shows the banner with all datalinks failed', () => {
    ledger.begin();
    ledger.recordOutcome(1, 'failed');
    ledger.complete();
    expect(ledger.getState().bannerVisible).toBe(true);
    expect(ledger.failedDatalinks()).toEqual([1]);
  });
});

describe('batchLedger — retry-only-failed binding', () => {
  it('targets only failed datalinks while the banner shows', () => {
    ledger.begin();
    ledger.recordOutcome(1, 'success');
    ledger.recordOutcome(2, 'failed');
    ledger.complete();
    expect(ledger.targetDatalinks([1, 2, 3])).toEqual([2]);
  });

  it('dismissing the banner restores full-set semantics', () => {
    ledger.begin();
    ledger.recordOutcome(1, 'success');
    ledger.recordOutcome(2, 'failed');
    ledger.complete();
    ledger.dismissBanner();
    expect(ledger.getState().bannerVisible).toBe(false);
    expect(ledger.targetDatalinks([1, 2, 3])).toEqual([1, 2, 3]);
  });
});

describe('batchLedger — durable ledger', () => {
  it('persists the outcomes on completion', async () => {
    ledger.begin();
    ledger.recordOutcome(1, 'success');
    ledger.recordOutcome(2, 'failed');
    ledger.complete();
    await vi.waitFor(() => expect(persist).toHaveBeenCalledTimes(1));
    expect(persist).toHaveBeenCalledWith([
      { dataId: 1, outcome: 'success', timestamp: 123 },
      { dataId: 2, outcome: 'failed', timestamp: 123 },
    ]);
  });

  it('does not persist an empty batch', async () => {
    ledger.begin();
    ledger.complete();
    expect(persist).not.toHaveBeenCalled();
  });
});

describe('batchLedger — reset', () => {
  it('clears in-flight state and outcomes', () => {
    ledger.begin();
    ledger.recordOutcome(1, 'failed');
    ledger.reset();
    expect(ledger.getState().inFlight).toBe(false);
    expect(ledger.getState().outcomes.size).toBe(0);
    expect(ledger.indeterminate()).toBe(false);
  });
});

export type BatchOutcome = 'success' | 'failed' | 'skipped';

export interface BatchLedgerEntryLike {
  dataId: number;
  outcome: string;
  timestamp: number;
}

export interface BatchLedgerState {
  inFlight: boolean;
  outcomes: Map<number, BatchOutcome>;
  bannerVisible: boolean;
}

export interface BatchLedgerDeps {
  now: () => number;
  persist: (entries: BatchLedgerEntryLike[]) => Promise<void>;
}

/**
 * R15 batch state machine: in-flight state, per-datalink outcomes, the
 * partial-failure banner with retry-only-failed binding, the indeterminate
 * aggregate for the master toggle, and the durable ledger hook (outcomes are
 * persisted so a partial failure survives process death).
 */
export function createBatchLedger(deps: BatchLedgerDeps) {
  let state: BatchLedgerState = { inFlight: false, outcomes: new Map(), bannerVisible: false };

  function begin(): void {
    state = { inFlight: true, outcomes: new Map(), bannerVisible: false };
  }

  function recordOutcome(dataId: number, outcome: BatchOutcome): void {
    const outcomes = new Map(state.outcomes);
    outcomes.set(dataId, outcome);
    state = { ...state, outcomes };
  }

  function complete(): void {
    const failed = Array.from(state.outcomes.entries()).some(([, o]) => o === 'failed');
    const anyApplied = state.outcomes.size > 0;
    state = { ...state, inFlight: false, bannerVisible: failed && anyApplied };
    void persist();
  }

  function cancel(): void {
    state = { ...state, inFlight: false };
  }

  function dismissBanner(): void {
    state = { ...state, bannerVisible: false };
  }

  /** Retry-only-failed binding while the banner is displayed. */
  function targetDatalinks(all: number[]): number[] {
    if (!state.bannerVisible) return all;
    const failed = Array.from(state.outcomes.entries())
      .filter(([, o]) => o === 'failed')
      .map(([dataId]) => dataId);
    return failed.length > 0 ? failed : all;
  }

  /** True when outcomes are mixed (some applied, some failed) — indeterminate toggle. */
  function indeterminate(): boolean {
    if (state.outcomes.size === 0) return false;
    const kinds = new Set(state.outcomes.values());
    return kinds.size > 1 || kinds.has('skipped');
  }

  function getState(): BatchLedgerState {
    return state;
  }

  function failedDatalinks(): number[] {
    return Array.from(state.outcomes.entries())
      .filter(([, o]) => o === 'failed')
      .map(([dataId]) => dataId);
  }

  async function persist(): Promise<void> {
    const entries: BatchLedgerEntryLike[] = Array.from(state.outcomes.entries()).map(([dataId, outcome]) => ({
      dataId,
      outcome,
      timestamp: deps.now(),
    }));
    if (entries.length === 0) return;
    await deps.persist(entries);
  }

  function reset(): void {
    state = { inFlight: false, outcomes: new Map(), bannerVisible: false };
  }

  return { begin, recordOutcome, complete, cancel, dismissBanner, targetDatalinks, indeterminate, getState, failedDatalinks, reset };
}

export type BatchLedger = ReturnType<typeof createBatchLedger>;

export interface DraftResult<T> {
  state: 'idle' | 'pending' | 'rejected' | 'failed';
  /** Last successfully committed value (the fallback for rejections). */
  lastCommitted: T | null;
  /** Current draft value (null when idle/failed-reverted). */
  value: T | null;
  /** Reason for the last rejection (inline notice). */
  reason: string | null;
}

export interface DraftDeps<T> {
  /** Returns null when valid, or a reason string when the draft must be rejected. */
  validate: (value: T) => string | null;
  commit: (value: T) => Promise<boolean>;
  onCommitted?: (value: T) => void;
}

/**
 * R15/R16/R17 draft state machine: pending drafts hold their value, commit on
 * blur/Enter (never per keystroke), reject with revert on validation failure,
 * and a failed commit retains the draft on screen (navigation-block clause).
 */
export function createDraft<T>(deps: DraftDeps<T>) {
  let state: DraftResult<T> = { state: 'idle', lastCommitted: null, value: null, reason: null };
  let committing = false;

  function start(value: T): void {
    state = { ...state, state: 'pending', value, reason: null };
  }

  function discard(): void {
    state = { ...state, state: 'idle', value: null, reason: null };
  }

  /** Revert to the last committed value on validation failure. */
  function reject(reason: string): void {
    state = { ...state, state: 'rejected', value: null, reason };
  }

  function acknowledgeRejection(): void {
    if (state.state === 'rejected') state = { ...state, state: 'idle', reason: null };
  }

  async function submit(): Promise<'committed' | 'rejected' | 'failed' | 'idle'> {
    if (state.value === null || committing) return 'idle';
    if (state.state !== 'pending' && state.state !== 'failed') return 'idle';
    const reason = deps.validate(state.value);
    if (reason !== null) {
      reject(reason);
      return 'rejected';
    }
    committing = true;
    try {
      const value = state.value;
      const ok = await deps.commit(value);
      if (!ok) {
        state = { ...state, state: 'failed', value };
        return 'failed';
      }
      state = { state: 'idle', lastCommitted: value, value: null, reason: null };
      deps.onCommitted?.(value);
      return 'committed';
    } finally {
      committing = false;
    }
  }

  function getState(): DraftResult<T> {
    return state;
  }

  /** True when a pending draft or a retained failed draft blocks navigation. */
  function blocksNavigation(): boolean {
    return state.state === 'pending' || state.state === 'failed';
  }

  function isPending(): boolean {
    return state.state === 'pending';
  }

  return { start, discard, submit, reject, acknowledgeRejection, getState, blocksNavigation, isPending };
}

export type Draft<T> = ReturnType<typeof createDraft<T>>;

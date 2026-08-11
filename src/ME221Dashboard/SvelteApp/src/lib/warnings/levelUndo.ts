export interface UndoableLevel {
  name: string;
  color: string;
  autolog: boolean;
  flash: boolean;
  order: number;
}

export interface UndoablePoint {
  value: number;
  direction: 'min' | 'max';
  levelId: string;
  enabled: boolean;
}

export interface LevelUndoSnapshot {
  dataId: number;
  levels: UndoableLevel[];
  points: UndoablePoint[];
  preDeleteStatus: 'Typical' | 'Custom';
  markerSet: boolean;
  markerLevelId: string | null;
  /** True when the deleted level was the marker-bearing level. */
  markerDeleted: boolean;
}

export interface LevelUndoState {
  snapshot: LevelUndoSnapshot;
  frozen: boolean;
  interveningEdit: boolean;
}

export interface LevelUndoDeps {
  now: () => number;
  /** Invoked when the undo window expires while the row is not visible. */
  onExpiryNotice?: (dataId: number) => void;
}

/**
 * R9 undo state machine: row-anchored, single-use, scope label, expiry on
 * subsequent level mutations, freeze while a failure banner shows, and the
 * per-datalink intervening-edit bit carried in the undo-restore payload.
 */
export function createLevelUndo(deps: LevelUndoDeps) {
  const windows = new Map<number, LevelUndoState>();
  const expiredNotices = new Set<number>();

  function has(dataId: number): boolean {
    return windows.has(dataId);
  }

  function get(dataId: number): LevelUndoState | undefined {
    return windows.get(dataId);
  }

  function begin(snapshot: LevelUndoSnapshot): void {
    windows.set(snapshot.dataId, {
      snapshot,
      frozen: false,
      interveningEdit: false,
    });
  }

  function appendRemoval(dataId: number, point: UndoablePoint): void {
    const w = windows.get(dataId);
    if (!w) return;
    w.snapshot.points.push(point);
  }

  function recordInterveningEdit(dataId: number): void {
    const w = windows.get(dataId);
    if (!w || w.frozen) return;
    w.interveningEdit = true;
  }

  /** Subsequent level add/delete/reorder expires the window (R9). */
  function expireByMutation(dataId: number): void {
    const w = windows.get(dataId);
    if (!w || w.frozen) return;
    windows.delete(dataId);
    expiredNotices.add(dataId);
    deps.onExpiryNotice?.(dataId);
  }

  function freeze(dataId: number, frozen: boolean): void {
    const w = windows.get(dataId);
    if (!w) return;
    w.frozen = frozen;
  }

  /** Single-use: returns the restore payload and clears the window. */
  function consume(dataId: number): LevelUndoSnapshot | null {
    const w = windows.get(dataId);
    if (!w) return null;
    windows.delete(dataId);
    return w.snapshot;
  }

  function scopeLabel(dataId: number): string {
    const w = windows.get(dataId);
    if (!w) return '';
    const levelCount = w.snapshot.levels.length;
    const pointCount = w.snapshot.points.length;
    if (levelCount > 0 && pointCount > 0) return `Undo level + ${pointCount} point(s)`;
    if (levelCount > 0) return 'Undo level';
    return `Undo ${pointCount} point(s)`;
  }

  function buildRestorePayload(dataId: number): {
    preDeleteStatus: 'Typical' | 'Custom';
    interveningEdit: boolean;
    markerSet: boolean;
    markerLevelName: string | null;
  } | null {
    const w = windows.get(dataId);
    if (!w) return null;
    const markerLevelName = w.snapshot.markerDeleted && w.snapshot.levels.length > 0
      ? w.snapshot.levels[0].name
      : null;
    return {
      preDeleteStatus: w.snapshot.preDeleteStatus,
      interveningEdit: w.interveningEdit,
      markerSet: w.snapshot.markerDeleted && w.snapshot.markerSet,
      markerLevelName,
    };
  }

  function takeExpiryNotices(): number[] {
    const notices = Array.from(expiredNotices);
    expiredNotices.clear();
    return notices;
  }

  function hasExpiryNotice(dataId: number): boolean {
    return expiredNotices.has(dataId);
  }

  function clearExpiryNotice(dataId: number): void {
    expiredNotices.delete(dataId);
  }

  function reset(): void {
    windows.clear();
    expiredNotices.clear();
  }

  return {
    has,
    get,
    begin,
    appendRemoval,
    recordInterveningEdit,
    expireByMutation,
    freeze,
    consume,
    scopeLabel,
    buildRestorePayload,
    takeExpiryNotices,
    hasExpiryNotice,
    clearExpiryNotice,
    reset,
  };
}

export type LevelUndo = ReturnType<typeof createLevelUndo>;

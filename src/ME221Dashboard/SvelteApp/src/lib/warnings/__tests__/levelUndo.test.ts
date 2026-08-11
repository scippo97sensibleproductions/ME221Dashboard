import { describe, it, expect, beforeEach } from 'vitest';
import { createLevelUndo, type LevelUndo, type LevelUndoSnapshot } from '../levelUndo';

let t = 0;
let notices: number[] = [];
let undo: LevelUndo;

const snapshot = (overrides: Partial<LevelUndoSnapshot> = {}): LevelUndoSnapshot => ({
  dataId: 100,
  levels: [{ name: 'critical', color: '#ef4444', autolog: true, flash: true, order: 1 }],
  points: [
    { value: 120, direction: 'max', levelId: 'old-critical', enabled: true },
    { value: 130, direction: 'max', levelId: 'old-critical', enabled: true },
  ],
  preDeleteStatus: 'Typical',
  markerSet: false,
  markerLevelId: null,
  markerDeleted: false,
  ...overrides,
});

beforeEach(() => {
  t = 0;
  notices = [];
  undo = createLevelUndo({ now: () => t, onExpiryNotice: (dataId) => notices.push(dataId) });
});

describe('levelUndo — lifecycle', () => {
  it('begin + consume restores the exact snapshot (single use)', () => {
    undo.begin(snapshot());
    const restored = undo.consume(100);
    expect(restored).toEqual(snapshot());
    expect(undo.has(100)).toBe(false);
    expect(undo.consume(100)).toBeNull();
  });

  it('scope label states the restore scope', () => {
    undo.begin(snapshot());
    expect(undo.scopeLabel(100)).toBe('Undo level + 2 point(s)');
    undo.begin(snapshot({ levels: [], points: [] }));
    expect(undo.scopeLabel(100)).toBe('Undo 0 point(s)');
    undo.begin(snapshot({ levels: [], points: [{ value: 120, direction: 'max', levelId: 'x', enabled: true }] }));
    expect(undo.scopeLabel(100)).toBe('Undo 1 point(s)');
    undo.begin(snapshot({ levels: [{ name: 'warning', color: '#f59e0b', autolog: false, flash: false, order: 0 }], points: [] }));
    expect(undo.scopeLabel(100)).toBe('Undo level');
  });

  it('point removals append to the same window', () => {
    undo.begin(snapshot({ points: [{ value: 120, direction: 'max', levelId: 'x', enabled: true }] }));
    undo.appendRemoval(100, { value: 110, direction: 'max', levelId: 'x', enabled: true });
    expect(undo.scopeLabel(100)).toBe('Undo level + 2 point(s)');
  });
});

describe('levelUndo — expiry', () => {
  it('a subsequent level mutation expires the window', () => {
    undo.begin(snapshot());
    undo.expireByMutation(100);
    expect(undo.has(100)).toBe(false);
    expect(undo.consume(100)).toBeNull();
    expect(notices).toContain(100);
  });

  it('expiry notice is queued once and can be taken', () => {
    undo.begin(snapshot());
    undo.expireByMutation(100);
    expect(undo.takeExpiryNotices()).toEqual([100]);
    expect(undo.takeExpiryNotices()).toEqual([]);
  });

  it('hasExpiryNotice reflects a queued notice until cleared', () => {
    undo.begin(snapshot());
    undo.expireByMutation(100);
    expect(undo.hasExpiryNotice(100)).toBe(true);
    undo.clearExpiryNotice(100);
    expect(undo.hasExpiryNotice(100)).toBe(false);
  });

  it('expiry is frozen while the failure banner shows', () => {
    undo.begin(snapshot());
    undo.freeze(100, true);
    undo.expireByMutation(100);
    expect(undo.has(100)).toBe(true);
    undo.freeze(100, false);
    undo.expireByMutation(100);
    expect(undo.has(100)).toBe(false);
  });
});

describe('levelUndo — intervening-edit bit', () => {
  it('is set by R4-qualifying writes and cleared by consume', () => {
    undo.begin(snapshot());
    expect(undo.buildRestorePayload(100)?.interveningEdit).toBe(false);
    undo.recordInterveningEdit(100);
    expect(undo.buildRestorePayload(100)?.interveningEdit).toBe(true);
    undo.consume(100);
    expect(undo.buildRestorePayload(100)).toBeNull();
  });

  it('is not set while frozen (during the failure banner)', () => {
    undo.begin(snapshot());
    undo.freeze(100, true);
    undo.recordInterveningEdit(100);
    expect(undo.buildRestorePayload(100)?.interveningEdit).toBe(false);
  });
});

describe('levelUndo — marker handling', () => {
  it('carries the marker-set + marker-level-name flags for the deleted marker level', () => {
    undo.begin(snapshot({
      markerSet: true,
      markerLevelId: 'm1',
      markerDeleted: true,
      levels: [{ name: 'warning', color: '#f59e0b', autolog: true, flash: false, order: 0 }],
    }));
    const payload = undo.buildRestorePayload(100);
    expect(payload).toMatchObject({ markerSet: true, markerLevelName: 'warning' });
  });

  it('does not fabricate a marker when the deleted level was not the marker', () => {
    undo.begin(snapshot({ markerSet: true, markerLevelId: 'other', markerDeleted: false }));
    expect(undo.buildRestorePayload(100)).toMatchObject({ markerSet: false, markerLevelName: null });
  });
});

describe('levelUndo — reset', () => {
  it('clears windows and notices', () => {
    undo.begin(snapshot());
    undo.expireByMutation(100);
    undo.reset();
    expect(undo.has(100)).toBe(false);
    expect(undo.takeExpiryNotices()).toEqual([]);
  });
});

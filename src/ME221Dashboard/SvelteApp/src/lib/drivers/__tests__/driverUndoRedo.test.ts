import { describe, it, expect } from 'vitest';
import {
  createDriverUndoRedoState,
  pushDriverUndo,
  canDriverUndo,
  canDriverRedo,
  driverUndo,
  driverRedo,
  nextDriverGroupId,
} from '../driverUndoRedo';
import type { DriverUndoResult } from '../driverUndoRedo';

const current: DriverUndoResult = {
  configs: [10, 20, 30],
  outputLinkIds: [1, 2],
  inputLinkIds: [5],
};

describe('driverUndoRedo', () => {
  it('starts empty and cannot undo/redo', () => {
    const s = createDriverUndoRedoState();
    expect(canDriverUndo(s)).toBe(false);
    expect(canDriverRedo(s)).toBe(false);
    expect(driverUndo(s, current)).toBeNull();
    expect(driverRedo(s, current)).toBeNull();
  });

  it('pushDriverUndo clears the redo stack', () => {
    const s = createDriverUndoRedoState();
    pushDriverUndo(s, [{ type: 'config', index: 0, oldVal: 10, newVal: 99, groupId: 'g1' }]);
    driverUndo(s, current);
    expect(canDriverRedo(s)).toBe(true);
    pushDriverUndo(s, [{ type: 'config', index: 0, oldVal: 10, newVal: 55, groupId: 'g2' }]);
    expect(canDriverRedo(s)).toBe(false);
  });

  it('undo reverts all entry types of the latest group', () => {
    const s = createDriverUndoRedoState();
    pushDriverUndo(s, [
      { type: 'config', index: 1, oldVal: 20, newVal: 200, groupId: 'g1' },
      { type: 'outputLink', index: 0, oldVal: 1, newVal: 9, groupId: 'g1' },
      { type: 'inputLink', index: 0, oldVal: 5, newVal: 7, groupId: 'g1' },
    ]);
    const reverted = driverUndo(s, current)!;
    expect(reverted.configs).toEqual([10, 20, 30]);
    expect(reverted.outputLinkIds).toEqual([1, 2]);
    expect(reverted.inputLinkIds).toEqual([5]);
    expect(canDriverUndo(s)).toBe(false);
  });

  it('redo re-applies the newest group', () => {
    const s = createDriverUndoRedoState();
    pushDriverUndo(s, [
      { type: 'config', index: 1, oldVal: 20, newVal: 200, groupId: 'g1' },
      { type: 'outputLink', index: 0, oldVal: 1, newVal: 9, groupId: 'g1' },
      { type: 'inputLink', index: 0, oldVal: 5, newVal: 7, groupId: 'g1' },
    ]);
    const reverted = driverUndo(s, current)!;
    const redone = driverRedo(s, reverted)!;
    expect(redone.configs).toEqual([10, 200, 30]);
    expect(redone.outputLinkIds).toEqual([9, 2]);
    expect(redone.inputLinkIds).toEqual([7]);
    expect(canDriverRedo(s)).toBe(false);
  });

  it('undo pops only the latest group', () => {
    const s = createDriverUndoRedoState();
    pushDriverUndo(s, [
      { type: 'config', index: 0, oldVal: 10, newVal: 11, groupId: 'g1' },
      { type: 'config', index: 1, oldVal: 20, newVal: 22, groupId: 'g2' },
    ]);
    driverUndo(s, current);
    expect(s.undoStack.map(e => e.groupId)).toEqual(['g1']);
  });

  it('returns fresh arrays without mutating inputs', () => {
    const s = createDriverUndoRedoState();
    pushDriverUndo(s, [{ type: 'config', index: 0, oldVal: 10, newVal: 99, groupId: 'g1' }]);
    const reverted = driverUndo(s, current)!;
    expect(reverted).not.toBe(current);
    expect(reverted.configs).not.toBe(current.configs);
    expect(current.configs[0]).toBe(10);
  });

  it('nextDriverGroupId produces unique ids', () => {
    const a = nextDriverGroupId();
    const b = nextDriverGroupId();
    expect(a).toMatch(/^driver-\d+$/);
    expect(a).not.toBe(b);
  });
});

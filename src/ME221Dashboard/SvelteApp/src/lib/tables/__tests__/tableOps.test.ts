import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { applyTransform, selBounds } from '../tableTransforms';
import { deriveSelectionType, handleSelectionComplete } from '../tableSelection';
import {
  createUndoRedoState,
  pushUndo,
  canUndo,
  canRedo,
  undo,
  redo,
  recalculateDirty,
  saveSessionCache,
  loadSessionCache,
  clearSessionCache,
} from '../tableUndoRedo';
import type { UndoEntry } from '../tableUndoRedo';
import type { TableData as TD, TableDefinition as TDef } from '../types';

const def: TDef = {
  id: 1, name: 'Timing', category: 'Fuel', viewInTree: true, enabled: true, tableType: 'T16x16',
  cols: 4, rows: 3,
  input0Name: 'RPM', input1Name: 'MAP', outputName: 'Timing',
  input0LinkId: 1, input1LinkId: 2, outputLinkId: 3,
  incrementValue: 0.1, defaultValue: null,
  input0Unit: 'rpm', input0UnitType: 0, input0DataType: 0,
  input1Unit: 'kpa', input1UnitType: 0, input1DataType: 0,
  outputUnit: 'deg', outputUnitType: 0, outputDataType: 0,
};

function data(overrides?: Partial<TD>): TD {
  return {
    enabled: true,
    input0: [1000, 2000, 3000, 4000],
    input1: [50, 75, 100],
    output: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
    ...overrides,
  };
}

const full = { startRow: 0, startCol: 0, endRow: 2, endCol: 3 };
const G = 'group-1';

describe('selBounds', () => {
  it('normalises reversed drag selections', () => {
    expect(selBounds({ startRow: 2, startCol: 3, endRow: 0, endCol: 1 })).toEqual({
      minRow: 0, maxRow: 2, minCol: 1, maxCol: 3,
    });
  });
  it('handles single-cell selections', () => {
    expect(selBounds({ startRow: 1, startCol: 2, endRow: 1, endCol: 2 })).toEqual({
      minRow: 1, maxRow: 1, minCol: 2, maxCol: 2,
    });
  });
});

describe('applyTransform (output cells)', () => {
  it('scale multiplies every selected cell and rounds to 2dp', () => {
    const d = data();
    const r = applyTransform('scale', { factor: 2 }, full, 'output', d, def, G)!;
    expect(r.tableData.output).toEqual([2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24]);
    expect(r.entries).toHaveLength(12);
    expect(r.entries.every(e => e.groupId === G)).toBe(true);
  });

  it('rounds fractional results', () => {
    const d = data({ output: [1.234, 2.5, 3.5, 4, 5, 6, 7, 8, 9, 10, 11, 12] });
    const r = applyTransform('scale', { factor: 2 }, full, 'output', d, def, G)!;
    expect(r.tableData.output[0]).toBe(2.47);
    expect(r.tableData.output[1]).toBe(5);
    expect(r.tableData.output[2]).toBe(7);
  });

  it('scale with factor 1 returns null (no changes)', () => {
    expect(applyTransform('scale', { factor: 1 }, full, 'output', data(), def, G)).toBeNull();
  });

  it('offset adds and rounds', () => {
    const r = applyTransform('offset', { offset: 1.5 }, { startRow: 0, startCol: 0, endRow: 0, endCol: 0 }, 'output', data(), def, G)!;
    expect(r.tableData.output[0]).toBe(2.5);
  });

  it('set overwrites cells', () => {
    const r = applyTransform('set', { value: 5 }, full, 'output', data(), def, G)!;
    expect(r.tableData.output).toEqual(Array(12).fill(5));
  });

  it('clamp clamps to min/max', () => {
    const r = applyTransform('clamp', { min: 4, max: 8 }, full, 'output', data(), def, G)!;
    expect(r.tableData.output).toEqual([4, 4, 4, 4, 5, 6, 7, 8, 8, 8, 8, 8]);
  });

  it('operates on a subset selection and leaves the rest untouched', () => {
    const d = data();
    const r = applyTransform('scale', { factor: 10 }, { startRow: 0, startCol: 1, endRow: 1, endCol: 2 }, 'output', d, def, G)!;
    expect(r.tableData.output).toEqual([1, 20, 30, 4, 5, 60, 70, 8, 9, 10, 11, 12]);
  });

  it('does not mutate the input tableData', () => {
    const d = data();
    applyTransform('scale', { factor: 2 }, full, 'output', d, def, G);
    expect(d.output).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);
  });

  it('fill interpolates horizontally in a single-row selection', () => {
    const d = data({ output: [1, 9, 9, 4, 5, 6, 7, 8, 9, 10, 11, 12] });
    const r = applyTransform('fill', {}, { startRow: 0, startCol: 0, endRow: 0, endCol: 3 }, 'output', d, def, G)!;
    expect(r.tableData.output.slice(0, 4)).toEqual([1, 2, 3, 4]);
  });

  it('fill interpolates vertically in a single-column selection', () => {
    const d = data({ output: [1, 2, 3, 4, 9, 6, 7, 8, 9, 10, 11, 12] });
    const r = applyTransform('fill', {}, { startRow: 0, startCol: 0, endRow: 2, endCol: 0 }, 'output', d, def, G)!;
    expect([r.tableData.output[0], r.tableData.output[4], r.tableData.output[8]]).toEqual([1, 5, 9]);
  });

  it('fill bilinear-interpolates the 2D interior and keeps corners', () => {
    const d = data({ output: [1, 9, 9, 4, 9, 9, 9, 9, 9, 9, 9, 12] });
    const r = applyTransform('fill', {}, full, 'output', d, def, G)!;
    const o = r.tableData.output;
    expect([o[0], o[3], o[8], o[11]]).toEqual([1, 4, 9, 12]); // corners untouched
    expect(o[1]).toBe(2); // (0,1)
    expect(o[2]).toBe(3); // (0,2)
    expect(o[4]).toBe(5); // (1,0)
    expect(o[5]).toBe(6); // (1,1)
    expect(o[6]).toBe(7); // (1,2)
    expect(o[9]).toBe(10); // (2,1)
    expect(o[10]).toBe(11); // (2,2)
  });

  it('fill on a single cell returns null', () => {
    expect(applyTransform('fill', {}, { startRow: 1, startCol: 1, endRow: 1, endCol: 1 }, 'output', data(), def, G)).toBeNull();
  });

  it('interpolate matches fill semantics', () => {
    const d = data({ output: [1, 9, 9, 4, 9, 9, 9, 9, 9, 9, 9, 12] });
    const r = applyTransform('interpolate', {}, full, 'output', d, def, G)!;
    expect(r.tableData.output[5]).toBe(6);
  });

  it('smooth averages the 3x3 neighbourhood (snapshot-based)', () => {
    const d = data({ output: [10, 10, 10, 10, 10, 0, 10, 10, 10, 10, 10, 10] });
    const r = applyTransform('smooth', { radius: 1 }, full, 'output', d, def, G)!;
    expect(r.tableData.output[5]).toBe(8.89); // (1,1): 80/9
  });

  it('gaussianSmooth applies a normalised kernel down columns', () => {
    const d = data({ output: [2, 2, 2, 2, 2, 6, 2, 2, 2, 10, 2, 2] });
    const r = applyTransform('gaussianSmooth', { sigma: 1 }, full, 'output', d, def, G)!;
    // Column 1 values 2,6,10, sigma=1 (kernel normalised over all 7 taps):
    // (0,1) ≈ 2.79, (1,1) ≈ 5.3, (2,1) ≈ 5.55
    expect(r.tableData.output[1]).toBeCloseTo(2.79, 2);
    expect(r.tableData.output[5]).toBeCloseTo(5.3, 2);
    expect(r.tableData.output[9]).toBeCloseTo(5.55, 2);
    // constant column still attenuated (kernel normalised over 7 taps, fewer valid at edges)
    expect(r.tableData.output[0]).toBeCloseTo(1.39, 2);
    expect(r.entries).toHaveLength(12);
  });

  it('mirrorH reverses each row of the selection', () => {
    const r = applyTransform('mirrorH', {}, full, 'output', data(), def, G)!;
    expect(r.tableData.output).toEqual([4, 3, 2, 1, 8, 7, 6, 5, 12, 11, 10, 9]);
  });

  it('mirrorV reverses each column of the selection', () => {
    const r = applyTransform('mirrorV', {}, full, 'output', data(), def, G)!;
    expect(r.tableData.output).toEqual([9, 10, 11, 12, 5, 6, 7, 8, 1, 2, 3, 4]);
  });

  it('copyRow copies the source row into all others', () => {
    const r = applyTransform('copyRow', { sourceRow: 0 }, full, 'output', data(), def, G)!;
    expect(r.tableData.output).toEqual([1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4]);
    expect(r.entries).toHaveLength(8);
  });

  it('copyRow clamps an out-of-range sourceRow', () => {
    const r = applyTransform('copyRow', { sourceRow: 99 }, full, 'output', data(), def, G)!;
    expect(r.tableData.output.slice(0, 4)).toEqual([9, 10, 11, 12]); // clamped to row 2
  });

  it('ramp fills the selection start→end in row-major order', () => {
    const r = applyTransform('ramp', { start: 0, end: 100 }, { startRow: 0, startCol: 0, endRow: 1, endCol: 1 }, 'output', data(), def, G)!;
    expect(r.tableData.output.slice(0, 2)).toEqual([0, 33.33]);
    expect([r.tableData.output[4], r.tableData.output[5]]).toEqual([66.67, 100]);
  });

  it('rowNormalize maps each row to 0–100', () => {
    const r = applyTransform('rowNormalize', {}, full, 'output', data(), def, G)!;
    expect(r.tableData.output.slice(0, 4)).toEqual([0, 33.33, 66.67, 100]);
  });

  it('rowNormalize maps a constant row to 50', () => {
    const d = data({ output: [5, 5, 5, 5, 5, 6, 7, 8, 9, 10, 11, 12] });
    const r = applyTransform('rowNormalize', {}, full, 'output', d, def, G)!;
    expect(r.tableData.output.slice(0, 4)).toEqual([50, 50, 50, 50]);
  });

  it('colNormalize maps each column to 0–100', () => {
    const r = applyTransform('colNormalize', {}, full, 'output', data(), def, G)!;
    expect([r.tableData.output[0], r.tableData.output[4], r.tableData.output[8]]).toEqual([0, 50, 100]);
  });

  it('conditionalScale only touches values meeting the condition (gt)', () => {
    const r = applyTransform('conditionalScale', { threshold: 5, factor: 2, condOp: 0 }, full, 'output', data(), def, G)!;
    expect(r.tableData.output).toEqual([1, 2, 3, 4, 5, 12, 14, 16, 18, 20, 22, 24]);
  });

  it('conditionalScale only touches values meeting the condition (lt)', () => {
    const r = applyTransform('conditionalScale', { threshold: 5, factor: 0.5, condOp: 1 }, full, 'output', data(), def, G)!;
    expect(r.tableData.output).toEqual([0.5, 1, 1.5, 2, 5, 6, 7, 8, 9, 10, 11, 12]);
  });

  it('conditionalScale only touches values meeting the condition (eq)', () => {
    const r = applyTransform('conditionalScale', { threshold: 7, factor: 2, condOp: 2 }, full, 'output', data(), def, G)!;
    expect(r.tableData.output[6]).toBe(14);
    expect(r.tableData.output[0]).toBe(1); // untouched
  });
});

describe('applyTransform (axis selections)', () => {
  it('input0 scale writes new input0 and leaves output untouched', () => {
    const d = data();
    const r = applyTransform('scale', { factor: 2 }, { startRow: 0, startCol: 0, endRow: 0, endCol: 3 }, 'input0', d, def, G)!;
    expect(r.tableData.input0).toEqual([2000, 4000, 6000, 8000]);
    expect(r.tableData.output).toEqual(d.output);
    expect(r.entries[0]).toMatchObject({ type: 'input0', key: 'input0[0]', idx: 0, oldVal: 1000, newVal: 2000 });
  });

  it('input1 offset writes new input1 (row indices)', () => {
    const r = applyTransform('offset', { offset: 10 }, { startRow: 0, startCol: 0, endRow: 2, endCol: 0 }, 'input1', data(), def, G)!;
    expect(r.tableData.input1).toEqual([60, 85, 110]);
    expect(r.entries[0]).toMatchObject({ type: 'input1', idx: 0 });
  });

  it('axis mirror reverses the selected range', () => {
    const r = applyTransform('mirrorH', {}, { startRow: 0, startCol: 0, endRow: 0, endCol: 3 }, 'input0', data(), def, G)!;
    expect(r.tableData.input0).toEqual([4000, 3000, 2000, 1000]);
  });

  it('axis ramp interpolates start→end across the range', () => {
    const r = applyTransform('ramp', { start: 0, end: 3000 }, { startRow: 0, startCol: 0, endRow: 0, endCol: 3 }, 'input0', data(), def, G)!;
    expect(r.tableData.input0).toEqual([0, 1000, 2000, 3000]);
  });

  it('axis smooth averages neighbours at the edges only', () => {
    const r = applyTransform('smooth', { radius: 1 }, { startRow: 0, startCol: 0, endRow: 0, endCol: 3 }, 'input0', data(), def, G)!;
    expect(r.tableData.input0).toEqual([1500, 2000, 3000, 3500]);
  });

  it('axis gaussianSmooth keeps the series monotonic and bounded', () => {
    const d = data();
    const r = applyTransform('gaussianSmooth', { sigma: 1.5, radius: 3 }, { startRow: 0, startCol: 0, endRow: 0, endCol: 3 }, 'input0', d, def, G)!;
    const out = r.tableData.input0;
    expect(out[0]).toBeCloseTo(1864.3, 0);
    expect(out[3]).toBeCloseTo(3135.7, 0);
    expect(out.every(v => v >= 1000 && v <= 4000)).toBe(true);
    expect(out[0] < out[1] && out[1] < out[2] && out[2] < out[3]).toBe(true);
  });

  it('axis rowNormalize maps the input1 range to 0–100', () => {
    const r = applyTransform('rowNormalize', {}, { startRow: 0, startCol: 0, endRow: 2, endCol: 0 }, 'input1', data(), def, G)!;
    expect(r.tableData.input1).toEqual([0, 50, 100]);
  });

  it('axis conditionalScale only scales qualifying values', () => {
    const r = applyTransform('conditionalScale', { threshold: 2500, factor: 2, condOp: 0 }, { startRow: 0, startCol: 0, endRow: 0, endCol: 3 }, 'input0', data(), def, G)!;
    expect(r.tableData.input0).toEqual([1000, 2000, 6000, 8000]);
  });

  it('axis copyRow copies the (clamped) source value', () => {
    const r = applyTransform('copyRow', { source: 1 }, { startRow: 0, startCol: 0, endRow: 0, endCol: 3 }, 'input0', data(), def, G)!;
    expect(r.tableData.input0).toEqual([2000, 2000, 2000, 2000]);
    expect(r.entries).toHaveLength(3);
  });
});

describe('deriveSelectionType', () => {
  it('column-0 vertical drag selects input1 axis', () => {
    expect(deriveSelectionType({ row: 0, col: 0 }, 2, 0, def)).toBe('input1');
  });
  it('row-0 horizontal drag selects input0 axis', () => {
    expect(deriveSelectionType({ row: 0, col: 0 }, 0, 3, def)).toBe('input0');
  });
  it('interior drag selects output', () => {
    expect(deriveSelectionType({ row: 1, col: 1 }, 2, 2, def)).toBe('output');
  });
  it('single-cell drag selects output', () => {
    expect(deriveSelectionType({ row: 0, col: 0 }, 0, 0, def)).toBe('output');
  });
  it('null tableDef selects output', () => {
    expect(deriveSelectionType({ row: 0, col: 0 }, 2, 0, null)).toBe('output');
  });
  it('1D table always selects output', () => {
    const oneDim = { ...def, rows: 1, tableType: 'T1x16' };
    expect(deriveSelectionType({ row: 0, col: 0 }, 0, 5, oneDim)).toBe('output');
  });
});

describe('handleSelectionComplete', () => {
  it('derives the type for 2D tables', () => {
    const r = handleSelectionComplete({ row: 0, col: 0 }, 2, 0, def);
    expect(r.selection).toEqual({ startRow: 0, startCol: 0, endRow: 2, endCol: 0 });
    expect(r.selectionType).toBe('input1');
  });
  it('keeps the current selection type for 1D tables', () => {
    const oneDim = { ...def, rows: 1, tableType: 'T1x16' };
    const r = handleSelectionComplete({ row: 0, col: 1 }, 0, 3, oneDim, 'output');
    expect(r.selectionType).toBe('output');
  });
});

describe('tableUndoRedo', () => {
  it('canUndo/canRedo reflect stack state', () => {
    const s = createUndoRedoState();
    expect(canUndo(s)).toBe(false);
    expect(canRedo(s)).toBe(false);
    pushUndo(s, [{ type: 'output', key: '0,0', row: 0, col: 0, oldVal: 1, newVal: 2, groupId: 'g1' }]);
    expect(canUndo(s)).toBe(true);
  });

  it('pushUndo clears the redo stack', () => {
    const s = createUndoRedoState();
    pushUndo(s, [{ type: 'output', key: '0,0', row: 0, col: 0, oldVal: 1, newVal: 2, groupId: 'g1' }]);
    const first = undo(s, data(), def)!;
    expect(canRedo(s)).toBe(true);
    pushUndo(s, [{ type: 'output', key: '0,0', row: 0, col: 0, oldVal: 1, newVal: 3, groupId: 'g2' }]);
    expect(canRedo(s)).toBe(false);
    expect(first).not.toBeNull();
  });

  it('undo reverts a whole group and redo re-applies it', () => {
    const s = createUndoRedoState();
    const d = data({ output: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12] });
    pushUndo(s, [
      { type: 'output', key: '0,0', row: 0, col: 0, oldVal: 1, newVal: 99, groupId: 'g1' },
      { type: 'output', key: '1,1', row: 1, col: 1, oldVal: 6, newVal: 77, groupId: 'g1' },
    ]);
    const reverted = undo(s, d, def)!;
    expect(reverted.output[0]).toBe(1);
    expect(reverted.output[5]).toBe(6);
    expect(canUndo(s)).toBe(false);
    const redone = redo(s, reverted, def)!;
    expect(redone.output[0]).toBe(99);
    expect(redone.output[5]).toBe(77);
  });

  it('undo pops only the latest group', () => {
    const s = createUndoRedoState();
    pushUndo(s, [
      { type: 'output', key: '0,0', row: 0, col: 0, oldVal: 1, newVal: 2, groupId: 'g1' },
      { type: 'output', key: '0,1', row: 0, col: 1, oldVal: 2, newVal: 3, groupId: 'g2' },
    ]);
    undo(s, data(), def);
    expect(s.undoStack.map(e => e.groupId)).toEqual(['g1']);
  });

  it('undo restores mixed output/input0/input1 entries', () => {
    const s = createUndoRedoState();
    const d = data();
    pushUndo(s, [
      { type: 'output', key: '1,2', row: 1, col: 2, oldVal: 7, newVal: 77, groupId: 'g1' },
      { type: 'input0', key: 'input0[2]', idx: 2, oldVal: 3000, newVal: 3300, groupId: 'g1' },
      { type: 'input1', key: 'input1[1]', idx: 1, oldVal: 75, newVal: 80, groupId: 'g1' },
    ]);
    const r = undo(s, d, def)!;
    expect(r.output[6]).toBe(7);
    expect(r.input0[2]).toBe(3000);
    expect(r.input1[1]).toBe(75);
    const rr = redo(s, r, def)!;
    expect(rr.output[6]).toBe(77);
    expect(rr.input0[2]).toBe(3300);
    expect(rr.input1[1]).toBe(80);
  });

  it('undo/redo on empty stacks return null', () => {
    const s = createUndoRedoState();
    expect(undo(s, data(), def)).toBeNull();
    expect(redo(s, data(), def)).toBeNull();
  });

  it('recalculateDirty finds changed cells and axes', () => {
    const orig = data();
    const modified = data({
      input0: [1000, 2000, 2500, 4000],
      input1: [50, 80, 100],
      output: [1, 2, 3, 4, 5, 6, 99, 8, 9, 10, 11, 12],
    });
    const dirty = recalculateDirty(modified, orig, def);
    expect(dirty.dirtyCells.has('1,2')).toBe(true);
    expect(dirty.dirtyCells.has('0,0')).toBe(false);
    expect(dirty.dirtyCells.size).toBe(1);
    expect([...dirty.dirtyInput0]).toEqual([2]);
    expect([...dirty.dirtyInput1]).toEqual([1]);
  });
});

describe('tableUndoRedo session cache', () => {
  let store: Map<string, string>;

  beforeEach(() => {
    store = new Map();
    vi.stubGlobal('localStorage', {
      getItem: (k: string) => store.get(k) ?? null,
      setItem: (k: string, v: string) => void store.set(k, v),
      removeItem: (k: string) => void store.delete(k),
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('save/load round-trips through the in-memory cache', () => {
    const d = data();
    saveSessionCache(42, [{ type: 'output', key: '0,0', row: 0, col: 0, oldVal: 1, newVal: 2, groupId: 'g' }], [], d, [{ groupId: 'g', label: 'x', timestamp: 1 }]);
    const loaded = loadSessionCache(42)!;
    expect(loaded.undoStack).toHaveLength(1);
    expect(loaded.originalData.output).toEqual(d.output);
    expect(loaded.bookmarks).toHaveLength(1);
  });

  it('persists to localStorage and survives a fresh table id', () => {
    const d = data();
    saveSessionCache(7, [], [], d);
    const key = 'table-undo-7';
    expect(store.has(key)).toBe(true);
    const parsed = JSON.parse(store.get(key)!);
    expect(parsed.originalData.output).toEqual(d.output);
  });

  it('prunes undo history beyond 100 groups but keeps bookmarked groups', () => {
    const d = data();
    const entries: UndoEntry[] = [];
    for (let i = 0; i < 105; i++) {
      entries.push({ type: 'output', key: '0,0', row: 0, col: 0, oldVal: 0, newVal: i, groupId: `g${i}` });
    }
    saveSessionCache(9, entries, [], d, [{ groupId: 'g0', label: 'keep', timestamp: 1 }]);
    const payload = JSON.parse(store.get('table-undo-9')!) as { undoStack: UndoEntry[] };
    const groupIds = new Set(payload.undoStack.map(e => e.groupId));
    expect(groupIds.has('g0')).toBe(true); // bookmarked survives
    expect(groupIds.has('g4')).toBe(false); // oldest non-bookmarked pruned
    expect(groupIds.has('g104')).toBe(true);
  });

  it('clearSessionCache removes memory and localStorage entries', () => {
    const d = data();
    saveSessionCache(11, [], [], d);
    clearSessionCache(11);
    expect(store.has('table-undo-11')).toBe(false);
    expect(loadSessionCache(11)).toBeUndefined();
  });

  it('corrupt localStorage payload falls back to undefined', () => {
    store.set('table-undo-3', '{not json');
    expect(loadSessionCache(3)).toBeUndefined();
  });

  it('missing localStorage entry returns undefined', () => {
    expect(loadSessionCache(12345)).toBeUndefined();
  });
});

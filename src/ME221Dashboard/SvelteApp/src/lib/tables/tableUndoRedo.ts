import { cellKey, getOutputValue } from './types';
import type { TableData, TableDefinition } from './types';

export interface UndoEntry {
  type: string;
  key: string;
  row?: number;
  col?: number;
  idx?: number;
  oldVal: number;
  newVal: number;
  groupId: string;
}

export interface UndoRedoState {
  undoStack: UndoEntry[];
  redoStack: UndoEntry[];
}

export function createUndoRedoState(): UndoRedoState {
  return { undoStack: [], redoStack: [] };
}

export function pushUndo(
  state: UndoRedoState,
  entries: UndoEntry[],
): void {
  state.undoStack = [...state.undoStack, ...entries];
  state.redoStack = [];
}

export function canUndo(state: UndoRedoState): boolean {
  return state.undoStack.length > 0;
}

export function canRedo(state: UndoRedoState): boolean {
  return state.redoStack.length > 0;
}

export function undo(
  state: UndoRedoState,
  tableData: TableData,
  tableDef: TableDefinition,
): TableData | null {
  if (state.undoStack.length === 0) return null;

  const groupId = state.undoStack[state.undoStack.length - 1].groupId;
  const entries: UndoEntry[] = [];
  let newOutput = [...tableData.output];
  let newInput0 = [...tableData.input0];
  let newInput1 = [...tableData.input1];

  while (state.undoStack.length > 0 && state.undoStack[state.undoStack.length - 1].groupId === groupId) {
    const entry = state.undoStack.pop()!;
    entries.push(entry);
    if (entry.type === 'output' && entry.row !== undefined && entry.col !== undefined) {
      newOutput[entry.row * tableDef.cols + entry.col] = entry.oldVal;
    } else if (entry.type === 'input0' && entry.idx !== undefined) {
      newInput0[entry.idx] = entry.oldVal;
    } else if (entry.type === 'input1' && entry.idx !== undefined) {
      newInput1[entry.idx] = entry.oldVal;
    }
  }

  state.undoStack = [...state.undoStack];
  state.redoStack = [...state.redoStack, ...entries];

  return { ...tableData, output: newOutput, input0: newInput0, input1: newInput1 };
}

export function redo(
  state: UndoRedoState,
  tableData: TableData,
  tableDef: TableDefinition,
): TableData | null {
  if (state.redoStack.length === 0) return null;

  const groupId = state.redoStack[state.redoStack.length - 1].groupId;
  const entries: UndoEntry[] = [];
  let newOutput = [...tableData.output];
  let newInput0 = [...tableData.input0];
  let newInput1 = [...tableData.input1];

  while (state.redoStack.length > 0 && state.redoStack[state.redoStack.length - 1].groupId === groupId) {
    const entry = state.redoStack.pop()!;
    entries.push(entry);
    if (entry.type === 'output' && entry.row !== undefined && entry.col !== undefined) {
      newOutput[entry.row * tableDef.cols + entry.col] = entry.newVal;
    } else if (entry.type === 'input0' && entry.idx !== undefined) {
      newInput0[entry.idx] = entry.newVal;
    } else if (entry.type === 'input1' && entry.idx !== undefined) {
      newInput1[entry.idx] = entry.newVal;
    }
  }

  state.redoStack = [...state.redoStack];
  state.undoStack = [...state.undoStack, ...entries];

  return { ...tableData, output: newOutput, input0: newInput0, input1: newInput1 };
}

export function recalculateDirty(
  tableData: TableData,
  originalData: TableData,
  tableDef: TableDefinition,
): { dirtyCells: Set<string>; dirtyInput0: Set<number>; dirtyInput1: Set<number> } {
  const dirtyCells = new Set<string>();
  for (let r = 0; r < tableDef.rows; r++) {
    for (let c = 0; c < tableDef.cols; c++) {
      const key = cellKey(r, c);
      const current = getOutputValue(tableData, r, c, tableDef.cols);
      const original = getOutputValue(originalData, r, c, tableDef.cols);
      if (current !== original) dirtyCells.add(key);
    }
  }

  const dirtyInput0 = new Set<number>();
  for (let c = 0; c < tableData.input0.length; c++) {
    if (tableData.input0[c] !== originalData.input0[c]) dirtyInput0.add(c);
  }

  const dirtyInput1 = new Set<number>();
  for (let r = 0; r < tableData.input1.length; r++) {
    if (tableData.input1[r] !== originalData.input1[r]) dirtyInput1.add(r);
  }

  return { dirtyCells, dirtyInput0, dirtyInput1 };
}

// ─── Session-scoped cache (survives page navigation, lost on app reload) ───

interface TableSessionCache {
  undoStack: UndoEntry[];
  redoStack: UndoEntry[];
  originalData: TableData;
}

const _sessionCache = new Map<number, TableSessionCache>();

export function loadSessionCache(tableId: number): TableSessionCache | undefined {
  return _sessionCache.get(tableId);
}

export function saveSessionCache(
  tableId: number,
  undoStack: UndoEntry[],
  redoStack: UndoEntry[],
  originalData: TableData,
): void {
  _sessionCache.set(tableId, { undoStack, redoStack, originalData });
}

export function clearSessionCache(tableId: number): void {
  _sessionCache.delete(tableId);
}

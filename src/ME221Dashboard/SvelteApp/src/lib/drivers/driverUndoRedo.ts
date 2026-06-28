/**
 * Driver undo/redo module — mirrors tableUndoRedo pattern with driver-specific entry shape.
 */

export interface DriverUndoEntry {
  type: 'config' | 'outputLink' | 'inputLink';
  index: number;
  oldVal: number;
  newVal: number;
  groupId: string;
}

export interface DriverUndoRedoState {
  undoStack: DriverUndoEntry[];
  redoStack: DriverUndoEntry[];
}

export function createDriverUndoRedoState(): DriverUndoRedoState {
  return { undoStack: [], redoStack: [] };
}

export function pushDriverUndo(
  state: DriverUndoRedoState,
  entries: DriverUndoEntry[],
): void {
  state.undoStack = [...state.undoStack, ...entries];
  state.redoStack = [];
}

export function canDriverUndo(state: DriverUndoRedoState): boolean {
  return state.undoStack.length > 0;
}

export function canDriverRedo(state: DriverUndoRedoState): boolean {
  return state.redoStack.length > 0;
}

export interface DriverUndoResult {
  configs: number[];
  outputLinkIds: number[];
  inputLinkIds: number[];
}

export function driverUndo(
  state: DriverUndoRedoState,
  current: DriverUndoResult,
): DriverUndoResult | null {
  if (state.undoStack.length === 0) return null;

  const groupId = state.undoStack[state.undoStack.length - 1].groupId;
  const entries: DriverUndoEntry[] = [];
  const configs = [...current.configs];
  const outputLinkIds = [...current.outputLinkIds];
  const inputLinkIds = [...current.inputLinkIds];

  while (state.undoStack.length > 0 && state.undoStack[state.undoStack.length - 1].groupId === groupId) {
    const entry = state.undoStack.pop()!;
    entries.push(entry);
    if (entry.type === 'config') {
      configs[entry.index] = entry.oldVal;
    } else if (entry.type === 'outputLink') {
      outputLinkIds[entry.index] = entry.oldVal;
    } else if (entry.type === 'inputLink') {
      inputLinkIds[entry.index] = entry.oldVal;
    }
  }

  state.undoStack = [...state.undoStack];
  state.redoStack = [...state.redoStack, ...entries];

  return { configs, outputLinkIds, inputLinkIds };
}

export function driverRedo(
  state: DriverUndoRedoState,
  current: DriverUndoResult,
): DriverUndoResult | null {
  if (state.redoStack.length === 0) return null;

  const groupId = state.redoStack[state.redoStack.length - 1].groupId;
  const entries: DriverUndoEntry[] = [];
  const configs = [...current.configs];
  const outputLinkIds = [...current.outputLinkIds];
  const inputLinkIds = [...current.inputLinkIds];

  while (state.redoStack.length > 0 && state.redoStack[state.redoStack.length - 1].groupId === groupId) {
    const entry = state.redoStack.pop()!;
    entries.push(entry);
    if (entry.type === 'config') {
      configs[entry.index] = entry.newVal;
    } else if (entry.type === 'outputLink') {
      outputLinkIds[entry.index] = entry.newVal;
    } else if (entry.type === 'inputLink') {
      inputLinkIds[entry.index] = entry.newVal;
    }
  }

  state.redoStack = [...state.redoStack];
  state.undoStack = [...state.undoStack, ...entries];

  return { configs, outputLinkIds, inputLinkIds };
}

let _groupIdCounter = 0;

export function nextDriverGroupId(): string {
  return `driver-${++_groupIdCounter}`;
}

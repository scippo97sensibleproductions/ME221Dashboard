import { is1DTable } from './types';
import type { TableDefinition } from './types';

export interface Selection {
  startRow: number;
  startCol: number;
  endRow: number;
  endCol: number;
}

export interface Anchor {
  row: number;
  col: number;
}

export function deriveSelectionType(
  anchor: Anchor,
  row: number,
  col: number,
  tableDef: TableDefinition | null,
): 'output' | 'input0' | 'input1' {
  if (!tableDef) return 'output';
  if (is1DTable(tableDef)) return 'output';

  const minR = Math.min(anchor.row, row);
  const maxR = Math.max(anchor.row, row);
  const minC = Math.min(anchor.col, col);
  const maxC = Math.max(anchor.col, col);

  if (minC === 0 && maxC === 0 && maxR > minR) {
    return 'input1';
  } else if (minR === 0 && maxR === 0 && maxC > minC) {
    return 'input0';
  } else if (minR === 0 && maxR === 0 && minC === 0 && maxC === 0) {
    return 'output';
  }
  return 'output';
}

export function handleSelectionComplete(
  anchor: Anchor,
  row: number,
  col: number,
  tableDef: TableDefinition | null,
  currentSelectionType: 'output' | 'input0' | 'input1' = 'output',
): { selection: Selection; selectionType: 'output' | 'input0' | 'input1' } {
  const selection: Selection = {
    startRow: anchor.row,
    startCol: anchor.col,
    endRow: row,
    endCol: col,
  };
  // For 1D tables, keep the anchor's selection type — all cells are in row 0
  // so coordinate-based re-derivation doesn't work.
  const selectionType = tableDef && is1DTable(tableDef)
    ? currentSelectionType
    : deriveSelectionType(anchor, row, col, tableDef);
  return { selection, selectionType };
}

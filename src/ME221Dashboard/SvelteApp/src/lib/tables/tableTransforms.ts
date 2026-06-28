import { cellKey, getOutputValue } from './types';
import type { TableData, TableDefinition } from './types';
import type { UndoEntry } from './tableUndoRedo';

export interface SelectionBounds {
  minRow: number;
  maxRow: number;
  minCol: number;
  maxCol: number;
}

export function selBounds(selection: { startRow: number; startCol: number; endRow: number; endCol: number }): SelectionBounds {
  return {
    minRow: Math.min(selection.startRow, selection.endRow),
    maxRow: Math.max(selection.startRow, selection.endRow),
    minCol: Math.min(selection.startCol, selection.endCol),
    maxCol: Math.max(selection.startCol, selection.endCol),
  };
}

export function applyTransform(
  operation: string,
  params: Record<string, number>,
  selection: { startRow: number; startCol: number; endRow: number; endCol: number },
  selectionType: 'output' | 'input0' | 'input1',
  tableData: TableData,
  tableDef: TableDefinition,
  groupId: string,
): { tableData: TableData; entries: UndoEntry[] } | null {
  const b = selBounds(selection);

  // Header (axis) selection — transform axis values
  if (selectionType === 'input0' || selectionType === 'input1') {
    const axis = selectionType === 'input0' ? [...tableData.input0] : [...tableData.input1];
    const indices = selectionType === 'input0'
      ? Array.from({ length: b.maxCol - b.minCol + 1 }, (_, i) => b.minCol + i)
      : Array.from({ length: b.maxRow - b.minRow + 1 }, (_, i) => b.minRow + i);
    const entries: UndoEntry[] = [];

    for (const idx of indices) {
      const oldVal = axis[idx];
      let newVal = oldVal;
      if (operation === 'scale') newVal = oldVal * (params.factor ?? 1);
      else if (operation === 'offset') newVal = oldVal + (params.offset ?? 0);
      else if (operation === 'set') newVal = params.value ?? oldVal;
      else if (operation === 'clamp') newVal = Math.max(params.min ?? 0, Math.min(params.max ?? 100, oldVal));
      else if (operation === 'fill' || operation === 'interpolate') {
        const firstIdx = indices[0];
        const lastIdx = indices[indices.length - 1];
        const firstVal = axis[firstIdx];
        const lastVal = axis[lastIdx];
        const span = lastIdx - firstIdx;
        const t = span === 0 ? 0 : (idx - firstIdx) / span;
        newVal = Math.round((firstVal + (lastVal - firstVal) * t) * 100) / 100;
      } else if (operation === 'smooth') {
        let sum = 0, count = 0;
        for (let d = -1; d <= 1; d++) {
          const ni = idx + d;
          if (ni >= 0 && ni < axis.length) { sum += axis[ni]; count++; }
        }
        newVal = Math.round((sum / count) * 100) / 100;
      }
      newVal = Math.round(newVal * 100) / 100;
      if (oldVal === newVal) continue;
      axis[idx] = newVal;
      entries.push({ type: selectionType, key: `${selectionType}[${idx}]`, idx, oldVal, newVal, groupId });
    }

    if (entries.length > 0) {
      const newData = selectionType === 'input0'
        ? { ...tableData, input0: axis }
        : { ...tableData, input1: axis };
      return { tableData: newData, entries };
    }
    return null;
  }

  // Output (data) selection
  const newOutput = [...tableData.output];
  const entries: UndoEntry[] = [];
  const cols = tableDef.cols;

  if (operation === 'smooth') {
    const snapshot = [...tableData.output];
    for (let r = b.minRow; r <= b.maxRow; r++) {
      for (let c = b.minCol; c <= b.maxCol; c++) {
        let sum = 0, count = 0;
        for (let dr = -1; dr <= 1; dr++) {
          for (let dc = -1; dc <= 1; dc++) {
            const nr = r + dr, nc = c + dc;
            if (nr >= 0 && nr < tableDef.rows && nc >= 0 && nc < cols) {
              sum += snapshot[nr * cols + nc];
              count++;
            }
          }
        }
        const oldVal = snapshot[r * cols + c];
        let newVal = Math.round((sum / count) * 100) / 100;
        if (oldVal === newVal) continue;
        newOutput[r * cols + c] = newVal;
        entries.push({ type: 'output', key: cellKey(r, c), row: r, col: c, oldVal, newVal, groupId });
      }
    }
  } else if (operation === 'fill') {
    const rowSpan = b.maxRow - b.minRow;
    const colSpan = b.maxCol - b.minCol;

    if (rowSpan === 0 && colSpan === 0) {
      // Single cell — nothing to fill
    } else if (rowSpan === 0) {
      const leftVal = getOutputValue(tableData, b.minRow, b.minCol, cols);
      const rightVal = getOutputValue(tableData, b.minRow, b.maxCol, cols);
      for (let c = b.minCol + 1; c < b.maxCol; c++) {
        const t = (c - b.minCol) / colSpan;
        let newVal = Math.round((leftVal + (rightVal - leftVal) * t) * 100) / 100;
        const oldVal = getOutputValue(tableData, b.minRow, c, cols);
        if (oldVal === newVal) continue;
        newOutput[b.minRow * cols + c] = newVal;
        entries.push({ type: 'output', key: cellKey(b.minRow, c), row: b.minRow, col: c, oldVal, newVal, groupId });
      }
    } else if (colSpan === 0) {
      const topVal = getOutputValue(tableData, b.minRow, b.minCol, cols);
      const bottomVal = getOutputValue(tableData, b.maxRow, b.minCol, cols);
      for (let r = b.minRow + 1; r < b.maxRow; r++) {
        const t = (r - b.minRow) / rowSpan;
        let newVal = Math.round((topVal + (bottomVal - topVal) * t) * 100) / 100;
        const oldVal = getOutputValue(tableData, r, b.minCol, cols);
        if (oldVal === newVal) continue;
        newOutput[r * cols + b.minCol] = newVal;
        entries.push({ type: 'output', key: cellKey(r, b.minCol), row: r, col: b.minCol, oldVal, newVal, groupId });
      }
    } else {
      const topLeft = getOutputValue(tableData, b.minRow, b.minCol, cols);
      const topRight = getOutputValue(tableData, b.minRow, b.maxCol, cols);
      const bottomLeft = getOutputValue(tableData, b.maxRow, b.minCol, cols);
      const bottomRight = getOutputValue(tableData, b.maxRow, b.maxCol, cols);
      for (let r = b.minRow; r <= b.maxRow; r++) {
        for (let c = b.minCol; c <= b.maxCol; c++) {
          if ((r === b.minRow && c === b.minCol) ||
              (r === b.minRow && c === b.maxCol) ||
              (r === b.maxRow && c === b.minCol) ||
              (r === b.maxRow && c === b.maxCol)) continue;
          const ty = (r - b.minRow) / rowSpan;
          const tx = (c - b.minCol) / colSpan;
          const top = topLeft + (topRight - topLeft) * tx;
          const bottom = bottomLeft + (bottomRight - bottomLeft) * tx;
          let newVal = Math.round((top + (bottom - top) * ty) * 100) / 100;
          const oldVal = getOutputValue(tableData, r, c, cols);
          if (oldVal === newVal) continue;
          newOutput[r * cols + c] = newVal;
          entries.push({ type: 'output', key: cellKey(r, c), row: r, col: c, oldVal, newVal, groupId });
        }
      }
    }
  } else if (operation === 'interpolate') {
    const rowSpan = b.maxRow - b.minRow;
    const colSpan = b.maxCol - b.minCol;

    if (rowSpan === 0 && colSpan === 0) {
      // Single cell — nothing to interpolate
    } else if (rowSpan === 0) {
      const leftVal = getOutputValue(tableData, b.minRow, b.minCol, cols);
      const rightVal = getOutputValue(tableData, b.minRow, b.maxCol, cols);
      for (let c = b.minCol + 1; c < b.maxCol; c++) {
        const t = (c - b.minCol) / colSpan;
        const newVal = Math.round((leftVal + (rightVal - leftVal) * t) * 100) / 100;
        const oldVal = getOutputValue(tableData, b.minRow, c, cols);
        if (oldVal === newVal) continue;
        newOutput[b.minRow * cols + c] = newVal;
        entries.push({ type: 'output', key: cellKey(b.minRow, c), row: b.minRow, col: c, oldVal, newVal, groupId });
      }
    } else if (colSpan === 0) {
      const topVal = getOutputValue(tableData, b.minRow, b.minCol, cols);
      const bottomVal = getOutputValue(tableData, b.maxRow, b.minCol, cols);
      for (let r = b.minRow + 1; r < b.maxRow; r++) {
        const t = (r - b.minRow) / rowSpan;
        const newVal = Math.round((topVal + (bottomVal - topVal) * t) * 100) / 100;
        const oldVal = getOutputValue(tableData, r, b.minCol, cols);
        if (oldVal === newVal) continue;
        newOutput[r * cols + b.minCol] = newVal;
        entries.push({ type: 'output', key: cellKey(r, b.minCol), row: r, col: b.minCol, oldVal, newVal, groupId });
      }
    } else {
      const topLeft = getOutputValue(tableData, b.minRow, b.minCol, cols);
      const topRight = getOutputValue(tableData, b.minRow, b.maxCol, cols);
      const bottomLeft = getOutputValue(tableData, b.maxRow, b.minCol, cols);
      const bottomRight = getOutputValue(tableData, b.maxRow, b.maxCol, cols);
      for (let r = b.minRow; r <= b.maxRow; r++) {
        for (let c = b.minCol; c <= b.maxCol; c++) {
          if ((r === b.minRow && c === b.minCol) ||
              (r === b.minRow && c === b.maxCol) ||
              (r === b.maxRow && c === b.minCol) ||
              (r === b.maxRow && c === b.maxCol)) continue;
          const ty = (r - b.minRow) / rowSpan;
          const tx = (c - b.minCol) / colSpan;
          const top = topLeft + (topRight - topLeft) * tx;
          const bottom = bottomLeft + (bottomRight - bottomLeft) * tx;
          const newVal = Math.round((top + (bottom - top) * ty) * 100) / 100;
          const oldVal = getOutputValue(tableData, r, c, cols);
          if (oldVal === newVal) continue;
          newOutput[r * cols + c] = newVal;
          entries.push({ type: 'output', key: cellKey(r, c), row: r, col: c, oldVal, newVal, groupId });
        }
      }
    }
  } else {
    // Per-cell operations: scale, offset, set, clamp
    for (let r = b.minRow; r <= b.maxRow; r++) {
      for (let c = b.minCol; c <= b.maxCol; c++) {
        const oldVal = getOutputValue(tableData, r, c, cols);
        let newVal = oldVal;
        if (operation === 'scale') {
          newVal = oldVal * (params.factor ?? 1);
        } else if (operation === 'offset') {
          newVal = oldVal + (params.offset ?? 0);
        } else if (operation === 'set') {
          newVal = params.value ?? oldVal;
        } else if (operation === 'clamp') {
          newVal = Math.max(params.min ?? 0, Math.min(params.max ?? 100, oldVal));
        }
        newVal = Math.round(newVal * 100) / 100;
        if (oldVal === newVal) continue;
        newOutput[r * cols + c] = newVal;
        entries.push({ type: 'output', key: cellKey(r, c), row: r, col: c, oldVal, newVal, groupId });
      }
    }
  }

  if (entries.length > 0) {
    return { tableData: { ...tableData, output: newOutput }, entries };
  }
  return null;
}

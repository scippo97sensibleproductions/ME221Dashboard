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
    const snapshot = [...axis];

    // Operations that need a snapshot (read-modify-write)
    if (operation === 'mirrorH' || operation === 'mirrorV') {
      // Reverse the selected range
      for (let i = 0; i < Math.floor(indices.length / 2); i++) {
        const a = indices[i], bIdx = indices[indices.length - 1 - i];
        const oldA = snapshot[a], oldB = snapshot[bIdx];
        if (oldA === oldB) continue;
        axis[a] = oldB;
        axis[bIdx] = oldA;
        entries.push({ type: selectionType, key: `${selectionType}[${a}]`, idx: a, oldVal: oldA, newVal: oldB, groupId });
        entries.push({ type: selectionType, key: `${selectionType}[${bIdx}]`, idx: bIdx, oldVal: oldB, newVal: oldA, groupId });
      }
    } else if (operation === 'gaussianSmooth') {
      const sigma = params.sigma ?? 1.5;
      const maxRadius = params.radius ?? 5;
      const radius = Math.min(Math.max(1, Math.ceil(sigma * 3)), maxRadius);
      for (const idx of indices) {
        const oldVal = snapshot[idx];
        let sum = 0, weightSum = 0;
        for (let d = -radius; d <= radius; d++) {
          const ni = idx + d;
          if (ni >= 0 && ni < axis.length) {
            const w = Math.exp(-(d * d) / (2 * sigma * sigma));
            sum += snapshot[ni] * w;
            weightSum += w;
          }
        }
        const newVal = Math.round((sum / weightSum) * 100) / 100;
        if (oldVal === newVal) continue;
        axis[idx] = newVal;
        entries.push({ type: selectionType, key: `${selectionType}[${idx}]`, idx, oldVal, newVal, groupId });
      }
    } else if (operation === 'smooth') {
      const radius = params.radius ?? 1;
      for (const idx of indices) {
        const oldVal = snapshot[idx];
        let sum = 0, count = 0;
        for (let d = -radius; d <= radius; d++) {
          const ni = idx + d;
          if (ni >= 0 && ni < axis.length) { sum += snapshot[ni]; count++; }
        }
        const newVal = Math.round((sum / count) * 100) / 100;
        if (oldVal === newVal) continue;
        axis[idx] = newVal;
        entries.push({ type: selectionType, key: `${selectionType}[${idx}]`, idx, oldVal, newVal, groupId });
      }
    } else if (operation === 'ramp') {
      const firstIdx = indices[0], lastIdx = indices[indices.length - 1];
      const startVal = params.start ?? snapshot[firstIdx];
      const endVal = params.end ?? snapshot[lastIdx];
      const span = lastIdx - firstIdx;
      for (const idx of indices) {
        const oldVal = snapshot[idx];
        const t = span === 0 ? 0 : (idx - firstIdx) / span;
        const newVal = Math.round((startVal + (endVal - startVal) * t) * 100) / 100;
        if (oldVal === newVal) continue;
        axis[idx] = newVal;
        entries.push({ type: selectionType, key: `${selectionType}[${idx}]`, idx, oldVal, newVal, groupId });
      }
    } else if (operation === 'copyRow') {
      const sourceIdx = indices[Math.min(params.source ?? 0, indices.length - 1)];
      const sourceVal = snapshot[sourceIdx];
      for (const idx of indices) {
        if (idx === sourceIdx) continue;
        const oldVal = snapshot[idx];
        if (oldVal === sourceVal) continue;
        axis[idx] = sourceVal;
        entries.push({ type: selectionType, key: `${selectionType}[${idx}]`, idx, oldVal, newVal: sourceVal, groupId });
      }
    } else if (operation === 'rowNormalize' || operation === 'colNormalize') {
      let mn = Infinity, mx = -Infinity;
      for (const idx of indices) { if (snapshot[idx] < mn) mn = snapshot[idx]; if (snapshot[idx] > mx) mx = snapshot[idx]; }
      const range = mx - mn || 1;
      for (const idx of indices) {
        const oldVal = snapshot[idx];
        const newVal = Math.round(((oldVal - mn) / range) * 100 * 100) / 100;
        if (oldVal === newVal) continue;
        axis[idx] = newVal;
        entries.push({ type: selectionType, key: `${selectionType}[${idx}]`, idx, oldVal, newVal, groupId });
      }
    } else if (operation === 'conditionalScale') {
      const threshold = params.threshold ?? 0;
      const condOp = params.condOp ?? 0; // 0=greater than, 1=less than, 2=equal to
      const scaleFactor = params.factor ?? 1;
      for (const idx of indices) {
        const oldVal = snapshot[idx];
        const meetsCondition = condOp === 0 ? oldVal > threshold : condOp === 1 ? oldVal < threshold : Math.abs(oldVal - threshold) < 0.001;
        if (!meetsCondition) continue;
        const newVal = Math.round(oldVal * scaleFactor * 100) / 100;
        if (oldVal === newVal) continue;
        axis[idx] = newVal;
        entries.push({ type: selectionType, key: `${selectionType}[${idx}]`, idx, oldVal, newVal, groupId });
      }
    } else {
      // Simple per-element operations: scale, offset, set, clamp, fill, interpolate
      for (const idx of indices) {
        const oldVal = snapshot[idx];
        let newVal = oldVal;
        if (operation === 'scale') newVal = oldVal * (params.factor ?? 1);
        else if (operation === 'offset') newVal = oldVal + (params.offset ?? 0);
        else if (operation === 'set') newVal = params.value ?? oldVal;
        else if (operation === 'clamp') newVal = Math.max(params.min ?? 0, Math.min(params.max ?? 100, oldVal));
        else if (operation === 'fill' || operation === 'interpolate') {
          const firstIdx = indices[0];
          const lastIdx = indices[indices.length - 1];
          const firstVal = snapshot[firstIdx];
          const lastVal = snapshot[lastIdx];
          const span = lastIdx - firstIdx;
          const t = span === 0 ? 0 : (idx - firstIdx) / span;
          newVal = Math.round((firstVal + (lastVal - firstVal) * t) * 100) / 100;
        }
        newVal = Math.round(newVal * 100) / 100;
        if (oldVal === newVal) continue;
        axis[idx] = newVal;
        entries.push({ type: selectionType, key: `${selectionType}[${idx}]`, idx, oldVal, newVal, groupId });
      }
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
    const radius = params.radius ?? 1;
    const snapshot = [...tableData.output];
    for (let r = b.minRow; r <= b.maxRow; r++) {
      for (let c = b.minCol; c <= b.maxCol; c++) {
        let sum = 0, count = 0;
        for (let dr = -radius; dr <= radius; dr++) {
          for (let dc = -radius; dc <= radius; dc++) {
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
  } else if (operation === 'rowNormalize') {
    for (let r = b.minRow; r <= b.maxRow; r++) {
      let rowMin = Infinity, rowMax = -Infinity;
      for (let c = b.minCol; c <= b.maxCol; c++) {
        const v = getOutputValue(tableData, r, c, cols);
        if (v < rowMin) rowMin = v;
        if (v > rowMax) rowMax = v;
      }
      const span = rowMax - rowMin;
      for (let c = b.minCol; c <= b.maxCol; c++) {
        const oldVal = getOutputValue(tableData, r, c, cols);
        const newVal = span === 0 ? 50 : Math.round(((oldVal - rowMin) / span) * 100 * 100) / 100;
        if (oldVal === newVal) continue;
        newOutput[r * cols + c] = newVal;
        entries.push({ type: 'output', key: cellKey(r, c), row: r, col: c, oldVal, newVal, groupId });
      }
    }
  } else if (operation === 'colNormalize') {
    for (let c = b.minCol; c <= b.maxCol; c++) {
      let colMin = Infinity, colMax = -Infinity;
      for (let r = b.minRow; r <= b.maxRow; r++) {
        const v = getOutputValue(tableData, r, c, cols);
        if (v < colMin) colMin = v;
        if (v > colMax) colMax = v;
      }
      const span = colMax - colMin;
      for (let r = b.minRow; r <= b.maxRow; r++) {
        const oldVal = getOutputValue(tableData, r, c, cols);
        const newVal = span === 0 ? 50 : Math.round(((oldVal - colMin) / span) * 100 * 100) / 100;
        if (oldVal === newVal) continue;
        newOutput[r * cols + c] = newVal;
        entries.push({ type: 'output', key: cellKey(r, c), row: r, col: c, oldVal, newVal, groupId });
      }
    }
  } else if (operation === 'gaussianSmooth') {
    const sigma = params.sigma ?? 1.0;
    const maxRadius = params.radius ?? 5;
    const snapshot = [...tableData.output];
    const kernelSize = Math.min(Math.ceil(sigma * 3), maxRadius) * 2 + 1;
    const half = Math.floor(kernelSize / 2);
    const kernel: number[] = [];
    let kSum = 0;
    for (let i = 0; i < kernelSize; i++) {
      const d = i - half;
      const w = Math.exp(-(d * d) / (2 * sigma * sigma));
      kernel.push(w);
      kSum += w;
    }
    for (let i = 0; i < kernelSize; i++) kernel[i] /= kSum;
    for (let r = b.minRow; r <= b.maxRow; r++) {
      for (let c = b.minCol; c <= b.maxCol; c++) {
        let sum = 0;
        for (let k = 0; k < kernelSize; k++) {
          const nr = r + k - half;
          if (nr >= 0 && nr < tableDef.rows) {
            sum += snapshot[nr * cols + c] * kernel[k];
          }
        }
        const oldVal = snapshot[r * cols + c];
        const newVal = Math.round(sum * 100) / 100;
        if (oldVal === newVal) continue;
        newOutput[r * cols + c] = newVal;
        entries.push({ type: 'output', key: cellKey(r, c), row: r, col: c, oldVal, newVal, groupId });
      }
    }
  } else if (operation === 'mirrorH') {
    for (let r = b.minRow; r <= b.maxRow; r++) {
      for (let c = b.minCol; c < b.minCol + Math.floor((b.maxCol - b.minCol + 1) / 2); c++) {
        const mirrorC = b.maxCol - (c - b.minCol);
        const oldVal = getOutputValue(tableData, r, c, cols);
        const mirrorVal = getOutputValue(tableData, r, mirrorC, cols);
        if (oldVal === mirrorVal) continue;
        newOutput[r * cols + c] = mirrorVal;
        newOutput[r * cols + mirrorC] = oldVal;
        entries.push({ type: 'output', key: cellKey(r, c), row: r, col: c, oldVal, newVal: mirrorVal, groupId });
        entries.push({ type: 'output', key: cellKey(r, mirrorC), row: r, col: mirrorC, oldVal: mirrorVal, newVal: oldVal, groupId });
      }
    }
  } else if (operation === 'mirrorV') {
    for (let c = b.minCol; c <= b.maxCol; c++) {
      for (let r = b.minRow; r < b.minRow + Math.floor((b.maxRow - b.minRow + 1) / 2); r++) {
        const mirrorR = b.maxRow - (r - b.minRow);
        const oldVal = getOutputValue(tableData, r, c, cols);
        const mirrorVal = getOutputValue(tableData, mirrorR, c, cols);
        if (oldVal === mirrorVal) continue;
        newOutput[r * cols + c] = mirrorVal;
        newOutput[mirrorR * cols + c] = oldVal;
        entries.push({ type: 'output', key: cellKey(r, c), row: r, col: c, oldVal, newVal: mirrorVal, groupId });
        entries.push({ type: 'output', key: cellKey(mirrorR, c), row: mirrorR, col: c, oldVal: mirrorVal, newVal: oldVal, groupId });
      }
    }
  } else if (operation === 'copyRow') {
    const sourceRow = Math.max(b.minRow, Math.min(b.maxRow, Math.round(params.sourceRow ?? b.minRow)));
    const srcVals: number[] = [];
    for (let c = b.minCol; c <= b.maxCol; c++) {
      srcVals.push(getOutputValue(tableData, sourceRow, c, cols));
    }
    for (let r = b.minRow; r <= b.maxRow; r++) {
      if (r === sourceRow) continue;
      for (let ci = 0; ci < srcVals.length; ci++) {
        const c = b.minCol + ci;
        const oldVal = getOutputValue(tableData, r, c, cols);
        const newVal = srcVals[ci];
        if (oldVal === newVal) continue;
        newOutput[r * cols + c] = newVal;
        entries.push({ type: 'output', key: cellKey(r, c), row: r, col: c, oldVal, newVal, groupId });
      }
    }
  } else if (operation === 'ramp') {
    const startVal = params.start ?? 0;
    const endVal = params.end ?? 100;
    const totalCells = (b.maxRow - b.minRow + 1) * (b.maxCol - b.minCol + 1);
    let idx = 0;
    for (let r = b.minRow; r <= b.maxRow; r++) {
      for (let c = b.minCol; c <= b.maxCol; c++) {
        const t = totalCells <= 1 ? 0 : idx / (totalCells - 1);
        const oldVal = getOutputValue(tableData, r, c, cols);
        const newVal = Math.round((startVal + (endVal - startVal) * t) * 100) / 100;
        if (oldVal === newVal) { idx++; continue; }
        newOutput[r * cols + c] = newVal;
        entries.push({ type: 'output', key: cellKey(r, c), row: r, col: c, oldVal, newVal, groupId });
        idx++;
      }
    }
  } else if (operation === 'conditionalScale') {
    const factor = params.factor ?? 1;
    const threshold = params.threshold ?? 0;
    const condOp = params.condOp ?? 0; // 0=gt, 1=lt, 2=eq
    for (let r = b.minRow; r <= b.maxRow; r++) {
      for (let c = b.minCol; c <= b.maxCol; c++) {
        const oldVal = getOutputValue(tableData, r, c, cols);
        let matches = false;
        if (condOp === 0) matches = oldVal > threshold;
        else if (condOp === 1) matches = oldVal < threshold;
        else matches = Math.abs(oldVal - threshold) < 0.001;
        if (!matches) continue;
        const newVal = Math.round(oldVal * factor * 100) / 100;
        if (oldVal === newVal) continue;
        newOutput[r * cols + c] = newVal;
        entries.push({ type: 'output', key: cellKey(r, c), row: r, col: c, oldVal, newVal, groupId });
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

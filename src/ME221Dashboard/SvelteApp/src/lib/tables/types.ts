// ─── Table Types ─────────────────────────────────────────────────────────────

export interface TableDefinition {
  id: number;
  name: string;
  category: string;
  viewInTree: boolean;
  enabled: boolean;
  tableType: string;
  cols: number;
  rows: number;
  input0Name: string;
  input1Name: string;
  outputName: string;
  input0LinkId: number;
  input1LinkId: number;
  outputLinkId: number;
  incrementValue: number;
  defaultValue: number | null;
}

export interface TableData {
  enabled: boolean;
  input0: number[];
  input1: number[];
  output: number[];
}

export interface OperatingPoint {
  rpm: number | null;
  map: number | null;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

export function is1DTable(table: TableDefinition): boolean {
  return table.tableType === 'T1x16' || table.tableType === 'T1x32';
}

export function cellKey(row: number, col: number): string {
  return `${row},${col}`;
}

export function getOutputValue(data: TableData, row: number, col: number, cols: number): number {
  return data.output[row * cols + col];
}

export function heatColor(value: number, min: number, max: number): string {
  if (max === min) return 'rgb(34, 139, 230)';
  const t = Math.max(0, Math.min(1, (value - min) / (max - min)));
  if (t < 0.5) {
    const s = t * 2;
    const r = Math.round(34 + (245 - 34) * s);
    const g = Math.round(139 + (159 - 139) * s);
    const b = Math.round(230 + (0 - 230) * s);
    return `rgb(${r}, ${g}, ${b})`;
  }
  const s = (t - 0.5) * 2;
  const r = Math.round(245 + (224 - 245) * s);
  const g = Math.round(159 + (49 - 159) * s);
  const b = Math.round(0 + (49 - 0) * s);
  return `rgb(${r}, ${g}, ${b})`;
}

export function findNearestIndex(value: number, axis: number[]): number {
  if (axis.length === 0) return 0;
  let best = 0;
  let bestDist = Math.abs(value - axis[0]);
  for (let i = 1; i < axis.length; i++) {
    const dist = Math.abs(value - axis[i]);
    if (dist < bestDist) {
      bestDist = dist;
      best = i;
    }
  }
  return best;
}

export function getDataRange(output: number[]): { min: number; max: number } {
  if (output.length === 0) return { min: 0, max: 100 };
  let min = Infinity;
  let max = -Infinity;
  for (let i = 0; i < output.length; i++) {
    if (output[i] < min) min = output[i];
    if (output[i] > max) max = output[i];
  }
  return { min, max };
}

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
  input0Unit: string;
  input0UnitType: number;
  input0DataType: number;
  input1Unit: string;
  input1UnitType: number;
  input1DataType: number;
  outputUnit: string;
  outputUnitType: number;
  outputDataType: number;
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
  output: number | null;
}

// ─── MeasurementUnitType Enum (mirrors C# flags enum) ───────────────────────

export enum MeasurementUnitType {
  Unknown = 0,
  Raw = 1,
  Volt = 2,
  Ohm = 4,
  KPa = 8,
  PSI = 16,
  Celsius = 32,
  Fahrenheit = 64,
  Percent = 128,
  Rpm = 256,
  Deg = 512,
  Ms = 1024,
  Bar = 2048,
}

const RAW_MAX = 65535;
const VOLT_MAX = 5;
const INTERNAL_RESISTANCE = 2700;
const PSI_TO_KPA = 6.89476;

/**
 * Convert raw ECU float to display value. Mirrors C# MeasurementUnitConverter.FromRaw().
 */
export function fromRaw(raw: number, unitType: MeasurementUnitType): number {
  if (unitType & MeasurementUnitType.Volt)
    return VOLT_MAX * raw / RAW_MAX;
  if (unitType & MeasurementUnitType.Ohm) {
    if (raw >= RAW_MAX) return Infinity;
    return INTERNAL_RESISTANCE * raw / (RAW_MAX - raw);
  }
  if (unitType & MeasurementUnitType.PSI)
    return raw / PSI_TO_KPA;
  if (unitType & MeasurementUnitType.Fahrenheit)
    return raw * 1.8 + 32;
  return raw;
}

// ─── DataType Enum ───────────────────────────────────────────────────────────

export enum DataType {
  Normal = 0,
  TrimModPercent = 1,
  Percent = 2,
  ADCRaw = 3,
}

// ─── Value Formatting ───────────────────────────────────────────────────────

/**
 * Format a display value based on its DataType.
 */
export function formatValue(value: number, dataType: DataType, decimalPlaces: number = 2): string {
  switch (dataType) {
    case DataType.TrimModPercent: {
      const trimmed = value - 1;
      return trimmed < 0
        ? `${trimmed.toFixed(2)} %`
        : `+${trimmed.toFixed(2)} %`;
    }
    case DataType.Percent:
      return `${value.toFixed(2)} %`;
    default:
      return value.toFixed(decimalPlaces);
  }
}

/**
 * Format a value with adaptive precision based on magnitude.
 */
export function formatValueAdaptive(value: number, dataType: DataType): string {
  const abs = Math.abs(value);
  let decimals: number;
  if (abs >= 100) decimals = 0;
  else if (abs >= 10) decimals = 1;
  else decimals = 2;
  return formatValue(value, dataType, decimals);
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

// ─── Color Schemes ───────────────────────────────────────────────────────────

export type ColorScheme = 'thermal' | 'viridis' | 'grayscale' | 'ember';

interface ColorStop { pos: number; r: number; g: number; b: number; }

const SCHEMES: Record<ColorScheme, ColorStop[]> = {
  thermal: [
    { pos: 0, r: 34, g: 139, b: 230 },
    { pos: 0.5, r: 245, g: 159, b: 0 },
    { pos: 1, r: 224, g: 49, b: 49 },
  ],
  viridis: [
    { pos: 0, r: 68, g: 1, b: 84 },
    { pos: 0.25, r: 59, g: 82, b: 139 },
    { pos: 0.5, r: 33, g: 144, b: 140 },
    { pos: 0.75, r: 94, g: 201, b: 98 },
    { pos: 1, r: 253, g: 231, b: 37 },
  ],
  grayscale: [
    { pos: 0, r: 20, g: 20, b: 20 },
    { pos: 1, r: 220, g: 220, b: 220 },
  ],
  ember: [
    { pos: 0, r: 10, g: 10, b: 10 },
    { pos: 0.5, r: 200, g: 50, b: 0 },
    { pos: 1, r: 255, g: 200, b: 100 },
  ],
};

function interpolateStops(stops: ColorStop[], t: number): string {
  if (!stops || stops.length === 0) return 'rgb(128, 128, 128)';
  if (t <= stops[0].pos) return `rgb(${stops[0].r}, ${stops[0].g}, ${stops[0].b})`;
  if (t >= stops[stops.length - 1].pos) {
    const s = stops[stops.length - 1];
    return `rgb(${s.r}, ${s.g}, ${s.b})`;
  }
  for (let i = 0; i < stops.length - 1; i++) {
    if (t >= stops[i].pos && t <= stops[i + 1].pos) {
      const range = stops[i + 1].pos - stops[i].pos;
      const localT = range === 0 ? 0 : (t - stops[i].pos) / range;
      const r = Math.round(stops[i].r + (stops[i + 1].r - stops[i].r) * localT);
      const g = Math.round(stops[i].g + (stops[i + 1].g - stops[i].g) * localT);
      const b = Math.round(stops[i].b + (stops[i + 1].b - stops[i].b) * localT);
      return `rgb(${r}, ${g}, ${b})`;
    }
  }
  const last = stops[stops.length - 1];
  return `rgb(${last.r}, ${last.g}, ${last.b})`;
}

export function heatColor(value: number, min: number, max: number, scheme: ColorScheme = 'thermal'): string {
  if (max === min) return interpolateStops(SCHEMES[scheme], 0.5);
  const t = Math.max(0, Math.min(1, (value - min) / (max - min)));
  return interpolateStops(SCHEMES[scheme], t);
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

// ─── Interpolation Range ─────────────────────────────────────────────────────

export interface InterpolationRange {
  lower: number;
  upper: number;
  fraction: number;
}

export function findInterpolationRange(value: number, axis: number[]): InterpolationRange {
  if (axis.length === 0) return { lower: 0, upper: 0, fraction: 0 };
  if (axis.length === 1) return { lower: 0, upper: 0, fraction: 0 };

  if (value <= axis[0]) return { lower: 0, upper: 0, fraction: 0 };

  if (value >= axis[axis.length - 1]) {
    const last = axis.length - 1;
    return { lower: last, upper: last, fraction: 0 };
  }

  for (let i = 0; i < axis.length - 1; i++) {
    if (value >= axis[i] && value <= axis[i + 1]) {
      const span = axis[i + 1] - axis[i];
      const fraction = span === 0 ? 0 : (value - axis[i]) / span;
      return { lower: i, upper: i + 1, fraction };
    }
  }

  const last = axis.length - 1;
  return { lower: last, upper: last, fraction: 0 };
}

export function rangeOpacity(range: InterpolationRange, cellIndex: number): number {
  if (range.lower === range.upper) {
    return cellIndex === range.lower ? 1 : 0;
  }
  if (cellIndex === range.lower) return 1 - range.fraction;
  if (cellIndex === range.upper) return range.fraction;
  return 0;
}

export function getDataRange(output: number[]): { min: number; max: number } {
  if (output.length === 0) return { min: 0, max: 100 };
  let min = Infinity;
  let max = -Infinity;
  for (let i = 0; i < output.length; i++) {
    const v = output[i];
    if (!Number.isFinite(v)) continue;
    if (v < min) min = v;
    if (v > max) max = v;
  }
  if (!isFinite(min) || !isFinite(max)) return { min: 0, max: 100 };
  return { min, max };
}

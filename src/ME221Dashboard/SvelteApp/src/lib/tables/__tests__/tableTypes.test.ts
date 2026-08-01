import { describe, it, expect } from 'vitest';
import {
  fromRaw,
  formatValue,
  formatValueAdaptive,
  is1DTable,
  cellKey,
  getOutputValue,
  heatColor,
  findNearestIndex,
  findInterpolationRange,
  rangeOpacity,
  getDataRange,
  MeasurementUnitType,
  DataType,
  type TableDefinition,
  type TableData,
} from '../types';

const TOL = 0.01;

describe('fromRaw', () => {
  it('converts Volt to 0-5V scale', () => {
    expect(fromRaw(0, MeasurementUnitType.Volt)).toBeCloseTo(0, 5);
    expect(fromRaw(32767.5, MeasurementUnitType.Volt)).toBeCloseTo(2.5, 5);
    expect(fromRaw(65535, MeasurementUnitType.Volt)).toBeCloseTo(5, 5);
  });

  it('converts Ohm via internal resistance', () => {
    expect(fromRaw(32767.5, MeasurementUnitType.Ohm)).toBeCloseTo(2700, 0);
  });

  it('Ohm saturates at raw max → Infinity', () => {
    expect(fromRaw(65535, MeasurementUnitType.Ohm)).toBe(Infinity);
  });

  it('converts PSI to kPa', () => {
    expect(fromRaw(6894.76, MeasurementUnitType.PSI)).toBeCloseTo(1000, 0);
  });

  it('converts Fahrenheit from Celsius raw', () => {
    expect(fromRaw(0, MeasurementUnitType.Fahrenheit)).toBe(32);
    expect(fromRaw(100, MeasurementUnitType.Fahrenheit)).toBe(212);
    expect(fromRaw(-40, MeasurementUnitType.Fahrenheit)).toBe(-40);
  });

  it('passes through passthrough units', () => {
    for (const unit of [MeasurementUnitType.KPa, MeasurementUnitType.Celsius, MeasurementUnitType.Rpm, MeasurementUnitType.Bar]) {
      expect(fromRaw(123.5, unit)).toBe(123.5);
    }
  });

  it('flag combos: Volt wins over KPa', () => {
    expect(fromRaw(32767.5, MeasurementUnitType.Volt | MeasurementUnitType.KPa)).toBeCloseTo(2.5, 5);
  });
});

describe('formatValue', () => {
  it('TrimModPercent subtracts one and formats sign (×100, mirrors C#)', () => {
    expect(formatValue(0.85, DataType.TrimModPercent)).toBe('-15.0 %');
    expect(formatValue(1.15, DataType.TrimModPercent)).toBe('+15.0 %');
    expect(formatValue(0.995, DataType.TrimModPercent)).toBe('-0.5 %');
  });

  it('Percent always uses two decimals', () => {
    expect(formatValue(50.5, DataType.Percent)).toBe('50.50 %');
  });

  it('Normal respects decimal places', () => {
    expect(formatValue(3.14159, DataType.Normal)).toBe('3.14');
    expect(formatValue(3.14159, DataType.Normal, 3)).toBe('3.142');
    expect(formatValue(3.14159, DataType.Normal, 0)).toBe('3');
  });
});

describe('formatValueAdaptive', () => {
  it('reduces decimals for large magnitudes', () => {
    expect(formatValueAdaptive(123.456, DataType.Normal)).toBe('123');
    expect(formatValueAdaptive(12.345, DataType.Normal)).toBe('12.3');
    expect(formatValueAdaptive(1.2345, DataType.Normal)).toBe('1.23');
    expect(formatValueAdaptive(-1.2345, DataType.Normal)).toBe('-1.23');
  });

  it('percent formatting ignores magnitude precision (fixed 2 dp)', () => {
    expect(formatValueAdaptive(123.4, DataType.Percent)).toBe('123.40 %');
  });
});

describe('is1DTable / cellKey / getOutputValue', () => {
  const def = (tableType: string): TableDefinition => ({
    id: 1, name: '', category: '', viewInTree: true, enabled: true,
    tableType, cols: 16, rows: 1,
    input0Name: '', input1Name: '', outputName: '',
    input0LinkId: 1, input1LinkId: 0, outputLinkId: 3,
    incrementValue: 0.1, defaultValue: null,
    input0Unit: '', input0UnitType: 0, input0DataType: 0,
    input1Unit: '', input1UnitType: 0, input1DataType: 0,
    outputUnit: '', outputUnitType: 0, outputDataType: 0,
  });

  it('is1DTable recognizes 1x16 and 1x32 only', () => {
    expect(is1DTable(def('T1x16'))).toBe(true);
    expect(is1DTable(def('T1x32'))).toBe(true);
    expect(is1DTable(def('T16x16'))).toBe(false);
    expect(is1DTable(def('T32x32'))).toBe(false);
  });

  it('cellKey builds row,col keys', () => {
    expect(cellKey(3, 7)).toBe('3,7');
    expect(cellKey(0, 0)).toBe('0,0');
  });

  it('getOutputValue indexes row-major', () => {
    const data: TableData = { enabled: true, input0: [], input1: [], output: [0, 1, 2, 3, 4, 5] };
    expect(getOutputValue(data, 1, 2, 3)).toBe(5);
    expect(getOutputValue(data, 0, 1, 3)).toBe(1);
  });
});

describe('heatColor', () => {
  it('clamps values below min and above max', () => {
    expect(heatColor(-1000, 0, 100)).toBe('rgb(34, 139, 230)');   // thermal min
    expect(heatColor(1000, 0, 100)).toBe('rgb(224, 49, 49)');     // thermal max
  });

  it('zero range returns midpoint color', () => {
    expect(heatColor(50, 50, 50)).toBe('rgb(245, 159, 0)'); // thermal 0.5
  });

  it('thermal midpoint is orange', () => {
    expect(heatColor(50, 0, 100)).toBe('rgb(245, 159, 0)');
  });

  it('thermal quarter is interpolated', () => {
    expect(heatColor(25, 0, 100)).toBe('rgb(140, 149, 115)');
  });

  it('grayscale min and max', () => {
    expect(heatColor(0, 0, 100, 'grayscale')).toBe('rgb(20, 20, 20)');
    expect(heatColor(100, 0, 100, 'grayscale')).toBe('rgb(220, 220, 220)');
    expect(heatColor(50, 0, 100, 'grayscale')).toBe('rgb(120, 120, 120)');
  });

  it('viridis min and max', () => {
    expect(heatColor(0, 0, 100, 'viridis')).toBe('rgb(68, 1, 84)');
    expect(heatColor(100, 0, 100, 'viridis')).toBe('rgb(253, 231, 37)');
  });

  it('ember min and max', () => {
    expect(heatColor(0, 0, 100, 'ember')).toBe('rgb(10, 10, 10)');
    expect(heatColor(100, 0, 100, 'ember')).toBe('rgb(255, 200, 100)');
  });
});

describe('findNearestIndex', () => {
  it('returns 0 for empty axis', () => {
    expect(findNearestIndex(5, [])).toBe(0);
  });

  it('finds nearest index', () => {
    expect(findNearestIndex(2050, [0, 1000, 2000, 3000])).toBe(2);
    expect(findNearestIndex(1100, [0, 1000, 2000, 3000])).toBe(1);
    expect(findNearestIndex(-500, [0, 1000, 2000, 3000])).toBe(0);
    expect(findNearestIndex(99999, [0, 1000, 2000, 3000])).toBe(3);
  });

  it('ties resolve to the first (lower) index', () => {
    expect(findNearestIndex(1500, [0, 1000, 2000])).toBe(1); // equidistant → keeps 1? 1000 vs 2000 both 500 → lower wins
  });
});

describe('findInterpolationRange', () => {
  const axis = [0, 1000, 2000, 4000, 8000];

  it('below first axis value clamps to lower bound', () => {
    expect(findInterpolationRange(-100, axis)).toEqual({ lower: 0, upper: 0, fraction: 0 });
  });

  it('above last axis value clamps to last index', () => {
    expect(findInterpolationRange(99999, axis)).toEqual({ lower: 4, upper: 4, fraction: 0 });
  });

  it('exact axis values return the containing segment with fraction 1', () => {
    // The loop matches the segment [i, i+1] where value ≤ axis[i+1];
    // an exact value sits at the segment's upper end → fraction 1.
    expect(findInterpolationRange(2000, axis)).toEqual({ lower: 1, upper: 2, fraction: 1 });
    expect(findInterpolationRange(1000, axis)).toEqual({ lower: 0, upper: 1, fraction: 1 });
  });

  it('interpolates between axis values', () => {
    const r = findInterpolationRange(2500, axis);
    expect(r).toEqual({ lower: 2, upper: 3, fraction: 0.25 });
  });

  it('handles non-uniform spacing', () => {
    const r = findInterpolationRange(3000, axis); // between 2000 and 4000, span 2000
    expect(r).toEqual({ lower: 2, upper: 3, fraction: 0.5 });
  });

  it('empty and single-element axes return zeroed range', () => {
    expect(findInterpolationRange(5, [])).toEqual({ lower: 0, upper: 0, fraction: 0 });
    expect(findInterpolationRange(5, [100])).toEqual({ lower: 0, upper: 0, fraction: 0 });
  });

  it('duplicate axis values do not divide by zero', () => {
    expect(() => findInterpolationRange(0, [0, 0, 1000])).not.toThrow();
    const r = findInterpolationRange(0, [0, 0, 1000]);
    expect(Number.isFinite(r.fraction)).toBe(true);
  });
});

describe('rangeOpacity', () => {
  it('point range lights only that cell', () => {
    expect(rangeOpacity({ lower: 2, upper: 2, fraction: 0 }, 2)).toBe(1);
    expect(rangeOpacity({ lower: 2, upper: 2, fraction: 0 }, 1)).toBe(0);
  });

  it('range weights lower/upper by fraction', () => {
    const r = { lower: 1, upper: 2, fraction: 0.25 };
    expect(rangeOpacity(r, 1)).toBeCloseTo(0.75, 5);
    expect(rangeOpacity(r, 2)).toBeCloseTo(0.25, 5);
    expect(rangeOpacity(r, 3)).toBe(0);
  });
});

describe('getDataRange', () => {
  it('empty array returns default range', () => {
    expect(getDataRange([])).toEqual({ min: 0, max: 100 });
  });

  it('finds min/max', () => {
    expect(getDataRange([5, -3, 100, 42])).toEqual({ min: -3, max: 100 });
  });

  it('single value range', () => {
    expect(getDataRange([7])).toEqual({ min: 7, max: 7 });
  });

  it('ignores non-finite values', () => {
    expect(getDataRange([1, Infinity, -Infinity, NaN, 5])).toEqual({ min: 1, max: 5 });
  });

  it('all non-finite returns default range', () => {
    expect(getDataRange([NaN, Infinity])).toEqual({ min: 0, max: 100 });
  });
});

import { describe, it, expect } from 'vitest';
import {
  computeValueFraction,
  interpolateNeedleAngle,
  buildColorLuts,
  gaugeValueColor,
  positionToCenterAngle,
  describeArc,
  formatValue,
  computeWarningState,
  estimateVisualSize,
} from '../gaugeUtils';
import { GaugeShapeCategory, DigitalStyle, ArcPosition } from '../gaugeTypes';
import type { DataLinkWarningSetting } from '../../HybridBridgeTypes';

function warning(partial: Partial<DataLinkWarningSetting>): DataLinkWarningSetting {
  return { dataId: 1, enabled: true, minWarning: null, maxWarning: null, ...partial } as DataLinkWarningSetting;
}

describe('computeValueFraction', () => {
  it('clamps to [0, 1] and handles zero/negative ranges', () => {
    expect(computeValueFraction(50, 0, 100)).toBe(0.5);
    expect(computeValueFraction(150, 0, 100)).toBe(1);
    expect(computeValueFraction(-10, 0, 100)).toBe(0);
    expect(computeValueFraction(5, 5, 5)).toBe(0);
    expect(computeValueFraction(5, 10, 5)).toBe(0);
  });
});

describe('interpolateNeedleAngle', () => {
  const curve = [
    { rawValue: 0, angle: 0 },
    { rawValue: 100, angle: 90 },
  ];

  it('handles empty and single-point curves', () => {
    expect(interpolateNeedleAngle(50, [])).toBe(0);
    expect(interpolateNeedleAngle(50, [{ rawValue: 10, angle: 45 }])).toBe(45);
  });

  it('clamps below and above the curve', () => {
    expect(interpolateNeedleAngle(-5, curve)).toBe(0);
    expect(interpolateNeedleAngle(500, curve)).toBe(90);
  });

  it('interpolates linearly between points', () => {
    expect(interpolateNeedleAngle(50, curve)).toBe(45);
    expect(interpolateNeedleAngle(25, curve)).toBe(22.5);
  });

  it('matches exact points', () => {
    expect(interpolateNeedleAngle(0, curve)).toBe(0);
    expect(interpolateNeedleAngle(100, curve)).toBe(90);
  });

  it('handles duplicate raw values (zero span)', () => {
    const c = [{ rawValue: 0, angle: 10 }, { rawValue: 0, angle: 20 }, { rawValue: 100, angle: 90 }];
    expect(interpolateNeedleAngle(0, c)).toBe(10);
  });
});

describe('buildColorLuts', () => {
  const stops = [
    { fraction: 0, r: 0, g: 0, b: 0 },
    { fraction: 0.5, r: 255, g: 255, b: 255 },
    { fraction: 1, r: 255, g: 0, b: 0 },
  ];

  it('produces identical LUTs with zero hysteresis', () => {
    const luts = buildColorLuts(stops, 0);
    expect(luts.increasing).toHaveLength(512);
    expect(luts.decreasing).toEqual(luts.increasing);
  });

  it('shifts internal stops for the decreasing LUT but keeps endpoints', () => {
    const luts = buildColorLuts(stops, 0.2);
    expect(luts.decreasing[0]).toBe(luts.increasing[0]);
    expect(luts.decreasing[511]).toBe(luts.increasing[511]);
    expect(luts.decreasing.some((c, i) => c !== luts.increasing[i])).toBe(true);
  });

  it('clamps shifted fractions into [0, 1]', () => {
    const luts = buildColorLuts(stops, 0.9);
    expect(luts.decreasing).toHaveLength(512);
    expect(luts.decreasing[511]).toBe('rgb(255,0,0)');
  });
});

describe('gaugeValueColor', () => {
  const luts = buildColorLuts([
    { fraction: 0, r: 0, g: 0, b: 0 },
    { fraction: 1, r: 255, g: 255, b: 255 },
  ], 0);

  it('uses the increasing LUT when rising or without history', () => {
    expect(gaugeValueColor(0.5, undefined, luts)).toBe(luts.increasing[(0.5 * 511 + 0.5) | 0]);
    expect(gaugeValueColor(0.5, 0.2, luts)).toBe(luts.increasing[(0.5 * 511 + 0.5) | 0]);
  });

  it('uses the decreasing LUT when falling', () => {
    expect(gaugeValueColor(0.5, 0.8, luts)).toBe(luts.decreasing[(0.5 * 511 + 0.5) | 0]);
  });

  it('clamps the index at the endpoints', () => {
    expect(gaugeValueColor(0, 0.5, luts)).toBe(luts.decreasing[0]);
    expect(gaugeValueColor(1, 0.5, luts)).toBe(luts.decreasing[511]);
  });
});

describe('positionToCenterAngle', () => {
  it('maps every position and defaults unknown values', () => {
    expect(positionToCenterAngle(ArcPosition.Top)).toBe(270);
    expect(positionToCenterAngle(ArcPosition.TopRight)).toBe(315);
    expect(positionToCenterAngle(ArcPosition.Right)).toBe(0);
    expect(positionToCenterAngle(ArcPosition.BottomRight)).toBe(45);
    expect(positionToCenterAngle(ArcPosition.Bottom)).toBe(90);
    expect(positionToCenterAngle(ArcPosition.BottomLeft)).toBe(135);
    expect(positionToCenterAngle(ArcPosition.Left)).toBe(180);
    expect(positionToCenterAngle(ArcPosition.TopLeft)).toBe(225);
    expect(positionToCenterAngle(999 as ArcPosition)).toBe(270);
  });
});

describe('describeArc', () => {
  function parse(path: string): { x1: number; y1: number; largeArc: string; sweep: string; x2: number; y2: number } {
    // 'M x1 y1 A r r rot largeArc sweep x2 y2'
    const p = path.split(' ');
    return {
      x1: parseFloat(p[1]),
      y1: parseFloat(p[2]),
      largeArc: p[7],
      sweep: p[8],
      x2: parseFloat(p[9]),
      y2: parseFloat(p[10]),
    };
  }

  it('emits a small-arc flag for < 180° sweeps', () => {
    const a = parse(describeArc(0, 0, 100, 0, 90));
    expect(a.x1).toBeCloseTo(100, 5);
    expect(a.y1).toBeCloseTo(0, 5);
    expect(a.largeArc).toBe('0');
    expect(a.sweep).toBe('1');
    expect(a.x2).toBeCloseTo(0, 5);
    expect(a.y2).toBeCloseTo(100, 5);
  });

  it('emits a large-arc flag for > 180° sweeps and wraps negative diffs', () => {
    const a = parse(describeArc(0, 0, 100, 90, 0));
    expect(a.x1).toBeCloseTo(0, 5);
    expect(a.y1).toBeCloseTo(100, 5);
    expect(a.largeArc).toBe('1');
    expect(a.x2).toBeCloseTo(100, 5);
    expect(a.y2).toBeCloseTo(0, 5);
  });

  it('places endpoints on the circle (floating-point aware)', () => {
    const a = parse(describeArc(10, 20, 50, 0, 90));
    expect(a.x1).toBeCloseTo(60, 5);
    expect(a.y1).toBeCloseTo(20, 5);
    expect(a.x2).toBeCloseTo(10, 5);
    expect(a.y2).toBeCloseTo(70, 5);
    const b = parse(describeArc(0, 0, 100, 180, 270));
    expect(b.x1).toBeCloseTo(-100, 5);
    expect(b.y1).toBeCloseTo(0, 5);
    expect(b.x2).toBeCloseTo(0, 5);
    expect(b.y2).toBeCloseTo(-100, 5);
  });
});

describe('formatValue', () => {
  it('formats rpm/speed without decimals', () => {
    expect(formatValue(1200, 'RPM', 'rpm')).toBe('1200');
    expect(formatValue(95.6, 'Speed', 'km/h')).toBe('96');
  });

  it('formats voltage with two decimals', () => {
    expect(formatValue(12.345, 'Voltage', 'V')).toBe('12.35');
  });

  it('formats other units with one decimal by default', () => {
    expect(formatValue(98.65, 'Temp', '°C')).toBe('98.7');
    expect(formatValue(1.2, 'Boost', 'kPa')).toBe('1.2');
  });

  it('converts AFR to lambda when lambda mode is enabled', () => {
    expect(formatValue(14.7, 'AFR', '', true)).toBe('1.00');
    expect(formatValue(14.4, 'AFR', '', true)).toBe('0.98');
  });

  it('skips conversion when the unit is already lambda', () => {
    expect(formatValue(1.0, 'Wideband', 'Lambda', true)).toBe('1.00');
  });

  it('does not convert when lambda mode is off (name still implies 2dp)', () => {
    expect(formatValue(14.4, 'AFR', '', false)).toBe('14.40');
  });

  it('guards against zero stoich ratio', () => {
    expect(formatValue(14.4, 'AFR', '', true, 0)).toBe('14.40');
  });

  it('accepts precomputed lowercase strings', () => {
    expect(formatValue(50, 'VOLTAGE', 'VOLTS', false, 14.7, 'voltage', 'volts')).toBe('50.00');
  });
});

describe('computeWarningState', () => {
  it('returns none without settings or when disabled', () => {
    expect(computeWarningState(50, null, 1)).toBe('none');
    expect(computeWarningState(50, new Map([[1, warning({ enabled: false, maxWarning: 10 })]]), 1)).toBe('none');
  });

  it('works with array and map lookups', () => {
    const arr = [warning({ dataId: 1, minWarning: 0, maxWarning: 100 })];
    const map = new Map([[1, warning({ dataId: 1, minWarning: 0, maxWarning: 100 })]]);
    expect(computeWarningState(150, arr, 1)).toBe('warning');
    expect(computeWarningState(150, map, 1)).toBe('warning');
  });

  it('does not flag exact threshold values', () => {
    expect(computeWarningState(100, [warning({ dataId: 1, minWarning: 0, maxWarning: 100 })], 1)).toBe('none');
    expect(computeWarningState(0, [warning({ dataId: 1, minWarning: 0, maxWarning: 100 })], 1)).toBe('none');
  });

  it('flags values beyond the threshold as warning', () => {
    expect(computeWarningState(101, [warning({ dataId: 1, minWarning: 0, maxWarning: 100 })], 1)).toBe('warning');
    expect(computeWarningState(-1, [warning({ dataId: 1, minWarning: 0, maxWarning: 100 })], 1)).toBe('warning');
  });

  it('escalates to critical beyond half the range', () => {
    const w = warning({ dataId: 1, minWarning: 0, maxWarning: 100 });
    expect(computeWarningState(120, [w], 1)).toBe('warning');
    expect(computeWarningState(160, [w], 1)).toBe('critical');
    expect(computeWarningState(-60, [w], 1)).toBe('critical');
    expect(computeWarningState(-40, [w], 1)).toBe('warning');
  });

  it('treats missing min/max with default single-unit ranges', () => {
    // maxWarning only: default min = max-1 → range 1 → critical > max+0.5
    expect(computeWarningState(101, [warning({ dataId: 1, maxWarning: 100 })], 1)).toBe('critical');
    expect(computeWarningState(100.2, [warning({ dataId: 1, maxWarning: 100 })], 1)).toBe('warning');
    // minWarning only: default max = min+1 → range 1 → critical < min-0.5
    expect(computeWarningState(-1, [warning({ dataId: 1, minWarning: 0 })], 1)).toBe('critical');
    expect(computeWarningState(-0.2, [warning({ dataId: 1, minWarning: 0 })], 1)).toBe('warning');
  });
});

describe('estimateVisualSize', () => {
  it('arc gauges are square', () => {
    expect(estimateVisualSize(GaugeShapeCategory.Arc, 200, 100, {})).toEqual({ w: 100, h: 100 });
  });

  it('bar/chart/wedge gauges use the design size', () => {
    for (const cat of [GaugeShapeCategory.Bar, GaugeShapeCategory.Chart, GaugeShapeCategory.WedgeBar]) {
      expect(estimateVisualSize(cat, 200, 100, {})).toEqual({ w: 200, h: 100 });
    }
  });

  it('text gauges grow with extra rows', () => {
    const valueOnly = estimateVisualSize(GaugeShapeCategory.Text, 200, 200, {});
    const withUnit = estimateVisualSize(GaugeShapeCategory.Text, 200, 200, { showUnit: true, unitText: 'kPa' });
    expect(withUnit.h).toBeGreaterThan(valueOnly.h);
    expect(withUnit.w).toBeGreaterThan(0);
  });

  it('text gauges enforce a minimum size', () => {
    const size = estimateVisualSize(GaugeShapeCategory.Text, 10, 10, {});
    expect(size.w).toBeGreaterThanOrEqual(20);
    expect(size.h).toBeGreaterThanOrEqual(20);
  });

  it('digital odometer width scales with digits', () => {
    const short = estimateVisualSize(GaugeShapeCategory.Digital, 200, 200, { digitalStyle: DigitalStyle.Odometer, formattedValue: '5' });
    const long = estimateVisualSize(GaugeShapeCategory.Digital, 200, 200, { digitalStyle: DigitalStyle.Odometer, formattedValue: '12345' });
    expect(long.w).toBeGreaterThan(short.w);
  });

  it('clamps the font scale to [0.5, 2]', () => {
    const below = estimateVisualSize(GaugeShapeCategory.Digital, 200, 200, { fontSizeScale: 0.1, formattedValue: '123' });
    const atMin = estimateVisualSize(GaugeShapeCategory.Digital, 200, 200, { fontSizeScale: 0.5, formattedValue: '123' });
    const above = estimateVisualSize(GaugeShapeCategory.Digital, 200, 200, { fontSizeScale: 10, formattedValue: '123' });
    const atMax = estimateVisualSize(GaugeShapeCategory.Digital, 200, 200, { fontSizeScale: 2, formattedValue: '123' });
    expect(below).toEqual(atMin);
    expect(above).toEqual(atMax);
  });

  it('led/multiring gauges are square rings', () => {
    const a = estimateVisualSize(GaugeShapeCategory.LedRing, 300, 100, {});
    const b = estimateVisualSize(GaugeShapeCategory.MultiRing, 100, 300, {});
    expect(a).toEqual({ w: 100, h: 100 });
    expect(b).toEqual({ w: 100, h: 100 });
  });
});

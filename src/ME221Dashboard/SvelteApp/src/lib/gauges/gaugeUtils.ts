import { GaugeShapeCategory, DigitalStyle } from './gaugeTypes';
import type { ArcPosition, ColorLuts, ColorStop, GaugeDefinition, NeedleCurvePoint } from './gaugeTypes';
import type { DataLinkWarningSetting } from '../HybridBridgeTypes';
import type { ValueTransformStep } from './transformUtils';

export function computeValueFraction(value: number, min: number, max: number): number {
  const range = max - min;
  if (range <= 0) return 0;
  return Math.max(0, Math.min(1, (value - min) / range));
}

// Hot path: curve MUST be pre-sorted by rawValue ascending (sort on save/load, never here).
// Raw value is the actual ECU value (e.g. 90 for 90°C).
export function interpolateNeedleAngle(rawValue: number, curve: NeedleCurvePoint[]): number {
  const len = curve.length;
  if (len === 0) return 0;
  if (len === 1) return curve[0].angle;
  if (rawValue <= curve[0].rawValue) return curve[0].angle;
  if (rawValue >= curve[len - 1].rawValue) return curve[len - 1].angle;
  for (let i = 0; i < len - 1; i++) {
    const a = curve[i];
    const b = curve[i + 1];
    if (rawValue >= a.rawValue && rawValue <= b.rawValue) {
      const range = b.rawValue - a.rawValue;
      const t = range === 0 ? 0 : (rawValue - a.rawValue) / range;
      return a.angle + t * (b.angle - a.angle);
    }
  }
  return curve[len - 1].angle;
}

export const DEFAULT_COLOR_STOPS: ColorStop[] = [
  { fraction: 0.0, r: 34, g: 139, b: 230 },
  { fraction: 0.6, r: 245, g: 159, b: 0 },
  { fraction: 0.85, r: 224, g: 49, b: 49 },
];

const LUT_SIZE = 512;

function buildLut(stops: ColorStop[], out: string[]): void {
  const n = stops.length;
  for (let i = 0; i < LUT_SIZE; i++) {
    const t = i / (LUT_SIZE - 1);
    let r: number, g: number, b: number;
    if (t <= stops[0].fraction) {
      r = stops[0].r; g = stops[0].g; b = stops[0].b;
    } else if (t >= stops[n - 1].fraction) {
      r = stops[n - 1].r; g = stops[n - 1].g; b = stops[n - 1].b;
    } else {
      let seg = 0;
      for (let j = 0; j < n - 1; j++) {
        if (t >= stops[j].fraction && t <= stops[j + 1].fraction) { seg = j; break; }
      }
      const a = stops[seg];
      const c = stops[seg + 1];
      const range = c.fraction - a.fraction;
      const lt = range > 0 ? (t - a.fraction) / range : 0;
      r = (a.r + (c.r - a.r) * lt + 0.5) | 0;
      g = (a.g + (c.g - a.g) * lt + 0.5) | 0;
      b = (a.b + (c.b - a.b) * lt + 0.5) | 0;
    }
    out[i] = `rgb(${r},${g},${b})`;
  }
}

export function buildColorLuts(stops: ColorStop[], hysteresis: number): ColorLuts {
  const inc = new Array<string>(LUT_SIZE);
  const dec = new Array<string>(LUT_SIZE);
  buildLut(stops, inc);
  if (hysteresis > 0) {
    const shifted = stops.map((s, i) =>
      i === 0 || i === stops.length - 1 ? s : { ...s, fraction: Math.max(0, Math.min(1, s.fraction - hysteresis)) }
    );
    buildLut(shifted, dec);
  } else {
    buildLut(stops, dec);
  }
  return { increasing: inc, decreasing: dec };
}

export function gaugeValueColor(
  fraction: number,
  previousFraction: number | undefined,
  luts: ColorLuts
): string {
  const idx = (fraction * (LUT_SIZE - 1) + 0.5) | 0;
  return (previousFraction === undefined || fraction >= previousFraction)
    ? luts.increasing[idx]
    : luts.decreasing[idx];
}

export function positionToCenterAngle(pos: ArcPosition): number {
  switch (pos) {
    case 0 /* ArcPosition.Top */: return 270;
    case 1 /* ArcPosition.TopRight */: return 315;
    case 2 /* ArcPosition.Right */: return 0;
    case 3 /* ArcPosition.BottomRight */: return 45;
    case 4 /* ArcPosition.Bottom */: return 90;
    case 5 /* ArcPosition.BottomLeft */: return 135;
    case 6 /* ArcPosition.Left */: return 180;
    case 7 /* ArcPosition.TopLeft */: return 225;
    default: return 270;
  }
}

export function describeArc(cx: number, cy: number, r: number, startAngle: number, endAngle: number): string {
  const startRad = startAngle * Math.PI / 180;
  const endRad = endAngle * Math.PI / 180;
  const x1 = cx + r * Math.cos(startRad);
  const y1 = cy + r * Math.sin(startRad);
  const x2 = cx + r * Math.cos(endRad);
  const y2 = cy + r * Math.sin(endRad);
  let diff = endAngle - startAngle;
  if (diff < 0) diff += 360;
  const largeArc = diff > 180 ? 1 : 0;
  return `M ${x1} ${y1} A ${r} ${r} 0 ${largeArc} 1 ${x2} ${y2}`;
}

// ── Shared formatting ──────────────────────────────────────────────
// Lambda mode: if enabled, AFR values are divided by stoichAfr to show Lambda.
// Entities with "lambda" in name/unit are already in Lambda and skip conversion.
// Optional pre-computed lowercase strings avoid toLowerCase() allocations in the 30fps hot path.
export function formatValue(
  val: number, name: string, unit: string,
  lambdaMode = false, stoichAfr = 14.7,
  lowerName?: string, lowerUnit?: string
): string {
  const ln = lowerName ?? name.toLowerCase();
  const lu = lowerUnit ?? unit.toLowerCase();
  if (ln.includes('rpm') || lu.includes('rpm') || ln.includes('speed')) return val.toFixed(0);

  // Apply Lambda conversion for AFR entities when mode is enabled
  let displayVal = val;
  let displayUnit = unit;
  if (lambdaMode && !lu.includes('lambda') && !ln.includes('lambda')) {
    if (ln.includes('afr') || lu.includes('afr') || lu.includes('air/fuel')) {
      displayVal = stoichAfr > 0 ? val / stoichAfr : val;
      displayUnit = 'λ';
    }
  } else if (lu.includes('lambda') || ln.includes('lambda')) {
    displayUnit = 'λ';
  }

  if (lu.includes('v') || lu.includes('lambda') || ln.includes('afr') || displayUnit === 'λ') return displayVal.toFixed(2);
  return val.toFixed(1);
}

// ── Warning state computation ──────────────────────────────────────
// Pre-build a Map for O(1) lookups in the hot path (replaces Array.find).
export function buildWarningMap(settings: DataLinkWarningSetting[]): Map<number, DataLinkWarningSetting> {
  const map = new Map<number, DataLinkWarningSetting>();
  for (const s of settings) {
    map.set(s.dataId, s);
  }
  return map;
}

export function computeWarningState(
  value: number,
  warningSettings: Map<number, DataLinkWarningSetting> | DataLinkWarningSetting[] | null,
  entityId: number
): 'none' | 'warning' | 'critical' {
  if (!warningSettings) return 'none';
  const setting = warningSettings instanceof Map
    ? warningSettings.get(entityId)
    : warningSettings.find(s => s.dataId === entityId);
  if (!setting || !setting.enabled) return 'none';

  const { minWarning, maxWarning } = setting;
  const exceededMin = minWarning != null && value < minWarning;
  const exceededMax = maxWarning != null && value > maxWarning;

  if (exceededMin || exceededMax) {
    // Determine severity: critical if value is far beyond the threshold
    if (maxWarning != null && exceededMax) {
      const range = maxWarning - (minWarning ?? maxWarning - 1);
      if (value > maxWarning + range * 0.5) return 'critical';
    }
    if (minWarning != null && exceededMin) {
      const range = (maxWarning ?? minWarning + 1) - minWarning;
      if (value < minWarning - range * 0.5) return 'critical';
    }
    return 'warning';
  }
  return 'none';
}

// ── GaugeConfigEntry → GaugeDefinition mapping ─────────────────────
// Shared between DashboardPage (live) and GaugeSettingsModal (preview).
// The `value`/`formattedValue`/`minValue`/`maxValue` args come from entity lookup;
// everything else comes from the persisted config.
export function toGaugeDefinition(
  config: {
    entityId: number;
    shapeCategory: number;
    sweepAngle: number;
    arcPosition: ArcPosition;
    digitalStyle: DigitalStyle;
    texturePath: string | null;
    needleStartAngle: number;
    needleEndAngle: number;
    needleOffsetX: number;
    needleOffsetY: number;
    needleWidth: number;
    needleLength: number;
    needleCurve?: NeedleCurvePoint[];
    scale: number;
    fontSizeScale: number;
    labelVerticalOffset: number;
    showName: boolean;
    showUnit: boolean;
    showValue: boolean;
    iconName: string | null;
    iconOffsetX: number;
    iconOffsetY: number;
    iconSize: number;
    barValuePosition: number;
    barUnitPosition: number;
    barNamePosition: number;
    colorStops?: ColorStop[];
    colorHysteresis?: number;
    fractionX: number;
    fractionY: number;
    widthFraction: number;
    heightFraction: number;
    // Chart-specific
    chartTimeWindowSec?: number;
    chartYMin?: number | null;
    chartYMax?: number | null;
    chartLineColor?: string;
    chartLineWidth?: number;
    chartShowGrid?: boolean;
    chartFillUnder?: boolean;
    chartShowLabels?: boolean;
    chartPrecision?: number;
    textColor?: string;
    smoothingEnabled?: boolean;
    smoothingFactor?: number;
    smoothingResponseMs?: number;
    spikeGatePercent?: number;
    zIndex?: number;
  },
  overrides: {
    name: string;
    unit: string;
    value: number;
    formattedValue: string;
    minValue: number;
    maxValue: number;
  }
): GaugeDefinition {
  return {
    entityId: config.entityId,
    name: overrides.name,
    unit: overrides.unit,
    category: config.shapeCategory as GaugeShapeCategory,
    value: overrides.value,
    formattedValue: overrides.formattedValue,
    minValue: overrides.minValue,
    maxValue: overrides.maxValue,
    sweepAngle: config.sweepAngle,
    arcPosition: config.arcPosition,
    digitalStyle: config.digitalStyle,
    texturePath: config.texturePath,
    needleStartAngle: config.needleStartAngle,
    needleEndAngle: config.needleEndAngle,
    needleOffsetX: config.needleOffsetX,
    needleOffsetY: config.needleOffsetY,
    needleWidth: config.needleWidth,
    needleLength: config.needleLength,
    needleCurve: config.needleCurve?.slice().sort((a, b) => a.rawValue - b.rawValue) ?? [],
    scale: config.scale,
    fontSizeScale: config.fontSizeScale,
    labelVerticalOffset: config.labelVerticalOffset,
    showName: config.showName,
    showUnit: config.showUnit,
    showValue: config.showValue,
    iconName: config.iconName,
    iconOffsetX: config.iconOffsetX,
    iconOffsetY: config.iconOffsetY,
    iconSize: config.iconSize,
    barValuePosition: config.barValuePosition,
    barUnitPosition: config.barUnitPosition,
    barNamePosition: config.barNamePosition,
    colorStops: config.colorStops?.length ? config.colorStops : DEFAULT_COLOR_STOPS,
    colorHysteresis: config.colorHysteresis ?? 0.03,
    fractionX: config.fractionX,
    fractionY: config.fractionY,
    widthFraction: config.widthFraction,
    heightFraction: config.heightFraction,
    chartTimeWindowSec: config.chartTimeWindowSec ?? 30,
    chartYMin: config.chartYMin ?? null,
    chartYMax: config.chartYMax ?? null,
    chartLineColor: config.chartLineColor ?? '#22c8e6',
    chartLineWidth: config.chartLineWidth ?? 2,
    chartShowGrid: config.chartShowGrid ?? true,
    chartFillUnder: config.chartFillUnder ?? false,
    chartShowLabels: config.chartShowLabels ?? true,
    chartPrecision: config.chartPrecision ?? 1,
    textColor: config.textColor ?? '#ffffff',
    smoothingEnabled: config.smoothingEnabled ?? false,
    smoothingFactor: config.smoothingFactor ?? 0.3,
    smoothingResponseMs: config.smoothingResponseMs ?? 0,
    spikeGatePercent: config.spikeGatePercent ?? 0,
    warningState: 'none',
    showHistogram: config.showHistogram ?? false,
    zIndex: config.zIndex ?? 0,
  };
}

// ── SaveLayoutPayload builder ──────────────────────────────────────
// Maps a GaugeConfigEntry to the save payload, omitting display-only fields.
export function toSavePayload(def: {
  entityId: number;
  fractionX: number;
  fractionY: number;
  widthFraction: number;
  heightFraction: number;
  sweepAngle: number;
  arcPosition: ArcPosition;
  digitalStyle: DigitalStyle;
  needleStartAngle: number;
  needleEndAngle: number;
  needleOffsetX: number;
  needleOffsetY: number;
  needleWidth: number;
  needleLength: number;
  needleCurve?: NeedleCurvePoint[];
  scale: number;
  fontSizeScale: number;
  labelVerticalOffset: number;
  showName: boolean;
  showUnit: boolean;
  showValue: boolean;
  iconName: string | null;
  iconOffsetX: number;
  iconOffsetY: number;
  iconSize: number;
  smoothingEnabled?: boolean;
  smoothingFactor?: number;
  smoothingResponseMs?: number;
  spikeGatePercent?: number;
  barValuePosition: number;
  barUnitPosition: number;
  barNamePosition: number;
  colorStops?: ColorStop[];
  colorHysteresis?: number;
  shapeCategory: number;
  texturePath?: string | null;
  textColor?: string;
  zIndex: number;
  transformSteps?: ValueTransformStep[];
  customUnitLabel?: string | null;
  showHistogram?: boolean;
}) {
  return {
    entityId: def.entityId,
    fractionX: def.fractionX,
    fractionY: def.fractionY,
    widthFraction: def.widthFraction,
    heightFraction: def.heightFraction,
    sweepAngle: def.sweepAngle,
    arcPosition: def.arcPosition,
    digitalStyle: def.digitalStyle,
    needleStartAngle: def.needleStartAngle,
    needleEndAngle: def.needleEndAngle,
    needleOffsetX: def.needleOffsetX,
    needleOffsetY: def.needleOffsetY,
    needleWidth: def.needleWidth,
    needleLength: def.needleLength,
    needleCurve: def.needleCurve,
    scale: def.scale,
    fontSizeScale: def.fontSizeScale,
    labelVerticalOffset: def.labelVerticalOffset,
    showName: def.showName,
    showUnit: def.showUnit,
    showValue: def.showValue,
    iconName: def.iconName,
    iconOffsetX: def.iconOffsetX,
    iconOffsetY: def.iconOffsetY,
    iconSize: def.iconSize,
    smoothingEnabled: def.smoothingEnabled,
    smoothingFactor: def.smoothingFactor,
    smoothingResponseMs: def.smoothingResponseMs,
    spikeGatePercent: def.spikeGatePercent,
    barValuePosition: def.barValuePosition,
    barUnitPosition: def.barUnitPosition,
    barNamePosition: def.barNamePosition,
    colorStops: def.colorStops,
    colorHysteresis: def.colorHysteresis,
    shapeCategory: def.shapeCategory,
    texturePath: def.texturePath,
    chartTimeWindowSec: def.chartTimeWindowSec,
    chartYMin: def.chartYMin,
    chartYMax: def.chartYMax,
    chartLineColor: def.chartLineColor,
    chartLineWidth: def.chartLineWidth,
    chartShowGrid: def.chartShowGrid,
    chartFillUnder: def.chartFillUnder,
    chartShowLabels: def.chartShowLabels,
    chartPrecision: def.chartPrecision,
    textColor: def.textColor,
    zIndex: def.zIndex,
    transformSteps: def.transformSteps,
    customUnitLabel: def.customUnitLabel,
    showHistogram: def.showHistogram,
  };
}

// SaveLayoutPayload is imported from HybridBridge where the canonical type lives.
// toSavePayload returns a plain object matching that shape — no import needed here.

// ── Visual content size estimation ──────────────────────────────────
// Returns the actual rendered bounding-box dimensions for a gauge,
// so the container can be sized to match instead of leaving dead space.
export function estimateVisualSize(
  category: GaugeShapeCategory,
  designPxW: number,
  designPxH: number,
  opts: {
    fontSizeScale?: number;
    digitalStyle?: DigitalStyle;
    formattedValue?: string;
    showValue?: boolean;
    showUnit?: boolean;
    showName?: boolean;
    unitText?: string;
    nameText?: string;
  }
): { w: number; h: number } {
  const dim = Math.min(designPxW, designPxH);
  const fs = Math.max(0.5, Math.min(2.0, opts.fontSizeScale ?? 1.0));
  const lh = 1.2; // default line-height factor — text is ~20% taller than font-size

  if (category === GaugeShapeCategory.Arc) {
    const arcBox = Math.min(designPxW, designPxH);
    return { w: arcBox, h: arcBox };
  }

  if (category === GaugeShapeCategory.Bar) {
    return { w: designPxW, h: designPxH };
  }

  if (category === GaugeShapeCategory.Chart) {
    return { w: designPxW, h: designPxH };
  }

  if (category === GaugeShapeCategory.Digital) {
    const showVal = opts.showValue !== false;
    const showUnit = opts.showUnit ?? false;
    const showName = opts.showName ?? false;

    const cellW = Math.max(14, dim * 0.10 * fs);
    const cellH = Math.max(20, dim * 0.16 * fs);
    const unitSize = Math.max(10, dim * 0.08 * fs);
    const nameSize = Math.max(7, dim * 0.04 * fs);
    const gap = 2;

    let contentW = 0;
    let contentH = 0;

    const style = opts.digitalStyle ?? DigitalStyle.Odometer;

    if (style === DigitalStyle.LargeDigit) {
      const valW = showVal ? (opts.formattedValue?.length ?? 3) * Math.max(14, dim * 0.16 * fs) * 0.65 : 0;
      contentW = valW + 16; // px-4 padding
      contentH = (showVal ? Math.max(14, dim * 0.16 * fs) * lh + 8 : 0);
    } else if (style === DigitalStyle.Cluster) {
      const valW = showVal ? (opts.formattedValue?.length ?? 3) * Math.max(16, dim * 0.18 * fs) * 0.6 : 0;
      const unitW = showUnit ? (opts.unitText?.length ?? 3) * Math.max(10, dim * 0.07 * fs) * 0.6 : 0;
      contentW = valW + unitW + (showVal && showUnit ? 4 : 0);
      const rowH = (showVal || showUnit ? Math.max(16, dim * 0.18 * fs) * lh : 0);
      contentH = rowH
        + 6 // separator + margin
        + (showName ? Math.max(7, dim * 0.05 * fs) * lh : 0);
    } else if (style === DigitalStyle.LabelTop) {
      const lblW = showName ? (opts.nameText?.length ?? 4) * Math.max(6, dim * 0.04 * fs) * 0.5 : 0;
      const valW = showVal ? (opts.formattedValue?.length ?? 3) * Math.max(12, dim * 0.16 * fs) * 0.6 : 0;
      const unitW = showUnit ? (opts.unitText?.length ?? 3) * Math.max(7, dim * 0.06 * fs) * 0.6 : 0;
      contentW = Math.max(lblW, valW, unitW);
      contentH = (showName ? Math.max(6, dim * 0.04 * fs) * lh + 1 : 0)
        + (showVal ? Math.max(12, dim * 0.16 * fs) * lh + 1 : 0)
        + (showUnit ? Math.max(7, dim * 0.06 * fs) * lh : 0);
    } else if (style === DigitalStyle.GlowRing) {
      const ringD = dim * 0.55;
      contentW = ringD;
      contentH = ringD
        + (showUnit ? Math.max(7, dim * 0.05 * fs) * lh + 2 : 0)
        + (showName ? Math.max(6, dim * 0.035 * fs) * lh + 2 : 0);
    } else if (style === DigitalStyle.InsetDisplay) {
      const insetValW = showVal ? (opts.formattedValue?.length ?? 3) * Math.max(10, dim * 0.13 * fs) * 0.6 : 0;
      contentW = insetValW + 24; // padding + border
      contentH = (showVal ? Math.max(10, dim * 0.13 * fs) * lh + 4 : 0)
        + (showUnit ? Math.max(7, dim * 0.06 * fs) * lh + 2 : 0)
        + 8 // border + padding
        + (showName ? Math.max(6, dim * 0.04 * fs) * lh + 2 : 0);
    } else {
      // Odometer / SevenSegment
      const numChars = showVal ? Math.max(3, opts.formattedValue?.length ?? 3) : 0;
      contentW = numChars * cellW + Math.max(0, numChars - 1) * gap;
      contentH = (showVal ? cellH * lh : 0)
        + (showUnit ? unitSize * lh + gap : 0)
        + (showName ? nameSize * lh + gap : 0);
    }

    return { w: Math.round(Math.max(20, contentW)), h: Math.round(Math.max(20, contentH + 4)) };
  }

  // Text gauge
  const valueSize = Math.max(14, dim * 0.22 * fs);
  const unitSize = Math.max(10, dim * 0.08 * fs);
  const nameSize = Math.max(6, 9 * fs);
  const gap = 2;

  const showVal = opts.showValue !== false;
  const showUnit = opts.showUnit ?? false;
  const showName = opts.showName ?? false;

  const valW = showVal ? Math.max(14, Math.min(designPxW * 0.9, (opts.formattedValue?.length ?? 3) * valueSize * 0.6)) : 0;
  const unitW = showUnit ? Math.max(14, Math.min(designPxW * 0.9, (opts.unitText?.length ?? 3) * unitSize * 0.6)) : 0;
  const nameW = showName ? Math.max(14, Math.min(designPxW * 0.9, (opts.nameText?.length ?? 3) * nameSize * 0.6)) : 0;

  const contentW = Math.max(valW, unitW, nameW);
  const contentH = (showVal ? valueSize * lh + gap : 0)
    + (showUnit ? unitSize * lh + gap : 0)
    + (showName ? nameSize * lh : 0);

  return { w: Math.round(Math.max(20, contentW + 4)), h: Math.round(Math.max(20, contentH + 4)) };
}

export enum GaugeShapeCategory {
  Arc = 0,
  Bar = 1,
  Text = 2,
  Digital = 3,
  Chart = 4,
}

export enum ArcPosition {
  Top = 0,
  TopRight = 1,
  Right = 2,
  BottomRight = 3,
  Bottom = 4,
  BottomLeft = 5,
  Left = 6,
  TopLeft = 7,
}

export enum DigitalStyle {
  Odometer = 0,
  LargeDigit = 1,
  SevenSegment = 2,
  Cluster = 3,
  LabelTop = 4,
  GlowRing = 5,
  InsetDisplay = 6,
}

export interface GaugeDefinition {
  entityId: number;
  name: string;
  unit: string;
  category: GaugeShapeCategory;
  value: number;
  formattedValue: string;
  minValue: number;
  maxValue: number;
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
  needleCurve: NeedleCurvePoint[];
  scale: number;
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
  colorStops: ColorStop[];
  colorHysteresis: number;
  fractionX: number;
  fractionY: number;
  widthFraction: number;
  heightFraction: number;
  // Chart-specific
  chartTimeWindowSec: number;
  chartYMin: number | null;
  chartYMax: number | null;
  chartLineColor: string;
  chartLineWidth: number;
  chartShowGrid: boolean;
  chartFillUnder: boolean;
  chartShowLabels: boolean;
  chartPrecision: number;
  textColor: string;
  // Smoothing
  smoothingEnabled: boolean;
  smoothingFactor: number;
  // Layering
  zIndex: number;
}

export interface LiveDataValues {
  values: Record<string, number | null>;
}

export interface ColorStop {
  fraction: number;
  r: number;
  g: number;
  b: number;
}

export interface NeedleCurvePoint {
  rawValue: number;
  angle: number;
}

export interface ColorLuts {
  increasing: string[];
  decreasing: string[];
}

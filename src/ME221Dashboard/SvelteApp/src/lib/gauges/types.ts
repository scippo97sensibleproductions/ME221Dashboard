export {
  GaugeShapeCategory,
  ArcPosition,
  DigitalStyle,
} from './gaugeTypes';

export type {
  GaugeDefinition,
  LiveDataValues,
  ColorStop,
  NeedleCurvePoint,
  ColorLuts,
} from './gaugeTypes';

export {
  computeValueFraction,
  interpolateNeedleAngle,
  DEFAULT_COLOR_STOPS,
  buildColorLuts,
  gaugeValueColor,
  positionToCenterAngle,
  describeArc,
  formatValue,
  toGaugeDefinition,
  toSavePayload,
  estimateVisualSize,
} from './gaugeUtils';

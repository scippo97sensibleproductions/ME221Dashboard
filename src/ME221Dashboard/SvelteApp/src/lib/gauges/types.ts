export {
  GaugeShapeCategory,
  ArcPosition,
  DigitalStyle,
  WedgeStyle,
} from './gaugeTypes';

export type {
  GaugeDefinition,
  LiveDataValues,
  ColorStop,
  NeedleCurvePoint,
  ColorLuts,
} from './gaugeTypes';

export {
  ValueTransformOperation,
  applyTransform,
  stepError,
  isTransformable,
} from './transformUtils';

export type {
  ValueTransformStep,
} from './transformUtils';

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
  levelVisualStyle,
} from './gaugeUtils';

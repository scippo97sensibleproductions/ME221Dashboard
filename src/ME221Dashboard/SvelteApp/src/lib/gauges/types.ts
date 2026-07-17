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
  computeWarningState,
  buildWarningMap,
} from './gaugeUtils';

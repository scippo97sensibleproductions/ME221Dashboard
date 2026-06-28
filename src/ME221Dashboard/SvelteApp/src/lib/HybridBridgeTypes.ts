/**
 * HybridWebView Bridge - TypeScript types for C# <-> Svelte communication
 */

// ─── Connection Types ────────────────────────────────────────────────────────

export interface ConnectionStateInfo {
  state: string;
  error?: string;
  protocolInfo?: {
    product: string;
    model: string;
    version: string;
    reportingVersion: number;
    entityCount: number;
  };
}

export interface ConnectionResult {
  success: boolean;
  state?: string;
  error?: string;
}

export interface AvailablePortsResult {
  ports: { name: string; description?: string; hasPermission?: boolean; vendorId?: number; productId?: number }[];
  error?: string;
}

// ─── Event Types ─────────────────────────────────────────────────────────────

export interface LiveDataEvent {
  event: 'liveDataUpdate';
  values: Record<string, number | null>;
}

export interface ConnectionStateChangedEvent {
  event: 'connectionStateChanged';
  state: string;
  error?: string;
}

// ─── Calibration Types ───────────────────────────────────────────────────────

export interface EcuInfoResult {
  success: boolean;
  product?: string;
  model?: string;
  version?: string;
  error?: string;
}

export interface PersistedCalibrationResult {
  type: string;
  metadata?: {
    productName: string;
    modelName: string;
    version: string;
  } | null;
  error?: string;
}

export interface CalibrationMatchResult {
  matched: boolean;
  hasSaved: boolean;
  metadata?: {
    productName: string;
    modelName: string;
    version: string;
  } | null;
  error?: string;
}

export interface PickCalibrationResult {
  picked: boolean;
  success?: boolean;
  metadata?: {
    productName: string;
    modelName: string;
    version: string;
  } | null;
  error?: string;
}

// ─── Dashboard Types ─────────────────────────────────────────────────────────

export interface DashboardConfigResult {
  found: boolean;
  gauges: GaugeConfigEntry[];
  gridRows: number;
  gridColumns: number;
  entities?: Record<string, EntityInfo>;
  backgroundImagePath?: string | null;
  error?: string;
}

export interface GaugeConfigEntry {
  entityId: number;
  shapeCategory: number;
  sweepAngle: number;
  arcPosition: number;
  digitalStyle: number;
  texturePath: string | null;
  needleStartAngle: number;
  needleEndAngle: number;
  needleOffsetX: number;
  needleOffsetY: number;
  needleWidth: number;
  needleLength: number;
  needleCurve?: { rawValue: number; angle: number }[];
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
  colorStops: { fraction: number; r: number; g: number; b: number }[];
  colorHysteresis: number;
  smoothingEnabled: boolean;
  smoothingFactor: number;
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
  zIndex: number;
}

export interface EntityInfo {
  name: string;
  unit: string;
  minValue?: number;
  maxValue?: number;
}

export interface SaveLayoutPayload {
  entityId: number;
  fractionX: number;
  fractionY: number;
  widthFraction: number;
  heightFraction: number;
  sweepAngle?: number;
  arcPosition?: number;
  digitalStyle?: number;
  needleStartAngle?: number;
  needleEndAngle?: number;
  needleOffsetX?: number;
  needleOffsetY?: number;
  needleWidth?: number;
  needleLength?: number;
  needleCurve?: { rawValue: number; angle: number }[];
  scale?: number;
  fontSizeScale?: number;
  labelVerticalOffset?: number;
  showName?: boolean;
  showUnit?: boolean;
  showValue?: boolean;
  iconName?: string | null;
  iconOffsetX?: number;
  iconOffsetY?: number;
  iconSize?: number;
  smoothingEnabled?: boolean;
  smoothingFactor?: number;
  barValuePosition?: number;
  barUnitPosition?: number;
  barNamePosition?: number;
  colorStops?: { fraction: number; r: number; g: number; b: number }[];
  colorHysteresis?: number;
  shapeCategory?: number;
  texturePath?: string | null;
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
  zIndex?: number;
}

// ─── Vehicle Config ──────────────────────────────────────────────────────────

export interface VehicleConfig {
  enabled: boolean;
  tireDiameterInches: number;
  finalDriveRatio: number;
  gearRatios: number[];
  wheelSlipPercent: number;
  rpmEntityId: number | null;
  vssSpeedEntityId: number | null;
  mapEntityId: number | null;
  baroEntityId: number | null;
  gearEntityId: number | null;
}

// ─── Sensor Selection / Config Types ─────────────────────────────────────────

export interface AvailableSensor {
  id: number;
  name: string;
  category: string;
  unit: string;
  inEntityMap: boolean;
  isSelected: boolean;
  customization: SensorCustomization | null;
}

export interface SensorCustomization {
  customName: string | null;
  customUnit: string | null;
  minRange: number | null;
  maxRange: number | null;
}

export interface AvailableSensorsResult {
  sensors: AvailableSensor[];
  selectedCount: number;
  totalCount: number;
  gridRows: number;
  gridColumns: number;
  backgroundImagePath: string | null;
  error?: string;
}

// ─── GPS Types ───────────────────────────────────────────────────────────────

export interface GpsLocation {
  latitude: number;
  longitude: number;
  altitude?: number;
  speed?: number;       // m/s
  course?: number;      // degrees
  accuracy?: number;    // meters
  timestamp: string;
  odometer?: number;
  odometerUnit?: string;
}

export interface GpsUpdateEvent {
  event: 'gpsUpdate';
  latitude: number;
  longitude: number;
  altitude?: number;
  speed?: number;
  course?: number;
  accuracy?: number;
  timestamp: string;
}

export interface OdometerUpdateEvent {
  event: 'odometerUpdate';
  odometer: number;
  odometerUnit: string;
}

export interface LogEntryEvent {
  event: 'logEntry';
  timestamp: string;
  level: string;
  category: string;
  message: string;
  exception?: string;
}

export interface GpsStatus {
  available: boolean;
  isRunning: boolean;
}

// ─── Union Types ─────────────────────────────────────────────────────────────

export type BridgeEvent = LiveDataEvent | ConnectionStateChangedEvent | GpsUpdateEvent | OdometerUpdateEvent | LogEntryEvent;

// ─── Driver Types ────────────────────────────────────────────────────────────

export interface ComboOption {
  id: number;
  name: string;
}

export interface ViewConstraint {
  paramIndex: number;
  acceptedValues: number[];
}

export interface DriverParamDefinition {
  name: string;
  displayName: string;
  sectionName: string;
  paramType: string;
  readOnly: boolean;
  requiresReset: boolean;
  value: number;
  min: number;
  max: number;
  checkRange: boolean;
  toolTipText: string;
  options: ComboOption[] | null;
  viewConstraint: ViewConstraint | null;
}

export interface DriverDefinition {
  id: number;
  name: string;
  category: string;
  viewInTree: boolean;
  numberOfConfigs: number;
  configs: DriverParamDefinition[];
  numberOfOutputs: number;
  outputLinkIds: number[];
  editableOutputs: boolean;
  outputNames: string[];
  numberOfInputs: number;
  inputLinkIds: number[];
  editableInputs: boolean;
  inputNames: string[];
}

export interface DriverDataResult {
  configs: number[];
  outputLinkIds: number[];
  inputLinkIds: number[];
  error?: string;
}

export interface DriverDefinitionsResult {
  drivers: DriverDefinition[];
  error?: string;
}

export interface DriverSetResult {
  success: boolean;
  error?: string;
}

export interface DataLinkDefinition {
  id: number;
  name: string;
  category: string;
}

export interface DataLinksResult {
  dataLinks: DataLinkDefinition[];
  error?: string;
}

// ─── HybridWebView Global ───────────────────────────────────────────────────

declare global {
  interface Window {
    HybridWebView: {
      SendRawMessage: (message: string) => void;
      InvokeDotNet: (methodName: string, params?: unknown[]) => Promise<string>;
    };
  }
}

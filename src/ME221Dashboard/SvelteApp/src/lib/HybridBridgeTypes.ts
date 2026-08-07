/**
 * HybridWebView Bridge - TypeScript types for C# <-> Svelte communication
 */

import type { ColorScheme } from './tables/types';
import type { ValueTransformStep } from './gauges/transformUtils';

export type { ColorScheme };

// ─── Connection Types ────────────────────────────────────────────────────────

export interface ConnectionStateInfo {
  state: string;
  connectionType?: 'tcp' | 'serial';
  connectionDetail?: string;
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
  deviceName?: string;
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

// ─── Device Types ────────────────────────────────────────────────────────────

export type UiMode = 'desktop' | 'mobile';

export interface DeviceProfile {
  platform: string;
  idiom: string;
  /** Explicit UI mode decided natively: desktop = keyboard/mouse expected, mobile = touch-first. */
  uiMode: UiMode;
}

// ─── Dashboard Types ─────────────────────────────────────────────────────────

export interface DashboardConfigResult {
  found: boolean;
  gauges: GaugeConfigEntry[];
  tables: DashboardTableEntry[];
  gridRows: number;
  gridColumns: number;
  entities?: Record<string, EntityInfo>;
  backgroundImagePath?: string | null;
  headerVisible?: boolean;
  sidebarVisible?: boolean;
  layoutLocked?: boolean;
  error?: string;
}

export interface DashboardViewState {
  headerVisible?: boolean;
  sidebarVisible?: boolean;
  layoutLocked?: boolean;
}

export interface ChartOverlayLine {
  entityId: number;
  color: string;
  lineWidth: number;
  lineStyle: number; // 0=solid 1=dashed 2=dotted
}

export interface GaugeConfigEntry {
  entityId: number;
  shapeCategory: number;
  sweepAngle: number;
  arcPosition: number;
  digitalStyle: number;
  wedgeStyle: number;
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
  smoothingResponseMs: number;
  spikeGatePercent: number;
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
  // Value transforms
  transformSteps?: ValueTransformStep[];
  customUnitLabel?: string | null;
  // Histogram
  showHistogram?: boolean;
  // Multi-entity support (Wedge, LED Ring, Multi-Ring)
  linkedEntities?: { entityId: number; color: string }[];
  // ── Gauge customization v2 (all optional; defaults in toGaugeDefinition) ──
  // Arc scale marks
  tickCount?: number;
  tickLabels?: boolean;
  tickLabelEvery?: number;
  tickSide?: number; // 0=inside 1=outside
  // Arc redline band
  redlineStart?: number; // 0 = off
  redlineWidth?: number;
  redlineColor?: string;
  // Arc needle shape
  needleShape?: number; // 0=line 1=tapered 2=paddle 3=counterweighted
  // Bar
  barOrientation?: number; // 0=auto 1=horizontal 2=vertical
  barThickness?: number; // 0=auto
  barTicks?: boolean;
  barMinMaxLabels?: boolean;
  barRedlineStart?: number; // 0 = off
  barRedlineColor?: string;
  // Text
  colorStopColoring?: boolean;
  panelStyle?: number; // 0=none 1=pill 2=glass 3=card
  flashThreshold?: number; // 0 = off
  // Digital theming
  ledColor?: string;
  digitBgColor?: string;
  glowStrength?: number; // 0 = current baseline
  digitDecimals?: number; // -1 = auto
  zeroPadding?: boolean;
  minDigitCount?: number;
  rollAnimation?: boolean;
  rollSpeedMs?: number;
  // LedRing geometry
  segmentCount?: number;
  segmentGap?: number;
  ringStartAngle?: number;
  ringSweepAngle?: number;
  amberThreshold?: number;
  redThreshold?: number;
  // MultiRing geometry
  ringCount?: number;
  ringWidth?: number; // 0 = auto
  ringGap?: number; // 0 = auto
  peakHoldEnabled?: boolean;
  peakHoldAutoResetSec?: number;
  // WedgeBar
  wedgeSegmentCount?: number;
  wedgeRedlineStart?: number;
  // Chart overlays / style
  chartOverlays?: ChartOverlayLine[];
  overlayPillPosition?: number; // 0=topRight 1=topLeft 2=bottomRight 3=bottomLeft
  overlayFontScale?: number;
  chartLineStyle?: number; // 0=solid 1=dashed 2=dotted
  chartBackgroundColor?: string; // '' = transparent
}

export interface DashboardTableEntry {
  tableId: number;
  fractionX: number;
  fractionY: number;
  widthFraction: number;
  heightFraction: number;
  zIndex: number;
  colorScheme?: ColorScheme;
  showLabels?: boolean;
  showDimensionBadge?: boolean;
  traceXLink?: number | null;
  traceYLink?: number | null;
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
  wedgeStyle?: number;
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
  smoothingResponseMs?: number;
  spikeGatePercent?: number;
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
  transformSteps?: ValueTransformStep[];
  customUnitLabel?: string | null;
  traceXLink?: number | null;
  traceYLink?: number | null;
  linkedEntities?: { entityId: number; color: string }[];
  // Gauge customization v2 (all optional; defaults in toGaugeDefinition)
  tickCount?: number;
  tickLabels?: boolean;
  tickLabelEvery?: number;
  tickSide?: number;
  redlineStart?: number;
  redlineWidth?: number;
  redlineColor?: string;
  needleShape?: number;
  barOrientation?: number;
  barThickness?: number;
  barTicks?: boolean;
  barMinMaxLabels?: boolean;
  barRedlineStart?: number;
  barRedlineColor?: string;
  colorStopColoring?: boolean;
  panelStyle?: number;
  flashThreshold?: number;
  ledColor?: string;
  digitBgColor?: string;
  glowStrength?: number;
  digitDecimals?: number;
  zeroPadding?: boolean;
  minDigitCount?: number;
  rollAnimation?: boolean;
  rollSpeedMs?: number;
  segmentCount?: number;
  segmentGap?: number;
  ringStartAngle?: number;
  ringSweepAngle?: number;
  amberThreshold?: number;
  redThreshold?: number;
  ringCount?: number;
  ringWidth?: number;
  ringGap?: number;
  peakHoldEnabled?: boolean;
  peakHoldAutoResetSec?: number;
  wedgeSegmentCount?: number;
  wedgeRedlineStart?: number;
  chartOverlays?: ChartOverlayLine[];
  overlayPillPosition?: number;
  overlayFontScale?: number;
  chartLineStyle?: number;
  chartBackgroundColor?: string;
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
  minValue: number;
  maxValue: number;
  inEntityMap: boolean;
  isSelected: boolean;
  customization: SensorCustomization | null;
}

export interface SensorCustomization {
  customName: string | null;
  customUnit: string | null;
  minRange: number | null;
  maxRange: number | null;
  minRangeBypass: boolean;
  maxRangeBypass: boolean;
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
  odometer?: number;
  odometerUnit?: string;
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

// ─── Update Check Types ─────────────────────────────────────────────────────

export interface UpdateCheckResult {
  updateAvailable: boolean;
  currentVersion: string;
  latestVersion: string;
  releaseUrl: string;
  releaseName: string;
  publishedAt: string | null;
}

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
  measurementUnitTypes: string[];
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
  measureUnit: string;
  measurementUnitTypes: number;
  dataType: number;
  minValue: number;
  maxValue: number;
}

export interface DataLinksResult {
  dataLinks: DataLinkDefinition[];
  error?: string;
}

// ─── Warning Centre Types ────────────────────────────────────────────────────

export type WarningSettingStatus = 'Typical' | 'Custom' | 'Disabled';

export interface DataLinkWarningSetting {
  dataId: number;
  enabled: boolean;
  minWarning: number | null;
  maxWarning: number | null;
  name: string;
  unit: string;
  category: string;
  status: WarningSettingStatus;
}

export interface WarningHistoryEntry {
  id: number;
  dataId: number;
  name: string;
  unit: string;
  category: string;
  value: number;
  severity: string;
  threshold: number;
  thresholdType: string;
  triggeredAt: number;
  clearedAt: number | null;
}

// ─── User Preferences Types ────────────────────────────────────────────────

export interface ConnectionPreference {
  type: string;
  host?: string;
  port?: number;
  serialPort?: string;
}

export interface LambdaSettings {
  useLambdaMode: boolean;
  stoichAfr: number;
}

// ─── Monitoring Preset Types ────────────────────────────────────────────────

export interface MonitoringPreset {
  id: string;
  name: string;
  datalinkIds: number[];
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

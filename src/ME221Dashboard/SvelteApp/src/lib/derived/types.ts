export enum DerivedEntityId {
  Gear = -3001,
  TrueSpeed = -3002,
  Boost = -3003,
  SpeedError = -3004,
  RpmToShift = -3005,
  ShiftState = -3006,
  FuelEfficiency = -3007,
  EcuClock = -3008,
  EcuDate = -3009,
  AverageFuelEfficiency = -3010,
}

export const DERIVED_ENTITIES: Record<number, { name: string; unit: string; minValue: number; maxValue: number; decimalPlaces: number }> = {
  [DerivedEntityId.Gear]:      { name: 'Gear',           unit: '',     minValue: -1, maxValue: 10, decimalPlaces: 0 },
  [DerivedEntityId.TrueSpeed]: { name: 'True Speed',     unit: 'km/h', minValue: 0,  maxValue: 400, decimalPlaces: 1 },
  [DerivedEntityId.Boost]:     { name: 'Boost Pressure', unit: 'kPa',  minValue: 0,  maxValue: 400, decimalPlaces: 1 },
  [DerivedEntityId.SpeedError]:{ name: 'Speed Error',    unit: 'km/h', minValue: -50, maxValue: 50, decimalPlaces: 1 },
  [DerivedEntityId.RpmToShift]:{ name: 'RPM to Shift',   unit: 'rpm',  minValue: 0,  maxValue: 9000, decimalPlaces: 1 },
  [DerivedEntityId.ShiftState]:{ name: 'Shift State',    unit: '',     minValue: -1, maxValue: 1, decimalPlaces: 0 },
  [DerivedEntityId.FuelEfficiency]: { name: 'Fuel Efficiency', unit: 'km/L', minValue: 0, maxValue: 50, decimalPlaces: 1 },
  // Clock and date carry no unit: their value renders as time/date text.
  [DerivedEntityId.EcuClock]:  { name: 'ECU Clock',      unit: '',     minValue: 0, maxValue: 86400, decimalPlaces: 0 },
  [DerivedEntityId.EcuDate]:   { name: 'ECU Date',       unit: '',     minValue: 0, maxValue: 99991231, decimalPlaces: 0 },
  [DerivedEntityId.AverageFuelEfficiency]: { name: 'Avg Fuel Efficiency', unit: 'km/L', minValue: 0, maxValue: 50, decimalPlaces: 1 },
};

/** Output units for the fuel-efficiency entity — the formula below inverts. */
export const FUEL_EFFICIENCY_UNITS = {
  l100km: 'L/100km',
  kmL: 'km/L',
  mpg: 'MPG',
} as const;

export type FuelEfficiencyUnit = keyof typeof FUEL_EFFICIENCY_UNITS;

/** Nominal gauge range for an efficiency unit; MPG spans higher numbers. */
export function fuelEfficiencyRange(unit: FuelEfficiencyUnit): { minValue: number; maxValue: number } {
  return unit === 'mpg' ? { minValue: 0, maxValue: 80 } : { minValue: 0, maxValue: 50 };
}

/** Where the fuel-efficiency entity takes its speed from. */
export type FuelSpeedSource = 'auto' | 'gps' | 'trueSpeed';

/** Display label for a speed source, shared by the settings surface. */
export const FUEL_SPEED_SOURCE_LABELS: Record<FuelSpeedSource, string> = {
  auto: 'Auto (VSS → GPS)',
  gps: 'GPS Speed',
  trueSpeed: 'True Speed (derived)',
};

export interface ShifterConfig {
  shiftPointRpm: number;
  downshiftFloorRpm: number;
}

/**
 * Source mapping and display options for the ECU clock/date entities. The
 * firmware reports the clock as separate datalinks, so each part is mapped
 * independently and auto-detected by name.
 */
export interface RtcConfig {
  statusEntityId: number | null;
  dayEntityId: number | null;
  monthEntityId: number | null;
  yearEntityId: number | null;
  hourEntityId: number | null;
  minuteEntityId: number | null;
  secondEntityId: number | null;
  /** Render the clock as 12-hour with an AM/PM suffix. */
  hour12: boolean;
  /** Include seconds in the clock text. */
  showSeconds: boolean;
}

export function defaultRtcConfig(): RtcConfig {
  return {
    statusEntityId: null,
    dayEntityId: null,
    monthEntityId: null,
    yearEntityId: null,
    hourEntityId: null,
    minuteEntityId: null,
    secondEntityId: null,
    hour12: false,
    showSeconds: true,
  };
}

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
  /** Fuel flow channel (L/hr) providing the consumption source (e.g. "Fuelfow Est." ID 35). */
  fuelFlowEntityId: number | null;
  /** Injector duty channel (%) used to calculate flow when no flow channel is mapped. */
  injectorDutyEntityId: number | null;
  /** Per-injector nominal flow (cc/min); zero disables the duty-based fallback. */
  injectorFlowCcPerMin: number;
  /** Physical cylinder count used with injector duty to calculate fuel flow. */
  cylinderCount: number;
  /** Output unit of the −3007 fuel-efficiency entity. */
  fuelEfficiencyUnit: FuelEfficiencyUnit;
  /** Speed source feeding the −3007 calculation. */
  fuelSpeedSource: FuelSpeedSource;
  /** ECU clock/date source mapping and display options. */
  rtc: RtcConfig;
  shifter?: ShifterConfig;
}

export function defaultDerivedConfig(): VehicleConfig {
  return {
    enabled: true,
    tireDiameterInches: 23,
    finalDriveRatio: 4.3,
    gearRatios: [3.6, 2.2, 1.5, 1.1, 0.85, 0.7],
    wheelSlipPercent: 3,
    rpmEntityId: null,
    vssSpeedEntityId: null,
    mapEntityId: null,
    baroEntityId: null,
    gearEntityId: null,
    fuelFlowEntityId: null,
    injectorDutyEntityId: null,
    injectorFlowCcPerMin: 0,
    cylinderCount: 4,
    fuelEfficiencyUnit: 'kmL',
    fuelSpeedSource: 'auto',
    rtc: defaultRtcConfig(),
    shifter: { shiftPointRpm: 0, downshiftFloorRpm: 0 },
  };
}

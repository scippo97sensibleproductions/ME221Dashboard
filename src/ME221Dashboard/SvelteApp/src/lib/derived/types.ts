export const DERIVED_ENTITY_BASE = -3000;

export enum DerivedEntityId {
  Gear = -3001,
  TrueSpeed = -3002,
  Boost = -3003,
  SpeedError = -3004,
  RpmToShift = -3005,
  ShiftState = -3006,
}

export const DERIVED_ENTITIES: Record<number, { name: string; unit: string; minValue: number; maxValue: number; decimalPlaces: number }> = {
  [DerivedEntityId.Gear]:      { name: 'Gear',           unit: '',     minValue: -1, maxValue: 10, decimalPlaces: 0 },
  [DerivedEntityId.TrueSpeed]: { name: 'True Speed',     unit: 'km/h', minValue: 0,  maxValue: 400, decimalPlaces: 1 },
  [DerivedEntityId.Boost]:     { name: 'Boost Pressure', unit: 'kPa',  minValue: 0,  maxValue: 400, decimalPlaces: 1 },
  [DerivedEntityId.SpeedError]:{ name: 'Speed Error',    unit: 'km/h', minValue: -50, maxValue: 50, decimalPlaces: 1 },
  [DerivedEntityId.RpmToShift]:{ name: 'RPM to Shift',   unit: 'rpm',  minValue: 0,  maxValue: 9000, decimalPlaces: 1 },
  [DerivedEntityId.ShiftState]:{ name: 'Shift State',    unit: '',     minValue: -1, maxValue: 1, decimalPlaces: 0 },
};

export interface ShifterConfig {
  shiftPointRpm: number;
  downshiftFloorRpm: number;
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
    shifter: { shiftPointRpm: 0, downshiftFloorRpm: 0 },
  };
}

import { DerivedEntityId } from './types';
import type { VehicleConfig } from './types';

export interface ComputationInputs {
  entityValues: Record<string, number | null>;
  config: VehicleConfig;
  gpsSpeedKmh: number | null;
  gpsValid: boolean;
}

export interface ComputationResult {
  [DerivedEntityId.Gear]: number | null;
  [DerivedEntityId.TrueSpeed]: number | null;
  [DerivedEntityId.Boost]: number | null;
  [DerivedEntityId.SpeedError]: number | null;
}

function safeEntity(values: Record<string, number | null>, id: number | null): number | null {
  if (id === null) return null;
  const v = values[String(id)];
  return v !== undefined && v !== null ? v : null;
}

function tireCircumferenceMeters(diameterInches: number): number {
  return diameterInches * 0.0254 * Math.PI;
}

export function computeDerived(inputs: ComputationInputs): ComputationResult {
  const { config } = inputs;
  const rpm = safeEntity(inputs.entityValues, config.rpmEntityId);
  const vssSpeed = safeEntity(inputs.entityValues, config.vssSpeedEntityId);
  const map = safeEntity(inputs.entityValues, config.mapEntityId);
  const baro = safeEntity(inputs.entityValues, config.baroEntityId);
  const gearRaw = safeEntity(inputs.entityValues, config.gearEntityId);

  const tireCirc = tireCircumferenceMeters(config.tireDiameterInches);

  let gear: number | null = null;
  let trueSpeed: number | null = null;

  // Prefer raw gear entity from ECU
  if (gearRaw !== null && gearRaw > 0 && gearRaw <= config.gearRatios.length) {
    gear = Math.round(gearRaw);
  }

  // Predict gear from RPM / speed ratio
  // From emulator RpmToSpeedKmh: speed = RPM * tireCirc * 60 / (gearRatio * finalDrive * 1000)
  // Therefore: RPM / speed = gearRatio * finalDrive * 1000 / (tireCirc * 60)
  const speedForGearCalc = vssSpeed ?? inputs.gpsSpeedKmh;
  if (gear === null && rpm !== null && speedForGearCalc !== null && speedForGearCalc > 0.5) {
    const ratio = rpm / speedForGearCalc;
    let bestMatch = -1;
    let bestDiff = Infinity;
    const expectedRatios: number[] = [];
    for (let i = 0; i < config.gearRatios.length; i++) {
      const expectedRatio = config.gearRatios[i] * config.finalDriveRatio * 1000 / (tireCirc * 60);
      expectedRatios.push(expectedRatio);
      const diff = Math.abs(ratio - expectedRatio);
      if (diff < bestDiff) {
        bestDiff = diff;
        bestMatch = i;
      }
    }
    if (bestMatch >= 0 && bestDiff / expectedRatios[bestMatch] < 0.15) {
      gear = bestMatch + 1;
    }
  }

  // Detect neutral / standstill
  if (gear === null && rpm !== null && speedForGearCalc !== null && speedForGearCalc < 0.5 && rpm > 800) {
    gear = 0; // Neutral
  }

  // True speed from RPM + predicted gear
  // speed = RPM * tireCirc * 60 / (gearRatio * finalDrive * 1000)
  if (gear !== null && gear > 0 && rpm !== null && gear <= config.gearRatios.length) {
    const calcSpeed = rpm * tireCirc * 60 / (config.gearRatios[gear - 1] * config.finalDriveRatio * 1000);
    trueSpeed = calcSpeed;
  }

  // Boost pressure
  let boost: number | null = null;
  if (map !== null) {
    if (baro !== null) {
      boost = map - baro;
    } else {
      boost = map - 100;
    }
  }

  // Speed error (GPS vs VSS)
  let speedError: number | null = null;
  if (inputs.gpsValid && inputs.gpsSpeedKmh !== null && vssSpeed !== null) {
    speedError = inputs.gpsSpeedKmh - vssSpeed;
  } else if (inputs.gpsValid && inputs.gpsSpeedKmh !== null && trueSpeed !== null) {
    speedError = inputs.gpsSpeedKmh - trueSpeed;
  }

  return {
    [DerivedEntityId.Gear]: gear,
    [DerivedEntityId.TrueSpeed]: trueSpeed,
    [DerivedEntityId.Boost]: boost,
    [DerivedEntityId.SpeedError]: speedError,
  };
}

export function formatDerivedValue(entityId: number, rawValue: number | null): string {
  if (rawValue === null) return '---';
  if (entityId === DerivedEntityId.Gear) {
    if (rawValue === 0) return 'N';
    if (rawValue === -1) return 'R';
    return String(Math.round(rawValue));
  }
  if (entityId === DerivedEntityId.Boost) {
    if (rawValue < 0) return rawValue.toFixed(1);
    return '+' + rawValue.toFixed(1);
  }
  return String(rawValue);
}

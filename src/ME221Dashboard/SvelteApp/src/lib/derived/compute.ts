import { DerivedEntityId } from './types';
import type { FuelEfficiencyUnit, VehicleConfig } from './types';
import { defaultRtcConfig } from './types';
import { rtcClockSeconds, rtcDateStamp } from './rtc';

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
  [DerivedEntityId.RpmToShift]: number | null;
  [DerivedEntityId.FuelEfficiency]: number | null;
  [DerivedEntityId.EcuClock]: number | null;
  [DerivedEntityId.EcuDate]: number | null;
}

/**
 * Countdown (R2): shift point minus current RPM, clamped at 0; 0 means the shift
 * point is reached. Null per R14 whenever the RPM datalink is unconfigured, the
 * RPM sample is null, or the shift point is unset or zero. Shared by the
 * recompute path and the shift tick so both writers agree on the −3005 value.
 */
export function computeRpmToShift(
  rpm: number | null,
  shiftPoint: number | null | undefined,
): number | null {
  if (rpm === null) return null;
  if (shiftPoint == null || shiftPoint <= 0) return null;
  return Math.max(0, shiftPoint - rpm);
}

function safeEntity(values: Record<string, number | null>, id: number | null): number | null {
  if (id === null) return null;
  const v = values[String(id)];
  return v !== undefined && v !== null ? v : null;
}

function tireCircumferenceMeters(diameterInches: number): number {
  return diameterInches * 0.0254 * Math.PI;
}

/** US gallon in liters — the gallon basis of the MPG unit. */
const GALLON_LITERS = 3.785411784;
/** Mile in kilometers — speed conversion for the MPG unit. */
const KM_PER_MILE = 1.609344;

/**
 * Resolve the instantaneous fuel flow (L/hr) from the configured sources.
 * The direct flow channel wins; otherwise duty % of the total injector
 * capacity (per-injector cc/min × cylinder count) is used. Null whenever
 * neither source is mapped/valid so the efficiency entity goes inert.
 */
export function fuelFlowLph(config: VehicleConfig, entityValues: Record<string, number | null>): number | null {
  const direct = safeEntity(entityValues, config.fuelFlowEntityId);
  if (direct !== null && direct > 0) return direct;

  return dutyFuelFlowLph(config, entityValues);
}

/**
 * Fuel usage from injector duty: duty % of the total injector capacity is
 * actually flowing. Total capacity = per-injector cc/min × cylinders, which is
 * inherently 100 % duty flow; × 60 / 1000 converts cc/min to L/hr.
 */
export function dutyFuelFlowLph(config: VehicleConfig, entityValues: Record<string, number | null>): number | null {
  const duty = safeEntity(entityValues, config.injectorDutyEntityId);
  if (duty === null || duty <= 0) return null;
  if (config.injectorFlowCcPerMin <= 0 || config.cylinderCount <= 0) return null;
  const capacityCcPerMin = config.injectorFlowCcPerMin * config.cylinderCount;
  return (duty / 100) * capacityCcPerMin * 60 / 1000;
}

/**
 * Convert a fuel flow (L/hr) and speed (km/h) pair into the configured output
 * unit. A zero/negative flow is treated as unknown (indistinguishable from a
 * dropped channel); below 1 km/h there is no distance anchor, so both null.
 */
export function computeFuelEfficiency(
  flowLph: number | null,
  speedKmh: number | null,
  unit: FuelEfficiencyUnit,
): number | null {
  if (flowLph === null || speedKmh === null || flowLph <= 0 || speedKmh < 1) return null;
  if (unit === 'kmL') return speedKmh / flowLph;
  if (unit === 'mpg') return (speedKmh / KM_PER_MILE) / (flowLph / GALLON_LITERS);
  return (flowLph / speedKmh) * 100;
}

/**
 * Speed (km/h) feeding the consumption figures, honouring the configured
 * source. Shared by the instantaneous entity and the trip accumulator so both
 * integrate the same number.
 */
export function selectFuelSpeedKmh(
  config: VehicleConfig,
  vssSpeedKmh: number | null,
  gpsSpeedKmh: number | null,
  trueSpeedKmh: number | null,
): number | null {
  if (config.fuelSpeedSource === 'gps') return gpsSpeedKmh;
  if (config.fuelSpeedSource === 'trueSpeed') return trueSpeedKmh;
  return vssSpeedKmh ?? gpsSpeedKmh;
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

  // Fuel efficiency shares the odometer speed-source convention, with two extra
  // configurable options: always-GPS, or the derived True Speed chain.
  const fuelSpeed = selectFuelSpeedKmh(config, vssSpeed, inputs.gpsSpeedKmh, trueSpeed);
  const fuelFlow = fuelFlowLph(config, inputs.entityValues);
  const fuelEfficiency = computeFuelEfficiency(fuelFlow, fuelSpeed, config.fuelEfficiencyUnit);

  const rtc = config.rtc ?? defaultRtcConfig();

  return {
    [DerivedEntityId.Gear]: gear,
    [DerivedEntityId.TrueSpeed]: trueSpeed,
    [DerivedEntityId.Boost]: boost,
    [DerivedEntityId.SpeedError]: speedError,
    [DerivedEntityId.RpmToShift]: computeRpmToShift(rpm, config.shifter?.shiftPointRpm),
    [DerivedEntityId.FuelEfficiency]: fuelEfficiency,
    [DerivedEntityId.EcuClock]: rtcClockSeconds(inputs.entityValues, rtc),
    [DerivedEntityId.EcuDate]: rtcDateStamp(inputs.entityValues, rtc),
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
  if (entityId === DerivedEntityId.ShiftState) {
    if (rawValue === -1) return 'DOWN';
    if (rawValue === 1) return 'SHIFT';
    return 'CRUISE';
  }
  if (entityId === DerivedEntityId.FuelEfficiency) {
    return rawValue.toFixed(1);
  }
  if (entityId === DerivedEntityId.AverageFuelEfficiency) {
    return rawValue.toFixed(1);
  }
  return String(rawValue);
}

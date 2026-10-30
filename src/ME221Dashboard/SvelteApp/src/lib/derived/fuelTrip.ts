import type { FuelEfficiencyUnit } from './types';

/**
 * Fuel-trip accumulator.
 *
 * Instantaneous efficiency (speed ÷ flow) swings wildly at low speed and reads
 * as "---" whenever the car stops. The trip average instead integrates fuel and
 * distance over time, so idling costs fuel without distance — which is exactly
 * what a tank-to-tank figure does.
 *
 * Pure functions only: the owning store supplies wall-clock deltas and the
 * persistence layer supplies the restored totals.
 */

/** US gallon in liters — the gallon basis of the MPG unit. */
const GALLON_LITERS = 3.785411784;
/** Mile in kilometers — speed conversion for the MPG unit. */
const KM_PER_MILE = 1.609344;

/**
 * Longest interval a single tick may integrate. A suspended page or a paused
 * app would otherwise credit one giant leap to the most recent sample.
 */
export const MAX_TICK_SECONDS = 5;

export interface FuelTripTotals {
  distanceKm: number;
  fuelLiters: number;
}

export function emptyFuelTrip(): FuelTripTotals {
  return { distanceKm: 0, fuelLiters: 0 };
}

export function hasFuelTripProgress(totals: FuelTripTotals): boolean {
  return totals.distanceKm > 0 || totals.fuelLiters > 0;
}

/**
 * Integrate one tick. Fuel accrues whenever a positive flow is reported (idling
 * included); distance accrues only while moving. Non-positive or unknown inputs
 * are ignored, and the interval is clamped to {@link MAX_TICK_SECONDS}.
 */
export function accumulateFuelTrip(
  totals: FuelTripTotals,
  speedKmh: number | null,
  flowLph: number | null,
  dtSeconds: number,
): FuelTripTotals {
  if (!Number.isFinite(dtSeconds) || dtSeconds <= 0) return totals;
  const dt = Math.min(dtSeconds, MAX_TICK_SECONDS);
  const hours = dt / 3600;

  const fuel = flowLph !== null && Number.isFinite(flowLph) && flowLph > 0
    ? totals.fuelLiters + flowLph * hours
    : totals.fuelLiters;

  const distance = speedKmh !== null && Number.isFinite(speedKmh) && speedKmh > 0
    ? totals.distanceKm + speedKmh * hours
    : totals.distanceKm;

  return { distanceKm: distance, fuelLiters: fuel };
}

/**
 * Average efficiency over the whole trip in the configured unit. Null until both
 * totals are usable — a distance-based unit (L/100km) also needs distance, so a
 * stationary idle period has an average flow ratio but no L/100km figure.
 */
export function averageFuelEfficiency(
  totals: FuelTripTotals,
  unit: FuelEfficiencyUnit,
): number | null {
  const { distanceKm, fuelLiters } = totals;
  if (!(fuelLiters > 0) || !Number.isFinite(fuelLiters)) return null;
  if (unit === 'l100km') {
    if (!(distanceKm > 0) || !Number.isFinite(distanceKm)) return null;
    return (fuelLiters / distanceKm) * 100;
  }
  if (!(distanceKm > 0) || !Number.isFinite(distanceKm)) return null;
  if (unit === 'mpg') return (distanceKm / KM_PER_MILE) / (fuelLiters / GALLON_LITERS);
  return distanceKm / fuelLiters;
}

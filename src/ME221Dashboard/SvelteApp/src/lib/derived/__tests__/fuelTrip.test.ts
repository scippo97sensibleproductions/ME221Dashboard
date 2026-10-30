import { describe, it, expect } from 'vitest';
import {
  MAX_TICK_SECONDS,
  accumulateFuelTrip,
  averageFuelEfficiency,
  emptyFuelTrip,
  hasFuelTripProgress,
} from '../fuelTrip';

describe('accumulateFuelTrip', () => {
  it('integrates distance and fuel over a full hour', () => {
    // 100 km/h for one hour = 100 km; 10 L/h = 10 L.
    let totals = emptyFuelTrip();
    for (let i = 0; i < 3600; i++) {
      totals = accumulateFuelTrip(totals, 100, 10, 1);
    }
    expect(totals.distanceKm).toBeCloseTo(100, 6);
    expect(totals.fuelLiters).toBeCloseTo(10, 6);
    expect(averageFuelEfficiency(totals, 'kmL')).toBeCloseTo(10, 5);
  });

  it('counts fuel burned at standstill but adds no distance', () => {
    const totals = accumulateFuelTrip(emptyFuelTrip(), 0, 2, 5);
    expect(totals.distanceKm).toBe(0);
    expect(totals.fuelLiters).toBeCloseTo(2 * 5 / 3600, 6);
  });

  it('adds distance when flow is unknown and fuel when speed is unknown', () => {
    const moving = accumulateFuelTrip(emptyFuelTrip(), 60, null, 5);
    expect(moving.distanceKm).toBeCloseTo(60 * 5 / 3600, 6);
    expect(moving.fuelLiters).toBe(0);

    const parked = accumulateFuelTrip(emptyFuelTrip(), null, 5, 5);
    expect(parked.distanceKm).toBe(0);
    expect(parked.fuelLiters).toBeCloseTo(5 * 5 / 3600, 6);
  });

  it('ignores non-positive or non-finite ticks', () => {
    const base = { distanceKm: 1, fuelLiters: 2 };
    expect(accumulateFuelTrip(base, 100, 10, 0)).toBe(base);
    expect(accumulateFuelTrip(base, 100, 10, -1)).toBe(base);
    expect(accumulateFuelTrip(base, 100, 10, Number.NaN)).toBe(base);
  });

  it('clamps a long interval so a suspended page cannot credit a leap', () => {
    const totals = accumulateFuelTrip(emptyFuelTrip(), 100, 10, 600);
    expect(totals.distanceKm).toBeCloseTo(100 * MAX_TICK_SECONDS / 3600, 6);
    expect(totals.fuelLiters).toBeCloseTo(10 * MAX_TICK_SECONDS / 3600, 6);
  });

  it('ignores negative flow (a dropped channel reads as noise, not fuel)', () => {
    const totals = accumulateFuelTrip(emptyFuelTrip(), 50, -3, 10);
    expect(totals.fuelLiters).toBe(0);
    expect(totals.distanceKm).toBeGreaterThan(0);
  });
});

describe('averageFuelEfficiency', () => {
  const trip = { distanceKm: 100, fuelLiters: 10 };

  it('converts the same totals into every configured unit', () => {
    expect(averageFuelEfficiency(trip, 'kmL')).toBeCloseTo(10, 6);
    expect(averageFuelEfficiency(trip, 'l100km')).toBeCloseTo(10, 6);
    // 100 km = 62.137 mi; 10 L = 2.642 gal → 23.52 MPG.
    expect(averageFuelEfficiency(trip, 'mpg')).toBeCloseTo(23.52, 2);
  });

  it('stays null until fuel has been used', () => {
    expect(averageFuelEfficiency(emptyFuelTrip(), 'kmL')).toBeNull();
    expect(averageFuelEfficiency({ distanceKm: 5, fuelLiters: 0 }, 'kmL')).toBeNull();
  });

  it('withholds distance-based units when no distance was covered', () => {
    const idling = { distanceKm: 0, fuelLiters: 1 };
    expect(averageFuelEfficiency(idling, 'kmL')).toBeNull();
    expect(averageFuelEfficiency(idling, 'l100km')).toBeNull();
    expect(averageFuelEfficiency(idling, 'mpg')).toBeNull();
  });
});

describe('hasFuelTripProgress', () => {
  it('reports whether the trip has anything to show', () => {
    expect(hasFuelTripProgress(emptyFuelTrip())).toBe(false);
    expect(hasFuelTripProgress({ distanceKm: 0, fuelLiters: 0.1 })).toBe(true);
    expect(hasFuelTripProgress({ distanceKm: 1, fuelLiters: 0 })).toBe(true);
  });
});

import { HybridBridge } from '../HybridBridge';
import {
  accumulateFuelTrip,
  averageFuelEfficiency,
  emptyFuelTrip,
  type FuelTripTotals,
} from './fuelTrip';
import type { FuelEfficiencyUnit } from './types';

/** How often accumulated totals are written to disk while driving. */
const SAVE_INTERVAL_MS = 10_000;

/**
 * Owns the fuel-trip totals and their persistence.
 *
 * The dashboard page feeds it one tick per rendered frame, and both the average
 * entity and the settings reset read from here — a single owner means a reset
 * takes effect immediately instead of being overwritten by a stale copy.
 */
class FuelTripStore {
  distanceKm = $state(0);
  fuelLiters = $state(0);

  private loaded = false;
  private dirty = false;
  private saving = false;
  private lastSavedAt = 0;

  get totals(): FuelTripTotals {
    return { distanceKm: this.distanceKm, fuelLiters: this.fuelLiters };
  }

  /** Restore persisted totals. Until this resolves, ticks are ignored so a
   *  slow read cannot be credited to a zeroed trip. */
  async load(): Promise<void> {
    try {
      const result = await HybridBridge.getFuelTrip();
      this.distanceKm = result.distanceKm ?? 0;
      this.fuelLiters = result.fuelLiters ?? 0;
    } catch (err) {
      console.error('[FUELTRIP] load failed:', err);
    }
    this.loaded = true;
    this.dirty = false;
    this.lastSavedAt = Date.now();
  }

  /** Integrate one frame; no-op until the persisted baseline has loaded. */
  accumulate(speedKmh: number | null, flowLph: number | null, dtSeconds: number): void {
    if (!this.loaded) return;
    const next = accumulateFuelTrip(this.totals, speedKmh, flowLph, dtSeconds);
    if (next.distanceKm === this.distanceKm && next.fuelLiters === this.fuelLiters) return;

    this.distanceKm = next.distanceKm;
    this.fuelLiters = next.fuelLiters;
    this.dirty = true;

    const now = Date.now();
    if (now - this.lastSavedAt >= SAVE_INTERVAL_MS) {
      this.lastSavedAt = now;
      void this.persist();
    }
  }

  average(unit: FuelEfficiencyUnit): number | null {
    return averageFuelEfficiency(this.totals, unit);
  }

  /** Zero the trip and persist immediately. */
  async reset(): Promise<void> {
    const cleared = emptyFuelTrip();
    this.distanceKm = cleared.distanceKm;
    this.fuelLiters = cleared.fuelLiters;
    this.loaded = true;
    this.dirty = true;
    this.lastSavedAt = Date.now();
    await this.persist();
  }

  /** Write pending totals (page teardown, connection loss). */
  async flush(): Promise<void> {
    await this.persist();
  }

  private async persist(): Promise<void> {
    if (!this.dirty || this.saving) return;
    this.saving = true;
    // Snapshot before the await: ticks that land mid-write stay dirty and are
    // written by the next save instead of being silently dropped.
    const { distanceKm, fuelLiters } = this.totals;
    this.dirty = false;
    try {
      await HybridBridge.saveFuelTrip(distanceKm, fuelLiters);
    } catch (err) {
      this.dirty = true;
      console.error('[FUELTRIP] save failed:', err);
    } finally {
      this.saving = false;
    }
  }
}

export const fuelTripStore = new FuelTripStore();

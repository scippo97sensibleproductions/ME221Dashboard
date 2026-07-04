import { HybridBridge } from '../HybridBridge';
import type { BridgeEvent, LiveDataEvent, GpsUpdateEvent, OdometerUpdateEvent, ConnectionStateChangedEvent } from '../HybridBridgeTypes';

interface GpsSnapshot {
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

const STALE_MS = 2500;
const RECONNECT_REENABLE_COOLDOWN_MS = 5000;

class LiveDataStore {
  values = $state<Record<string, number | null>>({});
  odometer = $state<number | null>(null);
  odometerUnit = $state<string>('km');
  gps = $state<GpsSnapshot | null>(null);
  connectionState = $state<'disconnected' | 'connecting' | 'connected' | 'error'>('disconnected');
  lastUpdateAt = $state(0);
  frameCount = $state(0);
  isReportingActive = $state(false);
  reenableTick = $state(0);

  #unsubscribe: (() => void) | null = null;
  #rafId: number | null = null;
  #watchdogTimer: ReturnType<typeof setInterval> | null = null;
  #lastFrameValues: Record<string, number | null> | null = null;
  #lastReenableAt = 0;
  #reenableInFlight = false;

  start(): void {
    if (this.#unsubscribe) return;
    this.#unsubscribe = HybridBridge.onMessage((event: BridgeEvent) => this._handleEvent(event));
    this.#startWatchdog();
  }

  stop(): void {
    this.#stopWatchdog();
    this.#unsubscribe?.();
    this.#unsubscribe = null;
    if (this.#rafId !== null) {
      cancelAnimationFrame(this.#rafId);
      this.#rafId = null;
    }
    this.#lastFrameValues = null;
    this.isReportingActive = false;
  }

  private _handleEvent(event: BridgeEvent): void {
    if (!event) return;
    switch (event.event) {
      case 'liveDataUpdate':
        this._onLiveData(event);
        break;
      case 'gpsUpdate':
        this._onGpsUpdate(event);
        break;
      case 'odometerUpdate':
        this._onOdometerUpdate(event);
        break;
      case 'connectionStateChanged':
        this._onConnectionStateChanged(event);
        break;
    }
  }

  private _onLiveData(event: LiveDataEvent): void {
    this.#lastFrameValues = event.values;
    if (this.#rafId !== null) return;
    this.#rafId = requestAnimationFrame(() => {
      this.#rafId = null;
      const values = this.#lastFrameValues;
      this.#lastFrameValues = null;
      if (values) this._applyFrame(values);
    });
  }

  private _applyFrame(values: Record<string, number | null>): void {
    const v = this.values;
    for (const id in values) {
      v[id] = values[id] as number | null;
    }
    this.lastUpdateAt = performance.now();
    this.frameCount++;
    if (!this.isReportingActive) this.isReportingActive = true;
  }

  private _onGpsUpdate(event: GpsUpdateEvent): void {
    this.gps = {
      latitude: event.latitude,
      longitude: event.longitude,
      altitude: event.altitude,
      speed: event.speed,
      course: event.course,
      accuracy: event.accuracy,
      timestamp: event.timestamp,
      odometer: event.odometer,
      odometerUnit: event.odometerUnit,
    };
  }

  private _onOdometerUpdate(event: OdometerUpdateEvent): void {
    this.odometer = event.odometer;
    this.odometerUnit = event.odometerUnit;
  }

  private _onConnectionStateChanged(event: ConnectionStateChangedEvent): void {
    const prev = this.connectionState;
    const next = event.state as 'disconnected' | 'connecting' | 'connected' | 'error';
    this.connectionState = next;
    if (prev === 'connected' && next !== 'connected') {
      this.isReportingActive = false;
    }
  }

  /** App-level hook: call when entering Connected state. Idempotent. */
  async enableReporting(): Promise<void> {
    // Don't guard on this.connectionState — the App calls this BEFORE
    // the C# connectionStateChanged event arrives, so the store state
    // may still be 'disconnected'. The App is the authority on when to
    // enable reporting, not the store's own event listener.
    try {
      await HybridBridge.enableReporting();
      this.connectionState = 'connected';
      this.#lastReenableAt = performance.now();
    } catch {
      // Best-effort: watchdog will retry if frames stop arriving.
    }
  }


  /** App-level hook for explicit re-enable (manual recovery). */
  async requestReenable(): Promise<void> {
    if (this.#reenableInFlight) return;
    const now = performance.now();
    if (now - this.#lastReenableAt < RECONNECT_REENABLE_COOLDOWN_MS) return;
    this.#reenableInFlight = true;
    try {
      await HybridBridge.enableReporting();
      this.#lastReenableAt = performance.now();
      this.reenableTick++;
    } catch {
      // Suppress — bridge may be dead.
    } finally {
      this.#reenableInFlight = false;
    }
  }

  #startWatchdog(): void {
    this.#stopWatchdog();
    this.#watchdogTimer = setInterval(() => {
      if (this.connectionState !== 'connected') return;
      if (this.frameCount === 0) return;
      const staleMs = performance.now() - this.lastUpdateAt;
      if (staleMs <= STALE_MS) return;
      if (this.isReportingActive) {
        this.isReportingActive = false;
        this.requestReenable();
      }
    }, 1000);
  }

  #stopWatchdog(): void {
    if (this.#watchdogTimer) {
      clearInterval(this.#watchdogTimer);
      this.#watchdogTimer = null;
    }
  }
}

export const liveDataStore = new LiveDataStore();
export type { GpsSnapshot };

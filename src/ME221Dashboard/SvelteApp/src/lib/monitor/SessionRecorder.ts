import { liveDataStore } from '../stores/LiveDataStore.svelte';
import type { FreezeFrame } from './SessionStore';
import { buildSessionCsv, buildSessionVdCsv } from './sessionCsv';

export type RecordingState = 'idle' | 'recording' | 'stopped';

interface Sample {
  t: number;
  v: number;
}

const MAX_DURATION_MS = 30 * 60 * 1000;

class SessionRecorderClass {
  #state: RecordingState = 'idle';
  #buffer = new Map<number, Sample[]>();
  #startTime = 0;
  #rafId: number | null = null;
  #recordedSensorIds: number[] = [];
  #sensorNames = new Map<number, string>();
  #freezeFrames: FreezeFrame[] = [];
  #companionMode = false;
  #recordAllFeed = false;
  #lastFeedSeq: number | null = null;
  #finalized = false;
  #completionNotice: { reason: string } | null = null;
  #segmentCount = 0;

  get state(): RecordingState {
    return this.#state;
  }

  get durationMs(): number {
    if (this.#state === 'idle') return 0;
    return performance.now() - this.#startTime;
  }

  get sensorIds(): number[] {
    return this.#recordedSensorIds;
  }

  /** Completion notice text (e.g. 'Session ended'); null until finalize. */
  get completionNotice(): string | null {
    return this.#completionNotice?.reason ?? null;
  }

  /** Number of completed segments in the current session (R18 reconnect continuation). */
  get segmentCount(): number {
    return this.#segmentCount;
  }

  /** True once finalize() has closed the open recording. */
  get isFinalized(): boolean {
    return this.#finalized;
  }

  start(sensorIds: number[], nameMap?: Map<number, string>): void {
    if (this.#state === 'recording') return;
    this.#state = 'recording';
    this.#companionMode = false;
    this.#recordAllFeed = false;
    this.#lastFeedSeq = null;
    this.#finalized = false;
    this.#completionNotice = null;
    this.#segmentCount = 0;
    this.#startTime = performance.now();
    this.#buffer.clear();
    this.#recordedSensorIds = sensorIds;
    this.#sensorNames = nameMap ?? new Map();
    this.#freezeFrames = [];
    for (const id of sensorIds) {
      this.#buffer.set(id, []);
    }
    this.#tick();
  }

  /** Companion mode (R18, KTD-9): buffers are fed by pushFeedFrame, not rAF.
   *  With an empty sensor list every key present in the feed is recorded. */
  startCompanion(sensorIds: number[], nameMap?: Map<number, string>): void {
    if (this.#state === 'recording') return;
    this.#state = 'recording';
    this.#companionMode = true;
    this.#recordAllFeed = sensorIds.length === 0;
    this.#lastFeedSeq = null;
    this.#finalized = false;
    this.#completionNotice = null;
    this.#startTime = performance.now();
    this.#buffer.clear();
    this.#recordedSensorIds = [...sensorIds];
    this.#sensorNames = nameMap ?? new Map();
    this.#freezeFrames = [];
    for (const id of sensorIds) {
      this.#buffer.set(id, []);
    }
  }

  /** One frame from the mirror recording feed. Missing sequence numbers are
   *  recorded as gap rows (v: NaN) so dropped segments stay visible in exports.
   *  No 30-minute cap in companion mode (KTD-9). */
  pushFeedFrame(seq: number, ts: number, values: Record<string, number | null>): void {
    if (this.#state !== 'recording' || !this.#companionMode) return;
    if (this.#lastFeedSeq !== null && seq <= this.#lastFeedSeq) return;
    const knownIds = [...this.#buffer.keys()];
    if (this.#lastFeedSeq !== null && seq !== this.#lastFeedSeq + 1) {
      const missing = seq - this.#lastFeedSeq - 1;
      for (let i = 0; i < missing; i++) {
        for (const id of knownIds) {
          this.#buffer.get(id)!.push({ t: ts, v: Number.NaN });
        }
      }
    }
    this.#lastFeedSeq = seq;
    for (const [key, value] of Object.entries(values)) {
      if (value == null) continue;
      const id = Number(key);
      if (!Number.isFinite(id)) continue;
      if (!this.#recordAllFeed && !this.#recordedSensorIds.includes(id)) continue;
      let buf = this.#buffer.get(id);
      if (!buf) {
        buf = [];
        this.#buffer.set(id, buf);
        this.#recordedSensorIds.push(id);
      }
      buf.push({ t: ts, v: value });
    }
  }

  /** Finish the open recording with a completion notice (session end, R18). */
  finalize(reason?: string): void {
    if (this.#state !== 'recording') return;
    this.#state = 'stopped';
    if (this.#rafId !== null) {
      cancelAnimationFrame(this.#rafId);
      this.#rafId = null;
    }
    this.#finalized = true;
    this.#completionNotice = { reason: reason ?? 'Recording finalized' };
  }

  /** Reconnect continuation (R18): finalize the current segment and start a
   *  fresh one without keeping the old samples. */
  newSegment(): void {
    // R21: a reconnect starts a new recording segment — whether the previous
    // segment was stopped by a finalize (ECU disconnect) or is still live.
    this.finalize(); // no-op when already stopped
    this.#segmentCount++;
    this.startCompanion(this.#recordAllFeed ? [] : this.#recordedSensorIds, this.#sensorNames);
  }

  #tick = (): void => {
    if (this.#state !== 'recording') return;
    const now = performance.now();
    for (const [id, buf] of this.#buffer) {
      const val = liveDataStore.values[id];
      if (val != null) {
        buf.push({ t: now - this.#startTime, v: val });
        if (buf.length > MAX_DURATION_MS / 16.7) {
          buf.splice(0, buf.length - MAX_DURATION_MS / 16.7);
        }
      }
    }
    this.#rafId = requestAnimationFrame(this.#tick);
  };

  stop(): void {
    if (this.#state !== 'recording') return;
    this.#state = 'stopped';
    if (this.#rafId !== null) {
      cancelAnimationFrame(this.#rafId);
      this.#rafId = null;
    }
  }

  freezeFrame(label?: string): void {
    if (this.#state !== 'recording') return;
    this.#freezeFrames.push({
      timeMs: this.durationMs,
      label: label ?? `Frame ${this.#freezeFrames.length + 1}`,
      timestamp: new Date().toISOString(),
    });
  }

  reset(): void {
    this.#state = 'idle';
    if (this.#rafId !== null) {
      cancelAnimationFrame(this.#rafId);
      this.#rafId = null;
    }
    this.#buffer.clear();
    this.#recordedSensorIds = [];
    this.#sensorNames = new Map();
    this.#freezeFrames = [];
    this.#companionMode = false;
    this.#recordAllFeed = false;
    this.#lastFeedSeq = null;
    this.#finalized = false;
    this.#completionNotice = null;
    this.#segmentCount = 0;
  }

  getRecordedData(): {
    sensorIds: number[];
    sensorNames: Record<number, string>;
    data: Record<number, Array<{ t: number; v: number }>>;
    durationMs: number;
    freezeFrames: FreezeFrame[];
  } {
    const sensorNames: Record<number, string> = {};
    for (const [id, name] of this.#sensorNames) {
      sensorNames[id] = name;
    }
    const data: Record<number, Array<{ t: number; v: number }>> = {};
    for (const [id, buf] of this.#buffer) {
      data[id] = [...buf];
    }
    return {
      sensorIds: [...this.#recordedSensorIds],
      sensorNames,
      data,
      durationMs: this.durationMs,
      freezeFrames: [...this.#freezeFrames],
    };
  }

  getBuffer(): Map<number, Sample[]> {
    return this.#buffer;
  }

  toCsv(): string {
    return buildSessionCsv(this.getRecordedData());
  }

  toVirtualDynoCsv(): string {
    return buildSessionVdCsv(this.getRecordedData());
  }

  toYaml(): string {
    const lines: string[] = ['session:'];
    lines.push(`  duration_ms: ${Math.round(this.durationMs)}`);
    lines.push('  sensors:');
    for (const id of this.#recordedSensorIds) {
      const buf = this.#buffer.get(id) ?? [];
      lines.push(`    ${id}:`);
      for (const s of buf) {
        lines.push(`      - t: ${s.t.toFixed(1)}`);
        lines.push(`        v: ${Number.isNaN(s.v) ? 'GAP' : s.v}`);
      }
    }
    return lines.join('\n');
  }
}

export const SessionRecorder = new SessionRecorderClass();

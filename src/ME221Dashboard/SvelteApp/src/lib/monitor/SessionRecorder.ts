import { liveDataStore } from '../stores/LiveDataStore.svelte';

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

  start(sensorIds: number[]): void {
    if (this.#state === 'recording') return;
    this.#state = 'recording';
    this.#startTime = performance.now();
    this.#buffer.clear();
    this.#recordedSensorIds = sensorIds;
    for (const id of sensorIds) {
      this.#buffer.set(id, []);
    }
    this.#tick();
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

  reset(): void {
    this.#state = 'idle';
    if (this.#rafId !== null) {
      cancelAnimationFrame(this.#rafId);
      this.#rafId = null;
    }
    this.#buffer.clear();
    this.#recordedSensorIds = [];
  }

  getBuffer(): Map<number, Sample[]> {
    return this.#buffer;
  }

  toCsv(): string {
    const ids = this.#recordedSensorIds;
    if (ids.length === 0) return '';

    const headers = ['time_ms', ...ids.map(String)];
    const rows: string[] = [headers.join(',')];

    const maxLen = Math.max(...ids.map((id) => this.#buffer.get(id)?.length ?? 0));
    for (let i = 0; i < maxLen; i++) {
      const row: string[] = [];
      for (const id of ids) {
        const buf = this.#buffer.get(id);
        const sample = buf?.[i];
        row.push(sample ? `${sample.t.toFixed(1)},${sample.v}` : ',');
      }
      rows.push(row.join(','));
    }
    return rows.join('\n');
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
        lines.push(`        v: ${s.v}`);
      }
    }
    return lines.join('\n');
  }
}

export const SessionRecorder = new SessionRecorderClass();

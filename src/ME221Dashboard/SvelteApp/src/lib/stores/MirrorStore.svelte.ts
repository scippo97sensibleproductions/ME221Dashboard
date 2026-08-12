import { HybridBridge } from '../HybridBridge';
import type { BridgeEvent } from '../HybridBridgeTypes';
import { MirrorChunkReassembler } from '../mirror/mirrorTypes';
import type { MirrorSnapshot, SnapshotChunkMessage, SnapshotDoneMessage } from '../mirror/mirrorTypes';

export type MirrorClientState =
  | 'Searching'
  | 'NoA'
  | 'Incompatible'
  | 'AwaitingApproval'
  | 'Refused'
  | 'Connected'
  | 'Disconnected'
  | 'Revoked'
  | 'RePair'
  | 'Stopped';

export interface MirrorSessionStateEvent {
  event: 'mirrorSessionState';
  state: MirrorClientState;
  error?: string | null;
  token?: string | null;
  displayName?: string | null;
}

/** R11 stale threshold: no frame for 2 s while the ECU was live and changing. */
const STALE_AFTER_MS = 2000;

/**
 * B-side mirror store (U8): reassembles the relayed snapshot, invalidates the bridge
 * config caches, applies dashboard names/active dashboard/warning settings before
 * opening the frame gate, and tracks the mirror session state + stale indicator.
 */
class MirrorStore {
  sessionState = $state<MirrorClientState>('Searching');
  sessionError = $state<string | null>(null);
  token = $state<string | null>(null);
  displayName = $state<string | null>(null);
  snapshotApplied = $state(false);
  snapshotSeq = $state(0);
  dashboardNames = $state<string[]>([]);
  activeDashboard = $state<string>('default');
  isStale = $state(false);

  #reassembler: MirrorChunkReassembler | null = null;
  #unsubscribe: (() => void) | null = null;
  #staleTimer: ReturnType<typeof setTimeout> | null = null;
  #lastFrameAt = 0;

  start(): void {
    if (this.#unsubscribe) return;
    this.#unsubscribe = HybridBridge.onMessage((event: BridgeEvent) => this.#handle(event));
  }

  stop(): void {
    this.#unsubscribe?.();
    this.#unsubscribe = null;
    this.#clearStaleTimer();
    this.#reset();
  }

  get isMirroring(): boolean {
    return this.sessionState === 'Connected';
  }

  /** Test seam: dispatch an event as if it arrived from the bridge router. */
  dispatchForTest(event: unknown): void {
    this.#handle(event as BridgeEvent);
  }

  #handle(event: BridgeEvent): void {
    if (!event || typeof event !== 'object') return;
    const e = event as BridgeEvent & { payload?: unknown };
    switch (e.event) {
      case 'mirrorSessionState':
        this.#onSessionState(e as unknown as MirrorSessionStateEvent);
        break;
      case 'mirrorSnapshotChunk':
        this.#onChunk((e as unknown as { payload: SnapshotChunkMessage }).payload);
        break;
      case 'mirrorSnapshotDone':
        this.#onSnapshotDone((e as unknown as { payload: SnapshotDoneMessage }).payload);
        break;
      case 'liveDataUpdate':
        this.#onFrame();
        break;
    }
  }

  #onSessionState(evt: MirrorSessionStateEvent): void {
    this.sessionState = evt.state;
    this.sessionError = evt.error ?? null;
    if (evt.token !== undefined) this.token = evt.token;
    if (evt.displayName !== undefined) this.displayName = evt.displayName;

    if (evt.state !== 'Connected') {
      this.#clearStaleTimer();
      this.isStale = false;
    }
    if (evt.state === 'Disconnected' || evt.state === 'Revoked' || evt.state === 'Stopped') {
      this.snapshotApplied = false;
      this.#reassembler = null;
    }
  }

  #onChunk(chunk: SnapshotChunkMessage): void {
    if (!chunk) return;
    // A new sequence (retry attempt or a new session) starts a fresh assembly; a
    // stale partial reassembler must never reject a restarted snapshot (KTD-2).
    if (this.#reassembler === null || this.#reassembler.expectedIndex === 0 || chunk.seq !== this.#reassembler.seq) {
      this.#reassembler = new MirrorChunkReassembler(chunk.seq);
    }
    this.#reassembler.tryAdd(chunk);
  }

  #onSnapshotDone(done: SnapshotDoneMessage): void {
    const json = this.#reassembler?.reassemble();
    this.#reassembler = null;
    if (!json) return;

    try {
      const snapshot = JSON.parse(json) as MirrorSnapshot;
      // Apply order (U8): settings first, then dashboards; the frame gate opens below.
      HybridBridge.invalidateConfigCaches();
      this.dashboardNames = Object.keys(snapshot.dashboardConfig.dashboards);
      this.activeDashboard = snapshot.dashboardConfig.activeDashboard;
      this.snapshotSeq = done.seq;
      this.snapshotApplied = true;
    } catch {
      // Malformed snapshot: keep the gate closed; the UI shows the failure state.
      this.snapshotApplied = false;
    }
  }

  #onFrame(): void {
    this.#lastFrameAt = performance.now();
    if (this.isStale) {
      // A fresh frame clears the stale state and re-arms the next check.
      this.isStale = false;
    }
    this.#armStaleTimer();
  }

  #armStaleTimer(): void {
    this.#clearStaleTimer();
    this.#staleTimer = setTimeout(() => {
      if (this.sessionState !== 'Connected') return;
      const now = performance.now();
      // >= because the timer fires exactly at STALE_AFTER_MS when the feed stops;
      // a strict > would never flip at the boundary.
      this.isStale = now - this.#lastFrameAt >= STALE_AFTER_MS;
      // Keep checking while connected: re-arm once so a fresh frame clears it.
      if (this.isStale) this.#armStaleTimer();
    }, STALE_AFTER_MS);
  }

  #clearStaleTimer(): void {
    if (this.#staleTimer) {
      clearTimeout(this.#staleTimer);
      this.#staleTimer = null;
    }
  }

  #reset(): void {
    this.sessionState = 'Searching';
    this.sessionError = null;
    this.token = null;
    this.displayName = null;
    this.snapshotApplied = false;
    this.snapshotSeq = 0;
    this.dashboardNames = [];
    this.activeDashboard = 'default';
    this.isStale = false;
  }
}

export const mirrorStore = new MirrorStore();

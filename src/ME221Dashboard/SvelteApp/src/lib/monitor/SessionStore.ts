import { HybridBridge, isBridgeAlive } from '../HybridBridge';

const STORAGE_KEY = 'monitor_sessions';
const CLEAR_FLAG_KEY = 'monitor_sessions_cleared';

export interface FreezeFrame {
  timeMs: number;
  label: string;
  timestamp: string;
}

export interface RecordedSession {
  id: string;
  name: string;
  startTime: string;
  durationMs: number;
  sensorIds: number[];
  sensorNames: Record<number, string>;
  data: Record<number, Array<{ t: number; v: number }>>;
  freezeFrames: FreezeFrame[];
  sensorCount?: number;
}

export interface SessionSummary {
  id: string;
  name: string;
  startTime: string;
  durationMs: number;
  sensorCount: number;
}

function generateId(): string {
  return crypto.randomUUID();
}

function loadFromLocalStorage(): RecordedSession[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    return JSON.parse(raw) as RecordedSession[];
  } catch {
    return [];
  }
}

function saveToLocalStorage(sessions: RecordedSession[]): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(sessions));
}

class SessionStoreClass {
  #sessions: RecordedSession[] = loadFromLocalStorage();
  #initialized = false;

  get sessions(): RecordedSession[] {
    return this.#sessions;
  }

  /**
   * Initialize by syncing with C# backend.
   * Called once from EcuMonitorPage on mount.
   * Does NOT load full session data — only syncs summaries.
   * Full data is loaded on-demand via loadSession() when user clicks a session.
   */
  async init(): Promise<void> {
    if (this.#initialized) return;

    if (!isBridgeAlive()) {
      this.#initialized = true;
      return;
    }

    try {
      // Check if user deliberately cleared sessions since last init
      const wasCleared = localStorage.getItem(CLEAR_FLAG_KEY) === '1';
      if (wasCleared) {
        localStorage.removeItem(CLEAR_FLAG_KEY);
        // Don't rehydrate from C# — the clear was deliberate
        this.#initialized = true;
        return;
      }

      const csharpSummaries = await HybridBridge.loadSessionList();
      if (csharpSummaries.length === 0 && this.#sessions.length > 0) {
        // C# is empty but localStorage has sessions — migrate in one call
        await HybridBridge.migrateSessions(this.#sessions);
      } else if (csharpSummaries.length > 0 && this.#sessions.length === 0) {
        // C# has sessions but localStorage is empty — load summaries only (not full data)
        this.#sessions = csharpSummaries.map(s => ({
          id: s.id,
          name: s.name,
          startTime: s.startTime,
          durationMs: s.durationMs,
          sensorIds: [],
          sensorNames: {},
          data: {},
          freezeFrames: [],
          sensorCount: s.sensorCount,
        }));
        saveToLocalStorage(this.#sessions);
      }
      // If both have sessions, don't merge — localStorage is primary
    } catch (err) {
      console.warn('[SessionStore] C# sync failed, using localStorage only:', err);
      // Don't initialize — allow retry on next init() call
      return;
    }

    this.#initialized = true;
  }

  save(recording: {
    sensorIds: number[];
    sensorNames: Record<number, string>;
    data: Record<number, Array<{ t: number; v: number }>>;
    durationMs: number;
    freezeFrames: FreezeFrame[];
  }): RecordedSession {
    const session: RecordedSession = {
      id: generateId(),
      name: `Drive ${this.#sessions.length + 1}`,
      startTime: new Date().toISOString(),
      durationMs: recording.durationMs,
      sensorIds: recording.sensorIds,
      sensorNames: recording.sensorNames,
      data: recording.data,
      freezeFrames: recording.freezeFrames,
    };

    // Write-through: localStorage first (fast, always works), then C# (async, may fail)
    this.#sessions = [...this.#sessions, session];
    saveToLocalStorage(this.#sessions);

    // Fire-and-forget C# write
    if (isBridgeAlive()) {
      HybridBridge.saveSessionMetadata(session).catch((err) =>
        console.warn('[SessionStore] C# save failed:', err),
      );
    }

    return session;
  }

  rename(id: string, name: string): void {
    this.#sessions = this.#sessions.map(s => s.id === id ? { ...s, name } : s);
    saveToLocalStorage(this.#sessions);

    if (isBridgeAlive()) {
      HybridBridge.renameSession(id, name).catch((err) =>
        console.warn('[SessionStore] C# rename failed:', err),
      );
    }
  }

  remove(id: string): void {
    this.#sessions = this.#sessions.filter(s => s.id !== id);
    saveToLocalStorage(this.#sessions);

    if (isBridgeAlive()) {
      HybridBridge.deleteSession(id).catch((err) =>
        console.warn('[SessionStore] C# delete failed:', err),
      );
    }
  }

  get(id: string): RecordedSession | undefined {
    return this.#sessions.find(s => s.id === id);
  }

  /**
   * Load full session data on demand from C# backend.
   * Used when a session in localStorage has empty data (loaded from summary only).
   */
  async loadFullSession(id: string): Promise<RecordedSession | null> {
    if (!isBridgeAlive()) return null;
    try {
      const session = await HybridBridge.loadSession(id);
      if (session) {
        // Update in-memory session with full data
        this.#sessions = this.#sessions.map(s => s.id === id ? session : s);
        saveToLocalStorage(this.#sessions);
      }
      return session;
    } catch (err) {
      console.warn('[SessionStore] loadFullSession failed:', err);
      return null;
    }
  }

  clear(): void {
    this.#sessions = [];
    localStorage.removeItem(STORAGE_KEY);
    // Set flag to prevent init() from rehydrating from C# after deliberate clear
    localStorage.setItem(CLEAR_FLAG_KEY, '1');

    if (isBridgeAlive()) {
      HybridBridge.clearAllSessions().catch((err) =>
        console.warn('[SessionStore] C# clearAll failed:', err),
      );
    }
  }

  // ── Export/Import ──────────────────────────────────────────────────────

  async exportToMes(session: RecordedSession): Promise<{ success: boolean; error?: string }> {
    // Ensure session exists on C# disk before exporting (fire-and-forget save may have failed)
    if (isBridgeAlive()) {
      await HybridBridge.saveSessionMetadata(session);
    }
    return HybridBridge.exportSession(session.id);
  }

  async exportAllToMes(): Promise<{ success: boolean; error?: string }> {
    if (this.#sessions.length === 0)
      return { success: false, error: 'No sessions to export' };
    return HybridBridge.exportAllSessions();
  }

  async importFromMes(): Promise<{ success: boolean; error?: string; count?: number }> {
    const result = await HybridBridge.importSession();
    if (!result.picked || !result.success || !result.sessions)
      return { success: false, error: result.error ?? 'Import cancelled' };

    // Add imported sessions to localStorage (dedup by ID)
    const existingIds = new Set(this.#sessions.map(s => s.id));
    const newSessions = result.sessions.filter(s => !existingIds.has(s.id));

    if (newSessions.length > 0) {
      this.#sessions = [...this.#sessions, ...newSessions];
      saveToLocalStorage(this.#sessions);
    }

    return { success: true, count: newSessions.length };
  }
}

export const SessionStore = new SessionStoreClass();

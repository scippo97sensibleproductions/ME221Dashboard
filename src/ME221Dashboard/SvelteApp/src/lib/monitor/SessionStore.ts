const STORAGE_KEY = 'monitor_sessions';
const MAX_SESSIONS = 50;

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
}

function generateId(): string {
  return Date.now().toString(36) + Math.random().toString(36).slice(2, 7);
}

function loadSessions(): RecordedSession[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    return JSON.parse(raw) as RecordedSession[];
  } catch {
    return [];
  }
}

function saveSessions(sessions: RecordedSession[]): void {
  const trimmed = sessions.slice(-MAX_SESSIONS);
  localStorage.setItem(STORAGE_KEY, JSON.stringify(trimmed));
}

class SessionStoreClass {
  #sessions: RecordedSession[] = loadSessions();

  get sessions(): RecordedSession[] {
    return this.#sessions;
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
    this.#sessions = [...this.#sessions, session];
    saveSessions(this.#sessions);
    return session;
  }

  rename(id: string, name: string): void {
    this.#sessions = this.#sessions.map(s => s.id === id ? { ...s, name } : s);
    saveSessions(this.#sessions);
  }

  remove(id: string): void {
    this.#sessions = this.#sessions.filter(s => s.id !== id);
    saveSessions(this.#sessions);
  }

  get(id: string): RecordedSession | undefined {
    return this.#sessions.find(s => s.id === id);
  }

  clear(): void {
    this.#sessions = [];
    localStorage.removeItem(STORAGE_KEY);
  }
}

export const SessionStore = new SessionStoreClass();

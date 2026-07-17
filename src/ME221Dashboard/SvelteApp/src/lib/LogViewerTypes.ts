export interface LogEntry {
  timestamp: string;
  level: string;
  category: string;
  message: string;
  exception?: string;
  source: 'live' | 'import';
  originalIndex?: number;
}

export interface LogMarker {
  id: 'A' | 'B';
  entryIndex: number;
  timestamp: string;
}

export interface LevelStats {
  level: string;
  count: number;
  color: string;
}

export interface ChannelStats {
  min: number;
  max: number;
  average: number;
  delta: number;
  rateOfChange: number;
}

export type SearchMode = 'text' | 'regex';

export const LEVEL_COLORS: Record<string, string> = {
  Trace: 'var(--metro-text-muted)',
  Debug: 'var(--metro-text-secondary)',
  Information: 'var(--metro-green)',
  Warning: 'var(--metro-yellow)',
  Error: 'var(--metro-red)',
  Critical: 'var(--metro-red)',
};

export const LEVEL_BG: Record<string, string> = {
  Trace: '#111111',
  Debug: '#141414',
  Information: '',
  Warning: '#1A1600',
  Error: '#1A0A0A',
  Critical: '#1A0A0A',
};

export const ALL_LEVELS = ['Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical'];

export function computeLevelStats(entries: LogEntry[]): LevelStats[] {
  const counts: Record<string, number> = {};
  for (const entry of entries) {
    counts[entry.level] = (counts[entry.level] || 0) + 1;
  }
  return ALL_LEVELS.map(level => ({
    level,
    count: counts[level] || 0,
    color: LEVEL_COLORS[level] ?? 'var(--metro-text-secondary)',
  }));
}

function tryParseNumber(message: string): number | null {
  const match = message.match(/(-?\d+\.?\d*)\s*$/);
  if (match) {
    const n = parseFloat(match[1]);
    if (Number.isFinite(n)) return n;
  }
  return null;
}

export function computeChannelStats(entries: LogEntry[]): ChannelStats | null {
  const values: number[] = [];
  for (const entry of entries) {
    const v = tryParseNumber(entry.message);
    if (v !== null) values.push(v);
  }
  if (values.length === 0) return null;

  let min = Infinity;
  let max = -Infinity;
  let sum = 0;
  for (const v of values) {
    if (v < min) min = v;
    if (v > max) max = v;
    sum += v;
  }

  const delta = values.length >= 2 ? values[values.length - 1] - values[0] : 0;
  const rateOfChange = entries.length >= 2
    ? delta / ((entries.length - 1) / 1000)
    : 0;

  return {
    min,
    max,
    average: sum / values.length,
    delta,
    rateOfChange,
  };
}

export function truncateCategory(cat: string): string {
  const parts = cat.split('.');
  return parts[parts.length - 1];
}

export function formatTimestamp(ts: string): string {
  if (ts.length <= 23) return ts;
  return ts.substring(11, 23);
}

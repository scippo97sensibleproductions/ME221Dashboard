import type { LogEntry } from './LogViewerTypes';

export type EventType = 'spike' | 'flatline' | 'dropout' | 'abnormal';

export interface DetectedEvent {
  entryIndex: number;
  type: EventType;
  description: string;
}

const FLATLINE_THRESHOLD = 50;
const SPIKE_THRESHOLD = 1000;

function extractNumber(message: string): number | null {
  const match = message.match(/(-?\d+\.?\d*)/);
  if (match) {
    const n = parseFloat(match[1]);
    if (Number.isFinite(n)) return n;
  }
  return null;
}

function isLikelySensorMessage(message: string): boolean {
  return /[:=]\s*-?\d+\.?\d*\s/.test(message) || /-?\d+\.?\d*\s*(°|%|rpm|kpa|deg|volts?|afr|lambda)/i.test(message);
}

export function detectEvents(entries: LogEntry[]): DetectedEvent[] {
  const events: DetectedEvent[] = [];

  const numericValues: (number | null)[] = entries.map(e =>
    isLikelySensorMessage(e.message) ? extractNumber(e.message) : null
  );

  // Spike detection: >1000 units change between consecutive numeric values
  for (let i = 1; i < entries.length; i++) {
    const prev = numericValues[i - 1];
    const curr = numericValues[i];
    if (prev !== null && curr !== null) {
      const diff = Math.abs(curr - prev);
      if (diff > SPIKE_THRESHOLD) {
        events.push({
          entryIndex: i,
          type: 'spike',
          description: `Value changed by ${diff.toFixed(1)} (${prev} → ${curr})`,
        });
      }
    }
  }

  // Flatline detection: 50+ consecutive identical numeric values
  let runStart = 0;
  for (let i = 1; i <= entries.length; i++) {
    const prev = numericValues[i - 1];
    const curr = i < entries.length ? numericValues[i] : null;
    const sameRun = prev !== null && curr !== null && prev === curr;

    if (!sameRun) {
      const runLen = i - runStart;
      if (runLen >= FLATLINE_THRESHOLD && numericValues[runStart] !== null) {
        events.push({
          entryIndex: runStart + Math.floor(runLen / 2),
          type: 'flatline',
          description: `${runLen} identical values (${numericValues[runStart]})`,
        });
      }
      runStart = i;
    }
  }

  // Dropout detection: non-numeric or empty values where numeric was expected
  let lastNumericIndex = -1;
  for (let i = 0; i < entries.length; i++) {
    const msg = entries[i].message.trim();
    if (numericValues[i] !== null) {
      lastNumericIndex = i;
    } else if (lastNumericIndex >= 0 && msg.length > 0 && /\d/.test(msg)) {
      // Message has digits but doesn't parse as a sensor reading
      const hasAlpha = /[a-zA-Z]/.test(msg);
      const hasDigit = /\d/.test(msg);
      if (hasAlpha && hasDigit && !isLikelySensorMessage(msg)) {
        events.push({
          entryIndex: i,
          type: 'dropout',
          description: `Non-numeric value where numeric expected: "${msg.substring(0, 40)}"`,
        });
      }
    }
  }

  // Empty message dropout
  for (let i = 0; i < entries.length; i++) {
    if (entries[i].message.trim() === '' && lastNumericIndex >= 0 && i - lastNumericIndex < 5) {
      events.push({
        entryIndex: i,
        type: 'dropout',
        description: 'Empty message in numeric context',
      });
    }
  }

  // Abnormal value detection: outside typical ranges
  const RANGE_CHECKS: { pattern: RegExp; min: number; max: number; label: string }[] = [
    { pattern: /coolant|water\s*temp/i, min: -40, max: 250, label: 'coolant temp' },
    { pattern: /intake\s*air|iat/i, min: -40, max: 200, label: 'intake air temp' },
    { pattern: /rpm/i, min: 0, max: 12000, label: 'RPM' },
    { pattern: /boost|manifold.*press/i, min: -1, max: 50, label: 'boost pressure' },
    { pattern: /afr|lambda|o2/i, min: 0, max: 3, label: 'AFR/lambda' },
    { pattern: /throttle|tps/i, min: 0, max: 100, label: 'throttle position' },
    { pattern: /battery|voltage|volts/i, min: 8, max: 18, label: 'battery voltage' },
    { pattern: /duty\s*cycle|injector/i, min: 0, max: 100, label: 'duty cycle' },
    { pattern: /ignition|timing|advance/i, min: -60, max: 60, label: 'ignition timing' },
    { pattern: /fuel\s*(press|pres)/i, min: 0, max: 100, label: 'fuel pressure' },
  ];

  for (let i = 0; i < entries.length; i++) {
    const val = numericValues[i];
    if (val === null) continue;
    const msg = entries[i].message;

    for (const check of RANGE_CHECKS) {
      if (check.pattern.test(msg) && (val < check.min || val > check.max)) {
        events.push({
          entryIndex: i,
          type: 'abnormal',
          description: `${check.label} value ${val} outside range [${check.min}–${check.max}]`,
        });
        break;
      }
    }
  }

  // Sort by entry index
  events.sort((a, b) => a.entryIndex - b.entryIndex);
  return events;
}

export const EVENT_ICONS: Record<EventType, string> = {
  spike: '\u26A1',
  flatline: '\u2796',
  dropout: '\u274C',
  abnormal: '\u26A0\uFE0F',
};

export const EVENT_COLORS: Record<EventType, string> = {
  spike: 'var(--metro-red)',
  flatline: 'var(--metro-yellow)',
  dropout: 'var(--metro-orange)',
  abnormal: '#9B59B6',
};

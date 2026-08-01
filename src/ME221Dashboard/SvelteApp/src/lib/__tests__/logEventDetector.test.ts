import { describe, it, expect } from 'vitest';
import { detectEvents, EVENT_COLORS, EVENT_ICONS } from '../logEventDetector';
import type { LogEntry } from '../LogViewerTypes';

function entries(messages: string[]): LogEntry[] {
  return messages.map((message, index) => ({
    timestamp: String(index),
    level: 'info',
    category: 'test',
    message,
    source: 'live',
  } as LogEntry));
}

// Real sensor log lines end with trailing whitespace — required by the
// `[:=]\s*-?\d+\.?\d*\s` sensor-message pattern.
const RPM = (v: number) => `rpm: ${v} `;
const TEMP = (v: number) => `temp: ${v} `;

describe('detectEvents — spikes', () => {
  it('flags a jump greater than 1000 units', () => {
    const events = detectEvents(entries([RPM(1000), RPM(2500)]));
    expect(events).toHaveLength(1);
    expect(events[0].type).toBe('spike');
    expect(events[0].entryIndex).toBe(1);
    expect(events[0].description).toBe('Value changed by 1500.0 (1000 → 2500)');
  });

  it('does not flag a jump of exactly 1000 units', () => {
    const events = detectEvents(entries([RPM(1000), RPM(2000)]));
    expect(events.filter(e => e.type === 'spike')).toEqual([]);
  });

  it('ignores non-numeric messages between numeric ones', () => {
    const events = detectEvents(entries([RPM(1000), 'handshake complete', RPM(2500)]));
    expect(events.filter(e => e.type === 'spike')).toEqual([]);
  });
});

describe('detectEvents — flatlines', () => {
  it('flags 50+ identical numeric values at the run midpoint', () => {
    const messages = Array.from({ length: 50 }, () => TEMP(90));
    const events = detectEvents(entries(messages));
    const flat = events.filter(e => e.type === 'flatline');
    expect(flat).toHaveLength(1);
    expect(flat[0].entryIndex).toBe(25);
    expect(flat[0].description).toBe('50 identical values (90)');
  });

  it('does not flag 49 identical values', () => {
    const messages = Array.from({ length: 49 }, () => TEMP(90));
    const events = detectEvents(entries(messages));
    expect(events.filter(e => e.type === 'flatline')).toEqual([]);
  });

  it('breaks the run when the value changes', () => {
    const messages = [...Array.from({ length: 50 }, () => TEMP(90)), TEMP(91)];
    const events = detectEvents(entries(messages));
    expect(events.filter(e => e.type === 'flatline')).toHaveLength(1);
  });
});

describe('detectEvents — dropouts', () => {
  it('flags a text message with digits after numeric context', () => {
    const events = detectEvents(entries([RPM(1000), 'waiting for response 5']));
    const drop = events.filter(e => e.type === 'dropout');
    expect(drop).toHaveLength(1);
    expect(drop[0].entryIndex).toBe(1);
  });

  it('flags an empty message near numeric context', () => {
    const events = detectEvents(entries([RPM(1000), '']));
    const drop = events.filter(e => e.type === 'dropout');
    expect(drop).toHaveLength(1);
    expect(drop[0].description).toBe('Empty message in numeric context');
  });

  it('does not flag empty messages far from numeric context', () => {
    const events = detectEvents(entries(['', '', '', '', '', '', '']));
    expect(events.filter(e => e.type === 'dropout')).toEqual([]);
  });
});

describe('detectEvents — abnormal values', () => {
  it('flags rpm above 12000', () => {
    const events = detectEvents(entries([RPM(15000)]));
    expect(events[0].type).toBe('abnormal');
    expect(events[0].description).toContain('RPM value 15000');
  });

  it('does not flag rpm at the boundary', () => {
    expect(detectEvents(entries([RPM(12000)]))).toEqual([]);
  });

  it('flags coolant below -40', () => {
    const events = detectEvents(entries(['coolant temp: -60 ']));
    expect(events[0].type).toBe('abnormal');
  });

  it('flags battery voltage below 8', () => {
    expect(detectEvents(entries(['battery voltage: 6 '])).map(e => e.type)).toContain('abnormal');
  });

  it('does not flag in-range values', () => {
    const events = detectEvents(entries(['battery voltage: 12 ', 'throttle: 45 ', 'afr: 1.2 ']));
    expect(events.filter(e => e.type === 'abnormal')).toEqual([]);
  });
});

describe('detectEvents — ordering & metadata', () => {
  it('sorts events by entry index', () => {
    const events = detectEvents(entries([RPM(15000), RPM(1000), RPM(3000)]));
    const idxs = events.map(e => e.entryIndex);
    expect(idxs).toEqual([...idxs].sort((a, b) => a - b));
    expect(events[0].entryIndex).toBe(0);
  });

  it('ignores messages that do not look like sensor readings', () => {
    const events = detectEvents(entries(['Starting ECU handshake', 'Table uploaded']));
    expect(events).toEqual([]);
  });

  it('exposes icons and colors for every event type', () => {
    for (const type of ['spike', 'flatline', 'dropout', 'abnormal'] as const) {
      expect(typeof EVENT_ICONS[type]).toBe('string');
      expect(typeof EVENT_COLORS[type]).toBe('string');
    }
  });
});

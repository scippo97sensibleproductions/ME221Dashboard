import { describe, it, expect } from 'vitest';
import { parseCsvLog } from '../logImporter';

const HEADER = 'timestamp,level,category,message,exception';

describe('parseCsvLog', () => {
  it('returns empty result for empty input', () => {
    expect(parseCsvLog('')).toEqual({ entries: [], skippedCount: 0, totalRows: 0 });
    expect(parseCsvLog('\n\n  \n')).toEqual({ entries: [], skippedCount: 0, totalRows: 0 });
  });

  it('parses a basic row with all columns', () => {
    const result = parseCsvLog(`${HEADER}\n2024-01-01T10:00:00,Info,Comms,connected,`);
    expect(result.totalRows).toBe(1);
    expect(result.skippedCount).toBe(0);
    expect(result.entries).toEqual([
      { timestamp: '2024-01-01T10:00:00', level: 'Info', category: 'Comms', message: 'connected', exception: undefined, source: 'import' },
    ]);
  });

  it('handles CRLF line endings', () => {
    const result = parseCsvLog(`${HEADER}\r\n2024-01-01T10:00:00,Info,Comms,connected,\r\n`);
    expect(result.entries).toHaveLength(1);
    expect(result.entries[0]?.message).toBe('connected');
  });

  it('keeps an empty exception column as undefined', () => {
    const result = parseCsvLog(`${HEADER}\n2024-01-01T10:00:00,Info,Comms,msg,`);
    expect(result.entries[0]?.exception).toBeUndefined();
  });

  it('preserves a non-empty exception column', () => {
    const result = parseCsvLog(`${HEADER}\n2024-01-01T10:00:00,Error,Comms,boom,stack trace`);
    expect(result.entries[0]?.exception).toBe('stack trace');
  });

  it('parses quoted fields containing commas', () => {
    const result = parseCsvLog(`${HEADER}\n"2024-01-01T10:00:00","Info","Comms","hello, world",`);
    expect(result.entries[0]?.message).toBe('hello, world');
  });

  it('unquotes escaped quotes inside quoted fields', () => {
    const result = parseCsvLog(`${HEADER}\n"2024-01-01T10:00:00","Info","Comms","say ""hi""",`);
    expect(result.entries[0]?.message).toBe('say "hi"');
  });

  it('trims surrounding whitespace in fields', () => {
    const result = parseCsvLog(`${HEADER}\n  2024-01-01T10:00:00 , Info , Comms ,  msg  ,`);
    expect(result.entries[0]?.timestamp).toBe('2024-01-01T10:00:00');
    expect(result.entries[0]?.level).toBe('Info');
    expect(result.entries[0]?.message).toBe('msg');
  });

  it('skips the whole file when required headers are missing', () => {
    const result = parseCsvLog('timestamp,level,category\n2024-01-01T10:00:00,Info,Comms');
    expect(result.entries).toHaveLength(0);
    expect(result.skippedCount).toBe(1);
    expect(result.totalRows).toBe(1);
  });

  it('treats headers case-insensitively', () => {
    const result = parseCsvLog('Timestamp,Level,Category,Message,Exception\n2024-01-01T10:00:00,Info,Comms,msg,');
    expect(result.entries).toHaveLength(1);
  });

  it('skips rows with too few fields', () => {
    const result = parseCsvLog(`${HEADER}\n2024-01-01T10:00:00,Info`);
    expect(result.entries).toHaveLength(0);
    expect(result.skippedCount).toBe(1);
  });

  it('skips rows with missing timestamp, level or category', () => {
    const csv = `${HEADER}\n,Info,Comms,msg,\n2024-01-01T10:00:00,,Comms,msg,\n2024-01-01T10:00:00,Info,,msg,`;
    const result = parseCsvLog(csv);
    expect(result.entries).toHaveLength(0);
    expect(result.skippedCount).toBe(3);
  });

  it('supports extra columns beyond the required ones', () => {
    const result = parseCsvLog(`${HEADER},extra\n2024-01-01T10:00:00,Info,Comms,msg,,junk`);
    expect(result.entries).toHaveLength(1);
    expect(result.entries[0]?.message).toBe('msg');
  });

  it('rejects a header without the exception column (required)', () => {
    const result = parseCsvLog('timestamp,level,category,message\n2024-01-01T10:00:00,Info,Comms,msg');
    expect(result.entries).toHaveLength(0);
    expect(result.skippedCount).toBe(1);
  });
});

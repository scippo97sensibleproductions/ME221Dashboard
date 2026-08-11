import { describe, it, expect } from 'vitest';
import { buildSessionCsv, buildSessionVdCsv } from '../sessionCsv';

const session = {
  sensorIds: [1, 2, 3],
  sensorNames: { 1: 'engine speed', 2: 'throttle position', 3: 'Fuel Rail Pressure' },
  data: {
    1: [{ t: 0, v: 800 }, { t: 16.7, v: 1200 }],
    2: [{ t: 0, v: 0 }, { t: 16.7, v: 5 }],
    3: [{ t: 0, v: 42 }, { t: 16.7, v: 43 }],
  },
};

describe('buildSessionCsv', () => {
  it('emits time_ms header plus one value column per sensor', () => {
    const lines = buildSessionCsv(session).split('\n');
    expect(lines[0]).toBe('time_ms,engine speed,throttle position,Fuel Rail Pressure');
    expect(lines[1]).toBe('0.0,800,0,42');
    expect(lines[2]).toBe('16.7,1200,5,43');
  });

  it('returns empty string when nothing recorded', () => {
    expect(buildSessionCsv({ sensorIds: [], sensorNames: {}, data: {} })).toBe('');
  });

  it('CSV-escapes names containing commas or quotes', () => {
    const s = {
      sensorIds: [1],
      sensorNames: { 1: 'RPM, speed' },
      data: { 1: [{ t: 0, v: 1 }] },
    };
    expect(buildSessionCsv(s).split('\n')[0]).toBe('time_ms,"RPM, speed"');
  });
});

describe('buildSessionVdCsv', () => {
  it('emits ME221 marker and Time plus one value per sensor per row', () => {
    const lines = buildSessionVdCsv(session).split('\n');
    expect(lines[0]).toBe('ME221');
    expect(lines[1]).toBe('Time,RPM,Throttle Position,Fuel Pressure');
    expect(lines[2]).toBe('0.000,800,0,42');
    expect(lines[3]).toBe('0.017,1200,5,43');
  });

  it('keeps column alignment when a sensor misses samples', () => {
    const s = {
      sensorIds: [1, 2],
      sensorNames: { 1: 'RPM', 2: 'TPS' },
      data: {
        1: [{ t: 0, v: 100 }, { t: 100, v: 150 }],
        2: [{ t: 0, v: 5 }],
      },
    };
    const lines = buildSessionVdCsv(s).split('\n');
    expect(lines[1]).toBe('Time,RPM,Throttle Position');
    expect(lines[2]).toBe('0.000,100,5');
    expect(lines[3]).toBe('0.100,150,');
  });

  it('every data row has exactly header-width cells (no (t,v) interleaving)', () => {
    const lines = buildSessionVdCsv(session).split('\n');
    const headerWidth = lines[1].split(',').length;
    for (const line of lines.slice(2)) {
      expect(line.split(',').length).toBe(headerWidth);
    }
  });
});

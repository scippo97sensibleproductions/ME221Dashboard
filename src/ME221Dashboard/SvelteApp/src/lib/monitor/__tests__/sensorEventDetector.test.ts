import { describe, it, expect } from 'vitest';
import { detectSensorEvents } from '../SensorEventDetector';

const steady = Array.from({ length: 20 }, (_, i) => ({ t: i * 100, v: 50 }));

describe('detectSensorEvents', () => {
  it('returns no events for steady data', () => {
    expect(detectSensorEvents(steady, 1, 'RPM')).toEqual([]);
  });

  it('returns no events for empty or single-point data', () => {
    expect(detectSensorEvents([], 1, 'RPM')).toEqual([]);
    expect(detectSensorEvents([{ t: 0, v: 50 }], 1, 'RPM')).toEqual([]);
  });

  it('flags a spike well outside the mean', () => {
    const data = [...steady, { t: 2000, v: 500 }];
    const events = detectSensorEvents(data, 1, 'RPM');
    expect(events).toHaveLength(1);
    expect(events[0]?.type).toBe('spike');
    expect(events[0]?.sensorId).toBe(1);
    expect(events[0]?.sensorName).toBe('RPM');
    expect(events[0]?.timeMs).toBe(2000);
    expect(events[0]?.value).toBe(500);
    expect(events[0]?.description).toContain('Spike');
  });

  it('does not flag spikes when stddev is zero', () => {
    const data = [{ t: 0, v: 50 }, { t: 100, v: 50 }, { t: 200, v: 50 }];
    expect(detectSensorEvents(data, 1, 'RPM')).toEqual([]);
  });

  it('respects a custom spike threshold', () => {
    // Alternating 49/51 gives a real stddev, so a 3σ-flagged outlier at 53
    // is not flagged at the default threshold but is flagged with 0.5σ.
    const base = Array.from({ length: 20 }, (_, i) => ({ t: i * 100, v: i % 2 === 0 ? 49 : 51 }));
    const data = [...base, { t: 2000, v: 53 }];
    expect(detectSensorEvents(data, 1, 'RPM')).toEqual([]);
    const events = detectSensorEvents(data, 1, 'RPM', { spikeThreshold: 0.5 });
    expect(events.some(e => e.type === 'spike')).toBe(true);
  });

  it('flags a flatline window at the default 2s window', () => {
    const data = [
      { t: 0, v: 50 }, { t: 500, v: 50 }, { t: 1000, v: 50 }, { t: 1500, v: 50 },
      { t: 2000, v: 50 }, { t: 2500, v: 60 },
    ];
    const events = detectSensorEvents(data, 1, 'RPM');
    expect(events.some(e => e.type === 'flatline')).toBe(true);
  });

  it('does not flag a flatline when values drift beyond maxDelta', () => {
    const data = [
      { t: 0, v: 50 }, { t: 500, v: 51 }, { t: 1000, v: 52 }, { t: 1500, v: 53 },
      { t: 2000, v: 54 },
    ];
    const events = detectSensorEvents(data, 1, 'RPM');
    expect(events.some(e => e.type === 'flatline')).toBe(false);
  });

  it('flags dropouts when the gap exceeds dropoutGapMs', () => {
    const data = [
      { t: 0, v: 50 }, { t: 100, v: 50 }, { t: 2000, v: 50 },
    ];
    const events = detectSensorEvents(data, 1, 'RPM');
    expect(events.some(e => e.type === 'dropout')).toBe(true);
    const gap = events.find(e => e.type === 'dropout');
    expect(gap?.timeMs).toBe(100);
    expect(gap?.description).toContain('gap');
  });

  it('does not flag dropouts for normal sampling intervals', () => {
    const data = [
      { t: 0, v: 50 }, { t: 100, v: 50 }, { t: 200, v: 50 },
    ];
    expect(detectSensorEvents(data, 1, 'RPM').some(e => e.type === 'dropout')).toBe(false);
  });

  it('respects custom dropout gap', () => {
    const data = [{ t: 0, v: 50 }, { t: 550, v: 50 }];
    expect(detectSensorEvents(data, 1, 'RPM').some(e => e.type === 'dropout')).toBe(true);
    expect(detectSensorEvents(data, 1, 'RPM', { dropoutGapMs: 600 })).toEqual([]);
  });

  it('combines spike, flatline and dropout in one dataset', () => {
    const data = [
      ...steady, // t=0..1900, all 50 → flatline window [0..2000]
      { t: 2000, v: 50 },
      { t: 2100, v: 500 }, // spike
      ...Array.from({ length: 19 }, (_, i) => ({ t: 2200 + i * 100, v: 50 })),
      { t: 4900, v: 50 }, // 900ms dropout gap
    ];
    const types = new Set(detectSensorEvents(data, 1, 'RPM').map(e => e.type));
    expect(types.has('spike')).toBe(true);
    expect(types.has('flatline')).toBe(true);
    expect(types.has('dropout')).toBe(true);
  });
});

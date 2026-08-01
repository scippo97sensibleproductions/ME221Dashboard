import { describe, it, expect } from 'vitest';
import { computeRangeStats, computeRangeStatsBetween } from '../StatsComputer';
import { detectSensorEvents, SENSOR_EVENT_COLORS, SENSOR_EVENT_ICONS } from '../SensorEventDetector';

describe('computeRangeStats', () => {
  it('returns null for empty data', () => {
    expect(computeRangeStats([])).toBeNull();
  });

  it('single sample: min = max = avg, zero rate of change', () => {
    const s = computeRangeStats([{ t: 100, v: 42 }])!;
    expect(s.min).toBe(42);
    expect(s.max).toBe(42);
    expect(s.avg).toBe(42);
    expect(s.delta).toBe(0);
    expect(s.rateOfChange).toBe(0);
    expect(s.durationMs).toBe(0);
    expect(s.count).toBe(1);
  });

  it('computes min/max/avg/delta/duration', () => {
    const s = computeRangeStats([
      { t: 0, v: 10 },
      { t: 1000, v: 30 },
      { t: 2000, v: 20 },
    ])!;
    expect(s.min).toBe(10);
    expect(s.max).toBe(30);
    expect(s.avg).toBe(20);
    expect(s.delta).toBe(10);
    expect(s.durationMs).toBe(2000);
    expect(s.count).toBe(3);
  });

  it('rate of change is per second (×1000)', () => {
    const s = computeRangeStats([
      { t: 0, v: 0 },
      { t: 500, v: 50 },
    ])!;
    expect(s.rateOfChange).toBe(100);
  });

  it('zero duration gives zero rate of change', () => {
    const s = computeRangeStats([
      { t: 1000, v: 5 },
      { t: 1000, v: 9 },
    ])!;
    expect(s.durationMs).toBe(0);
    expect(s.rateOfChange).toBe(0);
  });

  it('handles negative and identical values', () => {
    const s = computeRangeStats([
      { t: 0, v: -5 },
      { t: 10, v: -5 },
    ])!;
    expect(s.min).toBe(-5);
    expect(s.max).toBe(-5);
    expect(s.avg).toBe(-5);
    expect(s.delta).toBe(0);
  });
});

describe('computeRangeStatsBetween', () => {
  const data = [
    { t: 0, v: 1 },
    { t: 100, v: 2 },
    { t: 200, v: 3 },
    { t: 300, v: 4 },
  ];

  it('filters to the inclusive window', () => {
    const s = computeRangeStatsBetween(data, 100, 300)!;
    expect(s.count).toBe(3);
    expect(s.min).toBe(2);
    expect(s.max).toBe(4);
  });

  it('returns null when the window contains nothing', () => {
    expect(computeRangeStatsBetween(data, 1000, 2000)).toBeNull();
  });

  it('matches samples exactly on the boundary', () => {
    expect(computeRangeStatsBetween(data, 0, 0)!.count).toBe(1);
    expect(computeRangeStatsBetween(data, 300, 300)!.count).toBe(1);
  });
});

describe('detectSensorEvents', () => {
  const sensor = { id: 7, name: 'RPM' };

  it('returns no events for fewer than 2 samples', () => {
    expect(detectSensorEvents([{ t: 0, v: 1 }], 7, 'RPM')).toEqual([]);
    expect(detectSensorEvents([], 7, 'RPM')).toEqual([]);
  });

  it('constant data produces no spikes (stddev 0)', () => {
    const data = Array.from({ length: 50 }, (_, i) => ({ t: i * 100, v: 100 }));
    const events = detectSensorEvents(data, sensor.id, sensor.name);
    expect(events.filter(e => e.type === 'spike')).toEqual([]);
  });

  it('detects a spike beyond threshold × stddev', () => {
    const data = [
      ...Array.from({ length: 20 }, (_, i) => ({ t: i * 100, v: 100 })),
      { t: 2000, v: 1000 },
    ];
    const events = detectSensorEvents(data, sensor.id, sensor.name);
    const spikes = events.filter(e => e.type === 'spike');
    expect(spikes).toHaveLength(1);
    expect(spikes[0].timeMs).toBe(2000);
    expect(spikes[0].value).toBe(1000);
    expect(spikes[0].sensorId).toBe(7);
    expect(spikes[0].sensorName).toBe('RPM');
  });

  it('flags the flatline only when the window duration is reached', () => {
    const data = Array.from({ length: 20 }, (_, i) => ({ t: i * 100, v: 50 }));
    const within = detectSensorEvents(data, 7, 'RPM', { flatlineWindowMs: 1900 });
    expect(within.filter(e => e.type === 'flatline')).toHaveLength(1); // exactly 1900ms
    const tight = detectSensorEvents(data, 7, 'RPM', { flatlineWindowMs: 1901 });
    expect(tight.filter(e => e.type === 'flatline')).toEqual([]);
  });

  it('flatline requires the max delta bound', () => {
    const data = Array.from({ length: 30 }, (_, i) => ({ t: i * 100, v: 50 + (i % 2) }));
    const events = detectSensorEvents(data, 7, 'RPM', { flatlineWindowMs: 1000, flatlineMaxDelta: 0.01 });
    expect(events.filter(e => e.type === 'flatline')).toEqual([]);
  });

  it('detects dropouts only beyond the gap threshold', () => {
    const data = [
      { t: 0, v: 1 }, { t: 100, v: 2 }, { t: 500, v: 3 }, { t: 1001, v: 4 },
    ];
    const events = detectSensorEvents(data, 7, 'RPM', { dropoutGapMs: 500 });
    const dropouts = events.filter(e => e.type === 'dropout');
    // gap 100→500 is exactly 400 ≤ 500 → no; 500→1001 is 501 > 500 → yes
    expect(dropouts).toHaveLength(1);
    expect(dropouts[0].timeMs).toBe(500);
  });

  it('spike descriptions embed value and mean', () => {
    const data = [
      ...Array.from({ length: 20 }, (_, i) => ({ t: i * 100, v: 100 })),
      { t: 2000, v: 900 },
    ];
    const [spike] = detectSensorEvents(data, 7, 'RPM', { spikeThreshold: 2 });
    expect(spike.description).toContain('Spike: 900.00');
    expect(spike.description).toContain('mean:');
  });

  it('event colors and icons are complete per type', () => {
    for (const type of ['spike', 'flatline', 'dropout'] as const) {
      expect(typeof SENSOR_EVENT_COLORS[type]).toBe('string');
      expect(typeof SENSOR_EVENT_ICONS[type]).toBe('string');
    }
  });
});

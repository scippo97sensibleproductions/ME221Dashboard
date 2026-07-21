export type SensorEventType = 'spike' | 'flatline' | 'dropout';

export interface SensorEvent {
  sensorId: number;
  sensorName: string;
  type: SensorEventType;
  timeMs: number;
  value: number;
  description: string;
}

export const SENSOR_EVENT_COLORS: Record<SensorEventType, string> = {
  spike: '#ef4444',
  flatline: '#eab308',
  dropout: '#a855f7',
};

export const SENSOR_EVENT_ICONS: Record<SensorEventType, string> = {
  spike: '↑',
  flatline: '—',
  dropout: '⊘',
};

interface DetectOptions {
  spikeThreshold?: number;
  flatlineWindowMs?: number;
  flatlineMaxDelta?: number;
  dropoutGapMs?: number;
}

const DEFAULTS: Required<DetectOptions> = {
  spikeThreshold: 3,
  flatlineWindowMs: 2000,
  flatlineMaxDelta: 0.01,
  dropoutGapMs: 500,
};

export function detectSensorEvents(
  data: Array<{ t: number; v: number }>,
  sensorId: number,
  sensorName: string,
  opts?: DetectOptions,
): SensorEvent[] {
  const o = { ...DEFAULTS, ...opts };
  const events: SensorEvent[] = [];

  if (data.length < 2) return events;

  // Compute mean and stddev
  let sum = 0;
  let sumSq = 0;
  for (const s of data) {
    sum += s.v;
    sumSq += s.v * s.v;
  }
  const mean = sum / data.length;
  const variance = data.length > 1 ? sumSq / data.length - mean * mean : 0;
  const stddev = Math.sqrt(Math.max(0, variance));

  // Detect spikes (value deviates more than spikeThreshold * stddev from mean)
  for (const s of data) {
    if (stddev > 0 && Math.abs(s.v - mean) > o.spikeThreshold * stddev) {
      events.push({
        sensorId,
        sensorName,
        type: 'spike',
        timeMs: s.t,
        value: s.v,
        description: `Spike: ${s.v.toFixed(2)} (mean: ${mean.toFixed(2)}, σ: ${stddev.toFixed(2)})`,
      });
    }
  }

  // Detect flatlines (value stays nearly constant for flatlineWindowMs)
  let windowStart = 0;
  for (let i = 1; i < data.length; i++) {
    const windowDuration = data[i].t - data[windowStart].t;
    if (windowDuration >= o.flatlineWindowMs) {
      const windowData = data.slice(windowStart, i + 1);
      const windowMin = Math.min(...windowData.map(s => s.v));
      const windowMax = Math.max(...windowData.map(s => s.v));
      if (windowMax - windowMin <= o.flatlineMaxDelta) {
        events.push({
          sensorId,
          sensorName,
          type: 'flatline',
          timeMs: data[windowStart].t,
          value: data[windowStart].v,
          description: `Flatline: ${(windowDuration / 1000).toFixed(1)}s at ${data[windowStart].v.toFixed(2)}`,
        });
      }
      windowStart = i;
    }
  }

  // Detect dropouts (gaps in data)
  for (let i = 1; i < data.length; i++) {
    const gap = data[i].t - data[i - 1].t;
    if (gap > o.dropoutGapMs) {
      events.push({
        sensorId,
        sensorName,
        type: 'dropout',
        timeMs: data[i - 1].t,
        value: data[i - 1].v,
        description: `Dropout: ${(gap / 1000).toFixed(1)}s gap`,
      });
    }
  }

  return events;
}

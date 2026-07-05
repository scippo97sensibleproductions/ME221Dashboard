import type { Pt } from './TimeSeriesBuffer';

export interface SeriesConfig {
  id: string;
  name: string;
  color: string;
}

export interface ChartOptions {
  timeWindowSec: number;
  yMin?: number;
  yMax?: number;
  showDataZoom?: boolean;
}

export function buildMultiSeriesOption(
  series: SeriesConfig[],
  data: Map<string, Pt[]>,
  opts: ChartOptions,
  now: number,
) {
  const windowMs = opts.timeWindowSec * 1000;
  const cutoff = now - windowMs;

  const echartsSeries = series.map((s) => {
    const pts = data.get(s.id) ?? [];
    const visible = pts.filter((p) => p.t >= cutoff);
    return {
      name: s.name,
      type: 'line' as const,
      data: visible.map((p) => [p.t, p.v]),
      symbol: 'none' as const,
      lineStyle: { width: 1.5, color: s.color },
      itemStyle: { color: s.color },
      sampling: 'lttb' as const,
      large: true,
      largeThreshold: 500,
    };
  });

  const grid = {
    left: 50,
    right: opts.showDataZoom ? 20 : 16,
    top: 10,
    bottom: opts.showDataZoom ? 60 : 30,
  };

  const xAxis = {
    type: 'time' as const,
    axisLabel: {
      color: '#999',
      fontSize: 10,
      formatter: (val: number) => {
        const diff = (now - val) / 1000;
        if (diff < 1) return 'now';
        if (diff < 60) return `-${Math.round(diff)}s`;
        if (diff < 3600) return `-${Math.round(diff / 60)}m`;
        return `-${Math.round(diff / 3600)}h`;
      },
    },
    axisLine: { lineStyle: { color: '#333' } },
    splitLine: { show: false },
  };

  const yAxis = {
    type: 'value' as const,
    min: opts.yMin,
    max: opts.yMax,
    axisLabel: { color: '#999', fontSize: 10 },
    axisLine: { lineStyle: { color: '#333' } },
    splitLine: { lineStyle: { color: 'rgba(255,255,255,0.06)' } },
  };

  const tooltip = {
    trigger: 'axis' as const,
    backgroundColor: '#1a1a1a',
    borderColor: '#333',
    textStyle: { color: '#fff', fontSize: 11 },
    axisPointer: { type: 'cross' as const, crossStyle: { color: '#666' } },
  };

  const dataZoom = opts.showDataZoom
    ? [
        { type: 'inside' as const, xAxisIndex: 0, filterMode: 'none' as const },
        {
          type: 'slider' as const,
          xAxisIndex: 0,
          bottom: 4,
          height: 18,
          borderColor: '#333',
          backgroundColor: '#1a1a1a',
          fillerColor: 'rgba(14,165,233,0.15)',
          handleStyle: { color: '#0ea5e9', borderColor: '#0ea5e9' },
          textStyle: { color: '#999', fontSize: 10 },
          filterMode: 'none' as const,
        },
      ]
    : [];

  return {
    animation: false as const,
    animationDurationUpdate: 0,
    grid,
    xAxis,
    yAxis,
    tooltip,
    dataZoom,
    series: echartsSeries,
  };
}

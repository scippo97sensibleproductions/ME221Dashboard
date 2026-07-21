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

interface OverlaySession {
  name: string;
  color: string;
  data: Map<string, Array<{ t: number; v: number }>>;
}

export function buildMultiSeriesOption(
  series: SeriesConfig[],
  data: Map<string, Pt[]>,
  opts: ChartOptions,
  now: number,
  overlaySessions?: OverlaySession[],
  markerATimeMs?: number | null,
  markerBTimeMs?: number | null,
) {
  const windowMs = opts.timeWindowSec * 1000;
  const cutoff = now - windowMs;

  const echartsSeries: any[] = [];

  // Main series (live or playback)
  for (const s of series) {
    const pts = data.get(s.id) ?? [];
    const visible = pts.filter((p) => p.t >= cutoff);
    echartsSeries.push({
      name: s.name,
      type: 'line',
      data: visible.map((p) => [p.t, p.v]),
      symbol: 'none',
      lineStyle: { width: 1.5, color: s.color },
      itemStyle: { color: s.color },
      sampling: 'lttb',
      large: true,
      largeThreshold: 500,
    });
  }

  // Overlay sessions (dashed lines, dimmer)
  if (overlaySessions) {
    for (const overlay of overlaySessions) {
      for (const s of series) {
        const pts = overlay.data.get(s.id) ?? [];
        if (pts.length === 0) continue;
        const visible = pts.filter((p) => p.t >= cutoff);
        echartsSeries.push({
          name: `${overlay.name} - ${s.name}`,
          type: 'line',
          data: visible.map((p) => [p.t, p.v]),
          symbol: 'none',
          lineStyle: { width: 1, color: overlay.color, type: 'dashed', opacity: 0.6 },
          itemStyle: { color: overlay.color, opacity: 0.6 },
          sampling: 'lttb',
          large: true,
          largeThreshold: 500,
        });
      }
    }
  }

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
    splitLine: { lineStyle: { color: 'rgba(255,255,255,0.6)' } },
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

  // A/B marker markLines
  const markLine: any = {};
  if (markerATimeMs != null || markerBTimeMs != null) {
    const markData: any[] = [];
    if (markerATimeMs != null) {
      markData.push({
        xAxis: markerATimeMs,
        lineStyle: { color: '#3b82f6', type: 'solid', width: 2 },
        label: { formatter: 'A', color: '#3b82f6', fontSize: 10, fontWeight: 'bold' },
      });
    }
    if (markerBTimeMs != null) {
      markData.push({
        xAxis: markerBTimeMs,
        lineStyle: { color: '#f97316', type: 'solid', width: 2 },
        label: { formatter: 'B', color: '#f97316', fontSize: 10, fontWeight: 'bold' },
      });
    }
    markLine.data = markData;
  }

  // Apply markLine to first series if markers exist
  if (echartsSeries.length > 0 && markLine.data) {
    echartsSeries[0].markLine = markLine;
  }

  return {
    animation: false,
    animationDurationUpdate: 0,
    grid,
    xAxis,
    yAxis,
    tooltip,
    dataZoom,
    series: echartsSeries,
  };
}

<script lang="ts">
  import { liveDataStore } from '../stores/LiveDataStore.svelte';
  import { TimeSeriesBuffer } from './TimeSeriesBuffer';
  import { buildMultiSeriesOption, type SeriesConfig } from './timeSeriesConfig';

  let {
    series = [],
    timeWindowSec = 10,
    yMin,
    yMax,
    showDataZoom = true,
    mode = 'live',
    playbackData,
    playbackTimeMs,
    overlaySessions,
    markerA,
    markerB,
  }: {
    series: SeriesConfig[];
    timeWindowSec?: number;
    yMin?: number;
    yMax?: number;
    showDataZoom?: boolean;
    mode?: 'live' | 'playback';
    playbackData?: Map<string, Array<{ t: number; v: number }>>;
    playbackTimeMs?: number;
    overlaySessions?: Array<{ name: string; color: string; data: Map<string, Array<{ t: number; v: number }>> }>;
    markerA?: number | null;
    markerB?: number | null;
  } = $props();

  let container: HTMLDivElement;
  let chart: import('echarts').ECharts | null = null;
  let destroyed = false;
  const buffer = new TimeSeriesBuffer();
  // Full setOption(replaceMerge) is expensive (~full data-store rebuild).
  // Live frames use cheap incremental appends instead; a full rebuild runs
  // only every FULL_REBUILD_INTERVAL_MS to trim out-of-window points and
  // on config changes (series set, window, markers) via the props effect.
  const FULL_REBUILD_INTERVAL_MS = 2000;
  let pushedCounts = new Map<string, number>();
  let lastFullRebuildAt = 0;

  function initChart() {
    if (!container || chart) return;
    import('./lazy').then(({ getEcharts }) => {
      getEcharts().then((echartsCore) => {
        if (!container || chart || destroyed) return;
        chart = echartsCore.init(container, null, { useDirtyRect: true });
        renderChart();
      });
    });
  }

  function baseCounts() {
    pushedCounts = new Map();
    for (const s of series) {
      const pts = buffer.get(s.id);
      if (pts) pushedCounts.set(s.id, pts.length);
    }
  }

  function renderChart() {
    if (!chart) return;
    const now = Date.now();

    let data: Map<string, Pt[]>;
    let renderNow: number;

    if (mode === 'playback' && playbackData) {
      data = new Map();
      for (const [id, pts] of playbackData) {
        data.set(id, pts);
      }
      renderNow = playbackTimeMs ?? now;
    } else {
      data = buffer.getAllSeries();
      renderNow = now;
    }

    const option = buildMultiSeriesOption(
      series,
      data,
      { timeWindowSec, yMin, yMax, showDataZoom },
      renderNow,
      overlaySessions,
      markerA,
      markerB,
    );
    chart.setOption(option, { replaceMerge: ['series'] });
    if (mode === 'live') baseCounts();
  }

  function appendTick() {
    if (!chart || mode !== 'live') return;
    const now = Date.now();
    const seriesData: any[] = [];
    for (const s of series) {
      const pts = buffer.get(s.id);
      if (!pts) continue;
      const start = pushedCounts.get(s.id);
      if (start == null || start >= pts.length) continue;
      seriesData.push({ id: s.id, data: pts.slice(start).map((p) => [p.t, p.v]) });
      pushedCounts.set(s.id, pts.length);
    }
    if (seriesData.length > 0) {
      chart.setOption({ series: seriesData });
    }
    if (now - lastFullRebuildAt >= FULL_REBUILD_INTERVAL_MS) {
      lastFullRebuildAt = now;
      renderChart();
    }
  }

  export function pushData(seriesId: string, timeMs: number, value: number) {
    buffer.push(seriesId, timeMs, value);
  }

  export function getBuffer(): TimeSeriesBuffer {
    return buffer;
  }

  $effect(() => {
    initChart();
    return () => {
      destroyed = true;
      chart?.dispose();
      chart = null;
    };
  });

  $effect(() => {
    if (mode !== 'live') return;
    const frameCount = liveDataStore.frameCount;
    const now = Date.now();
    for (const s of series) {
      const val = liveDataStore.values[s.id];
      if (val != null) {
        buffer.push(s.id, now, val);
      }
    }
    appendTick();
  });

  $effect(() => {
    if (!container) return;
    const ro = new ResizeObserver(() => chart?.resize());
    ro.observe(container);
    return () => ro.disconnect();
  });

  $effect(() => {
    void timeWindowSec;
    void mode;
    void playbackTimeMs;
    renderChart();
  });
</script>

<div bind:this={container} class="h-full w-full bg-[#111] rounded border border-[#333]"></div>

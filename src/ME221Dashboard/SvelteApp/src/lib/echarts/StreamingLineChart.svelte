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
  }: {
    series: SeriesConfig[];
    timeWindowSec?: number;
    yMin?: number;
    yMax?: number;
    showDataZoom?: boolean;
  } = $props();

  let container: HTMLDivElement;
  let chart: import('echarts').ECharts | null = null;
  let destroyed = false;
  const buffer = new TimeSeriesBuffer();
  let lastRenderTime = 0;
  const RENDER_THROTTLE_MS = 50;

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

  function renderChart() {
    if (!chart) return;
    const now = performance.now();
    const option = buildMultiSeriesOption(
      series,
      buffer.getAllSeries(),
      { timeWindowSec, yMin, yMax, showDataZoom },
      now,
    );
    chart.setOption(option, { replaceMerge: ['series'] });
  }

  function throttledRender() {
    const now = performance.now();
    if (now - lastRenderTime >= RENDER_THROTTLE_MS) {
      lastRenderTime = now;
      renderChart();
    }
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
    const frameCount = liveDataStore.frameCount;
    const now = performance.now();
    for (const s of series) {
      const val = liveDataStore.values[s.id];
      if (val != null) {
        buffer.push(s.id, now, val);
      }
    }
    throttledRender();
  });

  $effect(() => {
    if (!container) return;
    const ro = new ResizeObserver(() => chart?.resize());
    ro.observe(container);
    return () => ro.disconnect();
  });

  $effect(() => {
    void timeWindowSec;
    renderChart();
  });
</script>

<div bind:this={container} class="h-full w-full bg-[#111] rounded border border-[#333]"></div>

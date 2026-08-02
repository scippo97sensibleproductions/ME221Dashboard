<script lang="ts">
  import 'uplot/dist/uPlot.min.css';
  import { liveDataStore } from '../stores/LiveDataStore.svelte';
  import {
    buildPlaybackColumns,
    buildTooltipRows,
    LiveColumns,
    relativeTimeLabels,
    type ChartSeries,
    type OverlaySession,
  } from './chartUtils';

  let {
    series = [],
    timeWindowSec = 10,
    yMin,
    yMax,
    mode = 'live',
    playbackData,
    playbackTimeMs,
    overlaySessions,
    markerA,
    markerB,
  }: {
    series: ChartSeries[];
    timeWindowSec?: number;
    yMin?: number;
    yMax?: number;
    mode?: 'live' | 'playback';
    playbackData?: Map<string, Array<{ t: number; v: number }>>;
    playbackTimeMs?: number;
    overlaySessions?: OverlaySession[];
    markerA?: number | null;
    markerB?: number | null;
  } = $props();

  let container: HTMLDivElement;
  let tooltipEl: HTMLDivElement;
  let u: import('uplot').default | null = null;
  let destroyed = false;
  // placeholder; the series-key effect below recreates columns whenever
  // `series` changes (and always on mount), so this initial count is never read
  let columns = new LiveColumns(0);

  function xRange(_u: unknown, _min: number, _max: number): [number, number] {
    const now = mode === 'live' ? Date.now() : (playbackTimeMs ?? Date.now());
    const w = timeWindowSec * 1000;
    return [now - w, now];
  }

  function yRange(_u: unknown, min: number, max: number): [number, number] {
    return [yMin ?? min, yMax ?? max];
  }

  function seriesOpts(): Array<Record<string, unknown>> {
    const opts: Array<Record<string, unknown>> = [{}];
    for (const s of series) {
      opts.push({
        label: s.name,
        stroke: s.color,
        width: 1.5,
        points: { show: false },
        spanGaps: false,
      });
    }
    if (overlaySessions) {
      for (const ov of overlaySessions) {
        for (const s of series) {
          opts.push({
            label: `${ov.name} - ${s.name}`,
            stroke: ov.color,
            width: 1,
            dash: [4, 3],
            points: { show: false },
            spanGaps: false,
          });
        }
      }
    }
    return opts;
  }

  function playbackColumns(): (number | null)[][] {
    return buildPlaybackColumns(series, playbackData ?? new Map(), overlaySessions ?? []);
  }

  async function initChart() {
    if (!container || u) return;
    const mod = await import('uplot');
    const uPlot = (mod.default ?? mod) as typeof import('uplot').default;
    if (destroyed || u || !container) return;
    u = new uPlot(
      {
        width: container.clientWidth || 600,
        height: container.clientHeight || 300,
        pxRatio: Math.round((globalThis.devicePixelRatio ?? 1) * 10) / 10,
        legend: { show: false },
        select: { show: false },
        focus: { show: false },
        scales: {
          x: { time: true, range: xRange },
          y: { range: yRange },
        },
        series: seriesOpts(),
        axes: [
          {
            stroke: '#666',
            size: 22,
            values: relativeTimeLabels(() => Date.now()),
            grid: { stroke: 'rgba(255,255,255,0.08)' },
            ticks: { stroke: '#444' },
            font: '10px ui-sans-serif, system-ui',
          },
          {
            stroke: '#666',
            size: 52,
            grid: { stroke: 'rgba(255,255,255,0.15)' },
            ticks: { stroke: '#444' },
            font: '10px ui-sans-serif, system-ui',
          },
        ],
        cursor: {
          show: true,
          x: true,
          y: true,
          stroke: '#666',
          width: 1,
          points: { show: false },
        },
        hooks: {
          setCursor: [onSetCursor],
          draw: [drawMarks],
        },
      },
      mode === 'live' ? columns.data() : playbackColumns(),
      container,
    );
  }

  // The uPlot tooltip is imperative DOM — render its content through a helper
  // that owns the innerHTML write.
  function setTooltipContent(node: HTMLElement, html: string): void {
    node.innerHTML = html;
  }

  function onSetCursor(c: import('uplot').default) {
    if (!tooltipEl) return;
    const dataIdx = c.cursor.idx ?? null;
    if (dataIdx == null) {
      tooltipEl.style.display = 'none';
      return;
    }
    const rows = buildTooltipRows(series, c.data, dataIdx, overlaySessions);
    setTooltipContent(
      tooltipEl,
      rows
        .map(
          (r) =>
            `<div class="flex items-center gap-1.5 whitespace-nowrap">
              <span class="w-2 h-2 rounded-full shrink-0" style="background:${r.color}"></span>
              <span class="text-gray-400">${r.name}</span>
              <span class="text-white font-mono font-bold">${r.value}</span>
            </div>`,
        )
        .join(''),
    );
    const pad = 12;
    let left = c.cursor.left + pad;
    let top = c.cursor.top + pad;
    if (left + tooltipEl.offsetWidth > c.width - 4) left = c.cursor.left - tooltipEl.offsetWidth - pad;
    if (top + tooltipEl.offsetHeight > c.height - 4) top = c.cursor.top - tooltipEl.offsetHeight - pad;
    tooltipEl.style.left = `${Math.max(0, left)}px`;
    tooltipEl.style.top = `${Math.max(0, top)}px`;
    tooltipEl.style.display = 'block';
  }

  function drawMarks(c: import('uplot').default) {
    const marks: Array<[number | null | undefined, string, string]> = [
      [markerA, '#3b82f6', 'A'],
      [markerB, '#f97316', 'B'],
    ];
    const active = marks.filter(([t]) => t != null);
    if (active.length === 0) return;
    const ctx = c.ctx;
    const { bbox } = c;
    for (const [t, color, label] of active) {
      const x = c.valToPos(t as number, 'x');
      ctx.save();
      ctx.strokeStyle = color;
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(x, bbox.top);
      ctx.lineTo(x, bbox.top + bbox.height);
      ctx.stroke();
      ctx.fillStyle = color;
      ctx.font = 'bold 10px sans-serif';
      ctx.textAlign = 'center';
      ctx.fillText(label, x, bbox.top + 10);
      ctx.restore();
    }
  }

  let lastSeriesKey = '';

  $effect(() => {
    const key = series.map((s) => s.id).join(',');
    if (key === lastSeriesKey) return;
    lastSeriesKey = key;
    // uPlot series options are fixed at construction, so a series-set
    // change (sensor selection) recreates the chart.
    u?.destroy();
    u = null;
    columns = new LiveColumns(series.length);
    initChart();
  });

  $effect(() => {
    return () => {
      destroyed = true;
      u?.destroy();
      u = null;
    };
  });

  $effect(() => {
    if (mode !== 'live') return;
    void liveDataStore.frameCount;
    columns.push(
      Date.now(),
      series.map((s) => liveDataStore.values[s.id] ?? null),
    );
    if (u) {
      u.setData(columns.data(), false);
      u.redraw();
    }
  });

  $effect(() => {
    void timeWindowSec;
    void yMin;
    void yMax;
    void overlaySessions;
    void mode;
    void playbackData;
    if (mode === 'playback' && u) {
      u.setData(playbackColumns(), false);
      u.redraw();
    }
  });

  $effect(() => {
    if (mode !== 'playback') return;
    void playbackTimeMs;
    u?.redraw();
  });

  $effect(() => {
    if (!container) return;
    const ro = new ResizeObserver(() => {
      if (!u) return;
      u.setSize({
        width: container.clientWidth || u.width,
        height: container.clientHeight || u.height,
      });
    });
    ro.observe(container);
    return () => ro.disconnect();
  });
</script>

<div class="relative h-full w-full">
  <div bind:this={container} class="h-full w-full bg-[#111] rounded border border-[#333]"></div>
  <div
    bind:this={tooltipEl}
    class="absolute z-10 pointer-events-none bg-[#1a1a1a]/95 border border-[#333] rounded px-2 py-1 text-[11px] shadow-lg"
    style="display:none"
  ></div>
</div>

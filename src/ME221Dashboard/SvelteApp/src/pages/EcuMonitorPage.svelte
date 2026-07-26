<script lang="ts">
  import StreamingLineChart from '../lib/echarts/StreamingLineChart.svelte';
  import SensorPicker from '../lib/monitor/PresetSensorPicker.svelte';
  import EcuInfoPanel from '../lib/monitor/EcuInfoPanel.svelte';
  import { SessionRecorder, type RecordingState } from '../lib/monitor/SessionRecorder';
  import { SessionStore } from '../lib/monitor/SessionStore';
  import { HybridBridge } from '../lib/HybridBridge';
  import { liveDataStore } from '../lib/stores/LiveDataStore.svelte';
  import type { DataLinkDefinition } from '../lib/HybridBridgeTypes';
  import { getSensorColor } from '../lib/monitor/sensorColors';
  import {
    IconChartLine, IconAdjustments, IconCircleDotted,
    IconBookmark, IconPlayerPlay, IconPlayerStop, IconStack2,
  } from '@tabler/icons-svelte';

  let {
    onNavigate,
    connectionState,
  }: {
    onNavigate: (page: string) => void;
    connectionState: { state: string };
  } = $props();

  // ── State ──────────────────────────────────────────────────────────────
  let allDataLinks = $state<DataLinkDefinition[]>([]);
  let selectedIds = $state<Set<number>>(new Set());
  let timeWindowSec = $state(10);
  let mobileTab: 'chart' | 'sensors' | 'info' = $state('chart');
  let isMobile = $state(false);

  // Recording state
  let recorderState = $state<RecordingState>(SessionRecorder.state);
  let timerTick = $state(0);
  let timerInterval: ReturnType<typeof setInterval> | null = null;

  // Stats overlay
  let showStats = $state(true);
  let liveStats = $state<Map<number, RangeStats>>(new Map());
  const _statsAccum = new Map<number, RangeStats>();

  // ── Derived ────────────────────────────────────────────────────────────
  const chartSeries = $derived(
    Array.from(selectedIds).map((id) => {
      const dl = allDataLinks.find((d) => d.id === id);
      return {
        id: String(id),
        name: dl?.name ?? `Sensor ${id}`,
        color: getSensorColor(id),
      };
    }),
  );

  // ── Effects ────────────────────────────────────────────────────────────
  $effect(() => {
    const mq = window.matchMedia('(max-width: 767px)');
    isMobile = mq.matches;
    const handler = (e: MediaQueryListEvent) => { isMobile = e.matches; };
    mq.addEventListener('change', handler);
    return () => mq.removeEventListener('change', handler);
  });

  // Persist settings
  $effect(() => {
    try {
      const v = localStorage.getItem('monitor_timeWindow');
      if (v) timeWindowSec = Number(v);
    } catch {}
  });
  $effect(() => {
    localStorage.setItem('monitor_timeWindow', String(timeWindowSec));
  });
  $effect(() => {
    try {
      const stored = localStorage.getItem('monitor_selectedIds');
      if (stored) selectedIds = new Set(JSON.parse(stored));
    } catch {}
  });
  $effect(() => {
    localStorage.setItem('monitor_selectedIds', JSON.stringify([...selectedIds]));
  });

  // Load data links
  $effect(() => {
    HybridBridge.getDataLinks().then((result) => {
      allDataLinks = result.dataLinks;
    }).catch(() => {});
  });

  // Cleanup on unmount
  $effect(() => {
    return () => { stopTimer(); };
  });

  // Compute live stats
  $effect(() => {
    const frameCount = liveDataStore.frameCount;
    for (const id of selectedIds) {
      const val = liveDataStore.values[id];
      if (val != null) {
        const existing = _statsAccum.get(id);
        if (existing) {
          _statsAccum.set(id, {
            min: Math.min(existing.min, val),
            max: Math.max(existing.max, val),
            avg: (existing.avg * existing.count + val) / (existing.count + 1),
            delta: val - existing.min,
            rateOfChange: existing.rateOfChange,
            count: existing.count + 1,
            durationMs: performance.now(),
          });
        } else {
          _statsAccum.set(id, {
            min: val, max: val, avg: val, delta: 0,
            rateOfChange: 0, count: 1, durationMs: 0,
          });
        }
      }
    }
    liveStats = new Map(_statsAccum);
  });

  // ── Recording ──────────────────────────────────────────────────────────
  function startTimer() {
    stopTimer();
    timerTick++;
    timerInterval = setInterval(() => timerTick++, 100);
  }
  function stopTimer() {
    if (timerInterval) { clearInterval(timerInterval); timerInterval = null; }
  }

  function handleRecord() {
    if (SessionRecorder.state === 'recording') {
      const recorded = SessionRecorder.getRecordedData();
      SessionStore.save(recorded);
      SessionRecorder.reset();
      recorderState = 'stopped';
      stopTimer();
      onNavigate('sessions');
    } else {
      _statsAccum.clear();
      liveStats = new Map();
      const nameMap = new Map<number, string>();
      for (const dl of allDataLinks) nameMap.set(dl.id, dl.name);
      SessionRecorder.start([...selectedIds], nameMap);
      recorderState = 'recording';
      startTimer();
    }
  }

  function handleFreezeFrame() {
    SessionRecorder.freezeFrame();
  }

  // ── Helpers ────────────────────────────────────────────────────────────
  function formatMs(ms: number): string {
    const s = Math.floor(ms / 1000);
    const m = Math.floor(s / 60);
    const h = Math.floor(m / 60);
    return `${String(h).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  }
</script>

<div class="flex flex-col h-full select-none">
  <!-- ── Top Bar ──────────────────────────────────────────────────────── -->
  <div class="flex items-center gap-3 px-4 py-2 bg-metro-surface border-b border-metro-border text-[11px]">
    <!-- Connection status -->
    <div class="flex items-center gap-2">
      <div class="w-2 h-2 rounded-full shrink-0
        {connectionState.state === 'Connected' ? 'bg-metro-green' :
         connectionState.state === 'Connecting' ? 'bg-metro-yellow' : 'bg-metro-red'}">
      </div>
      <span class="text-metro-text-secondary">{connectionState.state}</span>
    </div>
    <div class="text-metro-border">|</div>

    <!-- Recording controls -->
    <button
      class="flex items-center gap-1.5 px-3 py-1.5 rounded text-[11px] font-bold uppercase tracking-wider transition-colors duration-150
        {recorderState === 'recording'
          ? 'bg-metro-red text-white'
          : 'metro-btn-primary'}"
      onclick={handleRecord}
    >
      {#if recorderState === 'recording'}
        <IconPlayerStop size={14} />
        Stop
      {:else}
        <IconPlayerPlay size={14} />
        Record
      {/if}
    </button>
    {#if recorderState === 'recording'}
      <span class="text-metro-red font-mono tabular-nums text-[12px] font-bold">{timerTick ? formatMs(SessionRecorder.durationMs) : '00:00:00'}</span>
      <button
        class="metro-btn-secondary flex items-center gap-1 px-2 py-1 text-[10px]"
        onclick={handleFreezeFrame}
        title="Bookmark this moment"
      >
        <IconBookmark size={12} />
      </button>
    {/if}

    <div class="flex-1"></div>

    <!-- Stats toggle -->
    <button
      class="flex items-center gap-1 px-2.5 py-1 rounded text-[10px] font-bold uppercase tracking-wider transition-colors duration-150
        {showStats ? 'bg-metro-teal text-white' : 'metro-btn-secondary'}"
      onclick={() => showStats = !showStats}
    >
      Stats
    </button>

    <!-- Time window -->
    <select
      class="metro-input py-1 px-1.5 text-[10px]"
      bind:value={timeWindowSec}
    >
      <option value={5}>5s</option>
      <option value={10}>10s</option>
      <option value={30}>30s</option>
      <option value={60}>1m</option>
      <option value={300}>5m</option>
      <option value={1800}>30m</option>
    </select>

    <!-- Sessions (navigates to Sessions page) -->
    <button
      class="metro-btn-secondary flex items-center gap-1.5 px-2.5 py-1 text-[10px]"
      onclick={() => onNavigate('sessions')}
    >
      <IconStack2 size={14} />
      Sessions
    </button>
  </div>

  <!-- ── Main Content ─────────────────────────────────────────────────── -->
  {#if isMobile}
    <!-- Mobile: tab-based layout -->
    <div class="flex-1 min-h-0 flex flex-col">
      {#if mobileTab === 'chart'}
        <div class="flex-1 min-h-0 p-1">
          {#if chartSeries.length === 0}
            <div class="flex items-center justify-center h-full text-metro-text-muted text-[12px] px-4 text-center">
              Tap <span class="text-metro-blue mx-0.5 font-bold">Sensors</span> tab to select data links to chart
            </div>
          {:else}
            <div class="relative h-full">
              <StreamingLineChart
                series={chartSeries}
                {timeWindowSec}
                showDataZoom={true}
                mode="live"
              />
              {#if showStats}
                <div class="absolute top-1 right-1 flex flex-col gap-0.5 z-10">
                  {#each chartSeries as s}
                    {@const stats = liveStats.get(Number(s.id))}
                    {#if stats}
                      <div class="bg-metro-card/90 px-1.5 py-0.5 text-[9px] font-mono flex items-center gap-1.5 border border-metro-border">
                        <span class="w-2 h-2 rounded-full shrink-0" style="background: {s.color}"></span>
                        <span class="text-metro-text-muted">{stats.min.toFixed(1)}</span>
                        <span class="text-white font-bold">{stats.avg.toFixed(1)}</span>
                        <span class="text-metro-text-muted">{stats.max.toFixed(1)}</span>
                      </div>
                    {/if}
                  {/each}
                </div>
              {/if}
            </div>
          {/if}
        </div>
      {:else if mobileTab === 'sensors'}
        <div class="flex-1 min-h-0">
          <SensorPicker dataLinks={allDataLinks} bind:selectedIds />
        </div>
      {:else}
        <div class="flex-1 min-h-0">
          <EcuInfoPanel />
        </div>
      {/if}
    </div>

    <!-- Mobile tab bar -->
    <div class="flex border-t border-metro-border bg-metro-sidebar">
      <button class="flex-1 flex flex-col items-center gap-0.5 py-2 {mobileTab === 'chart' ? 'text-metro-blue' : 'text-metro-text-muted'}"
        onclick={() => mobileTab = 'chart'}>
        <IconChartLine size={20} />
        <span class="text-[10px]">Chart</span>
      </button>
      <button class="flex-1 flex flex-col items-center gap-0.5 py-2 {mobileTab === 'sensors' ? 'text-metro-blue' : 'text-metro-text-muted'}"
        onclick={() => mobileTab = 'sensors'}>
        <IconAdjustments size={20} />
        <span class="text-[10px]">Sensors</span>
        {#if selectedIds.size > 0}
          <span class="absolute top-1 right-1/4 w-4 h-4 rounded-full bg-metro-blue text-[9px] text-white flex items-center justify-center font-bold">{selectedIds.size}</span>
        {/if}
      </button>
      <button class="flex-1 flex flex-col items-center gap-0.5 py-2 {mobileTab === 'info' ? 'text-metro-green' : 'text-metro-text-muted'}"
        onclick={() => mobileTab = 'info'}>
        <IconCircleDotted size={20} />
        <span class="text-[10px]">ECU</span>
      </button>
    </div>
  {:else}
    <!-- Desktop: three-panel layout -->
    <div class="flex flex-1 min-h-0">
      <!-- Left: Sensor Picker -->
      <div class="w-64 shrink-0 border-r border-metro-border">
        <SensorPicker dataLinks={allDataLinks} bind:selectedIds />
      </div>

      <!-- Center: Chart -->
      <div class="flex-1 min-w-0 p-2 flex flex-col">
        {#if chartSeries.length === 0}
          <div class="flex items-center justify-center h-full text-metro-text-muted text-[13px]">
            Select sensors from the left panel to chart
          </div>
        {:else}
          <div class="flex-1 min-h-0 relative">
            <StreamingLineChart
              series={chartSeries}
              {timeWindowSec}
              showDataZoom={true}
              mode="live"
            />
            {#if showStats}
              <div class="absolute top-1 right-1 flex flex-col gap-0.5 z-10">
                {#each chartSeries as s}
                  {@const stats = liveStats.get(Number(s.id))}
                  {#if stats}
                    <div class="bg-metro-card/90 px-2 py-0.5 text-[10px] font-mono flex items-center gap-2 border border-metro-border">
                      <span class="w-2 h-2 rounded-full shrink-0" style="background: {s.color}"></span>
                      <span class="text-metro-text-muted">min:{stats.min.toFixed(1)}</span>
                      <span class="text-white font-bold">avg:{stats.avg.toFixed(1)}</span>
                      <span class="text-metro-text-muted">max:{stats.max.toFixed(1)}</span>
                    </div>
                  {/if}
                {/each}
              </div>
            {/if}
          </div>
        {/if}
      </div>

      <!-- Right: ECU Info -->
      <div class="w-60 shrink-0">
        <EcuInfoPanel />
      </div>
    </div>
  {/if}
</div>

<script lang="ts">
  import StreamingLineChart from '../lib/echarts/StreamingLineChart.svelte';
  import SensorPicker from '../lib/monitor/SensorPicker.svelte';
  import EcuInfoPanel from '../lib/monitor/EcuInfoPanel.svelte';
  import RecordingBar from '../lib/monitor/RecordingBar.svelte';
  import { SessionRecorder } from '../lib/monitor/SessionRecorder';
  import { HybridBridge } from '../lib/HybridBridge';
  import { liveDataStore } from '../lib/stores/LiveDataStore.svelte';
  import type { DataLinkDefinition } from '../lib/HybridBridgeTypes';
  import { getSensorColor } from '../lib/monitor/sensorColors';
  import { IconChartLine, IconAdjustments, IconCircleDotted } from '@tabler/icons-svelte';

  let {
    onNavigate,
    connectionState,
  }: {
    onNavigate: (page: string) => void;
    connectionState: { state: string };
  } = $props();

  let allDataLinks = $state<DataLinkDefinition[]>([]);
  let selectedIds = $state<Set<number>>(new Set());
  let timeWindowSec = $state(10);
  let mobileTab: 'chart' | 'sensors' | 'info' = $state('chart');
  let isMobile = $state(false);

  $effect(() => {
    const mq = window.matchMedia('(max-width: 767px)');
    isMobile = mq.matches;
    const handler = (e: MediaQueryListEvent) => { isMobile = e.matches; };
    mq.addEventListener('change', handler);
    return () => mq.removeEventListener('change', handler);
  });

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
    HybridBridge.getDataLinks().then((result) => {
      allDataLinks = result.dataLinks;
    }).catch(() => {});
  });

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

  function handleExport(format: 'csv' | 'yaml') {
    const content = format === 'csv' ? SessionRecorder.toCsv() : SessionRecorder.toYaml();
    const ext = format === 'csv' ? 'csv' : 'yaml';
    HybridBridge.saveFile(`session.${ext}`, content);
  }
</script>

<div class="flex flex-col h-full">
  <!-- Top status bar -->
  <div class="flex items-center gap-2 px-3 py-1.5 bg-[#1a1a1a] border-b border-[#333] text-[11px]">
    <div class="flex items-center gap-1.5">
      <div class="w-1.5 h-1.5 rounded-full shrink-0
        {connectionState.state === 'Connected' ? 'bg-green-500' :
         connectionState.state === 'Connecting' ? 'bg-amber-500' : 'bg-red-500'}">
      </div>
      <span class="text-gray-400">{connectionState.state}</span>
    </div>
    <div class="text-gray-600">|</div>
    <div class="text-gray-400 tabular-nums">{liveDataStore.frameCount}f</div>
    <div class="text-gray-600">|</div>
    <div class="text-gray-400 tabular-nums">{Array.from(selectedIds).length}s</div>
    <div class="flex-1"></div>
    <select
      class="bg-[#222] border border-[#444] rounded px-1.5 py-0.5 text-[10px] text-gray-300"
      bind:value={timeWindowSec}
    >
      <option value={5}>5s</option>
      <option value={10}>10s</option>
      <option value={30}>30s</option>
      <option value={60}>1m</option>
      <option value={300}>5m</option>
      <option value={1800}>30m</option>
    </select>
  </div>

  {#if isMobile}
    <!-- Mobile: tab-based layout -->
    <div class="flex-1 min-h-0 flex flex-col">
      {#if mobileTab === 'chart'}
        <div class="flex-1 min-h-0 p-1">
          {#if chartSeries.length === 0}
            <div class="flex items-center justify-center h-full text-gray-500 text-xs px-4 text-center">
              Tap <span class="text-sky-400 mx-0.5">Sensors</span> tab to select data links to chart
            </div>
          {:else}
            <StreamingLineChart
              series={chartSeries}
              {timeWindowSec}
              showDataZoom={true}
            />
          {/if}
        </div>
      {:else if mobileTab === 'sensors'}
        <div class="flex-1 min-h-0">
          <SensorPicker
            dataLinks={allDataLinks}
            bind:selectedIds
          />
        </div>
      {:else}
        <div class="flex-1 min-h-0">
          <EcuInfoPanel />
        </div>
      {/if}
    </div>

    <!-- Mobile tab bar -->
    <div class="flex border-t border-[#333] bg-[#1a1a1a]">
      <button
        class="flex-1 flex flex-col items-center gap-0.5 py-2 transition-colors
          {mobileTab === 'chart' ? 'text-sky-400' : 'text-gray-500'}"
        onclick={() => (mobileTab = 'chart')}
      >
        <IconChartLine size={18} />
        <span class="text-[9px]">Chart</span>
      </button>
      <button
        class="flex-1 flex flex-col items-center gap-0.5 py-2 transition-colors
          {mobileTab === 'sensors' ? 'text-sky-400' : 'text-gray-500'}"
        onclick={() => (mobileTab = 'sensors')}
      >
        <IconAdjustments size={18} />
        <span class="text-[9px]">Sensors</span>
        {#if selectedIds.size > 0}
          <span class="absolute top-1 right-1/4 w-4 h-4 rounded-full bg-sky-600 text-[8px] text-white flex items-center justify-center">
            {selectedIds.size}
          </span>
        {/if}
      </button>
      <button
        class="flex-1 flex flex-col items-center gap-0.5 py-2 transition-colors
          {mobileTab === 'info' ? 'text-sky-400' : 'text-gray-500'}"
        onclick={() => (mobileTab = 'info')}
      >
        <IconCircleDotted size={18} />
        <span class="text-[9px]">ECU</span>
      </button>
    </div>
  {:else}
    <!-- Desktop: three-panel layout -->
    <div class="flex flex-1 min-h-0">
      <div class="w-64 shrink-0">
        <SensorPicker
          dataLinks={allDataLinks}
          bind:selectedIds
        />
      </div>
      <div class="flex-1 min-w-0 p-2">
        {#if chartSeries.length === 0}
          <div class="flex items-center justify-center h-full text-gray-500 text-sm">
            Select sensors from the left panel to chart
          </div>
        {:else}
          <StreamingLineChart
            series={chartSeries}
            {timeWindowSec}
            showDataZoom={true}
          />
        {/if}
      </div>
      <div class="w-60 shrink-0">
        <EcuInfoPanel />
      </div>
    </div>
  {/if}

  <!-- Recording bar (both layouts) -->
  <RecordingBar
    selectedSensorIds={Array.from(selectedIds)}
    onExport={handleExport}
  />
</div>

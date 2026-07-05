<script lang="ts">
  import { liveDataStore } from '../stores/LiveDataStore.svelte';
  import { HybridBridge } from '../HybridBridge';
  import type { ConnectionStateInfo, EcuInfoResult } from '../HybridBridgeTypes';

  let activeTab: 'ecu' | 'settings' = $state('ecu');

  const TIME_WINDOW_OPTIONS = [5, 10, 30, 60, 300, 1800];

  let defaultTimeWindow = $state(10);
  let autoScaleY = $state(true);

  $effect(() => {
    try {
      const v = localStorage.getItem('monitor_timeWindow');
      if (v) defaultTimeWindow = Number(v);
    } catch {}
  });
  $effect(() => {
    try {
      const v = localStorage.getItem('monitor_autoScaleY');
      if (v != null) autoScaleY = v !== 'false';
    } catch {}
  });
  $effect(() => {
    localStorage.setItem('monitor_timeWindow', String(defaultTimeWindow));
  });
  $effect(() => {
    localStorage.setItem('monitor_autoScaleY', String(autoScaleY));
  });

  let connectionState = $state<ConnectionStateInfo | null>(null);
  let ecuInfo = $state<EcuInfoResult | null>(null);

  let tick = $state(0);
  $effect(() => {
    const id = setInterval(async () => {
      tick++;
      try {
        const cs = await HybridBridge.getConnectionState();
        connectionState = cs;
      } catch {}
      try {
        const info = await HybridBridge.getEcuInfo();
        ecuInfo = info;
      } catch {}
    }, 2000);
    return () => clearInterval(id);
  });

  function formatTimeSince(ms: number): string {
    if (ms <= 0) return '--';
    const s = Math.floor(ms / 1000);
    if (s < 60) return `${s}s`;
    const m = Math.floor(s / 60);
    if (m < 60) return `${m}m ${s % 60}s`;
    return `${Math.floor(m / 60)}h ${m % 60}m`;
  }
</script>

<div class="flex flex-col h-full bg-[#1a1a1a] border-l border-[#333]">
  <div class="flex border-b border-[#333]">
    <button
      class="flex-1 px-2 py-1.5 text-[11px] transition-colors
        {activeTab === 'ecu' ? 'text-sky-400 border-b-2 border-sky-400' : 'text-gray-500 hover:text-gray-300'}"
      onclick={() => (activeTab = 'ecu')}
    >
      ECU Info
    </button>
    <button
      class="flex-1 px-2 py-1.5 text-[11px] transition-colors
        {activeTab === 'settings' ? 'text-sky-400 border-b-2 border-sky-400' : 'text-gray-500 hover:text-gray-300'}"
      onclick={() => (activeTab = 'settings')}
    >
      Settings
    </button>
  </div>

  <div class="flex-1 overflow-y-auto p-2 scrollbar-thin">
    {#if activeTab === 'ecu'}
      <div class="space-y-2">
        <div class="flex items-center gap-2">
          <div class="w-2 h-2 rounded-full
            {connectionState?.state === 'connected' ? 'bg-green-500' :
             connectionState?.state === 'connecting' ? 'bg-amber-500' : 'bg-red-500'}">
          </div>
          <span class="text-[11px] text-gray-300">
            {connectionState?.state ?? 'unknown'}
          </span>
        </div>

        {#if connectionState?.connectionType}
          <div class="bg-[#222] rounded p-2">
            <div class="text-[10px] text-gray-500 uppercase tracking-wider mb-1">Connection</div>
            <div class="text-[11px] text-white">
              {connectionState.connectionType === 'tcp' ? 'TCP' : 'Serial'}
            </div>
            <div class="text-[10px] text-gray-400 font-mono">
              {connectionState.connectionDetail}
            </div>
          </div>
        {/if}

        {#if connectionState?.protocolInfo}
          <div class="bg-[#222] rounded p-2">
            <div class="text-[10px] text-gray-500 uppercase tracking-wider mb-1">Protocol</div>
            <div class="space-y-1">
              <div class="flex justify-between text-[11px]">
                <span class="text-gray-400">Product</span>
                <span class="text-white">{connectionState.protocolInfo.product}</span>
              </div>
              <div class="flex justify-between text-[11px]">
                <span class="text-gray-400">Model</span>
                <span class="text-white">{connectionState.protocolInfo.model}</span>
              </div>
              <div class="flex justify-between text-[11px]">
                <span class="text-gray-400">Firmware</span>
                <span class="text-white">{connectionState.protocolInfo.version}</span>
              </div>
              <div class="flex justify-between text-[11px]">
                <span class="text-gray-400">Entities</span>
                <span class="text-white">{connectionState.protocolInfo.entityCount}</span>
              </div>
            </div>
          </div>
        {/if}

        <div class="bg-[#222] rounded p-2">
          <div class="text-[10px] text-gray-500 uppercase tracking-wider mb-1">Live Data</div>
          <div class="space-y-1">
            <div class="flex justify-between text-[11px]">
              <span class="text-gray-400">Frames</span>
              <span class="text-white">{liveDataStore.frameCount}</span>
            </div>
            <div class="flex justify-between text-[11px]">
              <span class="text-gray-400">Last Update</span>
              <span class="text-white">
                {formatTimeSince(performance.now() - liveDataStore.lastUpdateAt)}
              </span>
            </div>
            <div class="flex justify-between text-[11px]">
              <span class="text-gray-400">Reporting</span>
              <span class="text-white">{liveDataStore.isReportingActive ? 'Active' : 'Inactive'}</span>
            </div>
          </div>
        </div>
      </div>
    {:else}
      <div class="space-y-3">
        <div class="bg-[#222] rounded p-2">
          <div class="text-[10px] text-gray-500 uppercase tracking-wider mb-2">Chart</div>
          <div class="space-y-2">
            <div>
              <label for="monitor-timewindow" class="text-[11px] text-gray-400 block mb-1">Default Time Window</label>
              <select
                id="monitor-timewindow"
                class="w-full bg-[#2a2a2a] border border-[#444] rounded px-2 py-1 text-[11px] text-white"
                bind:value={defaultTimeWindow}
              >
                {#each TIME_WINDOW_OPTIONS as opt}
                  <option value={opt}>{opt >= 60 ? `${opt / 60}m` : `${opt}s`}</option>
                {/each}
              </select>
            </div>
            <div class="flex items-center justify-between">
              <label class="text-[11px] text-gray-400">Auto-scale Y-axis</label>
              <button
                class="w-9 h-5 rounded-full transition-colors relative
                  {autoScaleY ? 'bg-sky-600' : 'bg-[#444]'}"
                aria-label="Toggle auto-scale Y-axis"
                onclick={() => (autoScaleY = !autoScaleY)}
              >
                <div class="absolute top-0.5 w-4 h-4 rounded-full bg-white transition-transform
                  {autoScaleY ? 'left-[18px]' : 'left-0.5'}"></div>
              </button>
            </div>
          </div>
        </div>
      </div>
    {/if}
  </div>
</div>

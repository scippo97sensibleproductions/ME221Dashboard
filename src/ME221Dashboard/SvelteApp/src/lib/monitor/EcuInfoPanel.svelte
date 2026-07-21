<script lang="ts">
  import { liveDataStore } from '../stores/LiveDataStore.svelte';
  import { HybridBridge } from '../HybridBridge';
  import type { ConnectionStateInfo, EcuInfoResult } from '../HybridBridgeTypes';

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
  <div class="px-2 py-1.5 border-b border-[#333]">
    <span class="text-[10px] font-bold uppercase tracking-wider text-gray-500">ECU Info</span>
  </div>

  <div class="flex-1 overflow-y-auto p-2 scrollbar-thin">
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
  </div>
</div>

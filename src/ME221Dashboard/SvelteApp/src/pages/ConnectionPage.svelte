<script lang="ts">
  import { onMount } from 'svelte';
  import { HybridBridge, type ConnectionStateInfo } from '../lib/HybridBridge';
  import {
    IconPlugConnected, IconRefresh, IconWifi, IconUsb, IconConnection,
    IconNetwork, IconDeviceLaptop, IconCpu, IconCircleCheck,
    IconCircleX, IconLoader, IconPlug, IconArrowRight
  } from '@tabler/icons-svelte';
  import { fly, scale } from 'svelte/transition';

  let { connectionState, onConnectionChange }: {
    connectionState: ConnectionStateInfo;
    onConnectionChange: (state: ConnectionStateInfo) => void;
  } = $props();

  type ConnType = 'tcp' | 'serial';
  let connectionType = $state<ConnType>('tcp');
  let host = $state('127.0.0.1');
  let port = $state('22100');
  let serialPorts = $state<{ name: string; description?: string; hasPermission?: boolean }[]>([]);
  let selectedPort = $state('');
  let isConnecting = $state(false);

  try {
    const last = localStorage.getItem('me221_lastConnection');
    if (last) {
      const parsed = JSON.parse(last);
      if (parsed.host) host = parsed.host;
      if (parsed.port) port = String(parsed.port);
      if (parsed.type) connectionType = parsed.type;
    }
  } catch {}

  let isConnected = $derived(connectionState.state === 'Connected');
  let isDisabled = $derived(isConnected || isConnecting);
  let statusLabel = $derived(
    isConnected ? 'Connected' :
    isConnecting ? 'Connecting...' :
    connectionState.state === 'Error' ? 'Error' : 'Ready'
  );
  let statusColor = $derived(
    isConnected ? 'var(--metro-green)' :
    isConnecting ? 'var(--metro-orange)' :
    connectionState.state === 'Error' ? 'var(--metro-red)' : 'var(--metro-border)'
  );

  async function refreshPorts() {
    const result = await HybridBridge.getAvailablePorts();
    serialPorts = result.ports;
    if (result.ports.length > 0 && !selectedPort) {
      selectedPort = result.ports[0].name;
    }
  }

  async function connect() {
    isConnecting = true;
    try {
      let result;
      if (connectionType === 'tcp') {
        result = await HybridBridge.connectTcp(host, parseInt(port));
      } else {
        result = await HybridBridge.connectSerial(selectedPort);
      }
      if (result.success) {
        try {
          localStorage.setItem('me221_lastConnection', JSON.stringify({
            type: connectionType,
            host: connectionType === 'tcp' ? host : undefined,
            port: connectionType === 'tcp' ? parseInt(port) : undefined,
            serialPort: connectionType === 'serial' ? selectedPort : undefined,
          }));
        } catch {}
        onConnectionChange({ state: result.state || 'Connected' });
      } else {
        onConnectionChange({ state: 'Error', error: result.error });
      }
    } catch (err) {
      onConnectionChange({ state: 'Error', error: String(err) });
    } finally {
      isConnecting = false;
    }
  }

  async function disconnect() {
    try {
      await HybridBridge.disconnect();
    } catch {}
    onConnectionChange({ state: 'Disconnected' });
  }

  function selectType(type: ConnType) {
    if (!isDisabled) {
      connectionType = type;
    }
  }

  onMount(() => {
    refreshPorts();
  });
</script>

<div class="h-full overflow-auto" style="background-color: var(--metro-bg);">
  <div class="max-w-lg mx-auto p-4 lg:max-w-4xl lg:grid lg:grid-cols-2 lg:gap-12 lg:items-center">

    <!-- Left column: Branding (desktop only) -->
    <div class="hidden lg:flex flex-col items-center justify-center text-center">
      <div class="mb-4 inline-flex h-20 w-20 items-center justify-center" style="background-color: var(--metro-blue);">
        <IconPlugConnected size={40} style="color: var(--metro-text-on-accent);" />
      </div>
      <h1 class="mb-2 text-[20px] font-extrabold uppercase tracking-[-0.5px] text-white" style="line-height: 1.1;">
        Connect to <span style="color: var(--metro-blue);">ECU</span>
      </h1>
      <p class="mb-6 text-[13px]" style="color: var(--metro-text-secondary); line-height: 1.4;">
        Establish a connection to your engine control unit
      </p>
      <div class="space-y-2">
        <div class="flex items-center gap-2 text-[13px]" style="color: var(--metro-text-secondary);">
          <IconCircleCheck size={16} style="color: var(--metro-blue);" />
          <span>Real-time data monitoring</span>
        </div>
        <div class="flex items-center gap-2 text-[13px]" style="color: var(--metro-text-secondary);">
          <IconCircleCheck size={16} style="color: var(--metro-blue);" />
          <span>Calibration management</span>
        </div>
        <div class="flex items-center gap-2 text-[13px]" style="color: var(--metro-text-secondary);">
          <IconCircleCheck size={16} style="color: var(--metro-blue);" />
          <span>Live dashboard with gauges</span>
        </div>
      </div>
    </div>

    <!-- Right column: Connection card -->
    <div>
      <!-- Mobile header -->
      <div class="mb-4 lg:hidden text-center">
        <span class="mb-2 inline-flex h-12 w-12 items-center justify-center" style="background-color: var(--metro-blue);">
          <IconPlugConnected size={24} style="color: var(--metro-text-on-accent);" />
        </span>
        <h1 class="text-[16px] font-extrabold uppercase tracking-wider text-white">Connect to ECU</h1>
      </div>

      <div style="background-color: var(--metro-card); border: 1px solid var(--metro-border);">

        <!-- Status bar -->
        <div class="flex items-center justify-between px-4 py-2.5" style="background-color: var(--metro-input-bg); border-bottom: 1px solid var(--metro-border);">
          <div class="flex items-center gap-2.5">
            {#if isConnected}
              <span class="flex h-2.5 w-2.5 items-center justify-center">
                <span class="absolute h-2.5 w-2.5 animate-ping rounded-full opacity-75" style="background-color: var(--metro-green);"></span>
                <span class="relative h-2.5 w-2.5 rounded-full" style="background-color: var(--metro-green);"></span>
              </span>
              <span class="text-[13px] font-semibold" style="color: var(--metro-green);">Connected</span>
            {:else if isConnecting}
              <IconLoader size={16} class="animate-spin" style="color: var(--metro-orange);" />
              <span class="text-[13px] font-semibold" style="color: var(--metro-orange);">Connecting...</span>
            {:else if connectionState.state === 'Error'}
              <IconCircleX size={16} style="color: var(--metro-red);" />
              <span class="text-[13px] font-semibold" style="color: var(--metro-red);">Failed</span>
            {:else}
              <span class="flex h-2.5 w-2.5 rounded-full" style="background-color: var(--metro-text-muted);"></span>
              <span class="text-[13px] font-semibold" style="color: var(--metro-text-secondary);">Ready</span>
            {/if}
          </div>
          <span class="text-white font-bold uppercase" style="background-color: {statusColor}; font-size: 9px; letter-spacing: 0.5px; padding: 2px 8px; line-height: 1;">
            {statusLabel}
          </span>
        </div>

        <!-- Connection type selector -->
        <div class="px-4 pt-4">
          <span class="mb-2 block text-[11px] font-bold uppercase tracking-wider" style="color: var(--metro-text-secondary);">Connection Type</span>
          <div class="grid grid-cols-2 gap-2">
            <button
              class="flex flex-col items-center gap-2 p-4 text-center transition-colors duration-150"
              style="background-color: {connectionType === 'tcp' ? 'var(--metro-blue)' : 'var(--metro-input-bg)'}; border: 1px solid {connectionType === 'tcp' ? 'var(--metro-blue)' : 'var(--metro-input-border)'}; {isDisabled ? 'opacity: 0.5; cursor: not-allowed;' : ''}"
              onclick={() => selectType('tcp')}
            >
              <IconNetwork size={24} style={connectionType === 'tcp' ? 'color: var(--metro-text-on-accent);' : 'color: var(--metro-text-secondary);'} />
              <span class="text-[13px] font-bold" style="color: {connectionType === 'tcp' ? 'var(--metro-text-on-accent)' : 'var(--metro-text-secondary)'};">TCP/IP</span>
              <span class="text-[11px]" style="color: {connectionType === 'tcp' ? 'rgba(255,255,255,0.7)' : 'var(--metro-text-muted)'};">Network connection</span>
            </button>
            <button
              class="flex flex-col items-center gap-2 p-4 text-center transition-colors duration-150"
              style="background-color: {connectionType === 'serial' ? 'var(--metro-purple)' : 'var(--metro-input-bg)'}; border: 1px solid {connectionType === 'serial' ? 'var(--metro-purple)' : 'var(--metro-input-border)'}; {isDisabled ? 'opacity: 0.5; cursor: not-allowed;' : ''}"
              onclick={() => selectType('serial')}
            >
              <IconUsb size={24} style={connectionType === 'serial' ? 'color: var(--metro-text-on-accent);' : 'color: var(--metro-text-secondary);'} />
              <span class="text-[13px] font-bold" style="color: {connectionType === 'serial' ? 'var(--metro-text-on-accent)' : 'var(--metro-text-secondary)'};">Serial</span>
              <span class="text-[11px]" style="color: {connectionType === 'serial' ? 'rgba(255,255,255,0.7)' : 'var(--metro-text-muted)'};">USB device</span>
            </button>
          </div>
        </div>

        <!-- Connection settings -->
        <div class="px-4 pt-4">
          <span class="mb-2 block text-[11px] font-bold uppercase tracking-wider" style="color: var(--metro-text-secondary);">
            {connectionType === 'tcp' ? 'Network Settings' : 'Serial Port Settings'}
          </span>

          {#if connectionType === 'tcp'}
            <div transition:fly={{ y: 8, duration: 200 }} class="space-y-3">
              <div>
                <label for="host" class="mb-1.5 flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider" style="color: var(--metro-text-secondary);">
                  <IconDeviceLaptop size={14} /> Host Address
                </label>
                <input
                  id="host"
                  type="text"
                  bind:value={host}
                  placeholder="192.168.1.100"
                  disabled={isDisabled}
                  class="w-full text-white px-3 py-2 text-[13px] focus:outline-none transition-colors duration-150 disabled:opacity-50 disabled:cursor-not-allowed"
                  style="background-color: var(--metro-input-bg); border: 1px solid var(--metro-input-border); border-radius: 2px;"
                  onfocus={(e) => { if (!isDisabled) e.currentTarget.style.borderColor = 'var(--metro-blue)'; }}
                  onblur={(e) => { e.currentTarget.style.borderColor = 'var(--metro-input-border)'; }}
                />
                <p class="mt-1 text-[11px]" style="color: var(--metro-text-muted);">IP address or hostname of the ECU</p>
              </div>
              <div>
                <label for="port" class="mb-1.5 flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider" style="color: var(--metro-text-secondary);">
                  <IconPlug size={14} /> Port
                </label>
                <input
                  id="port"
                  type="number"
                  bind:value={port}
                  placeholder="22100"
                  disabled={isDisabled}
                  class="w-full text-white px-3 py-2 text-[13px] font-mono focus:outline-none transition-colors duration-150 disabled:opacity-50 disabled:cursor-not-allowed"
                  style="background-color: var(--metro-input-bg); border: 1px solid var(--metro-input-border); border-radius: 2px;"
                  onfocus={(e) => { if (!isDisabled) e.currentTarget.style.borderColor = 'var(--metro-blue)'; }}
                  onblur={(e) => { e.currentTarget.style.borderColor = 'var(--metro-input-border)'; }}
                />
                <p class="mt-1 text-[11px]" style="color: var(--metro-text-muted);">Default: 22100</p>
              </div>
            </div>
          {:else}
            <div transition:fly={{ y: 8, duration: 200 }} class="space-y-3">
              <div>
                <label for="serialPort" class="mb-1.5 flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider" style="color: var(--metro-text-secondary);">
                  <IconUsb size={14} /> Serial Port
                </label>
                <div class="flex gap-2">
                  <select
                    id="serialPort"
                    bind:value={selectedPort}
                    disabled={isDisabled}
                    class="flex-1 text-white px-3 py-2 text-[13px] focus:outline-none appearance-none transition-colors duration-150 disabled:opacity-50 disabled:cursor-not-allowed"
                    style="background-color: var(--metro-input-bg); border: 1px solid var(--metro-input-border); border-radius: 2px;"
                    onfocus={(e) => { if (!isDisabled) e.currentTarget.style.borderColor = 'var(--metro-blue)'; }}
                    onblur={(e) => { e.currentTarget.style.borderColor = 'var(--metro-input-border)'; }}
                  >
                    {#if serialPorts.length === 0}
                      <option value="">No ports found</option>
                    {/if}
                    {#each serialPorts as port}
                      <option value={port.name}>{port.description || port.name}</option>
                    {/each}
                  </select>
                  <button
                    class="metro-hover-bg w-10 h-10 flex items-center justify-center transition-colors duration-150 disabled:opacity-50 disabled:cursor-not-allowed"
                    style="background-color: var(--metro-input-bg); border: 1px solid var(--metro-input-border); border-radius: 2px; color: var(--metro-text-secondary);"
                    onclick={refreshPorts}
                    disabled={isDisabled}
                  >
                    <IconRefresh size={16} />
                  </button>
                </div>
                {#if selectedPort && serialPorts.find(p => p.name === selectedPort)?.hasPermission === false}
                  <p class="mt-1 text-[11px]" style="color: var(--metro-yellow);">USB permission required — will prompt on connect</p>
                {:else}
                  <p class="mt-1 text-[11px]" style="color: var(--metro-text-muted);">Select a USB serial device</p>
                {/if}
              </div>
            </div>
          {/if}
        </div>

        <!-- Action button -->
        <div class="mt-4 px-4 py-3" style="border-top: 1px solid var(--metro-border);">
          {#if !isConnected}
            <button
              class="metro-btn-primary group w-full px-4 py-2.5 text-[13px] flex items-center justify-center gap-2"
              onclick={connect}
              disabled={isConnecting}
            >
              {#if isConnecting}
                <IconLoader size={18} class="animate-spin" />
                Connecting...
              {:else}
                <IconWifi size={18} />
                Connect
                <IconArrowRight size={16} class="transition-transform duration-150 group-hover:translate-x-0.5" />
              {/if}
            </button>
          {:else}
            <button
              class="metro-btn-danger w-full px-4 py-2.5 text-[13px]"
              onclick={disconnect}
            >
              Disconnect
            </button>
          {/if}
        </div>
      </div>

      <!-- Protocol info card -->
      {#if connectionState.protocolInfo}
        <div transition:fly={{ y: 12, duration: 300 }} class="mt-4" style="background-color: var(--metro-card); border: 1px solid var(--metro-border); border-left: 4px solid var(--metro-green);">
          <div class="flex items-center gap-3 px-4 py-3">
            <span class="flex h-12 w-12 shrink-0 items-center justify-center" style="background-color: var(--metro-green);">
              <IconCpu size={22} style="color: var(--metro-text-on-accent);" />
            </span>
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-2">
                <p class="truncate text-[13px] font-bold text-white">
                  {connectionState.protocolInfo.product} {connectionState.protocolInfo.model}
                </p>
                <span class="text-white font-bold uppercase" style="background-color: var(--metro-green); font-size: 9px; letter-spacing: 0.5px; padding: 2px 8px; line-height: 1;">Active</span>
              </div>
              <p class="mt-0.5 text-[12px] font-mono" style="color: var(--metro-text-secondary);">
                Firmware v{connectionState.protocolInfo.version}
              </p>
            </div>
          </div>
        </div>
      {/if}

      <!-- Error message -->
      {#if connectionState.state === 'Error' && connectionState.error}
        <div transition:fly={{ y: 12, duration: 300 }} class="mt-4" style="background-color: var(--metro-card); border: 1px solid var(--metro-border); border-left: 4px solid var(--metro-red);">
          <div class="flex items-start gap-3 px-4 py-3">
            <IconCircleX size={20} class="mt-0.5 shrink-0" style="color: var(--metro-red);" />
            <div class="flex-1 min-w-0">
              <p class="text-[13px] font-bold" style="color: var(--metro-red);">Connection Failed</p>
              <p class="mt-0.5 text-[12px]" style="color: var(--metro-text-secondary);">{connectionState.error}</p>
            </div>
            <button
              class="metro-hover-bg shrink-0 px-3 py-1 text-[11px] font-bold uppercase tracking-wider transition-colors duration-150"
              style="border: 1px solid var(--metro-input-border); border-radius: 2px; color: var(--metro-text-secondary);"
              onclick={() => onConnectionChange({ state: 'Disconnected' })}
            >
              Dismiss
            </button>
          </div>
        </div>
      {/if}

      <!-- Tip -->
      <p class="mt-4 text-center text-[11px]" style="color: var(--metro-text-muted);">
        Make sure the ECU is powered on and reachable
      </p>
    </div>
  </div>
</div>

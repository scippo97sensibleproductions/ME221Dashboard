<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { HybridBridge } from '../lib/HybridBridge';
  import { mirrorStore } from '../lib/stores/MirrorStore.svelte';
  import {
    IconRefresh, IconArrowBackUp, IconDeviceMobile, IconPlugConnected,
    IconLoader, IconCircleX,
  } from '@tabler/icons-svelte';

  let { onNavigate, onExit }: { onNavigate: (page: string) => void; onExit: () => void } = $props();

  interface MirrorDevice {
    instanceId: string;
    displayName: string;
    appVersion: string;
    ecuConnected: boolean;
    compatible: boolean;
    address: string;
  }

  let devices = $state<MirrorDevice[]>([]);
  let scanning = $state(false);
  let scanError = $state<string | null>(null);
  let connectError = $state<string | null>(null);
  let connectingId = $state<string | null>(null);
  let lastConnectDevice = $state<MirrorDevice | null>(null);
  // User-initiated re-scan wins over a stale session state screen (revoked / re-pair).
  let forcedSearching = $state(false);

  let sessionState = $derived(mirrorStore.sessionState);
  let hasCompatible = $derived(devices.some(d => d.compatible));

  async function scan() {
    forcedSearching = true;
    scanning = true;
    scanError = null;
    connectError = null;
    try {
      const result = await HybridBridge.mirrorScan();
      if (result.success) {
        devices = result.devices;
      } else {
        devices = [];
        scanError = result.error ?? 'Scan failed';
      }
    } catch (err) {
      devices = [];
      scanError = err instanceof Error ? err.message : String(err);
    } finally {
      scanning = false;
    }
  }

  async function connect(device: MirrorDevice) {
    if (!device.compatible || connectingId) return;
    connectingId = device.instanceId;
    connectError = null;
    try {
      const result = await HybridBridge.mirrorConnect(device.instanceId);
      if (result.success) {
        lastConnectDevice = device;
        onNavigate('dashboard');
      } else {
        connectError = result.error ?? 'Connection failed';
      }
    } catch (err) {
      connectError = err instanceof Error ? err.message : String(err);
    } finally {
      connectingId = null;
    }
  }

  async function cancelApproval() {
    try {
      await HybridBridge.mirrorDisconnect();
    } catch { /* best effort */ }
    scan();
  }

  async function requestAgain() {
    if (!lastConnectDevice) return;
    // R20: drop the stored token + pin so the next connect re-runs approval.
    try {
      await HybridBridge.mirrorForgetDevice(lastConnectDevice.instanceId);
    } catch { /* best effort — connect will re-run approval anyway */ }
    connect(lastConnectDevice);
  }

  onMount(() => {
    mirrorStore.start();
    scan();
  });

  onDestroy(() => {
    // The store keeps running once a session is live (approval pending or
    // connected) — App.svelte owns stop on exit. Only stop a pure-discovery visit.
    if (mirrorStore.sessionState === 'AwaitingApproval' || mirrorStore.isMirroring) return;
    mirrorStore.stop();
  });
</script>

<div class="h-full overflow-auto" style="background-color: var(--metro-bg);">
  <div class="mx-auto max-w-lg p-4">

    <div class="mb-4 flex items-center gap-3">
      <button
        class="flex h-8 w-8 items-center justify-center transition-colors duration-150"
        style="color: var(--metro-text-secondary);"
        onclick={onExit}
        aria-label="Back"
      >
        <IconArrowBackUp size={18} />
      </button>
      <h1 class="text-[20px] font-extrabold uppercase tracking-[-0.5px] text-white" style="line-height: 1.1;">
        Companion Mode
      </h1>
    </div>

    {#if !forcedSearching && sessionState === 'AwaitingApproval'}
      <div class="flex flex-col items-center justify-center gap-4 py-20">
        <span class="inline-block h-8 w-8 animate-spin rounded-full border-2 border-gray-500 border-t-teal-400"></span>
        <p class="text-[10px] font-bold uppercase tracking-wider" style="color: var(--metro-teal);">Waiting for approval on host</p>
        <p class="text-[13px]" style="color: var(--metro-text-secondary);">{mirrorStore.displayName ?? 'ME221'}</p>
        <button class="metro-btn-secondary flex items-center gap-2" onclick={cancelApproval}>
          <IconCircleX size={16} />
          Cancel
        </button>
      </div>
    {:else if !forcedSearching && sessionState === 'Refused'}
      <div class="flex flex-col items-center justify-center gap-4 py-20">
        <IconCircleX size={32} style="color: var(--metro-red);" />
        <p class="text-[10px] font-bold uppercase tracking-wider" style="color: var(--metro-red);">Request Refused</p>
        <p class="text-[13px]" style="color: var(--metro-text-secondary);">The host declined your connection request</p>
        <div class="flex gap-2">
          <button class="metro-btn-primary flex items-center gap-2" onclick={requestAgain} disabled={connectingId !== null}>
            <IconRefresh size={16} />
            Request Again
          </button>
          <button class="metro-btn-secondary flex items-center gap-2" onclick={onExit}>
            <IconArrowBackUp size={16} />
            Back
          </button>
        </div>
      </div>
    {:else if !forcedSearching && sessionState === 'Revoked'}
      <div class="flex flex-col items-center justify-center gap-4 py-20">
        <IconCircleX size={32} style="color: var(--metro-red);" />
        <p class="text-[10px] font-bold uppercase tracking-wider" style="color: var(--metro-red);">Revoked by Host</p>
        <p class="text-center text-[13px]" style="color: var(--metro-text-secondary);">{mirrorStore.sessionError ?? 'This device was revoked by the host'}</p>
        <button class="metro-btn-secondary flex items-center gap-2" onclick={scan}>
          <IconRefresh size={16} />
          Return to Discovery
        </button>
      </div>
    {:else if !forcedSearching && sessionState === 'RePair'}
      <div class="flex flex-col items-center justify-center gap-4 py-20">
        <IconRefresh size={32} style="color: var(--metro-orange);" />
        <p class="text-center text-[10px] font-bold uppercase tracking-wider" style="color: var(--metro-orange);">Certificate Changed — Re-Pair Required</p>
        <p class="text-center text-[13px]" style="color: var(--metro-text-secondary);">{mirrorStore.sessionError ?? 'The host certificate changed since pairing'}</p>
        <div class="flex gap-2">
          <button class="metro-btn-primary flex items-center gap-2" onclick={requestAgain} disabled={connectingId !== null}>
            <IconRefresh size={16} />
            Forget and Request Again
          </button>
          <button class="metro-btn-secondary flex items-center gap-2" onclick={onExit}>
            <IconArrowBackUp size={16} />
            Return to Discovery
          </button>
        </div>
      </div>
    {:else if scanning}
      <div class="flex flex-col items-center justify-center gap-4 py-20">
        <span class="inline-block h-8 w-8 animate-spin rounded-full border-2 border-gray-500 border-t-teal-400"></span>
        <p class="text-[10px] font-bold uppercase tracking-wider" style="color: var(--metro-teal);">Searching for Hosts</p>
        <p class="text-[13px]" style="color: var(--metro-text-secondary);">Looking for ME221 dashboards on your network</p>
      </div>
    {:else if scanError}
      <div class="flex flex-col items-center justify-center gap-4 py-20">
        <IconCircleX size={32} style="color: var(--metro-red);" />
        <p class="text-[10px] font-bold uppercase tracking-wider" style="color: var(--metro-red);">Scan Failed</p>
        <p class="text-center text-[13px]" style="color: var(--metro-text-secondary);">{scanError}</p>
        <button class="metro-btn-secondary flex items-center gap-2" onclick={scan}>
          <IconRefresh size={16} />
          Scan Again
        </button>
      </div>
    {:else if devices.length === 0}
      <div class="flex flex-col items-center justify-center gap-4 py-20">
        <IconCircleX size={32} style="color: var(--metro-text-muted);" />
        <p class="text-[10px] font-bold uppercase tracking-wider" style="color: var(--metro-text-secondary);">No Host Found on This Network</p>
        <p class="text-[13px]" style="color: var(--metro-text-secondary);">Make sure the dashboard device is powered on and on the same network</p>
        <button class="metro-btn-secondary flex items-center gap-2" onclick={scan}>
          <IconRefresh size={16} />
          Scan Again
        </button>
      </div>
    {:else}
      <div class="flex items-center gap-3 border-l-4 pl-3" style="border-color: var(--metro-teal);">
        <IconDeviceMobile size={16} style="color: var(--metro-teal);" />
        <h2 class="text-[13px] font-extrabold uppercase tracking-wider text-white">Hosts Found</h2>
      </div>

      <div class="mt-4 space-y-3">
        {#each devices as device (device.instanceId)}
          <div
            class="p-4 transition-colors duration-150 {device.compatible ? '' : 'opacity-50'}"
            style="background-color: var(--metro-card); border: 1px solid var(--metro-border);"
          >
            <div class="flex items-center justify-between gap-3">
              <div class="min-w-0 flex-1">
                <div class="flex items-center gap-2">
                  <p class="truncate text-[13px] font-bold text-white">{device.displayName || 'ME221'}</p>
                  {#if device.compatible}
                    {#if device.ecuConnected}
                      <span class="metro-badge" style="background-color: var(--metro-green); color: var(--metro-text-on-accent);">ECU</span>
                    {:else}
                      <span class="metro-badge" style="background-color: var(--metro-border); color: var(--metro-text-secondary);">Idle</span>
                    {/if}
                  {:else}
                    <span class="metro-badge" style="background-color: var(--metro-red); color: var(--metro-text-on-accent);">Incompatible (Version Mismatch)</span>
                  {/if}
                </div>
                <p class="mt-0.5 text-[11px] font-mono" style="color: var(--metro-text-secondary);">
                  v{device.appVersion} · {device.address}
                </p>
              </div>
              {#if device.compatible}
                <button
                  class="metro-btn-primary flex shrink-0 items-center gap-2 px-3 py-2"
                  onclick={() => connect(device)}
                  disabled={connectingId !== null}
                >
                  {#if connectingId === device.instanceId}
                    <IconLoader size={16} class="animate-spin" />
                  {:else}
                    <IconPlugConnected size={16} />
                  {/if}
                  Connect
                </button>
              {/if}
            </div>
          </div>
        {/each}

        {#if !hasCompatible}
          <p class="pt-2 text-[11px]" style="color: var(--metro-text-muted);">
            Incompatible hosts are not connectable — update the dashboard app on this device
          </p>
        {/if}

        {#if connectError}
          <p class="text-[12px]" style="color: var(--metro-red);">{connectError}</p>
        {/if}

        <button class="metro-btn-secondary flex items-center gap-2" onclick={scan}>
          <IconRefresh size={16} />
          Scan Again
        </button>
      </div>
    {/if}
  </div>
</div>

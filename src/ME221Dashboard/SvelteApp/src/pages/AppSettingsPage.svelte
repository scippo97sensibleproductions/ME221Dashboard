<script lang="ts">
  import { onDestroy, onMount } from 'svelte';
  import { HybridBridge } from '../lib/HybridBridge';
  import type { EcuInfoResult } from '../lib/HybridBridgeTypes';
  import { IconDownload, IconUpload, IconCar, IconPackage, IconFileCode, IconCheck, IconX, IconLoader2, IconInfoCircle, IconArrowRight, IconDeviceTv } from '@tabler/icons-svelte';
  import MecalImportPreview from '../lib/MecalImportPreview.svelte';

  let { onNavigate }: {
    onNavigate: (page: string) => void;
  } = $props();

  let connected = $state(false);
  let ecuInfo = $state<EcuInfoResult | null>(null);

  // ── Calibration state ──
  let calExporting = $state(false);
  let calImporting = $state(false);
  let calResult = $state<{ success: boolean; message: string } | null>(null);

  // ── Dashboard package state ──
  let dashExporting = $state(false);
  let dashImporting = $state(false);
  let dashResult = $state<{ success: boolean; message: string } | null>(null);
  let dashNames = $state<string[]>([]);
  let selectedDashName = $state('');

  // ── Companion viewers state ──
  let companionName = $state('');
  let companionDevices = $state<{ clientId: string; deviceName: string; approvedAtUtc: string }[]>([]);
  let companionServer = $state<{ success: boolean; running: boolean; port: number; viewerCount: number; pendingCount: number } | null>(null);
  let companionLoaded = $state(false);
  let renaming = $state(false);
  let companionResult = $state<{ success: boolean; message: string } | null>(null);
  let confirmingRevokeId = $state<string | null>(null);
  let confirmingReset = $state(false);
  let confirmTimer: ReturnType<typeof setTimeout> | null = null;

  function getErrorMessage(e: unknown): string {
    return e instanceof Error ? e.message : String(e);
  }

  function clearConfirmTimer() {
    if (confirmTimer) {
      clearTimeout(confirmTimer);
      confirmTimer = null;
    }
  }

  async function refreshCompanion() {
    try {
      const devices = await HybridBridge.getApprovedDevices();
      companionLoaded = devices.success;
      if (devices.success) {
        companionName = devices.displayName;
        companionDevices = devices.devices;
      }
      const server = await HybridBridge.getMirrorServerState();
      companionServer = server;
    } catch (e) {
      companionLoaded = false;
      companionResult = { success: false, message: getErrorMessage(e) };
    }
  }

  async function handleRenameDevice() {
    const name = companionName.trim();
    if (!name || renaming) return;
    renaming = true;
    companionResult = null;
    try {
      const result = await HybridBridge.renameDevice(name);
      companionResult = {
        success: result.success,
        message: result.success ? `Device name saved as "${name}"` : (result.error || 'Rename failed'),
      };
      if (result.success) await refreshCompanion();
    } catch (e) {
      companionResult = { success: false, message: getErrorMessage(e) };
    } finally {
      renaming = false;
    }
  }

  function armRevoke(clientId: string) {
    if (confirmingRevokeId === clientId) {
      void executeRevoke(clientId);
      return;
    }
    confirmingRevokeId = clientId;
    clearConfirmTimer();
    confirmTimer = setTimeout(() => { confirmingRevokeId = null; }, 3000);
  }

  async function executeRevoke(clientId: string) {
    confirmingRevokeId = null;
    clearConfirmTimer();
    companionResult = null;
    try {
      const result = await HybridBridge.revokeDevice(clientId);
      companionResult = { success: result.success, message: result.success ? 'Viewer revoked' : (result.error || 'Revoke failed') };
      await refreshCompanion();
    } catch (e) {
      companionResult = { success: false, message: getErrorMessage(e) };
    }
  }

  function armResetPairing() {
    if (confirmingReset) {
      void executeResetPairing();
      return;
    }
    confirmingReset = true;
    clearConfirmTimer();
    confirmTimer = setTimeout(() => { confirmingReset = false; }, 3000);
  }

  async function executeResetPairing() {
    confirmingReset = false;
    clearConfirmTimer();
    companionResult = null;
    try {
      const result = await HybridBridge.resetPairing();
      companionResult = { success: result.success, message: result.success ? 'All pairings reset' : (result.error || 'Reset failed') };
      await refreshCompanion();
    } catch (e) {
      companionResult = { success: false, message: getErrorMessage(e) };
    }
  }

  function formatApprovedAt(iso: string): string {
    const date = new Date(iso);
    return isNaN(date.getTime()) ? iso : date.toLocaleString();
  }

  onMount(async () => {
    try {
      const connInfo = await HybridBridge.getConnectionState();
      connected = connInfo.state === 'Connected';

      if (connected) {
        const info = await HybridBridge.getEcuInfo();
        if (info.success) ecuInfo = info;
      }

      const names = await HybridBridge.getDashboardNames();
      dashNames = names?.names ?? [];
      selectedDashName = names?.activeDashboard ?? dashNames[0] ?? '';
    } catch (e) {
      console.error('AppSettings init failed', e);
    }
    void refreshCompanion();
  });

  onDestroy(() => {
    clearConfirmTimer();
  });

  // ── Calibration Export ──
  async function handleCalExport() {
    calExporting = true;
    calResult = null;
    try {
      const result = await HybridBridge.exportMecal();
      if (result.success) {
        calResult = { success: true, message: `Exported ${result.tables} tables, ${result.drivers} drivers${result.path ? ' to ' + result.path : ''}` };
      } else {
        calResult = { success: false, message: result.error || 'Export failed' };
      }
    } catch (e) {
      calResult = { success: false, message: getErrorMessage(e) };
    } finally {
      calExporting = false;
    }
  }

  // ── Calibration Import ──
  let mecalPreviewOpen = $state(false);
  let mecalFileContent = $state('');

  async function handleCalImport() {
    calImporting = true;
    calResult = null;
    try {
      const result = await HybridBridge.pickMecalFile();
      if (result.picked && result.content) {
        mecalFileContent = result.content;
        mecalPreviewOpen = true;
      }
    } catch (e) {
      calResult = { success: false, message: getErrorMessage(e) };
    } finally {
      calImporting = false;
    }
  }

  function handleMecalApplied() {
    mecalPreviewOpen = false;
    calResult = { success: true, message: 'Calibration imported successfully' };
  }

  function handleMecalCancel() {
    mecalPreviewOpen = false;
    mecalFileContent = '';
  }

  // ── Dashboard Export ──
  async function handleDashExport() {
    if (!selectedDashName) return;
    dashExporting = true;
    dashResult = null;
    try {
      const result = await HybridBridge.exportDashboard(selectedDashName);
      if (result.success) {
        dashResult = { success: true, message: result.message || `Dashboard "${selectedDashName}" exported` };
      } else {
        dashResult = { success: false, message: result.error || 'Export failed' };
      }
    } catch (e) {
      dashResult = { success: false, message: getErrorMessage(e) };
    } finally {
      dashExporting = false;
    }
  }

  // ── Dashboard Import ──
  async function handleDashImport() {
    dashImporting = true;
    dashResult = null;
    try {
      const result = await HybridBridge.importDashboard();
      if (result.picked && result.success) {
        dashResult = { success: true, message: `Dashboard imported as "${result.dashboardName}"` };
      } else if (result.picked && !result.success) {
        dashResult = { success: false, message: result.error || 'Import failed' };
      }
    } catch (e) {
      dashResult = { success: false, message: getErrorMessage(e) };
    } finally {
      dashImporting = false;
    }
  }
</script>

<div class="flex h-full flex-col overflow-hidden" style="background-color: var(--metro-bg, #1a1a2e);">
  <!-- Header -->
  <div class="flex items-center gap-3 border-b px-4 py-3" style="border-color: var(--metro-border, #333);">
    <IconInfoCircle size={20} style="color: var(--metro-blue, #0078D7);" />
    <h1 class="text-lg font-semibold" style="color: var(--metro-text, #fff);">App Settings</h1>
  </div>

  <div class="flex-1 overflow-y-auto p-4 space-y-6">
    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <!-- CALIBRATION SECTION -->
    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <section class="rounded-lg border p-4 space-y-3" style="border-color: var(--metro-border, #333); background-color: var(--metro-card, #16213e);">
      <div class="flex items-center gap-2">
        <IconFileCode size={18} style="color: var(--metro-blue, #0078D7);" />
        <h2 class="text-sm font-semibold uppercase tracking-wider" style="color: var(--metro-text-secondary, #A0A0A0);">Calibration (.mecal)</h2>
      </div>

      {#if ecuInfo}
        <div class="rounded p-2 text-xs" style="background-color: var(--metro-bg, #1a1a2e); color: var(--metro-text-secondary, #A0A0A0);">
          {ecuInfo.product} / {ecuInfo.model} v{ecuInfo.version}
        </div>
      {:else if !connected}
        <div class="text-xs" style="color: var(--metro-text-muted, #666);">ECU not connected</div>
      {/if}

      <div class="flex gap-2">
        <button
          class="flex items-center gap-2 rounded px-3 py-2 text-sm font-medium transition-colors"
          style="background-color: {connected ? 'var(--metro-blue, #0078D7)' : 'var(--metro-bg-hover, #2a2a4a)'}; color: {connected ? '#fff' : 'var(--metro-text-muted, #666)'};"
          disabled={!connected || calExporting}
          onclick={handleCalExport}
        >
          {#if calExporting}
            <IconLoader2 size={14} class="animate-spin" />
            Exporting...
          {:else}
            <IconUpload size={14} />
            Export .mecal
          {/if}
        </button>

        <button
          class="flex items-center gap-2 rounded px-3 py-2 text-sm font-medium transition-colors"
          style="background-color: {connected ? 'var(--metro-green, #107C10)' : 'var(--metro-bg-hover, #2a2a4a)'}; color: {connected ? '#fff' : 'var(--metro-text-muted, #666)'};"
          disabled={!connected || calImporting}
          onclick={handleCalImport}
        >
          {#if calImporting}
            <IconLoader2 size={14} class="animate-spin" />
            Importing...
          {:else}
            <IconDownload size={14} />
            Import .mecal
          {/if}
        </button>
      </div>

      {#if calResult}
        <div class="flex items-start gap-2 rounded p-2 text-xs" style="background-color: {calResult.success ? 'rgba(16,124,16,0.15)' : 'rgba(232,17,35,0.15)'}; color: {calResult.success ? '#4ade80' : '#f87171'};">
          {#if calResult.success}
            <IconCheck size={14} class="mt-0.5 shrink-0" />
          {:else}
            <IconX size={14} class="mt-0.5 shrink-0" />
          {/if}
          <span>{calResult.message}</span>
        </div>
      {/if}
    </section>

    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <!-- DASHBOARD PACKAGES SECTION -->
    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <section class="rounded-lg border p-4 space-y-3" style="border-color: var(--metro-border, #333); background-color: var(--metro-card, #16213e);">
      <div class="flex items-center gap-2">
        <IconPackage size={18} style="color: var(--metro-purple, #6B2C91);" />
        <h2 class="text-sm font-semibold uppercase tracking-wider" style="color: var(--metro-text-secondary, #A0A0A0);">Dashboard Packages (.mez)</h2>
      </div>

      {#if dashNames.length > 1}
        <label class="flex flex-col gap-1">
          <span class="text-xs" style="color: var(--metro-text-secondary, #A0A0A0);">Select Dashboard</span>
          <select
            class="rounded border px-2 py-1.5 text-sm"
            style="border-color: var(--metro-border, #333); background-color: var(--metro-bg, #1a1a2e); color: var(--metro-text, #fff);"
            bind:value={selectedDashName}
          >
            {#each dashNames as name (name)}
              <option value={name}>{name}</option>
            {/each}
          </select>
        </label>
      {/if}

      <div class="flex gap-2">
        <button
          class="flex items-center gap-2 rounded px-3 py-2 text-sm font-medium transition-colors"
          style="background-color: var(--metro-purple, #6B2C91); color: #fff;"
          disabled={dashExporting || !selectedDashName}
          onclick={handleDashExport}
        >
          {#if dashExporting}
            <IconLoader2 size={14} class="animate-spin" />
            Exporting...
          {:else}
            <IconUpload size={14} />
            Export Dashboard
          {/if}
        </button>

        <button
          class="flex items-center gap-2 rounded px-3 py-2 text-sm font-medium transition-colors"
          style="background-color: var(--metro-purple, #6B2C91); color: #fff;"
          disabled={dashImporting}
          onclick={handleDashImport}
        >
          {#if dashImporting}
            <IconLoader2 size={14} class="animate-spin" />
            Importing...
          {:else}
            <IconDownload size={14} />
            Import Dashboard
          {/if}
        </button>
      </div>

      {#if dashResult}
        <div class="flex items-start gap-2 rounded p-2 text-xs" style="background-color: {dashResult.success ? 'rgba(16,124,16,0.15)' : 'rgba(232,17,35,0.15)'}; color: {dashResult.success ? '#4ade80' : '#f87171'};">
          {#if dashResult.success}
            <IconCheck size={14} class="mt-0.5 shrink-0" />
          {:else}
            <IconX size={14} class="mt-0.5 shrink-0" />
          {/if}
          <span>{dashResult.message}</span>
        </div>
      {/if}
    </section>

    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <!-- VEHICLE CONFIGURATION POINTER (R8) -->
    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <section class="rounded-lg border p-4 space-y-3" style="border-color: var(--metro-border, #333); background-color: var(--metro-card, #16213e);">
      <div class="flex items-center justify-between gap-3">
        <div class="flex items-center gap-2">
          <IconCar size={18} style="color: var(--metro-green, #107C10);" />
          <div>
            <h2 class="text-sm font-semibold uppercase tracking-wider" style="color: var(--metro-text-secondary, #A0A0A0);">Vehicle Configuration</h2>
            <p class="text-xs" style="color: var(--metro-text-muted, #666);">Gear ratios, tire, final drive, and sensor mapping are now per-dashboard.</p>
          </div>
        </div>
        <button
          class="flex items-center gap-1 rounded px-3 py-2 text-sm font-medium transition-colors"
          style="background-color: var(--metro-green, #107C10); color: #fff;"
          onclick={() => onNavigate('config')}
        >
          Configure
          <IconArrowRight size={14} />
        </button>
      </div>
    </section>

    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <!-- COMPANION VIEWERS SECTION (U11) -->
    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <section class="rounded-lg border p-4 space-y-3" style="border-color: var(--metro-border, #333); background-color: var(--metro-card, #16213e);">
      <div class="flex items-center gap-2">
        <IconDeviceTv size={18} style="color: var(--metro-teal, #00A5A5);" />
        <h2 class="text-sm font-semibold uppercase tracking-wider" style="color: var(--metro-text-secondary, #A0A0A0);">Companion viewers</h2>
      </div>

      <div class="flex gap-2">
        <span class="rounded px-2 py-1 text-[9px] font-bold uppercase tracking-wider" style="background-color: var(--metro-teal, #00A5A5); color: #fff;">
          {companionServer?.viewerCount ?? 0} Viewers
        </span>
        <span class="rounded px-2 py-1 text-[9px] font-bold uppercase tracking-wider" style="background-color: var(--metro-orange, #D83B01); color: #fff;">
          {companionServer?.pendingCount ?? 0} Pending
        </span>
        {#if companionServer && companionServer.success && !companionServer.running}
          <span class="rounded px-2 py-1 text-[9px] font-bold uppercase tracking-wider" style="background-color: var(--metro-border, #333); color: var(--metro-text-muted, #666);">
            Server Offline
          </span>
        {/if}
      </div>

      <label class="flex flex-col gap-1">
        <span class="text-xs" style="color: var(--metro-text-secondary, #A0A0A0);">Device name (shown to companions)</span>
        <div class="flex gap-2">
          <input
            class="min-w-0 flex-1 rounded border px-3 py-2 text-sm font-mono"
            style="border-color: var(--metro-border, #333); background-color: var(--metro-bg, #1a1a2e); color: var(--metro-text, #fff);"
            bind:value={companionName}
            placeholder="Device name"
          />
          <button
            class="flex items-center gap-2 rounded px-3 py-2 text-sm font-medium transition-colors"
            style="background-color: var(--metro-teal, #00A5A5); color: #fff;"
            disabled={renaming || !companionName.trim()}
            onclick={handleRenameDevice}
          >
            {#if renaming}
              <IconLoader2 size={14} class="animate-spin" />
              Saving...
            {:else}
              <IconCheck size={14} />
              Save
            {/if}
          </button>
        </div>
      </label>

      <div>
        <span class="text-xs" style="color: var(--metro-text-secondary, #A0A0A0);">Approved viewers</span>
        {#if companionDevices.length === 0}
          <div class="mt-1 rounded p-2 text-xs" style="background-color: var(--metro-bg, #1a1a2e); color: var(--metro-text-muted, #666);">
            {companionLoaded ? 'No approved viewers yet' : 'Companion features require the native app'}
          </div>
        {:else}
          <ul class="mt-1 divide-y" style="border-color: var(--metro-border, #333);">
            {#each companionDevices as device (device.clientId)}
              <li class="flex items-center gap-3 py-2">
                <div class="min-w-0 flex-1">
                  <div class="truncate text-sm" style="color: var(--metro-text, #fff);">{device.deviceName}</div>
                  <div class="text-[10px]" style="color: var(--metro-text-muted, #666);">Approved {formatApprovedAt(device.approvedAtUtc)}</div>
                </div>
                <button
                  class="flex items-center gap-1 rounded border px-3 py-1.5 text-xs font-medium transition-colors"
                  style="border-color: var(--metro-red, #E81123); background-color: {confirmingRevokeId === device.clientId ? 'var(--metro-red, #E81123)' : 'transparent'}; color: {confirmingRevokeId === device.clientId ? '#fff' : 'var(--metro-red, #E81123)'};"
                  onclick={() => armRevoke(device.clientId)}
                >
                  {confirmingRevokeId === device.clientId ? 'Confirm revoke?' : 'Revoke'}
                </button>
              </li>
            {/each}
          </ul>
        {/if}
      </div>

      <div class="flex flex-col gap-1">
        <div>
          <button
            class="flex items-center gap-2 rounded border px-3 py-2 text-sm font-medium transition-colors"
            style="border-color: var(--metro-red, #E81123); background-color: {confirmingReset ? 'var(--metro-red, #E81123)' : 'transparent'}; color: {confirmingReset ? '#fff' : 'var(--metro-red, #E81123)'};"
            onclick={armResetPairing}
          >
            {confirmingReset ? 'Confirm reset?' : 'Reset all pairings'}
          </button>
        </div>
        <p class="text-[11px]" style="color: var(--metro-text-muted, #666);">
          Approval binds to this device's credential; resetting invalidates every approved device and regenerates the host certificate.
        </p>
      </div>

      {#if companionResult}
        <div class="flex items-start gap-2 rounded p-2 text-xs" style="background-color: {companionResult.success ? 'rgba(16,124,16,0.15)' : 'rgba(232,17,35,0.15)'}; color: {companionResult.success ? '#4ade80' : '#f87171'};">
          {#if companionResult.success}
            <IconCheck size={14} class="mt-0.5 shrink-0" />
          {:else}
            <IconX size={14} class="mt-0.5 shrink-0" />
          {/if}
          <span>{companionResult.message}</span>
        </div>
      {/if}
    </section>
  </div>
</div>

<MecalImportPreview
  bind:open={mecalPreviewOpen}
  fileContent={mecalFileContent}
  onApply={handleMecalApplied}
  onCancel={handleMecalCancel}
/>

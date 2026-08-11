<script lang="ts">
  import { onMount } from 'svelte';
  import { HybridBridge } from '../lib/HybridBridge';
  import type { EcuInfoResult } from '../lib/HybridBridgeTypes';
  import { IconDownload, IconUpload, IconCar, IconPackage, IconFileCode, IconCheck, IconX, IconLoader2, IconInfoCircle, IconArrowRight } from '@tabler/icons-svelte';
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

  function getErrorMessage(e: unknown): string {
    return e instanceof Error ? e.message : String(e);
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
  </div>
</div>

<MecalImportPreview
  bind:open={mecalPreviewOpen}
  fileContent={mecalFileContent}
  onApply={handleMecalApplied}
  onCancel={handleMecalCancel}
/>

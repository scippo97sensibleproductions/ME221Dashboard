<script lang="ts">
  import { onMount } from 'svelte';
  import { HybridBridge } from '../lib/HybridBridge';
  import { IconDownload, IconUpload, IconCar, IconPackage, IconFileCode, IconCheck, IconX, IconLoader2, IconInfoCircle } from '@tabler/icons-svelte';

  let connected = $state(false);
  let ecuInfo = $state<{ product: string; model: string; version: string } | null>(null);

  // ── Calibration state ──
  let calExporting = $state(false);
  let calImporting = $state(false);
  let calImportSummary = $state<any>(null);
  let calImportConfirming = $state(false);
  let calResult = $state<{ success: boolean; message: string } | null>(null);

  // ── Dashboard package state ──
  let dashExporting = $state(false);
  let dashImporting = $state(false);
  let dashResult = $state<{ success: boolean; message: string } | null>(null);
  let dashNames = $state<string[]>([]);
  let selectedDashName = $state('');

  // ── Vehicle config state ──
  let vehicleConfig = $state({
    enabled: true,
    tireDiameterInches: 23,
    finalDriveRatio: 4.3,
    gearRatios: [3.0, 2.0, 1.5, 1.1, 0.85, 0.7],
    wheelSlipPercent: 3,
    rpmEntityId: null as number | null,
    vssSpeedEntityId: null as number | null,
    mapEntityId: null as number | null,
    baroEntityId: null as number | null,
    gearEntityId: null as number | null,
  });
  let vehicleSaving = $state(false);
  let vehicleResult = $state<{ success: boolean; message: string } | null>(null);

  onMount(async () => {
    try {
      const connInfo = await HybridBridge.getConnectionState();
      connected = connInfo.state === 'Connected';

      if (connected) {
        const info = await HybridBridge.getEcuInfo();
        if (info.success) ecuInfo = info;
      }

      const vc = await HybridBridge.getVehicleConfig();
      if (vc) vehicleConfig = { ...vehicleConfig, ...vc };

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
    } catch (e: any) {
      calResult = { success: false, message: e.message };
    } finally {
      calExporting = false;
    }
  }

  // ── Calibration Import ──
  async function handleCalImport() {
    calImporting = true;
    calResult = null;
    calImportSummary = null;
    calImportConfirming = false;
    try {
      const result = await HybridBridge.importMecal();
      if (result.picked && result.success) {
        calResult = { success: true, message: `Imported: ${result.tablesWritten} tables, ${result.driversWritten} drivers written${result.tablesFailed ? ', ' + result.tablesFailed + ' tables failed' : ''}${result.driversFailed ? ', ' + result.driversFailed + ' drivers failed' : ''}` };
      } else if (result.picked && !result.success) {
        calResult = { success: false, message: result.error || 'Import failed' };
      }
    } catch (e: any) {
      calResult = { success: false, message: e.message };
    } finally {
      calImporting = false;
    }
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
    } catch (e: any) {
      dashResult = { success: false, message: e.message };
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
    } catch (e: any) {
      dashResult = { success: false, message: e.message };
    } finally {
      dashImporting = false;
    }
  }

  // ── Vehicle Config Save ──
  async function handleVehicleSave() {
    vehicleSaving = true;
    vehicleResult = null;
    try {
      await HybridBridge.setVehicleConfig(vehicleConfig);
      vehicleResult = { success: true, message: 'Vehicle config saved' };
    } catch (e: any) {
      vehicleResult = { success: false, message: e.message };
    } finally {
      vehicleSaving = false;
    }
  }

  function addGear() {
    vehicleConfig.gearRatios = [...vehicleConfig.gearRatios, 1.0];
  }

  function removeGear(index: number) {
    vehicleConfig.gearRatios = vehicleConfig.gearRatios.filter((_, i) => i !== index);
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
            {#each dashNames as name}
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
    <!-- VEHICLE CONFIGURATION SECTION -->
    <!-- ═══════════════════════════════════════════════════════════════════ -->
    <section class="rounded-lg border p-4 space-y-3" style="border-color: var(--metro-border, #333); background-color: var(--metro-card, #16213e);">
      <div class="flex items-center gap-2">
        <IconCar size={18} style="color: var(--metro-green, #107C10);" />
        <h2 class="text-sm font-semibold uppercase tracking-wider" style="color: var(--metro-text-secondary, #A0A0A0);">Vehicle Configuration</h2>
      </div>

      <div class="grid grid-cols-2 gap-3">
        <label class="flex flex-col gap-1">
          <span class="text-xs" style="color: var(--metro-text-secondary, #A0A0A0);">Tire Diameter (inches)</span>
          <input
            type="number"
            step="0.1"
            class="rounded border px-2 py-1.5 text-sm"
            style="border-color: var(--metro-border, #333); background-color: var(--metro-bg, #1a1a2e); color: var(--metro-text, #fff);"
            bind:value={vehicleConfig.tireDiameterInches}
          />
        </label>

        <label class="flex flex-col gap-1">
          <span class="text-xs" style="color: var(--metro-text-secondary, #A0A0A0);">Final Drive Ratio</span>
          <input
            type="number"
            step="0.01"
            class="rounded border px-2 py-1.5 text-sm"
            style="border-color: var(--metro-border, #333); background-color: var(--metro-bg, #1a1a2e); color: var(--metro-text, #fff);"
            bind:value={vehicleConfig.finalDriveRatio}
          />
        </label>

        <label class="flex flex-col gap-1">
          <span class="text-xs" style="color: var(--metro-text-secondary, #A0A0A0);">Wheel Slip (%)</span>
          <input
            type="number"
            step="0.1"
            class="rounded border px-2 py-1.5 text-sm"
            style="border-color: var(--metro-border, #333); background-color: var(--metro-bg, #1a1a2e); color: var(--metro-text, #fff);"
            bind:value={vehicleConfig.wheelSlipPercent}
          />
        </label>
      </div>

      <!-- Gear Ratios -->
      <div class="space-y-2">
        <div class="flex items-center justify-between">
          <span class="text-xs" style="color: var(--metro-text-secondary, #A0A0A0);">Gear Ratios</span>
          <button
            class="rounded px-2 py-0.5 text-xs transition-colors"
            style="background-color: var(--metro-bg, #1a1a2e); color: var(--metro-blue, #0078D7);"
            onclick={addGear}
          >+ Add Gear</button>
        </div>
        <div class="flex flex-wrap gap-2">
          {#each vehicleConfig.gearRatios as ratio, i}
            <div class="flex items-center gap-1">
              <span class="text-[10px]" style="color: var(--metro-text-muted, #666);">G{i + 1}</span>
              <input
                type="number"
                step="0.01"
                class="w-16 rounded border px-1.5 py-1 text-xs"
                style="border-color: var(--metro-border, #333); background-color: var(--metro-bg, #1a1a2e); color: var(--metro-text, #fff);"
                bind:value={vehicleConfig.gearRatios[i]}
              />
              {#if vehicleConfig.gearRatios.length > 1}
                <button
                  class="p-0.5 transition-colors"
                  style="color: var(--metro-text-muted, #666);"
                  onclick={() => removeGear(i)}
                >
                  <IconX size={10} />
                </button>
              {/if}
            </div>
          {/each}
        </div>
      </div>

      <button
        class="flex items-center gap-2 rounded px-3 py-2 text-sm font-medium transition-colors"
        style="background-color: var(--metro-green, #107C10); color: #fff;"
        disabled={vehicleSaving}
        onclick={handleVehicleSave}
      >
        {#if vehicleSaving}
          <IconLoader2 size={14} class="animate-spin" />
          Saving...
        {:else}
          <IconCheck size={14} />
          Save Vehicle Config
        {/if}
      </button>

      {#if vehicleResult}
        <div class="flex items-start gap-2 rounded p-2 text-xs" style="background-color: {vehicleResult.success ? 'rgba(16,124,16,0.15)' : 'rgba(232,17,35,0.15)'}; color: {vehicleResult.success ? '#4ade80' : '#f87171'};">
          {#if vehicleResult.success}
            <IconCheck size={14} class="mt-0.5 shrink-0" />
          {:else}
            <IconX size={14} class="mt-0.5 shrink-0" />
          {/if}
          <span>{vehicleResult.message}</span>
        </div>
      {/if}
    </section>
  </div>
</div>

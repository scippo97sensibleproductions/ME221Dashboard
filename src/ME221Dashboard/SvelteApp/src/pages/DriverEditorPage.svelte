<script lang="ts">
  import { onMount } from 'svelte';
  import { IconChevronLeft, IconDeviceFloppy } from '@tabler/icons-svelte';
  import { HybridBridge } from '../lib/HybridBridge';
  import type { DriverDefinition, DriverParamDefinition, DataLinkDefinition } from '../lib/HybridBridgeTypes';
  import DriverParamRow from '../lib/drivers/DriverParamRow.svelte';
  import DriverCompanionPanel from '../lib/drivers/DriverCompanionPanel.svelte';
  import PrecisionNumberModal from '../lib/drivers/PrecisionNumberModal.svelte';
  import ComboBoxPicker from '../lib/drivers/ComboBoxPicker.svelte';
  import { createDriverUndoRedoState, pushDriverUndo, canDriverUndo, canDriverRedo, driverUndo, driverRedo, nextDriverGroupId, type DriverUndoResult } from '../lib/drivers/driverUndoRedo';
  import { toast } from '../lib/toasts.svelte';

  let { driverId, onNavigate }: {
    driverId: number;
    onNavigate: (page: string, params?: Record<string, unknown>) => void;
  } = $props();

  let driverDef = $state<DriverDefinition | null>(null);
  let configs = $state<number[]>([]);
  let outputLinkIds = $state<number[]>([]);
  let inputLinkIds = $state<number[]>([]);
  let originalConfigs = $state<number[]>([]);
  let originalOutputLinkIds = $state<number[]>([]);
  let originalInputLinkIds = $state<number[]>([]);
  let dataLinks = $state<DataLinkDefinition[]>([]);
  let tableDefinitions = $state<{ id: number; name: string; category: string; rows: number; cols: number; tableType: string }[]>([]);
  let loading = $state(true);
  let saving = $state(false);
  let showConfirmDiscard = $state(false);
  let mounted = false;

  let undoState = $state(createDriverUndoRedoState());

  let isDirty = $derived(
    configs.some((v, i) => v !== originalConfigs[i]) ||
    outputLinkIds.some((v, i) => v !== originalOutputLinkIds[i]) ||
    inputLinkIds.some((v, i) => v !== originalInputLinkIds[i])
  );

  let sections = $derived.by(() => {
    if (!driverDef) return [];
    const map = new Map<string, DriverParamDefinition[]>();
    for (const cfg of driverDef.configs) {
      const key = cfg.sectionName || 'General';
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(cfg);
    }
    return Array.from(map.entries());
  });

  function isParamEnabled(param: DriverParamDefinition): boolean {
    if (!param.viewConstraint) return true;
    const refIndex = param.viewConstraint.paramIndex;
    if (refIndex < 0 || refIndex >= configs.length) return true;
    const refValue = configs[refIndex];
    return param.viewConstraint.acceptedValues.some(v => Math.abs(v - refValue) < 0.001);
  }

  let precisionModalOpen = $state(false);
  let precisionModalTitle = $state('');
  let precisionModalValue = $state(0);
  let precisionModalMin = $state<number | undefined>(undefined);
  let precisionModalMax = $state<number | undefined>(undefined);
  let precisionModalCallback: ((v: number) => void) | null = null;

  let comboBoxOpen = $state(false);
  let comboBoxTitle = $state('');
  let comboBoxOptions = $state<{ id: number; name: string }[]>([]);
  let comboBoxCurrentValue = $state(0);
  let comboBoxCallback: ((v: number) => void) | null = null;

  function openPrecisionModal(title: string, value: number, min?: number, max?: number, callback?: (v: number) => void) {
    precisionModalTitle = title;
    precisionModalValue = value;
    precisionModalMin = min;
    precisionModalMax = max;
    precisionModalCallback = callback || null;
    precisionModalOpen = true;
  }

  function openComboBox(title: string, options: { id: number; name: string }[], currentValue: number, callback?: (v: number) => void) {
    comboBoxTitle = title;
    comboBoxOptions = options;
    comboBoxCurrentValue = currentValue;
    comboBoxCallback = callback || null;
    comboBoxOpen = true;
  }

  function handlePrecisionConfirm(newValue: number) {
    precisionModalOpen = false;
    if (precisionModalCallback) precisionModalCallback(newValue);
  }

  function handleConfigChange(paramIndex: number, newValue: number) {
    const groupId = nextDriverGroupId();
    pushDriverUndo(undoState, [{
      type: 'config',
      index: paramIndex,
      oldVal: configs[paramIndex],
      newVal: newValue,
      groupId,
    }]);
    configs = configs.map((v, i) => i === paramIndex ? newValue : v);
  }

  function handleOutputLinkChange(slotIndex: number, newLinkId: number) {
    const groupId = nextDriverGroupId();
    pushDriverUndo(undoState, [{
      type: 'outputLink',
      index: slotIndex,
      oldVal: outputLinkIds[slotIndex],
      newVal: newLinkId,
      groupId,
    }]);
    outputLinkIds = outputLinkIds.map((v, i) => i === slotIndex ? newLinkId : v);
  }

  function handleInputLinkChange(slotIndex: number, newLinkId: number) {
    const groupId = nextDriverGroupId();
    pushDriverUndo(undoState, [{
      type: 'inputLink',
      index: slotIndex,
      oldVal: inputLinkIds[slotIndex],
      newVal: newLinkId,
      groupId,
    }]);
    inputLinkIds = inputLinkIds.map((v, i) => i === slotIndex ? newLinkId : v);
  }

  function handleUndo() {
    const current: DriverUndoResult = { configs, outputLinkIds, inputLinkIds };
    const result = driverUndo(undoState, current);
    if (result) {
      configs = result.configs;
      outputLinkIds = result.outputLinkIds;
      inputLinkIds = result.inputLinkIds;
    }
  }

  function handleRedo() {
    const current: DriverUndoResult = { configs, outputLinkIds, inputLinkIds };
    const result = driverRedo(undoState, current);
    if (result) {
      configs = result.configs;
      outputLinkIds = result.outputLinkIds;
      inputLinkIds = result.inputLinkIds;
    }
  }

  async function handleSave() {
    if (!isDirty || saving) return;
    saving = true;
    try {
      const result = await HybridBridge.setDriverConfig(driverId, configs, outputLinkIds, inputLinkIds);
      if (result.success) {
        originalConfigs = [...configs];
        originalOutputLinkIds = [...outputLinkIds];
        originalInputLinkIds = [...inputLinkIds];
        toast('Driver saved to ECU', 'success');
      } else {
        toast(result.error || 'Failed to save driver', 'error');
      }
    } catch (e) {
      toast('Failed to save driver', 'error');
    } finally {
      saving = false;
    }
  }

  function handleBack() {
    if (isDirty) {
      showConfirmDiscard = true;
    } else {
      onNavigate('driverList');
    }
  }

  function confirmDiscard() {
    showConfirmDiscard = false;
    onNavigate('driverList');
  }

  function handleKeydown(e: KeyboardEvent) {
    if ((e.ctrlKey || e.metaKey) && e.key === 'z') {
      e.preventDefault();
      if (e.shiftKey) handleRedo();
      else handleUndo();
    } else if ((e.ctrlKey || e.metaKey) && e.key === 'y') {
      e.preventDefault();
      handleRedo();
    }
  }

  onMount(() => {
    const init = async () => {
      mounted = true;
      try {
        const [defsResult, dataResult, linksResult, tablesResult] = await Promise.all([
          HybridBridge.getDriverDefinitions(),
          HybridBridge.readDriverData(driverId),
          HybridBridge.getDataLinks(),
          HybridBridge.getTableDefinitions(),
        ]);
        if (!mounted) return;

        driverDef = defsResult.drivers.find(d => d.id === driverId) || null;
        if (!driverDef) {
          toast('Driver not found', 'error');
          onNavigate('driverList');
          return;
        }

        configs = [...dataResult.configs];
        outputLinkIds = [...dataResult.outputLinkIds];
        while (outputLinkIds.length < driverDef.numberOfOutputs) outputLinkIds.push(0);
        inputLinkIds = [...dataResult.inputLinkIds];
        while (inputLinkIds.length < driverDef.numberOfInputs) inputLinkIds.push(0);
        originalConfigs = [...dataResult.configs];
        originalOutputLinkIds = [...outputLinkIds];
        originalInputLinkIds = [...inputLinkIds];
        dataLinks = linksResult.dataLinks || [];
        tableDefinitions = (tablesResult.tables || []) as unknown as { id: number; name: string; category: string; rows: number; cols: number; tableType: string }[];
      } catch (e) {
        console.error('Failed to load driver data:', e);
        if (mounted) {
          toast('Failed to load driver data', 'error');
          onNavigate('driverList');
        }
      } finally {
        if (mounted) loading = false;
      }
    };
    init();
    return () => { mounted = false; };
  });
</script>

<svelte:window onkeydown={handleKeydown} />

<div class="flex flex-col h-full">
  {#if loading}
    <div class="flex flex-1 items-center justify-center py-12">
      <span class="inline-block h-6 w-6 animate-spin rounded-full border-2 border-gray-500 border-t-emerald-400"></span>
    </div>
  {:else if driverDef}
    <!-- Header bar -->
  <div class="sticky top-0 z-10 flex items-center gap-3 border-b border-gray-700 bg-gray-900 px-3 py-2.5">
    <button
      class="flex h-8 w-8 items-center justify-center rounded-lg text-gray-400 transition-colors hover:bg-gray-700 hover:text-gray-200"
      onclick={handleBack}
    >
      <IconChevronLeft size={18} />
    </button>
    <div class="min-w-0 flex-1">
      <h1 class="text-lg font-bold text-gray-100 truncate">{driverDef.name}</h1>
      <div class="text-xs text-gray-500">{driverDef.category}</div>
    </div>

    <div class="flex items-center gap-2">
      {#if canDriverUndo(undoState)}
        <button
          class="rounded-lg px-2 py-1.5 text-xs text-gray-400 hover:bg-gray-700 hover:text-gray-200"
          onclick={handleUndo}
          title="Undo (Ctrl+Z)"
        >Undo</button>
      {/if}
      {#if canDriverRedo(undoState)}
        <button
          class="rounded-lg px-2 py-1.5 text-xs text-gray-400 hover:bg-gray-700 hover:text-gray-200"
          onclick={handleRedo}
          title="Redo (Ctrl+Y)"
        >Redo</button>
      {/if}
      <button
        class="flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-sm font-medium transition-colors {isDirty && !saving ? 'bg-emerald-600 text-white hover:bg-emerald-500' : 'bg-gray-700 text-gray-400 cursor-not-allowed'}"
        onclick={handleSave}
        disabled={!isDirty || saving}
      >
        <IconDeviceFloppy size={14} />
        {saving ? 'Saving...' : 'Save to ECU'}
      </button>
    </div>
  </div>

  <!-- Two-column layout -->
  <div class="flex flex-col md:flex-row flex-1 min-h-0">
    <!-- Left column: params — compact width, the form doesn't need full space -->
    <div class="overflow-y-auto px-3 py-2 md:w-[420px] md:flex-shrink-0 md:border-r border-gray-700/40">
      {#if sections.length === 0 && driverDef.numberOfConfigs === 0}
        <div class="py-8 text-center text-sm text-gray-500">
          This driver has no configurable parameters.
        </div>
      {:else}
        {#each sections as [sectionName, params]}
          <div class="mb-4">
            <h3 class="mb-1 text-xs font-semibold uppercase tracking-wider text-gray-400 px-3">{sectionName}</h3>
            <div class="rounded-lg border border-gray-700/50 bg-gray-800/30 divide-y divide-gray-700/30">
              {#each params as param}
                {@const paramIndex = driverDef.configs.indexOf(param)}
                {@const configValue = configs[paramIndex] ?? param.value}
                {@const enabled = isParamEnabled(param)}
                <DriverParamRow
                  {param}
                  value={configValue}
                  disabled={!enabled}
                  onValueChange={(v) => handleConfigChange(paramIndex, v)}
                  onOpenPrecisionModal={() => openPrecisionModal(
                    param.displayName || param.name,
                    configValue,
                    param.checkRange ? param.min : undefined,
                    param.checkRange ? param.max : undefined,
                    (v) => handleConfigChange(paramIndex, v)
                  )}
                  onOpenComboBox={() => openComboBox(
                    param.displayName || param.name,
                    param.options || [],
                    Math.round(configValue),
                    (v) => handleConfigChange(paramIndex, v)
                  )}
                />
              {/each}
            </div>
          </div>
        {/each}
      {/if}
    </div>

    <!-- Right column: companion panel — gets remaining space -->
    <div class="w-full md:flex-1 md:overflow-hidden md:h-full">
      <DriverCompanionPanel
        driverDefinition={driverDef}
        {outputLinkIds}
        {inputLinkIds}
        {dataLinks}
        {tableDefinitions}
        onOutputLinkChange={handleOutputLinkChange}
        onInputLinkChange={handleInputLinkChange}
        onNavigate={onNavigate}
      />
    </div>
  </div>
  {/if}
</div>

<PrecisionNumberModal
  open={precisionModalOpen}
  title={precisionModalTitle}
  value={precisionModalValue}
  min={precisionModalMin}
  max={precisionModalMax}
  onClose={() => { precisionModalOpen = false; }}
  onConfirm={handlePrecisionConfirm}
/>

<ComboBoxPicker
  open={comboBoxOpen}
  title={comboBoxTitle}
  options={comboBoxOptions}
  currentValue={comboBoxCurrentValue}
  onSelect={(v) => { comboBoxOpen = false; if (comboBoxCallback) comboBoxCallback(v); }}
  onClose={() => { comboBoxOpen = false; }}
/>

{#if showConfirmDiscard}
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" role="dialog" aria-modal="true" tabindex="-1" onclick={() => { showConfirmDiscard = false; }} onkeydown={(e) => { if (e.key === 'Escape') showConfirmDiscard = false; }}>
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <div class="w-80 rounded-xl border border-gray-700 bg-gray-800 p-4 shadow-2xl" onclick={(e) => e.stopPropagation()} onkeydown={(e) => e.stopPropagation()}>
      <h3 class="mb-2 text-sm font-semibold text-gray-100">Unsaved Changes</h3>
      <p class="mb-4 text-sm text-gray-400">You have unsaved changes. Discard them?</p>
      <div class="flex gap-2">
        <button
          class="flex-1 rounded-lg border border-gray-600 bg-gray-700 px-3 py-2 text-sm text-gray-300 hover:bg-gray-600"
          onclick={() => { showConfirmDiscard = false; }}
        >Cancel</button>
        <button
          class="flex-1 rounded-lg bg-red-600 px-3 py-2 text-sm font-medium text-white hover:bg-red-500"
          onclick={confirmDiscard}
        >Discard</button>
      </div>
    </div>
  </div>
{/if}

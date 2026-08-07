<script lang="ts">
  import { onMount } from 'svelte';
  import { IconChevronLeft, IconDeviceFloppy, IconArrowBackUp, IconArrowForwardUp, IconChevronDown, IconX, IconAdjustments } from '@tabler/icons-svelte';
  import { HybridBridge } from '../lib/HybridBridge';
  import type { DriverDefinition, DriverParamDefinition, DataLinkDefinition } from '../lib/HybridBridgeTypes';
  import type { TableDefinition } from '../lib/tables/types';
  import DriverParamRow from '../lib/drivers/DriverParamRow.svelte';
  import DataLinkRow from '../lib/drivers/DataLinkRow.svelte';
  import TableWidget from '../lib/tables/TableWidget.svelte';
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
  let tableDefinitions = $state<TableDefinition[]>([]);
  let loading = $state(true);
  let saving = $state(false);
  let showConfirmDiscard = $state(false);
  let showResetConfirm = $state(false);
  let relatedOpen = $state<'tables' | 'links' | null>('tables');
  let mounted = false;

  let undoState = $state(createDriverUndoRedoState());

  const isDirty = $derived(
    configs.some((v, i) => v !== originalConfigs[i]) ||
    outputLinkIds.some((v, i) => v !== originalOutputLinkIds[i]) ||
    inputLinkIds.some((v, i) => v !== originalInputLinkIds[i])
  );

  const sections = $derived.by(() => {
    if (!driverDef) return [];
    const result: [string, DriverParamDefinition[]][] = [];
    for (const cfg of driverDef.configs) {
      const key = cfg.sectionName || 'General';
      const existing = result.find(([k]) => k === key);
      if (existing) existing[1].push(cfg);
      else result.push([key, [cfg]]);
    }
    return result;
  });

  function isParamEnabled(param: DriverParamDefinition): boolean {
    if (!param.viewConstraint) return true;
    const refIndex = param.viewConstraint.paramIndex;
    if (refIndex < 0 || refIndex >= configs.length) return true;
    const refValue = configs[refIndex];
    return param.viewConstraint.acceptedValues.some(v => Math.abs(v - refValue) < 0.001);
  }

  function constraintReason(param: DriverParamDefinition): string {
    if (!param.viewConstraint || !driverDef) return '';
    const refIndex = param.viewConstraint.paramIndex;
    if (refIndex < 0 || refIndex >= driverDef.configs.length) return '';
    const refParam = driverDef.configs[refIndex];
    const refName = refParam.displayName || refParam.name;
    const refLabel = refParam.paramType === 'ComboBox'
      ? (param.viewConstraint.acceptedValues
          .map(v => (refParam.options || []).find(o => o.id === Math.round(v))?.name ?? formatValue(v))
          .join(' / '))
      : param.viewConstraint.acceptedValues.map(formatValue).join(' / ');
    return `Enabled when ${refName} = ${refLabel}`;
  }

  function formatValue(v: number): string {
    return Number.isInteger(v) ? v.toString() : parseFloat(v.toFixed(3)).toString();
  }

  function isParamDirty(index: number): boolean {
    return index >= 0 && index < configs.length && configs[index] !== originalConfigs[index];
  }

  const pendingResetParams = $derived.by(() => {
    if (!driverDef) return [];
    return driverDef.configs.filter((p, i) => p.requiresReset && isParamDirty(i));
  });

  const linkAssignments = $derived.by(() => {
    const map: Record<number, string> = {};
    const def = driverDef;
    if (def) {
      inputLinkIds.forEach((id, i) => {
        if (id !== 0 && map[id] === undefined) map[id] = def.inputNames[i] || `Input ${i + 1}`;
      });
      outputLinkIds.forEach((id, i) => {
        if (id !== 0 && map[id] === undefined) map[id] = def.outputNames[i] || `Output ${i + 1}`;
      });
    }
    return map;
  });

  function duplicateOf(linkId: number, selfLabel: string): string {
    if (linkId === 0) return '';
    const first = linkAssignments[linkId];
    return first && first !== selfLabel ? first : '';
  }

  const relatedTables = $derived(
    tableDefinitions.filter(t => t.category === (driverDef?.category ?? ''))
  );
  const relatedLinks = $derived(
    dataLinks.filter(l => l.category === (driverDef?.category ?? ''))
  );

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

  async function doSave() {
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
    } catch {
      toast('Failed to save driver', 'error');
    } finally {
      saving = false;
    }
  }

  function handleSave() {
    if (!isDirty || saving) return;
    if (pendingResetParams.length > 0) {
      showResetConfirm = true;
    } else {
      doSave();
    }
  }

  function handleBack() {
    if (isDirty) {
      showConfirmDiscard = true;
    } else {
      onNavigate('driverList');
    }
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
        originalConfigs = [...configs];
        originalOutputLinkIds = [...outputLinkIds];
        originalInputLinkIds = [...inputLinkIds];
        dataLinks = linksResult.dataLinks || [];
        tableDefinitions = (tablesResult.tables as TableDefinition[]) || [];
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

<div class="flex h-full flex-col">
  {#if loading}
    <div class="flex flex-1 items-center justify-center py-12">
      <span class="inline-block h-6 w-6 animate-spin rounded-full border-2 border-gray-500 border-t-emerald-400"></span>
    </div>
  {:else if driverDef}
    <!-- Header bar -->
    <div class="sticky top-0 z-10 flex items-center gap-2 border-b border-metro-border bg-metro-bg px-2 py-2">
      <button
        class="flex h-8 w-8 shrink-0 items-center justify-center text-metro-text-secondary transition-colors hover:bg-metro-card-hover hover:text-metro-text"
        onclick={handleBack}
        aria-label="Back to drivers"
      >
        <IconChevronLeft size={18} />
      </button>
      <div class="flex h-8 w-8 shrink-0 items-center justify-center bg-emerald-500">
        <IconAdjustments size={18} class="text-white" />
      </div>
      <div class="min-w-0 flex-1">
        <h1 class="truncate text-[13px] font-bold uppercase tracking-wider text-metro-text">{driverDef.name}</h1>
        <div class="truncate text-[10px] uppercase tracking-wider text-emerald-400">{driverDef.category}</div>
      </div>

      <div class="flex shrink-0 items-center gap-1">
        <button
          class="flex h-8 w-8 items-center justify-center text-metro-text-secondary transition-colors hover:bg-metro-card-hover hover:text-metro-text disabled:opacity-30 disabled:hover:bg-transparent"
          onclick={handleUndo}
          disabled={!canDriverUndo(undoState)}
          title="Undo (Ctrl+Z)"
          aria-label="Undo"
        >
          <IconArrowBackUp size={16} />
        </button>
        <button
          class="flex h-8 w-8 items-center justify-center text-metro-text-secondary transition-colors hover:bg-metro-card-hover hover:text-metro-text disabled:opacity-30 disabled:hover:bg-transparent"
          onclick={handleRedo}
          disabled={!canDriverRedo(undoState)}
          title="Redo (Ctrl+Y)"
          aria-label="Redo"
        >
          <IconArrowForwardUp size={16} />
        </button>
        {#if isDirty}
          <span class="bg-yellow-400 px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wider text-black">Unsaved</span>
        {/if}
        <button
          class="flex h-8 items-center gap-1.5 px-3 text-[11px] font-bold uppercase tracking-wider transition-colors
            {isDirty && !saving ? 'text-white' : 'border border-metro-input-border bg-metro-input-bg text-metro-text-muted'}"
          style={isDirty && !saving ? 'background: linear-gradient(180deg, #107C10 0%, #0C5E0C 100%);' : ''}
          onclick={handleSave}
          disabled={!isDirty || saving}
        >
          <IconDeviceFloppy size={13} />
          {saving ? 'Saving…' : 'Save to ECU'}
        </button>
      </div>
    </div>

    <!-- Sheet -->
    <div class="min-h-0 flex-1 overflow-y-auto">
      <div class="mx-auto max-w-3xl p-3 pb-8">
        {#if sections.length === 0 && driverDef.numberOfConfigs === 0}
          <div class="border border-metro-border bg-metro-card px-4 py-8 text-center text-[13px] text-metro-text-muted">
            This driver has no configurable parameters.
          </div>
        {:else}
          {#each sections as [sectionName, params] (sectionName)}
            <div class="mb-5">
              <h3 class="mb-1.5 flex items-center gap-2 border-l-4 border-l-emerald-500 pl-2.5 text-[12px] font-extrabold uppercase tracking-wider text-metro-text">
                <span class="truncate">{sectionName}</span>
                <span class="shrink-0 bg-emerald-500 px-1.5 py-px text-[9px] font-bold leading-4 text-white">{params.length}</span>
              </h3>
              <div class="border border-metro-border bg-metro-card">
                {#each params as param, i (i)}
                  {@const paramIndex = driverDef.configs.indexOf(param)}
                  {@const configValue = configs[paramIndex] ?? param.value}
                  {@const enabled = isParamEnabled(param)}
                  <DriverParamRow
                    {param}
                    value={configValue}
                    disabled={!enabled}
                    disabledReason={!enabled ? constraintReason(param) : ''}
                    dirty={isParamDirty(paramIndex)}
                    onValueChange={(v) => handleConfigChange(paramIndex, v)}
                  />
                {/each}
              </div>
            </div>
          {/each}
        {/if}

        {#if driverDef.numberOfInputs > 0 || driverDef.numberOfOutputs > 0}
          <div class="mb-5">
            <h3 class="mb-1.5 flex items-center gap-2 border-l-4 border-l-emerald-500 pl-2.5 text-[12px] font-extrabold uppercase tracking-wider text-metro-text">
              <span>Channel Links</span>
              <span class="shrink-0 bg-emerald-500 px-1.5 py-px text-[9px] font-bold leading-4 text-white">
                {driverDef.numberOfInputs + driverDef.numberOfOutputs}
              </span>
            </h3>
            <div class="border border-metro-border bg-metro-card">
              {#if driverDef.numberOfInputs > 0}
                {#if driverDef.editableInputs || driverDef.numberOfInputs > 0}
                  <div class="border-b border-metro-border px-3 py-1.5 text-[10px] font-bold uppercase tracking-wider text-metro-text-muted">
                    {driverDef.editableInputs ? 'Inputs' : 'Inputs (fixed by calibration)'}
                  </div>
                {/if}
                {#each inputLinkIds as linkId, i (i)}
                  {@const slotLabel = driverDef.inputNames[i] || `Input ${i + 1}`}
                  <DataLinkRow
                    slotType="input"
                    slotIndex={i}
                    slotName={slotLabel}
                    currentLinkId={linkId}
                    editable={driverDef.editableInputs}
                    duplicateOf={duplicateOf(linkId, slotLabel)}
                    {dataLinks}
                    onAssign={(newId) => handleInputLinkChange(i, newId)}
                  />
                {/each}
              {/if}
              {#if driverDef.numberOfOutputs > 0}
                <div class="border-b border-metro-border px-3 py-1.5 text-[10px] font-bold uppercase tracking-wider text-metro-text-muted">
                  {driverDef.editableOutputs ? 'Outputs' : 'Outputs (fixed by calibration)'}
                </div>
                {#each outputLinkIds as linkId, i (i)}
                  {@const slotLabel = driverDef.outputNames[i] || `Output ${i + 1}`}
                  <DataLinkRow
                    slotType="output"
                    slotIndex={i}
                    slotName={slotLabel}
                    currentLinkId={linkId}
                    editable={driverDef.editableOutputs}
                    duplicateOf={duplicateOf(linkId, slotLabel)}
                    {dataLinks}
                    onAssign={(newId) => handleOutputLinkChange(i, newId)}
                  />
                {/each}
              {/if}
            </div>
          </div>
        {/if}

        {#if relatedTables.length > 0 || relatedLinks.length > 0}
          <div class="mb-5">
            <h3 class="mb-1.5 flex items-center gap-2 border-l-4 border-l-emerald-500 pl-2.5 text-[12px] font-extrabold uppercase tracking-wider text-metro-text">
              <span>Related</span>
            </h3>
            {#if relatedTables.length > 0}
              <div class="mb-2 border border-metro-border bg-metro-card">
                <button
                  class="flex h-9 w-full items-center justify-between px-3 text-[10px] font-bold uppercase tracking-wider text-metro-text-secondary transition-colors hover:bg-metro-card-hover"
                  onclick={() => relatedOpen = relatedOpen === 'tables' ? null : 'tables'}
                  aria-expanded={relatedOpen === 'tables'}
                >
                  <span>Tables</span>
                  <IconChevronDown size={14} class={relatedOpen === 'tables' ? 'rotate-180' : ''} />
                </button>
                {#if relatedOpen === 'tables'}
                  <div class="flex flex-col gap-2 border-t border-metro-border-subtle p-2">
                    {#each relatedTables as table (table.id)}
                      {@const is1D = table.rows <= 1 || table.tableType.startsWith('T1x')}
                      <div class={is1D ? 'h-16' : 'h-44'}>
                        <TableWidget
                          tableId={table.id}
                          tableName={table.name}
                          onTap={() => onNavigate('tableEditor', { tableId: table.id })}
                          showDimensionBadge={false}
                          maxFontSize={22}
                        />
                      </div>
                    {/each}
                  </div>
                {/if}
              </div>
            {/if}
            {#if relatedLinks.length > 0}
              <div class="border border-metro-border bg-metro-card">
                <button
                  class="flex h-9 w-full items-center justify-between px-3 text-[10px] font-bold uppercase tracking-wider text-metro-text-secondary transition-colors hover:bg-metro-card-hover"
                  onclick={() => relatedOpen = relatedOpen === 'links' ? null : 'links'}
                  aria-expanded={relatedOpen === 'links'}
                >
                  <span>Data Links</span>
                  <IconChevronDown size={14} class={relatedOpen === 'links' ? 'rotate-180' : ''} />
                </button>
                {#if relatedOpen === 'links'}
                  <div class="border-t border-metro-border-subtle">
                    {#each relatedLinks as link (link.id)}
                      <div class="flex items-center justify-between border-b border-metro-border-subtle px-3 py-2 last:border-b-0">
                        <span class="truncate text-[13px] text-metro-text">{link.name}</span>
                        <span class="shrink-0 pl-3 text-[10px] text-metro-text-muted">
                          {link.measureUnit || (link.measurementUnitTypes ? link.measurementUnitTypes.toString() : '')}
                        </span>
                      </div>
                    {/each}
                  </div>
                {/if}
              </div>
            {/if}
          </div>
        {/if}
      </div>
    </div>
  {/if}
</div>

{#if showConfirmDiscard}
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/80" role="button" tabindex="-1" onclick={() => { showConfirmDiscard = false; }} onkeydown={(e) => { if (e.key === 'Escape') showConfirmDiscard = false; }}>
    <div class="w-80 border border-metro-border bg-metro-card shadow-2xl" role="dialog" tabindex="-1" onclick={(e) => e.stopPropagation()} onkeydown={(e) => e.stopPropagation()}>
      <div class="flex items-center justify-between bg-red-600 px-3 py-2.5">
        <h3 class="text-[12px] font-bold uppercase tracking-wider text-white">Unsaved Changes</h3>
        <button class="text-white/80 hover:text-white" onclick={() => { showConfirmDiscard = false; }} aria-label="Close">
          <IconX size={16} />
        </button>
      </div>
      <div class="p-4">
        <p class="mb-4 text-[13px] text-metro-text-secondary">You have unsaved changes. Discard them?</p>
        <div class="flex gap-2">
          <button
            class="metro-btn-secondary flex-1 px-3 py-2 text-[11px] font-bold uppercase tracking-wider"
            onclick={() => { showConfirmDiscard = false; }}
          >Cancel</button>
          <button
            class="metro-btn-danger flex-1 px-3 py-2 text-[11px] font-bold uppercase tracking-wider"
            onclick={() => { showConfirmDiscard = false; onNavigate('driverList'); }}
          >Discard</button>
        </div>
      </div>
    </div>
  </div>
{/if}

{#if showResetConfirm}
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/80" role="button" tabindex="-1" onclick={() => { showResetConfirm = false; }} onkeydown={(e) => { if (e.key === 'Escape') showResetConfirm = false; }}>
    <div class="w-80 border border-metro-border bg-metro-card shadow-2xl" role="dialog" tabindex="-1" onclick={(e) => e.stopPropagation()} onkeydown={(e) => e.stopPropagation()}>
      <div class="flex items-center justify-between bg-yellow-500 px-3 py-2.5">
        <h3 class="text-[12px] font-bold uppercase tracking-wider text-black">Power Cycle Required</h3>
        <button class="text-black/70 hover:text-black" onclick={() => { showResetConfirm = false; }} aria-label="Close">
          <IconX size={16} />
        </button>
      </div>
      <div class="p-4">
        <p class="mb-2 text-[13px] text-metro-text-secondary">These parameters require a power cycle to take effect:</p>
        <ul class="mb-4 space-y-1">
          {#each pendingResetParams as p (p.name)}
            <li class="text-[13px] text-metro-text">{p.displayName || p.name}</li>
          {/each}
        </ul>
        <div class="flex gap-2">
          <button
            class="metro-btn-secondary flex-1 px-3 py-2 text-[11px] font-bold uppercase tracking-wider"
            onclick={() => { showResetConfirm = false; }}
          >Cancel</button>
          <button
            class="metro-btn-primary flex-1 px-3 py-2 text-[11px] font-bold uppercase tracking-wider"
            onclick={() => { showResetConfirm = false; doSave(); }}
          >Save Anyway</button>
        </div>
      </div>
    </div>
  </div>
{/if}

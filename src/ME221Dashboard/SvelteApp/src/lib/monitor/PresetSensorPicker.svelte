<script lang="ts">
  import type { DataLinkDefinition } from '../HybridBridgeTypes';
  import SensorPicker from './SensorPicker.svelte';
  import { presetStore } from './PresetStore.svelte';
  import type { MonitoringPreset } from '../HybridBridge';
  import { onMount } from 'svelte';
  import {
    IconCheck, IconChevronDown, IconBookmarkPlus, IconSettings,
  } from '@tabler/icons-svelte';

  let {
    dataLinks = [],
    selectedIds = $bindable(new Set<number>()),
  }: {
    dataLinks: DataLinkDefinition[];
    selectedIds?: Set<number>;
  } = $props();

  let dropdownOpen = $state(false);
  let presetSearch = $state('');
  let saveMode = $state<'none' | 'quick'>('none');
  let quickSaveName = $state('');
  let quickSaveError = $state<string | null>(null);
  let quickSaveBusy = $state(false);
  let showConfirmLoad = $state<MonitoringPreset | null>(null);
  let confirmLoadError = $state<string | null>(null);

  let showEditor = $state(false);

  let dropdownEl = $state<HTMLDivElement>();

  onMount(() => {
    presetStore.init();
  });

  const filteredPresets = $derived.by(() => {
    let presets = presetStore.sortedPresets;
    if (presetSearch.trim()) {
      const q = presetSearch.toLowerCase();
      presets = presets.filter(p => p.name.toLowerCase().includes(q));
    }
    return presets;
  });

  const activePreset = $derived(
    presetStore.activePresetId
      ? presetStore.getPresetById(presetStore.activePresetId) ?? null
      : null
  );

  function handleClickOutside(e: MouseEvent) {
    if (dropdownEl && !dropdownEl.contains(e.target as Node)) {
      dropdownOpen = false;
      presetSearch = '';
    }
  }

  function toggleDropdown() {
    dropdownOpen = !dropdownOpen;
    if (dropdownOpen) {
      setTimeout(() => document.addEventListener('mousedown', handleClickOutside), 0);
    } else {
      document.removeEventListener('mousedown', handleClickOutside);
      presetSearch = '';
    }
  }

  function requestLoadPreset(preset: MonitoringPreset) {
    if (selectedIds.size > 0) {
      showConfirmLoad = preset;
      confirmLoadError = null;
    } else {
      doLoadPreset(preset);
    }
  }

  function doLoadPreset(preset: MonitoringPreset) {
    const resolved = new Set(preset.datalinkIds);
    selectedIds = resolved;
    presetStore.loadPreset(preset.id);
    dropdownOpen = false;
    presetSearch = '';
    showConfirmLoad = null;
  }

  function cancelLoad() {
    showConfirmLoad = null;
    confirmLoadError = null;
  }

  async function handleQuickSave() {
    const name = quickSaveName.trim();
    if (!name || selectedIds.size === 0) return;

    quickSaveBusy = true;
    quickSaveError = null;

    const result = await presetStore.createPreset(name, Array.from(selectedIds));
    quickSaveBusy = false;

    if (result.success) {
      saveMode = 'none';
      quickSaveName = '';
      if (result.preset) {
        presetStore.loadPreset(result.preset.id);
      }
    } else {
      quickSaveError = result.error ?? 'Failed to save preset';
    }
  }

  function cancelQuickSave() {
    saveMode = 'none';
    quickSaveName = '';
    quickSaveError = null;
  }

  function openQuickSave() {
    quickSaveName = '';
    quickSaveError = null;
    saveMode = 'quick';
  }

  function handleKeyDown(e: KeyboardEvent) {
    if (e.key === 'Escape') {
      if (showConfirmLoad) cancelLoad();
      else if (saveMode === 'quick') cancelQuickSave();
      else if (dropdownOpen) toggleDropdown();
    }
    if (e.key === 'Enter' && saveMode === 'quick') {
      e.preventDefault();
      handleQuickSave();
    }
  }

  $effect(() => {
    if (selectedIds.size > 0 && presetStore.activePresetId) {
      presetStore.clearActivePreset();
    }
  });
</script>

<div class="flex flex-col h-full bg-[#1a1a1a] border-r border-[#333]" role="navigation" aria-label="Sensor presets">
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="contents" onkeydown={handleKeyDown}>
  <div class="relative px-2 py-1.5 border-b border-[#333]" bind:this={dropdownEl}>
    <div class="flex items-center gap-1">
      <button
        class="flex-1 flex items-center justify-between gap-1 px-2 py-1 text-[11px] bg-[#222] border border-[#444] rounded text-white hover:border-sky-500 transition-colors min-w-0"
        onclick={toggleDropdown}
      >
        <span class="truncate">
          {activePreset ? activePreset.name : 'Load preset...'}
        </span>
        <IconChevronDown size={12} class="shrink-0 text-gray-400 {dropdownOpen ? 'rotate-180' : ''}" style="transition: transform 0.15s" />
      </button>
      {#if selectedIds.size > 0 && saveMode === 'none'}
        <button
          class="p-1 text-gray-500 hover:text-sky-400 transition-colors shrink-0"
          title="Save as preset"
          onclick={openQuickSave}
        >
          <IconBookmarkPlus size={14} />
        </button>
      {/if}
      <button
        class="p-1 text-gray-500 hover:text-gray-300 transition-colors shrink-0"
        title="Manage presets"
        onclick={() => { showEditor = true; dropdownOpen = false; }}
      >
        <IconSettings size={14} />
      </button>
    </div>

    {#if dropdownOpen}
      <div class="absolute top-full left-0 right-0 z-40 mt-1 bg-[#1a1a1a] border border-[#333] rounded-lg shadow-2xl max-h-[50vh] flex flex-col overflow-hidden">
        {#if filteredPresets.length > 5 || presetSearch}
          <div class="px-2 py-1.5 border-b border-[#333]">
            <input
              type="text"
              placeholder="Search presets..."
              bind:value={presetSearch}
              class="w-full px-2 py-1 text-[10px] bg-[#222] border border-[#444] rounded text-white placeholder-gray-500 focus:outline-none focus:border-sky-500"
            />
          </div>
        {/if}
        <div class="flex-1 overflow-y-auto">
          {#if filteredPresets.length === 0}
            <div class="px-3 py-3 text-center text-gray-500 text-[10px]">
              {presetStore.presets.length === 0 ? 'No presets saved yet' : 'No matching presets'}
            </div>
          {/if}
          {#each filteredPresets as preset}
            {@const isActive = preset.id === presetStore.activePresetId}
            <button
              class="w-full flex items-center gap-2 px-3 py-1.5 text-left text-[11px] transition-colors
                {isActive ? 'bg-sky-600/10' : 'hover:bg-[#222]'}"
              onclick={() => requestLoadPreset(preset)}
            >
              <span class="w-3 text-center shrink-0">
                {#if isActive}
                  <IconCheck size={12} class="text-sky-400" />
                {/if}
              </span>
              <div class="flex-1 min-w-0">
                <div class="truncate text-white">{preset.name}</div>
                <div class="text-[9px] text-gray-500">{preset.datalinkIds.length} sensors</div>
              </div>
            </button>
          {/each}
        </div>
      </div>
    {/if}
  </div>

  <!-- Quick save form -->
  {#if saveMode === 'quick'}
    <div class="px-2 py-1.5 border-b border-[#333] bg-[#1a1a1a]">
      <div class="flex items-center gap-1">
        <input
          type="text"
          placeholder="Preset name..."
          bind:value={quickSaveName}
          class="flex-1 px-2 py-1 text-[10px] bg-[#222] border border-[#444] rounded text-white placeholder-gray-500 focus:outline-none focus:border-sky-500"
          disabled={quickSaveBusy}
        />
        <button
          class="px-2 py-1 text-[10px] bg-sky-600 text-white rounded hover:bg-sky-500 transition-colors disabled:opacity-50"
          onclick={handleQuickSave}
          disabled={quickSaveBusy || !quickSaveName.trim()}
        >
          {quickSaveBusy ? '...' : 'Save'}
        </button>
        <button
          class="px-1.5 py-1 text-[10px] text-gray-500 hover:text-gray-300 transition-colors"
          onclick={cancelQuickSave}
          disabled={quickSaveBusy}
        >
          Cancel
        </button>
      </div>
      {#if quickSaveError}
        <div class="mt-1 px-2 py-1 bg-red-900/30 border border-red-500/30 rounded text-[9px] text-red-400">
          {quickSaveError}
        </div>
      {/if}
    </div>
  {/if}

  <!-- Confirmation dialog -->
  {#if showConfirmLoad}
    <div class="px-2 py-1.5 border-b border-[#333] bg-[#1a1a1a]">
      <div class="text-[10px] text-gray-400 mb-1">
        Replace current selection with <span class="text-white font-medium">{showConfirmLoad.name}</span>?
      </div>
      {#if confirmLoadError}
        <div class="mb-1 px-2 py-1 bg-red-900/30 border border-red-500/30 rounded text-[9px] text-red-400">
          {confirmLoadError}
        </div>
      {/if}
      <div class="flex items-center gap-1">
        <button
          class="px-2 py-0.5 text-[10px] bg-sky-600 text-white rounded hover:bg-sky-500 transition-colors"
          onclick={() => showConfirmLoad && doLoadPreset(showConfirmLoad)}
        >
          Confirm
        </button>
        <button
          class="px-2 py-0.5 text-[10px] text-gray-500 hover:text-gray-300 transition-colors"
          onclick={cancelLoad}
        >
          Cancel
        </button>
      </div>
    </div>
  {/if}

  <!-- Wrapped SensorPicker -->
  <div class="flex-1 min-h-0">
    <SensorPicker {dataLinks} bind:selectedIds />
  </div>
  </div>
</div>

{#if showEditor}
  {#await import('./PresetEditorModal.svelte') then { default: PresetEditorModal }}
    <PresetEditorModal bind:open={showEditor} {dataLinks} bind:selectedIds />
  {/await}
{/if}

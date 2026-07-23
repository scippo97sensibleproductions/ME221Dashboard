<script lang="ts">
  import type { DataLinkDefinition } from '../HybridBridgeTypes';
  import { presetStore } from './PresetStore.svelte';
  import type { MonitoringPreset } from '../HybridBridge';
  import { IconX, IconPlus, IconTrash } from '@tabler/icons-svelte';

  let {
    open = $bindable(false),
    dataLinks = [],
    selectedIds = $bindable(new Set<number>()),
  }: {
    open: boolean;
    dataLinks: DataLinkDefinition[];
    selectedIds?: Set<number>;
  } = $props();

  type View = 'list' | 'edit' | 'create';
  let view = $state<View>('list');
  let editingPreset = $state<MonitoringPreset | null>(null);

  // Edit/create form state
  let formName = $state('');
  let formDatalinkIds = $state<Set<number>>(new Set());
  let formError = $state<string | null>(null);
  let formBusy = $state(false);
  let deleteConfirmId = $state<string | null>(null);
  let deleteBusy = $state(false);

  // Categories for datalink selection
  const categories = $derived.by(() => {
    const cats = new Set<string>();
    for (const dl of dataLinks) {
      if (dl.category) cats.add(dl.category);
    }
    return ['All', ...Array.from(cats).sort()];
  });

  let activeCategory = $state('All');
  let formSearch = $state('');

  const filteredFormLinks = $derived.by(() => {
    let links = dataLinks;
    if (activeCategory !== 'All') {
      links = links.filter(dl => dl.category === activeCategory);
    }
    if (formSearch.trim()) {
      const q = formSearch.toLowerCase();
      links = links.filter(dl => dl.name.toLowerCase().includes(q));
    }
    return links;
  });

  const sortedPresets = $derived(presetStore.sortedPresets);
  const canSave = $derived(formName.trim().length > 0 && formDatalinkIds.size > 0);

  function openCreate() {
    view = 'create';
    formName = '';
    formDatalinkIds = new Set();
    formError = null;
    activeCategory = 'All';
    formSearch = '';
  }

  function openEdit(preset: MonitoringPreset) {
    view = 'edit';
    editingPreset = preset;
    formName = preset.name;
    formDatalinkIds = new Set(preset.datalinkIds);
    formError = null;
    activeCategory = 'All';
    formSearch = '';
  }

  function closeForm() {
    view = 'list';
    editingPreset = null;
    formName = '';
    formDatalinkIds = new Set();
    formError = null;
  }

  function toggleFormDatalink(id: number) {
    const next = new Set(formDatalinkIds);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    formDatalinkIds = next;
  }

  async function saveForm() {
    const name = formName.trim();
    if (!name || formDatalinkIds.size === 0) return;

    formBusy = true;
    formError = null;

    let result;
    if (view === 'edit' && editingPreset) {
      result = await presetStore.updatePreset(editingPreset.id, name, Array.from(formDatalinkIds));
    } else {
      result = await presetStore.createPreset(name, Array.from(formDatalinkIds));
    }

    formBusy = false;

    if (result.success) {
      closeForm();
    } else {
      formError = result.error ?? 'Failed to save preset';
    }
  }

  async function confirmDelete(id: string) {
    deleteBusy = true;
    const result = await presetStore.deletePreset(id);
    deleteBusy = false;
    if (result.success) {
      deleteConfirmId = null;
    }
  }

  function handleBackdropClick(e: MouseEvent) {
    if (e.target === e.currentTarget) {
      if (view !== 'list') closeForm();
      else open = false;
    }
  }

  function handleKeyDown(e: KeyboardEvent) {
    if (e.key === 'Escape') {
      if (view !== 'list') closeForm();
      else open = false;
    }
  }
</script>

{#if open}
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div
    class="fixed inset-0 z-[90] flex items-center justify-center p-4"
    style="background-color: rgba(0,0,0,0.7);"
    onclick={handleBackdropClick}
    onkeydown={handleKeyDown}
  >
    <div
      class="bg-[#1a1a1a] border border-[#333] rounded-xl shadow-2xl w-full max-w-lg max-h-[80vh] flex flex-col overflow-hidden"
      onclick={(e) => e.stopPropagation()}
      onkeydown={(e) => e.stopPropagation()}
    >
      <!-- Header -->
      <div class="flex items-center justify-between px-4 py-3 border-b border-[#333]">
        <span class="text-sm font-semibold text-white">
          {view === 'list' ? 'Manage Presets' : view === 'create' ? 'New Preset' : 'Edit Preset'}
        </span>
        <button
          class="p-1 text-gray-500 hover:text-gray-300 transition-colors"
          onclick={() => { if (view !== 'list') closeForm(); else open = false; }}
        >
          <IconX size={16} />
        </button>
      </div>

      <!-- Content -->
      <div class="flex-1 overflow-y-auto min-h-0">
        {#if view === 'list'}
          <!-- Preset list -->
          <div class="p-3">
            <button
              class="w-full flex items-center justify-center gap-1.5 px-3 py-2 mb-3 text-[11px] bg-sky-600/10 border border-sky-500/30 rounded text-sky-400 hover:bg-sky-600/20 transition-colors"
              onclick={openCreate}
            >
              <IconPlus size={14} />
              New Preset
            </button>

            {#if sortedPresets.length === 0}
              <div class="text-center text-gray-500 text-[11px] py-6">No presets saved yet</div>
            {:else}
              <div class="space-y-1">
                {#each sortedPresets as preset}
                  {@const isActive = preset.id === presetStore.activePresetId}
                  {#if deleteConfirmId === preset.id}
                    <div class="p-2 bg-red-900/10 border border-red-500/30 rounded">
                      <div class="text-[10px] text-gray-400 mb-1.5">
                        Delete <span class="text-white">{preset.name}</span>?
                      </div>
                      <div class="flex items-center gap-1.5">
                        <button
                          class="px-2 py-0.5 text-[10px] bg-red-600 text-white rounded hover:bg-red-500 transition-colors disabled:opacity-50"
                          onclick={() => confirmDelete(preset.id)}
                          disabled={deleteBusy}
                        >
                          {deleteBusy ? '...' : 'Delete'}
                        </button>
                        <button
                          class="px-2 py-0.5 text-[10px] text-gray-500 hover:text-gray-300 transition-colors"
                          onclick={() => deleteConfirmId = null}
                          disabled={deleteBusy}
                        >
                          Cancel
                        </button>
                      </div>
                    </div>
                  {:else}
                    <div class="flex items-center gap-2 px-2 py-1.5 bg-[#222] rounded hover:bg-[#2a2a2a] transition-colors">
                      <div class="flex-1 min-w-0">
                        <div class="flex items-center gap-1.5">
                          {#if isActive}
                            <span class="text-sky-400 text-[10px]">&#x2713;</span>
                          {/if}
                          <span class="text-[11px] text-white truncate">{preset.name}</span>
                        </div>
                        <span class="text-[9px] text-gray-500">{preset.datalinkIds.length} sensors</span>
                      </div>
                      <button
                        class="p-1 text-gray-500 hover:text-sky-400 transition-colors"
                        title="Edit"
                        onclick={() => openEdit(preset)}
                      >
                        <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"/></svg>
                      </button>
                      <button
                        class="p-1 text-gray-500 hover:text-red-400 transition-colors"
                        title="Delete"
                        onclick={() => deleteConfirmId = preset.id}
                      >
                        <IconTrash size={12} />
                      </button>
                    </div>
                  {/if}
                {/each}
              </div>
            {/if}
          </div>
        {:else}
          <!-- Create/Edit form -->
          <div class="p-3 space-y-3">
            <!-- Name input -->
            <div>
              <label for="preset-name" class="block text-[10px] text-gray-500 uppercase tracking-wider mb-1">Name</label>
              <input
                id="preset-name"
                type="text"
                placeholder="Preset name..."
                bind:value={formName}
                maxlength="50"
                class="w-full px-2 py-1.5 text-[11px] bg-[#222] border border-[#444] rounded text-white placeholder-gray-500 focus:outline-none focus:border-sky-500"
              />
              <div class="text-[9px] text-gray-600 mt-0.5">{formName.length}/50</div>
            </div>

            {#if formError}
              <div class="px-2 py-1.5 bg-red-900/30 border border-red-500/30 rounded text-[10px] text-red-400">
                {formError}
              </div>
            {/if}

            <!-- Datalink search + category filter -->
            <div>
              <label for="preset-sensor-search" class="block text-[10px] text-gray-500 uppercase tracking-wider mb-1">
                Sensors ({formDatalinkIds.size} selected)
              </label>
              <input
                id="preset-sensor-search"
                type="text"
                placeholder="Search sensors..."
                bind:value={formSearch}
                class="w-full px-2 py-1 text-[10px] bg-[#222] border border-[#444] rounded text-white placeholder-gray-500 focus:outline-none focus:border-sky-500 mb-1.5"
              />
              <div class="flex gap-1 overflow-x-auto mb-1.5 scrollbar-thin">
                {#each categories as cat}
                  <button
                    class="px-2 py-0.5 text-[9px] rounded whitespace-nowrap transition-colors
                      {activeCategory === cat ? 'bg-sky-600 text-white' : 'bg-[#2a2a2a] text-gray-400 hover:bg-[#333]'}"
                    onclick={() => activeCategory = cat}
                  >
                    {cat}
                  </button>
                {/each}
              </div>
            </div>

            <!-- Datalink list with checkboxes -->
            <div class="max-h-[30vh] overflow-y-auto border border-[#333] rounded">
              {#if filteredFormLinks.length === 0}
                <div class="p-3 text-center text-gray-500 text-[10px]">No sensors found</div>
              {:else}
                {#each filteredFormLinks as dl}
                  <button
                    class="w-full flex items-center gap-2 px-2 py-1 text-left text-[10px] border-b border-[#2a2a2a] transition-colors
                      {formDatalinkIds.has(dl.id) ? 'bg-sky-600/10' : 'hover:bg-[#222]'}"
                    onclick={() => toggleFormDatalink(dl.id)}
                  >
                    <input
                      type="checkbox"
                      checked={formDatalinkIds.has(dl.id)}
                      class="w-3 h-3 accent-sky-500"
                      tabindex="-1"
                    />
                    <div class="flex-1 min-w-0">
                      <div class="truncate text-white">{dl.name}</div>
                      <div class="text-[9px] text-gray-500">{dl.category}{dl.measureUnit ? ` · ${dl.measureUnit}` : ''}</div>
                    </div>
                  </button>
                {/each}
              {/if}
            </div>

            <!-- Action buttons -->
            <div class="flex items-center gap-2 pt-1">
              <button
                class="flex-1 px-3 py-1.5 text-[11px] bg-sky-600 text-white rounded hover:bg-sky-500 transition-colors disabled:opacity-50"
                onclick={saveForm}
                disabled={formBusy || !canSave}
              >
                {formBusy ? 'Saving...' : view === 'edit' ? 'Update' : 'Create'}
              </button>
              <button
                class="px-3 py-1.5 text-[11px] text-gray-500 hover:text-gray-300 transition-colors"
                onclick={closeForm}
                disabled={formBusy}
              >
                Cancel
              </button>
            </div>
          </div>
        {/if}
      </div>
    </div>
  </div>
{/if}

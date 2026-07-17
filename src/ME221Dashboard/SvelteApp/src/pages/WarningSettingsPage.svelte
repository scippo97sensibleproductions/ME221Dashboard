<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { IconAlertTriangle, IconChevronLeft, IconFilter, IconBookmark, IconBookmarkOff, IconRefresh } from '@tabler/icons-svelte';
  import { HybridBridge } from '../lib/HybridBridge';
  import type { DataLinkWarningSetting } from '../lib/HybridBridgeTypes';

  let { onNavigate }: {
    onNavigate: (page: string, params?: Record<string, unknown>) => void;
  } = $props();

  let settings = $state<DataLinkWarningSetting[]>([]);
  let dataLinks = $state<{ id: number; name: string; category: string; unit: string; minValue: number; maxValue: number }[]>([]);
  let delayMs = $state(500);
  let searchQuery = $state('');
  let selectedCategory = $state<string | null>(null);
  let loading = $state(true);
  let mounted = false;

  let categories = $derived.by(() => {
    const cats = new Set<string>();
    for (const s of settings) {
      if (s.category) cats.add(s.category);
    }
    return Array.from(cats).sort();
  });

  let filteredSettings = $derived.by(() => {
    let result = [...settings];
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      result = result.filter(s => s.name.toLowerCase().includes(q) || s.category.toLowerCase().includes(q) || String(s.dataId).includes(q));
    }
    if (selectedCategory) {
      result = result.filter(s => s.category === selectedCategory);
    }
    result.sort((a, b) => a.name.localeCompare(b.name));
    return result;
  });

  let enabledCount = $derived(settings.filter(s => s.enabled).length);

  function enableAll() {
    settings = settings.map(s => ({ ...s, enabled: true }));
  }

  function disableAll() {
    settings = settings.map(s => ({ ...s, enabled: false, status: 'Disabled' as const }));
  }

  async function applyDefXmlDefaults() {
    try {
      const defaults = await HybridBridge.getDefXmlDefaults();
      const defaultsById = new Map(defaults.map(d => [d.dataId, d]));
      settings = settings.map(s => {
        const def = defaultsById.get(s.dataId);
        if (!def) return s;
        return {
          ...s,
          minWarning: def.minWarning,
          maxWarning: def.maxWarning,
          status: 'Typical' as const,
        };
      });
    } catch (e) {
      console.error('Failed to load DEF XML defaults:', e);
    }
  }

  function updateMinWarning(dataId: number, value: string) {
    const num = value === '' ? null : parseFloat(value);
    settings = settings.map(s => {
      if (s.dataId !== dataId) return s;
      return {
        ...s,
        minWarning: num,
        status: s.status === 'Disabled' ? 'Custom' as const : (num !== null || s.maxWarning !== null) ? 'Custom' as const : 'Disabled' as const,
      };
    });
  }

  function updateMaxWarning(dataId: number, value: string) {
    const num = value === '' ? null : parseFloat(value);
    settings = settings.map(s => {
      if (s.dataId !== dataId) return s;
      return {
        ...s,
        maxWarning: num,
        status: s.status === 'Disabled' ? 'Custom' as const : (num !== null || s.minWarning !== null) ? 'Custom' as const : 'Disabled' as const,
      };
    });
  }

  function toggleEnabled(dataId: number) {
    settings = settings.map(s => {
      if (s.dataId !== dataId) return s;
      const newEnabled = !s.enabled;
      return {
        ...s,
        enabled: newEnabled,
        status: newEnabled ? (s.minWarning !== null || s.maxWarning !== null ? 'Custom' as const : 'Disabled' as const) : 'Disabled' as const,
      };
    });
  }

  function getStatusBadge(status: string): { label: string; class: string } {
    switch (status) {
      case 'Typical': return { label: 'Typical', class: 'bg-blue-500/20 text-blue-300' };
      case 'Custom': return { label: 'Custom', class: 'bg-amber-500/20 text-amber-300' };
      case 'Disabled': return { label: 'Off', class: 'bg-gray-700 text-gray-400' };
      default: return { label: status, class: 'bg-gray-700 text-gray-400' };
    }
  }

  async function loadSettings() {
    try {
      const result = await HybridBridge.getWarningSettings();
      if (Array.isArray(result)) {
        settings = result;
      }
    } catch (e) {
      console.error('Failed to load warning settings:', e);
    }
  }

  async function loadDataLinks() {
    try {
      const result = await HybridBridge.getDataLinks();
      if (result.dataLinks) {
        dataLinks = result.dataLinks.map(dl => ({
          id: dl.id,
          name: dl.name,
          category: dl.category,
          unit: dl.measureUnit,
          minValue: dl.minValue,
          maxValue: dl.maxValue,
        }));
        // Fill in any data links that don't have settings yet (without overwriting existing ones)
        const existingIds = new Set(settings.map(s => s.dataId));
        const missing = dataLinks
          .filter(dl => !existingIds.has(dl.id))
          .map(dl => ({
            dataId: dl.id,
            enabled: false,
            minWarning: null,
            maxWarning: null,
            name: dl.name,
            unit: dl.unit,
            category: dl.category,
            status: 'Disabled' as const,
          }));
        if (missing.length > 0) {
          settings = [...settings, ...missing];
        }
      }
    } catch (e) {
      console.error('Failed to load data links:', e);
    }
  }

  async function saveSettings() {
    try {
      await HybridBridge.saveWarningSettings(settings);
    } catch (e) {
      console.error('Failed to save warning settings:', e);
    }
  }

  // Auto-save on changes
  let saveTimer: ReturnType<typeof setTimeout> | null = null;
  $effect(() => {
    // Track dependencies
    const _s = settings;
    const _d = delayMs;
    if (!mounted) return;
    if (saveTimer) clearTimeout(saveTimer);
    saveTimer = setTimeout(() => {
      saveSettings();
    }, 800);
  });

  onMount(async () => {
    await loadSettings();
    await loadDataLinks();
    loading = false;
    // mounted must be set AFTER data loads so the auto-save effect
    // doesn't fire with empty settings and overwrite persisted config.
    mounted = true;
  });

  onDestroy(() => {
    mounted = false;
    if (saveTimer) {
      clearTimeout(saveTimer);
      saveTimer = null;
    }
    // Always save on unmount to catch any unsaved changes.
    // The bridge call is fire-and-forget; the C# side handles persistence.
    saveSettings();
  });
</script>

<div class="mx-auto max-w-4xl">
  <div class="mb-4 flex items-center gap-3">
    <button
      class="flex h-8 w-8 items-center justify-center rounded-lg text-gray-400 transition-colors hover:bg-gray-700 hover:text-gray-200"
      onclick={() => onNavigate('dashboard')}
    >
      <IconChevronLeft size={18} />
    </button>
    <h1 class="text-xl font-bold text-gray-100">Warning Centre</h1>
    {#if enabledCount > 0}
      <span class="rounded-full bg-amber-500/20 px-2 py-0.5 text-xs font-medium text-amber-300">
        {enabledCount} active
      </span>
    {/if}
  </div>

  <!-- Preset buttons + Delay config -->
  <div class="mb-4 flex flex-wrap items-center gap-2">
    <button
      class="flex items-center gap-1.5 rounded-lg border border-gray-700 bg-gray-800 px-3 py-1.5 text-xs font-medium text-gray-300 transition-colors hover:border-amber-500 hover:text-amber-300"
      onclick={applyDefXmlDefaults}
    >
      <IconBookmark size={14} />
      ECU Defaults
    </button>
    <button
      class="rounded-lg border border-gray-700 bg-gray-800 px-3 py-1.5 text-xs font-medium text-gray-300 transition-colors hover:border-green-500 hover:text-green-300"
      onclick={enableAll}
    >
      Enable All
    </button>
    <button
      class="flex items-center gap-1.5 rounded-lg border border-gray-700 bg-gray-800 px-3 py-1.5 text-xs font-medium text-gray-300 transition-colors hover:border-gray-500 hover:text-gray-300"
      onclick={disableAll}
    >
      <IconBookmarkOff size={14} />
      None
    </button>

    <div class="ml-auto flex items-center gap-2">
      <span class="text-xs text-gray-500">Delay:</span>
      <input
        type="number"
        bind:value={delayMs}
        min="0"
        max="5000"
        step="50"
        class="w-20 rounded-lg border border-gray-700 bg-gray-800 px-2 py-1 text-xs text-gray-200 outline-none focus:border-amber-500 focus:ring-1 focus:ring-amber-500"
      />
      <span class="text-xs text-gray-500">ms</span>
    </div>
  </div>

  <!-- Search + Category chips -->
  <div class="mb-3 flex flex-wrap items-center gap-2">
    <input
      type="text"
      placeholder="Search data links..."
      bind:value={searchQuery}
      class="w-full rounded-lg border border-gray-700 bg-gray-800 py-2 px-3 text-sm text-gray-100 placeholder-gray-500 outline-none focus:border-amber-500 focus:ring-1 focus:ring-amber-500 sm:w-64"
    />
    {#each categories as cat}
      <button
        class="rounded-full px-2.5 py-0.5 text-xs font-medium transition-colors {selectedCategory === cat ? 'bg-amber-500/20 text-amber-300' : 'bg-gray-800 text-gray-400 hover:bg-gray-700 hover:text-gray-200'}"
        onclick={() => { selectedCategory = selectedCategory === cat ? null : cat; }}
      >
        {cat}
      </button>
    {/each}
  </div>

  <!-- Settings table -->
  {#if loading}
    <div class="flex items-center justify-center py-12">
      <span class="inline-block h-6 w-6 animate-spin rounded-full border-2 border-gray-500 border-t-amber-400"></span>
    </div>
  {:else if filteredSettings.length === 0}
    <div class="py-12 text-center text-sm text-gray-500">
      {settings.length === 0 ? 'No data links available. Connect to ECU first.' : 'No data links match your search.'}
    </div>
  {:else}
    <div class="overflow-x-auto rounded-lg border border-gray-700">
      <table class="min-w-full text-sm">
        <thead>
          <tr class="border-b border-gray-700 bg-gray-800/50">
            <th class="sticky left-0 z-10 bg-gray-800/95 px-3 py-2 text-left text-xs font-medium text-gray-400 backdrop-blur-sm">On</th>
            <th class="px-3 py-2 text-left text-xs font-medium text-gray-400">ID</th>
            <th class="px-3 py-2 text-left text-xs font-medium text-gray-400">Name</th>
            <th class="px-3 py-2 text-left text-xs font-medium text-gray-400">Category</th>
            <th class="px-3 py-2 text-left text-xs font-medium text-gray-400">Unit</th>
            <th class="px-3 py-2 text-left text-xs font-medium text-gray-400">Min</th>
            <th class="px-3 py-2 text-left text-xs font-medium text-gray-400">Max</th>
            <th class="px-3 py-2 text-left text-xs font-medium text-gray-400">Status</th>
          </tr>
        </thead>
        <tbody>
          {#each filteredSettings as setting (setting.dataId)}
            <tr class="border-b border-gray-700/50 transition-colors hover:bg-gray-800/30">
              <!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
              <td
                class="sticky left-0 z-10 bg-gray-900/95 px-3 py-2 backdrop-blur-sm"
                onclick={() => toggleEnabled(setting.dataId)}
              >
                <label class="relative inline-flex cursor-pointer items-center">
                  <input
                    type="checkbox"
                    checked={setting.enabled}
                    class="peer sr-only"
                    onchange={() => toggleEnabled(setting.dataId)}
                  />
                  <div class="h-5 w-9 rounded-full bg-gray-700 after:absolute after:left-[2px] after:top-[2px] after:h-4 after:w-4 after:rounded-full after:border after:border-gray-600 after:bg-gray-400 after:transition-all peer-checked:bg-amber-500 peer-checked:after:translate-x-full peer-checked:after:border-white peer-checked:after:bg-white"></div>
                </label>
              </td>
              <td class="px-3 py-2 font-mono text-xs text-gray-400">{setting.dataId}</td>
              <td class="px-3 py-2 text-xs font-medium text-gray-200">{setting.name}</td>
              <td class="px-3 py-2 text-xs text-gray-400">{setting.category}</td>
              <td class="px-3 py-2 text-xs text-gray-500">{setting.unit}</td>
              <td class="px-3 py-2">
                <input
                  type="number"
                  value={setting.minWarning ?? ''}
                  oninput={(e) => updateMinWarning(setting.dataId, (e.target as HTMLInputElement).value)}
                  disabled={!setting.enabled}
                  placeholder="-"
                  class="w-20 rounded border border-gray-700 bg-gray-800 px-2 py-1 text-xs text-gray-200 outline-none focus:border-amber-500 disabled:opacity-40"
                />
              </td>
              <td class="px-3 py-2">
                <input
                  type="number"
                  value={setting.maxWarning ?? ''}
                  oninput={(e) => updateMaxWarning(setting.dataId, (e.target as HTMLInputElement).value)}
                  disabled={!setting.enabled}
                  placeholder="-"
                  class="w-20 rounded border border-gray-700 bg-gray-800 px-2 py-1 text-xs text-gray-200 outline-none focus:border-amber-500 disabled:opacity-40"
                />
              </td>
              <td class="px-3 py-2">
                {#if setting.status === 'Typical'}
                  <span class="rounded-full px-2 py-0.5 text-[10px] font-medium bg-blue-500/20 text-blue-300">Typical</span>
                {:else if setting.status === 'Custom'}
                  <span class="rounded-full px-2 py-0.5 text-[10px] font-medium bg-amber-500/20 text-amber-300">Custom</span>
                {:else}
                  <span class="rounded-full px-2 py-0.5 text-[10px] font-medium bg-gray-700 text-gray-400">Off</span>
                {/if}
              </td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>

    <div class="mt-3 text-right text-xs text-gray-500">
      {filteredSettings.length} of {settings.length} data links
    </div>
  {/if}
</div>

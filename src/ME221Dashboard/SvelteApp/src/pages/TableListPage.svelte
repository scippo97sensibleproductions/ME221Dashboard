<script lang="ts">
  import { onMount } from 'svelte';
  import { IconTable, IconSearch, IconChevronLeft, IconStar, IconArrowsSort } from '@tabler/icons-svelte';
  import { HybridBridge } from '../lib/HybridBridge';
  import type { TableDefinition } from '../lib/tables/types';
  import { getDataRange } from '../lib/tables/types';
  import TableMiniMap from '../lib/tables/TableMiniMap.svelte';
  import { toast } from '../lib/toasts.svelte';

  let { onNavigate }: {
    onNavigate: (page: string, params?: Record<string, unknown>) => void;
  } = $props();

  let tables = $state<TableDefinition[]>([]);
  let tableDataCache = $state<Record<number, { output: number[]; rows: number; cols: number }>>({});
  let searchQuery = $state('');
  let selectedCategory = $state<string | null>(null);
  let sortBy = $state<'name' | 'category' | 'dimensions' | 'recent'>('name');
  let favorites = $state<Set<number>>(new Set());
  let recentIds = $state<number[]>([]);
  let loading = $state(true);

  // Load favorites from localStorage
  function loadFavorites() {
    try {
      const stored = localStorage.getItem('me221-favorites');
      if (stored) favorites = new Set(JSON.parse(stored));
    } catch {}
  }

  function saveFavorites() {
    try {
      localStorage.setItem('me221-favorites', JSON.stringify([...favorites]));
    } catch {}
  }

  function toggleFavorite(id: number) {
    const newFavs = new Set(favorites);
    if (newFavs.has(id)) {
      newFavs.delete(id);
    } else {
      newFavs.add(id);
    }
    favorites = newFavs;
    saveFavorites();
  }

  // Load recent from localStorage
  function loadRecent() {
    try {
      const stored = localStorage.getItem('me221-recent-tables');
      if (stored) recentIds = JSON.parse(stored);
    } catch {}
  }

  function trackRecent(id: number) {
    const newRecent = [id, ...recentIds.filter(r => r !== id)].slice(0, 5);
    recentIds = newRecent;
    try {
      localStorage.setItem('me221-recent-tables', JSON.stringify(newRecent));
    } catch {}
  }

  let categories = $derived.by(() => {
    const cats = new Set<string>();
    for (const t of tables) {
      if (t.category) cats.add(t.category);
    }
    return Array.from(cats).sort();
  });

  let filteredTables = $derived.by(() => {
    let result = [...tables];
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      result = result.filter(t => t.name.toLowerCase().includes(q) || t.category.toLowerCase().includes(q));
    }
    if (selectedCategory) {
      result = result.filter(t => t.category === selectedCategory);
    }
    // Sort
    result.sort((a, b) => {
      if (sortBy === 'name') return a.name.localeCompare(b.name);
      if (sortBy === 'category') return a.category.localeCompare(b.category) || a.name.localeCompare(b.name);
      if (sortBy === 'dimensions') return (a.rows * a.cols) - (b.rows * b.cols) || a.name.localeCompare(b.name);
      if (sortBy === 'recent') {
        const ai = recentIds.indexOf(a.id);
        const bi = recentIds.indexOf(b.id);
        return (ai === -1 ? 999 : ai) - (bi === -1 ? 999 : bi);
      }
      return 0;
    });
    // Favorites first
    result.sort((a, b) => (favorites.has(b.id) ? 1 : 0) - (favorites.has(a.id) ? 1 : 0));
    return result;
  });

  let favoriteTables = $derived(filteredTables.filter(t => favorites.has(t.id)));
  let nonFavoriteTables = $derived(filteredTables.filter(t => !favorites.has(t.id)));

  function is1D(table: TableDefinition): boolean {
    return table.tableType === 'T1x16' || table.tableType === 'T1x32';
  }

  function formatDimensions(table: TableDefinition): string {
    return `${table.rows}×${table.cols}`;
  }

  function handleSelectTable(table: TableDefinition) {
    trackRecent(table.id);
    onNavigate('tableEditor', { tableId: table.id });
  }

  // Load table data for thumbnails using batch bridge call
  async function loadThumbnails() {
    const toLoad = tables.filter(t => !tableDataCache[t.id]).map(t => t.id);
    if (toLoad.length === 0) return;

    // Load in batches of 30
    const batchSize = 30;
    for (let i = 0; i < toLoad.length; i += batchSize) {
      const batch = toLoad.slice(i, i + batchSize);
      try {
        const batchResult = await HybridBridge.readTableDataBatch(batch);
        if (batchResult.results) {
          const newCache = { ...tableDataCache };
          for (const [idStr, data] of Object.entries(batchResult.results)) {
            const id = parseInt(idStr);
            const table = tables.find(t => t.id === id);
            if (table && data.success && data.output) {
              newCache[id] = { output: data.output, rows: table.rows, cols: table.cols };
            }
          }
          tableDataCache = newCache;
        }
      } catch {}
    }
  }

  onMount(async () => {
    loadFavorites();
    loadRecent();
    try {
      const result = await HybridBridge.getTableDefinitions();
      tables = (result.tables as TableDefinition[]) || [];
    } catch (e) {
      console.error('Failed to load table definitions:', e);
    } finally {
      loading = false;
    }
    // Load all thumbnails in one batch call after initial render
    setTimeout(() => loadThumbnails(), 100);
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
    <h1 class="text-xl font-bold text-gray-100">ECU Tables</h1>
  </div>

  <!-- Search -->
  <div class="relative mb-3">
    <IconSearch size={16} class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-500" />
    <input
      type="text"
      placeholder="Search tables..."
      bind:value={searchQuery}
      class="w-full rounded-lg border border-gray-700 bg-gray-800 py-2.5 pl-10 pr-4 text-sm text-gray-100 placeholder-gray-500 outline-none focus:border-amber-500 focus:ring-1 focus:ring-amber-500"
    />
  </div>

  <!-- Sort + Category chips row -->
  <div class="mb-3 flex flex-wrap items-center gap-2">
    <!-- Sort dropdown -->
    <div class="relative">
      <select
        bind:value={sortBy}
        class="appearance-none rounded-lg border border-gray-700 bg-gray-800 py-1.5 pl-2 pr-7 text-xs text-gray-300 outline-none hover:border-gray-600"
      >
        <option value="name">Name A-Z</option>
        <option value="category">Category</option>
        <option value="dimensions">Dimensions</option>
        <option value="recent">Recently Edited</option>
      </select>
      <IconArrowsSort size={12} class="pointer-events-none absolute right-2 top-1/2 -translate-y-1/2 text-gray-500" />
    </div>
    <!-- Category chips -->
    {#each categories as cat}
      <button
        class="rounded-full px-2.5 py-0.5 text-xs font-medium transition-colors {selectedCategory === cat ? 'bg-amber-500/20 text-amber-300' : 'bg-gray-800 text-gray-400 hover:bg-gray-700 hover:text-gray-200'}"
        onclick={() => { selectedCategory = selectedCategory === cat ? null : cat; }}
      >
        {cat}
      </button>
    {/each}
  </div>

  <!-- Table list -->
  {#if loading}
    <div class="flex items-center justify-center py-12">
      <span class="inline-block h-6 w-6 animate-spin rounded-full border-2 border-gray-500 border-t-amber-400"></span>
    </div>
  {:else if filteredTables.length === 0}
    <div class="py-12 text-center text-sm text-gray-500">
      {tables.length === 0 ? 'No tables found in calibration.' : 'No tables match your search.'}
    </div>
  {:else}
    <!-- Favorites section -->
    {#if favoriteTables.length > 0 && !searchQuery && !selectedCategory}
      <div class="mb-3">
        <div class="mb-1.5 flex items-center gap-1.5 text-xs font-medium text-amber-400">
          <IconStar size={12} class="fill-amber-400" />
          Favorites
        </div>
        <div class="space-y-1">
          {#each favoriteTables as table (table.id)}
            <button
              class="flex w-full items-center gap-3 rounded-lg border border-gray-700/50 bg-gray-800/50 px-3 py-2.5 text-left transition-colors hover:border-gray-600 hover:bg-gray-800"
              onclick={() => handleSelectTable(table)}
            >
              {#if tableDataCache[table.id]}
                <TableMiniMap
                  data={tableDataCache[table.id].output}
                  rows={tableDataCache[table.id].rows}
                  cols={tableDataCache[table.id].cols}
                  minVal={getDataRange(tableDataCache[table.id].output).min}
                  maxVal={getDataRange(tableDataCache[table.id].output).max}
                />
              {:else}
                <div class="flex h-[50px] w-[80px] shrink-0 items-center justify-center rounded border border-gray-700 bg-gray-800">
                  <IconTable size={14} class="text-gray-600" />
                </div>
              {/if}
              <div class="min-w-0 flex-1">
                <div class="text-sm font-medium text-gray-100 truncate">{table.name}</div>
                <div class="text-xs text-gray-500">
                   {formatDimensions(table)} · {table.input0Name}{!is1D(table) ? ` × ${table.input1Name}` : ''}
                </div>
              </div>
              <!-- svelte-ignore a11y_no_static_element_interactions -->
              <span
                class="shrink-0 p-1 text-amber-400 hover:text-amber-300 cursor-pointer"
                onclick={(e) => { e.stopPropagation(); toggleFavorite(table.id); }}
                role="button"
                tabindex="-1"
                onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggleFavorite(table.id); } }}
                title="Unfavorite"
              >
                <IconStar size={14} class="fill-amber-400" />
              </span>
            </button>
          {/each}
        </div>
      </div>
    {/if}

    <!-- All tables / search results -->
    <div class="space-y-1">
      {#each (favoriteTables.length > 0 && !searchQuery && !selectedCategory ? nonFavoriteTables : filteredTables) as table (table.id)}
        <button
          class="flex w-full items-center gap-3 rounded-lg border border-gray-700/50 bg-gray-800/50 px-3 py-2.5 text-left transition-colors hover:border-gray-600 hover:bg-gray-800"
          onclick={() => handleSelectTable(table)}
        >
          {#if tableDataCache[table.id]}
            <TableMiniMap
              data={tableDataCache[table.id].output}
              rows={tableDataCache[table.id].rows}
              cols={tableDataCache[table.id].cols}
              minVal={getDataRange(tableDataCache[table.id].output).min}
              maxVal={getDataRange(tableDataCache[table.id].output).max}
            />
          {:else}
            <div class="flex h-[50px] w-[80px] shrink-0 items-center justify-center rounded border border-gray-700 bg-gray-800">
              <IconTable size={14} class="text-gray-600" />
            </div>
          {/if}
          <div class="min-w-0 flex-1">
            <div class="text-sm font-medium text-gray-100 truncate">{table.name}</div>
            <div class="text-xs text-gray-500">
              {formatDimensions(table)} · {table.input0Name}{!is1D(table) ? ` × ${table.input1Name}` : ''}
            </div>
          </div>
          <span class="shrink-0 rounded px-1.5 py-0.5 text-xs font-medium {is1D(table) ? 'bg-amber-500/15 text-amber-300' : 'bg-amber-500/15 text-amber-300'}">
            {is1D(table) ? '1D' : '2D'}
          </span>
          <!-- svelte-ignore a11y_no_static_element_interactions -->
          <span
            class="shrink-0 p-1 transition-colors cursor-pointer {favorites.has(table.id) ? 'text-amber-400 hover:text-amber-300' : 'text-gray-600 hover:text-gray-400'}"
            onclick={(e) => { e.stopPropagation(); toggleFavorite(table.id); }}
            role="button"
            tabindex="-1"
            onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggleFavorite(table.id); } }}
            title={favorites.has(table.id) ? 'Unfavorite' : 'Favorite'}
          >
            <IconStar size={14} class={favorites.has(table.id) ? 'fill-amber-400' : ''} />
          </span>
        </button>
      {/each}
    </div>
  {/if}
</div>

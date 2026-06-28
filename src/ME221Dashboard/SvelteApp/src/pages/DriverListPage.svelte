<script lang="ts">
  import { onMount } from 'svelte';
  import { IconSearch, IconChevronLeft, IconStar, IconArrowsSort, IconAdjustments } from '@tabler/icons-svelte';
  import { HybridBridge } from '../lib/HybridBridge';
  import type { DriverDefinition } from '../lib/HybridBridgeTypes';

  let { onNavigate }: {
    onNavigate: (page: string, params?: Record<string, unknown>) => void;
  } = $props();

  let drivers = $state<DriverDefinition[]>([]);
  let searchQuery = $state('');
  let selectedCategory = $state<string | null>(null);
  let sortBy = $state<'name' | 'category' | 'recent'>('name');
  let favorites = $state<Set<number>>(new Set());
  let recentIds = $state<number[]>([]);
  let loading = $state(true);

  function loadFavorites() {
    try {
      const stored = localStorage.getItem('me221-driver-favorites');
      if (stored) favorites = new Set(JSON.parse(stored));
    } catch {}
  }

  function saveFavorites() {
    try {
      localStorage.setItem('me221-driver-favorites', JSON.stringify([...favorites]));
    } catch {}
  }

  function toggleFavorite(id: number) {
    const newFavs = new Set(favorites);
    if (newFavs.has(id)) newFavs.delete(id);
    else newFavs.add(id);
    favorites = newFavs;
    saveFavorites();
  }

  function loadRecent() {
    try {
      const stored = localStorage.getItem('me221-recent-drivers');
      if (stored) recentIds = JSON.parse(stored);
    } catch {}
  }

  function trackRecent(id: number) {
    const newRecent = [id, ...recentIds.filter(r => r !== id)].slice(0, 10);
    recentIds = newRecent;
    try {
      localStorage.setItem('me221-recent-drivers', JSON.stringify(newRecent));
    } catch {}
  }

  let categories = $derived.by(() => {
    const cats = new Set<string>();
    for (const d of drivers) {
      if (d.category) cats.add(d.category);
    }
    return Array.from(cats).sort();
  });

  let filteredDrivers = $derived.by(() => {
    let result = [...drivers];
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      result = result.filter(d => d.name.toLowerCase().includes(q) || d.category.toLowerCase().includes(q));
    }
    if (selectedCategory) {
      result = result.filter(d => d.category === selectedCategory);
    }
    result.sort((a, b) => {
      if (sortBy === 'name') return a.name.localeCompare(b.name);
      if (sortBy === 'category') return a.category.localeCompare(b.category) || a.name.localeCompare(b.name);
      if (sortBy === 'recent') {
        const ai = recentIds.indexOf(a.id);
        const bi = recentIds.indexOf(b.id);
        return (ai === -1 ? 999 : ai) - (bi === -1 ? 999 : bi);
      }
      return 0;
    });
    result.sort((a, b) => (favorites.has(b.id) ? 1 : 0) - (favorites.has(a.id) ? 1 : 0));
    return result;
  });

  let favoriteDrivers = $derived(filteredDrivers.filter(d => favorites.has(d.id)));
  let nonFavoriteDrivers = $derived(filteredDrivers.filter(d => !favorites.has(d.id)));

  function handleSelectDriver(driver: DriverDefinition) {
    trackRecent(driver.id);
    onNavigate('driverEditor', { driverId: driver.id });
  }

  onMount(async () => {
    loadFavorites();
    loadRecent();
    try {
      const result = await HybridBridge.getDriverDefinitions();
      drivers = result.drivers || [];
    } catch (e) {
      console.error('Failed to load driver definitions:', e);
    } finally {
      loading = false;
    }
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
    <IconAdjustments size={20} class="text-rose-400" />
    <h1 class="text-xl font-bold text-gray-100">ECU Drivers</h1>
  </div>

  <div class="relative mb-3">
    <IconSearch size={16} class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-500" />
    <input
      type="text"
      placeholder="Search drivers..."
      bind:value={searchQuery}
      class="w-full rounded-lg border border-gray-700 bg-gray-800 py-2.5 pl-10 pr-4 text-sm text-gray-100 placeholder-gray-500 outline-none focus:border-rose-500 focus:ring-1 focus:ring-rose-500"
    />
  </div>

  <div class="mb-3 flex flex-wrap items-center gap-2">
    <div class="relative">
      <select
        bind:value={sortBy}
        class="appearance-none rounded-lg border border-gray-700 bg-gray-800 py-1.5 pl-2 pr-7 text-xs text-gray-300 outline-none hover:border-gray-600"
      >
        <option value="name">Name A-Z</option>
        <option value="category">Category</option>
        <option value="recent">Recently Edited</option>
      </select>
      <IconArrowsSort size={12} class="pointer-events-none absolute right-2 top-1/2 -translate-y-1/2 text-gray-500" />
    </div>
    {#each categories as cat}
      <button
        class="rounded-full px-2.5 py-0.5 text-xs font-medium transition-colors {selectedCategory === cat ? 'bg-rose-500/20 text-rose-300' : 'bg-gray-800 text-gray-400 hover:bg-gray-700 hover:text-gray-200'}"
        onclick={() => { selectedCategory = selectedCategory === cat ? null : cat; }}
      >
        {cat}
      </button>
    {/each}
  </div>

  {#if loading}
    <div class="flex items-center justify-center py-12">
      <span class="inline-block h-6 w-6 animate-spin rounded-full border-2 border-gray-500 border-t-cyan-400"></span>
    </div>
  {:else if filteredDrivers.length === 0}
    <div class="py-12 text-center text-sm text-gray-500">
      {drivers.length === 0 ? 'No drivers found in calibration.' : 'No drivers match your search.'}
    </div>
  {:else}
    {#if favoriteDrivers.length > 0 && !searchQuery && !selectedCategory}
      <div class="mb-3">
        <div class="mb-1.5 flex items-center gap-1.5 text-xs font-medium text-amber-400">
          <IconStar size={12} class="fill-amber-400" />
          Favorites
        </div>
        <div class="space-y-1">
          {#each favoriteDrivers as driver (driver.id)}
            <button
              class="flex w-full items-center gap-3 rounded-lg border border-gray-700/50 bg-gray-800/50 px-3 py-2.5 text-left transition-colors hover:border-gray-600 hover:bg-gray-800"
              onclick={() => handleSelectDriver(driver)}
            >
              <div class="min-w-0 flex-1">
                <div class="text-sm font-medium text-gray-100 truncate">{driver.name}</div>
                <div class="text-xs text-gray-500">{driver.category} · {driver.numberOfConfigs} params</div>
              </div>
              <span
                class="shrink-0 p-1 text-amber-400 hover:text-amber-300 cursor-pointer"
                onclick={(e) => { e.stopPropagation(); toggleFavorite(driver.id); }}
                role="button"
                tabindex="-1"
                onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggleFavorite(driver.id); } }}
              >
                <IconStar size={14} class="fill-amber-400" />
              </span>
            </button>
          {/each}
        </div>
      </div>
    {/if}

    <div class="space-y-1">
      {#each (favoriteDrivers.length > 0 && !searchQuery && !selectedCategory ? nonFavoriteDrivers : filteredDrivers) as driver (driver.id)}
        <button
          class="flex w-full items-center gap-3 rounded-lg border border-gray-700/50 bg-gray-800/50 px-3 py-2.5 text-left transition-colors hover:border-gray-600 hover:bg-gray-800"
          onclick={() => handleSelectDriver(driver)}
        >
          <div class="min-w-0 flex-1">
            <div class="text-sm font-medium text-gray-100 truncate">{driver.name}</div>
            <div class="text-xs text-gray-500">{driver.category} · {driver.numberOfConfigs} params</div>
          </div>
          <span class="shrink-0 rounded px-1.5 py-0.5 text-xs font-medium bg-gray-600/30 text-gray-400">
            {driver.numberOfConfigs}p
          </span>
          <span
            class="shrink-0 p-1 transition-colors cursor-pointer {favorites.has(driver.id) ? 'text-amber-400 hover:text-amber-300' : 'text-gray-600 hover:text-gray-400'}"
            onclick={(e) => { e.stopPropagation(); toggleFavorite(driver.id); }}
            role="button"
            tabindex="-1"
            onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggleFavorite(driver.id); } }}
          >
            <IconStar size={14} class={favorites.has(driver.id) ? 'fill-amber-400' : ''} />
          </span>
        </button>
      {/each}
    </div>
  {/if}
</div>

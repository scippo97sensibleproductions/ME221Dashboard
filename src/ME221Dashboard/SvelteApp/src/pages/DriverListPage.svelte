<script lang="ts">
  import { onMount } from 'svelte';
  import { SvelteSet } from 'svelte/reactivity';
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
  let favorites = new SvelteSet<number>();
  let recentIds = $state<number[]>([]);
  let loading = $state(true);
  let mounted = false;

  async function loadFavorites() {
    try {
      const stored = await HybridBridge.getFavoriteDrivers();
      if (Array.isArray(stored)) {
        favorites.clear();
        for (const id of stored) favorites.add(id);
      }
    } catch { /* favorites are best-effort */ }
  }

  function saveFavorites() {
    HybridBridge.saveFavoriteDrivers([...favorites]).catch(() => {});
  }

  function toggleFavorite(id: number) {
    if (favorites.has(id)) favorites.delete(id);
    else favorites.add(id);
    saveFavorites();
  }

  async function loadRecent() {
    try {
      const stored = await HybridBridge.getRecentDrivers();
      if (Array.isArray(stored)) recentIds = stored;
    } catch { /* recent list is best-effort */ }
  }

  function trackRecent(id: number) {
    const newRecent = [id, ...recentIds.filter(r => r !== id)].slice(0, 10);
    recentIds = newRecent;
    HybridBridge.saveRecentDrivers(newRecent).catch(() => {});
  }

  let categories = $derived.by(() => {
    const cats = new SvelteSet<string>();
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
    mounted = true;
    loadFavorites();
    loadRecent();
    try {
      const result = await HybridBridge.getDriverDefinitions();
      if (!mounted) return;
      drivers = result.drivers || [];
    } catch (e) {
      console.error('Failed to load driver definitions:', e);
    } finally {
      if (mounted) loading = false;
    }
  });
</script>

<div class="mx-auto max-w-4xl">
  <div class="mb-4 flex items-center gap-3">
    <button
      class="flex h-8 w-8 items-center justify-center text-metro-text-secondary transition-colors hover:bg-metro-card-hover hover:text-metro-text"
      onclick={() => onNavigate('dashboard')}
      aria-label="Back to dashboard"
    >
      <IconChevronLeft size={18} />
    </button>
    <div class="flex h-8 w-8 shrink-0 items-center justify-center bg-emerald-500">
      <IconAdjustments size={18} class="text-white" />
    </div>
    <h1 class="text-lg font-extrabold uppercase tracking-wider text-metro-text">ECU Drivers</h1>
  </div>

  <div class="relative mb-3">
    <IconSearch size={16} class="absolute left-3 top-1/2 -translate-y-1/2 text-metro-text-muted" />
    <input
      type="text"
      placeholder="Search drivers..."
      bind:value={searchQuery}
      class="w-full rounded-sm border border-metro-input-border bg-metro-input-bg py-2.5 pl-10 pr-4 text-sm text-metro-text outline-none placeholder:text-metro-text-muted focus:border-emerald-500"
    />
  </div>

  <div class="mb-3 flex flex-wrap items-center gap-2">
    <div class="relative">
      <select
        bind:value={sortBy}
        class="appearance-none rounded-sm border border-metro-input-border bg-metro-input-bg py-1.5 pl-2 pr-7 text-xs text-metro-text outline-none hover:border-[#555]"
      >
        <option value="name">Name A-Z</option>
        <option value="category">Category</option>
        <option value="recent">Recently Edited</option>
      </select>
      <IconArrowsSort size={12} class="pointer-events-none absolute right-2 top-1/2 -translate-y-1/2 text-metro-text-muted" />
    </div>
    {#each categories as cat (cat)}
      <button
        class="rounded-sm px-2.5 py-0.5 text-xs font-medium transition-colors {selectedCategory === cat ? 'bg-emerald-500 text-white' : 'bg-metro-card text-metro-text-muted hover:bg-metro-card-hover hover:text-metro-text'}"
        onclick={() => { selectedCategory = selectedCategory === cat ? null : cat; }}
      >
        {cat}
      </button>
    {/each}
  </div>

  {#if loading}
    <div class="flex items-center justify-center py-12">
      <span class="inline-block h-6 w-6 animate-spin rounded-full border-2 border-gray-500 border-t-emerald-400"></span>
    </div>
  {:else if filteredDrivers.length === 0}
    <div class="py-12 text-center text-sm text-metro-text-muted">
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
              class="flex w-full items-center gap-3 overflow-hidden rounded-sm border border-metro-border bg-metro-card text-left transition-colors hover:border-emerald-500/60 hover:bg-metro-card-hover"
              onclick={() => handleSelectDriver(driver)}
            >
              <div class="flex h-[50px] w-[80px] shrink-0 items-center justify-center border-r border-emerald-600 bg-emerald-500">
                <IconAdjustments size={18} class="text-white" />
              </div>
              <div class="min-w-0 flex-1">
                <div class="truncate text-sm font-medium text-metro-text">{driver.name}</div>
                <div class="mt-0.5 text-[11px] uppercase tracking-wider text-metro-text-muted">{driver.numberOfConfigs} configs</div>
              </div>
              <span class="shrink-0 rounded-sm bg-emerald-500/10 px-1.5 py-0.5 text-xs font-medium text-emerald-400">
                {driver.category}
              </span>
              <span
                class="shrink-0 p-1 text-amber-400 hover:text-amber-300"
                onclick={(e) => { e.stopPropagation(); toggleFavorite(driver.id); }}
                role="button"
                tabindex="-1"
                onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggleFavorite(driver.id); } }}
                title="Unfavorite"
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
          class="flex w-full items-center gap-3 overflow-hidden rounded-sm border border-metro-border bg-metro-card text-left transition-colors hover:border-emerald-500/60 hover:bg-metro-card-hover"
          onclick={() => handleSelectDriver(driver)}
        >
          <div class="flex h-[50px] w-[80px] shrink-0 items-center justify-center border-r border-emerald-600 bg-emerald-500">
            <IconAdjustments size={18} class="text-white" />
          </div>
          <div class="min-w-0 flex-1">
            <div class="truncate text-sm font-medium text-metro-text">{driver.name}</div>
            <div class="mt-0.5 text-[11px] uppercase tracking-wider text-metro-text-muted">{driver.numberOfConfigs} configs</div>
          </div>
          <span class="shrink-0 rounded-sm bg-emerald-500/10 px-1.5 py-0.5 text-xs font-medium text-emerald-400">
            {driver.category}
          </span>
          <span
            class="shrink-0 p-1 transition-colors {favorites.has(driver.id) ? 'text-amber-400 hover:text-amber-300' : 'text-metro-text-muted hover:text-metro-text'}"
            onclick={(e) => { e.stopPropagation(); toggleFavorite(driver.id); }}
            role="button"
            tabindex="-1"
            onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggleFavorite(driver.id); } }}
            title={favorites.has(driver.id) ? 'Unfavorite' : 'Favorite'}
          >
            <IconStar size={14} class={favorites.has(driver.id) ? 'fill-amber-400' : ''} />
          </span>
        </button>
      {/each}
    </div>
  {/if}
</div>

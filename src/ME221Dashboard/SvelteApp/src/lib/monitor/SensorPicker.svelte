<script lang="ts">
  import type { DataLinkDefinition } from '../HybridBridgeTypes';
  import { liveDataStore } from '../stores/LiveDataStore.svelte';
  import { getSensorColor } from './sensorColors';

  let {
    dataLinks = [],
    selectedIds = $bindable(new Set<number>()),
  }: {
    dataLinks: DataLinkDefinition[];
    selectedIds?: Set<number>;
  } = $props();

  let searchQuery = $state('');
  let activeCategory = $state('All');

  const categories = $derived.by(() => {
    const cats = new Set<string>();
    for (const dl of dataLinks) {
      if (dl.category) cats.add(dl.category);
    }
    return ['All', ...Array.from(cats).sort()];
  });

  const filteredLinks = $derived.by(() => {
    let links = dataLinks;
    if (activeCategory !== 'All') {
      links = links.filter((dl) => dl.category === activeCategory);
    }
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      links = links.filter(
        (dl) =>
          dl.name.toLowerCase().includes(q) ||
          dl.category.toLowerCase().includes(q),
      );
    }
    return links;
  });

  function toggle(id: number) {
    const next = new Set(selectedIds);
    if (next.has(id)) {
      next.delete(id);
    } else {
      next.add(id);
    }
    selectedIds = next;
  }

  let tick = $state(0);
  $effect(() => {
    const id = setInterval(() => tick++, 200);
    return () => clearInterval(id);
  });

  function formatValue(id: number): string {
    void tick;
    const v = liveDataStore.values[id];
    if (v == null) return '--';
    if (Math.abs(v) >= 1000) return v.toFixed(0);
    if (Math.abs(v) >= 100) return v.toFixed(1);
    return v.toFixed(2);
  }
</script>

<div class="flex flex-col h-full bg-[#1a1a1a] border-r border-[#333]">
  <div class="p-2 border-b border-[#333]">
    <input
      type="text"
      placeholder="Search sensors..."
      bind:value={searchQuery}
      class="w-full px-2 py-1.5 text-xs bg-[#222] border border-[#444] rounded text-white placeholder-gray-500 focus:outline-none focus:border-sky-500"
    />
  </div>

  <div class="flex gap-1 px-2 py-1.5 overflow-x-auto border-b border-[#333] scrollbar-thin">
    {#each categories as cat}
      <button
        class="px-2 py-0.5 text-[10px] rounded whitespace-nowrap transition-colors
          {activeCategory === cat
            ? 'bg-sky-600 text-white'
            : 'bg-[#2a2a2a] text-gray-400 hover:bg-[#333]'}"
        onclick={() => (activeCategory = cat)}
      >
        {cat}
      </button>
    {/each}
  </div>

  <div class="flex-1 overflow-y-auto scrollbar-thin">
    {#if filteredLinks.length === 0}
      <div class="p-4 text-center text-gray-500 text-xs">No sensors found</div>
    {:else}
      {#each filteredLinks as dl (dl.id)}
        {@const color = getSensorColor(dl.id)}
        {@const isSelected = selectedIds.has(dl.id)}
        <button
          class="w-full flex items-center gap-2 px-2 py-1.5 text-left text-xs border-b border-[#2a2a2a] transition-colors
            {isSelected
              ? 'bg-[#1e293b] border-l-2'
              : 'bg-transparent hover:bg-[#222] border-l-2 border-l-transparent'}"
          style="border-left-color: {isSelected ? color : 'transparent'}"
          onclick={() => toggle(dl.id)}
        >
          <div
            class="w-2 h-2 rounded-full shrink-0"
            style="background-color: {color}; opacity: {isSelected ? 1 : 0.4}"
          ></div>
          <div class="flex-1 min-w-0">
            <div class="truncate text-white">{dl.name}</div>
            <div class="text-[10px] text-gray-500 truncate">
              {dl.category}{dl.measureUnit ? ` · ${dl.measureUnit}` : ''}
            </div>
          </div>
          <div class="text-gray-300 font-mono tabular-nums text-[11px] shrink-0">
            {formatValue(dl.id)}
          </div>
        </button>
      {/each}
    {/if}
  </div>

  <div class="px-2 py-1.5 text-[10px] text-gray-500 border-t border-[#333]">
    {selectedIds.size} / {dataLinks.length} selected
  </div>
</div>

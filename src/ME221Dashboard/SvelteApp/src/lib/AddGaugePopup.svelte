<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { HybridBridge, type AvailableSensor } from '../lib/HybridBridge';
  import { IconSearch, IconX, IconPlus } from '@tabler/icons-svelte';

  let { dashboardName, existingEntityIds = new Set(), anchorX, anchorY, onAdd, onclose }: {
    dashboardName: string;
    existingEntityIds?: Set<number>;
    anchorX: number;
    anchorY: number;
    onAdd: (sensor: AvailableSensor, fractionX: number, fractionY: number) => void;
    onclose: () => void;
  } = $props();

  let sensors = $state<AvailableSensor[]>([]);
  let searchText = $state('');
  let loading = $state(true);
  let popupEl = $state<HTMLDivElement | null>(null);
  let searchInputEl = $state<HTMLInputElement | null>(null);

  let filtered = $derived.by(() => {
    if (!searchText.trim()) return sensors.slice(0, 30);
    const q = searchText.toLowerCase();
    return sensors.filter(s =>
      s.name.toLowerCase().includes(q) ||
      s.category.toLowerCase().includes(q) ||
      s.unit.toLowerCase().includes(q) ||
      String(s.id).includes(q)
    ).slice(0, 30);
  });

  onMount(async () => {
    searchInputEl?.focus();
    try {
      const result = await HybridBridge.getAvailableSensors(dashboardName);
      sensors = result.sensors;
    } catch {
      sensors = [];
    } finally {
      loading = false;
    }
  });

  function handleClickOutside(e: PointerEvent) {
    if (popupEl && !popupEl.contains(e.target as Node)) {
      onclose();
    }
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape') onclose();
  }

  onMount(() => {
    document.addEventListener('pointerdown', handleClickOutside, true);
    document.addEventListener('keydown', handleKeydown, true);
    return () => {
      document.removeEventListener('pointerdown', handleClickOutside, true);
      document.removeEventListener('keydown', handleKeydown, true);
    };
  });

  // Clamp popup position to viewport
  let popupStyle = $derived.by(() => {
    const maxW = 320;
    const maxH = 400;
    let left = anchorX;
    let top = anchorY;
    if (typeof window !== 'undefined') {
      if (left + maxW > window.innerWidth) left = window.innerWidth - maxW - 8;
      if (top + maxH > window.innerHeight) top = window.innerHeight - maxH - 8;
      if (left < 8) left = 8;
      if (top < 8) top = 8;
    }
    return `left: ${left}px; top: ${top}px;`;
  });
</script>

<div
  bind:this={popupEl}
  class="fixed z-[200] w-80 max-h-[400px] flex flex-col rounded-lg border border-gray-700 bg-gray-900 shadow-2xl"
  style={popupStyle}
>
  <!-- Header -->
  <div class="flex items-center gap-2 border-b border-gray-700/50 px-3 py-2">
    <IconPlus size={14} class="text-cyan-400 shrink-0" />
    <span class="text-xs font-semibold text-gray-300">Add Gauge</span>
    <button class="ml-auto text-gray-500 hover:text-gray-300" onclick={onclose}>
      <IconX size={14} />
    </button>
  </div>

  <!-- Search -->
  <div class="border-b border-gray-700/50 px-3 py-2">
    <div class="relative">
      <IconSearch size={14} class="absolute left-2 top-1/2 -translate-y-1/2 text-gray-500" />
      <input
        bind:this={searchInputEl}
        bind:value={searchText}
        type="text"
        placeholder="Search sensors..."
        class="w-full rounded bg-gray-800 py-1.5 pl-7 pr-2 text-xs text-gray-200 placeholder-gray-500 outline-none focus:ring-1 focus:ring-cyan-500/50"
      />
    </div>
  </div>

  <!-- Sensor list -->
  <div class="flex-1 overflow-y-auto">
    {#if loading}
      <div class="flex items-center justify-center py-6">
        <span class="inline-block h-4 w-4 animate-spin rounded-full border-2 border-gray-500 border-t-cyan-400"></span>
      </div>
    {:else if filtered.length === 0}
      <div class="py-6 text-center text-xs text-gray-500">No sensors found</div>
    {:else}
      {#each filtered as sensor (sensor.id)}
        {@const alreadyAdded = existingEntityIds.has(sensor.id)}
        <button
          class="flex w-full items-center gap-2 px-3 py-2 text-left transition-colors"
          class:hover:bg-gray-800={!alreadyAdded}
          class:opacity-40={alreadyAdded}
          onclick={() => {
            if (alreadyAdded) return;
            onAdd(sensor, anchorX, anchorY);
            onclose();
          }}
        >
          <div class="min-w-0 flex-1">
            <div class="truncate text-[11px] font-medium text-gray-200">
              {sensor.customization?.customName || sensor.name}
            </div>
            <div class="mt-0.5 flex items-center gap-1.5">
              <span class="px-1.5 py-0.5 text-[8px] font-bold uppercase tracking-wider rounded bg-gray-700 text-gray-400">
                {sensor.category}
              </span>
              {#if sensor.unit}
                <span class="text-[9px] text-gray-500">{sensor.unit}</span>
              {/if}
            </div>
          </div>
          {#if alreadyAdded}
            <span class="text-[9px] text-gray-500 shrink-0">Added</span>
          {:else}
            <IconPlus size={12} class="shrink-0 text-gray-500" />
          {/if}
        </button>
      {/each}
    {/if}
  </div>
</div>

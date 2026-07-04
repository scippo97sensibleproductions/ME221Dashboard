<script lang="ts">
  import { IconX, IconPlus, IconActivity } from '@tabler/icons-svelte';
  import LiveSensorReadout from './LiveSensorReadout.svelte';
  import type { DataLinkDefinition } from '../HybridBridgeTypes';

  let { open, tableDef, liveValues, dataLinks, sensorIds, onAddSensor, onRemoveSensor, onClose }: {
    open: boolean;
    tableDef: { input0LinkId: number; input1LinkId: number; outputLinkId: number; input0Name: string; input1Name: string; outputName: string } | null;
    liveValues: Record<string, number>;
    dataLinks: DataLinkDefinition[];
    sensorIds: number[];
    onAddSensor: (id: number) => void;
    onRemoveSensor: (id: number) => void;
    onClose: () => void;
  } = $props();

  let pickerOpen = $state(false);
  let searchQuery = $state('');

  // Build the list of sensors to display: table's native links + user-added
  let displaySensors = $derived.by(() => {
    if (!tableDef) return [];
    const result: { id: number; name: string; unit: string; isNative: boolean }[] = [];

    // Native table links first
    const nativeIds = new Set<number>();
    if (tableDef.input0LinkId) {
      nativeIds.add(tableDef.input0LinkId);
      const link = dataLinks.find(d => d.id === tableDef.input0LinkId);
      result.push({ id: tableDef.input0LinkId, name: tableDef.input0Name || link?.name || 'Input 0', unit: link?.measureUnit || '', isNative: true });
    }
    if (tableDef.input1LinkId) {
      nativeIds.add(tableDef.input1LinkId);
      const link = dataLinks.find(d => d.id === tableDef.input1LinkId);
      result.push({ id: tableDef.input1LinkId, name: tableDef.input1Name || link?.name || 'Input 1', unit: link?.measureUnit || '', isNative: true });
    }
    if (tableDef.outputLinkId) {
      nativeIds.add(tableDef.outputLinkId);
      const link = dataLinks.find(d => d.id === tableDef.outputLinkId);
      result.push({ id: tableDef.outputLinkId, name: tableDef.outputName || link?.name || 'Output', unit: link?.measureUnit || '', isNative: true });
    }

    // User-added sensors (not already native)
    for (const id of sensorIds) {
      if (nativeIds.has(id)) continue;
      const link = dataLinks.find(d => d.id === id);
      if (link) {
        result.push({ id: link.id, name: link.name, unit: link.measureUnit, isNative: false });
      }
    }

    return result;
  });

  let filteredLinks = $derived.by(() => {
    if (!searchQuery.trim()) return dataLinks;
    const q = searchQuery.toLowerCase();
    return dataLinks.filter(d => d.name.toLowerCase().includes(q) || d.category.toLowerCase().includes(q));
  });

  function handleAdd(id: number) {
    onAddSensor(id);
    pickerOpen = false;
    searchQuery = '';
  }
</script>

{#if open}
  <div
    class="flex flex-col border-l overflow-hidden"
    style="width: 220px; min-width: 220px; border-color: var(--metro-border); background-color: var(--metro-sidebar);"
  >
    <!-- Header -->
    <div class="flex items-center justify-between border-b px-3 py-2" style="border-color: var(--metro-border);">
      <div class="flex items-center gap-1.5">
        <IconActivity size={14} style="color: var(--metro-orange);" />
        <span class="text-[11px] font-bold uppercase tracking-wider" style="color: var(--metro-text-secondary);">Live Data</span>
      </div>
      <button class="p-0.5 transition-colors duration-150" style="color: var(--metro-text-muted);" onclick={onClose}>
        <IconX size={14} />
      </button>
    </div>

    <!-- Sensor list -->
    <div class="flex-1 overflow-auto">
      {#each displaySensors as sensor (sensor.id)}
        {@const val = liveValues[String(sensor.id)] ?? null}
        <LiveSensorReadout
          name={sensor.name}
          value={val}
          unit={sensor.unit}
          highlight={sensor.id === tableDef?.outputLinkId}
        />
      {/each}

      {#if displaySensors.length === 0}
        <p class="px-3 py-4 text-center text-[11px]" style="color: var(--metro-text-muted);">No sensors configured</p>
      {/if}
    </div>

    <!-- Add sensor button -->
    <div class="border-t p-2" style="border-color: var(--metro-border);">
      {#if pickerOpen}
        <div class="mb-2">
          <input
            bind:value={searchQuery}
            type="text"
            placeholder="Search sensors..."
            class="metro-input w-full text-[11px]"
          />
        </div>
        <div class="max-h-40 overflow-auto">
          {#each filteredLinks as link (link.id)}
            {@const alreadyAdded = displaySensors.some(s => s.id === link.id)}
            <button
              class="flex w-full items-center justify-between px-2 py-1 text-left text-[11px] transition-colors duration-150
                     {alreadyAdded ? 'opacity-40' : 'hover:bg-white/5'}"
              style="color: var(--metro-text-secondary);"
              onclick={() => handleAdd(link.id)}
              disabled={alreadyAdded}
            >
              <span class="truncate">{link.name}</span>
              <span class="shrink-0 text-[9px]" style="color: var(--metro-text-muted);">{link.measureUnit}</span>
            </button>
          {/each}
        </div>
        <button
          class="mt-1 w-full text-center text-[10px] transition-colors duration-150"
          style="color: var(--metro-text-muted);"
          onclick={() => { pickerOpen = false; searchQuery = ''; }}
        >Cancel</button>
      {:else}
        <button
          class="flex w-full items-center justify-center gap-1 rounded py-1.5 text-[11px] font-medium transition-colors duration-150"
          style="border: 1px dashed var(--metro-border); color: var(--metro-text-secondary);"
          onclick={() => { pickerOpen = true; }}
        >
          <IconPlus size={12} />
          Add Sensor
        </button>
      {/if}
    </div>
  </div>
{/if}

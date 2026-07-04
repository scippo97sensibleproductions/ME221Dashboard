<script lang="ts">
  import type { DataLinkDefinition } from '../HybridBridgeTypes';

  let { slotType, slotIndex, slotName, currentLinkId, editable = true, dataLinks, onAssign }: {
    slotType: 'output' | 'input';
    slotIndex: number;
    slotName: string;
    currentLinkId: number;
    editable?: boolean;
    dataLinks: DataLinkDefinition[];
    onAssign: (linkId: number) => void;
  } = $props();

  let showPicker = $state(false);

  let currentLink = $derived(dataLinks.find(dl => dl.id === currentLinkId));
  let filteredLinks = $derived(dataLinks);

  function handleSelect(link: DataLinkDefinition) {
    onAssign(link.id);
    showPicker = false;
  }
</script>

<div class="flex items-center justify-between py-2 px-3 {editable ? '' : 'opacity-40'}">
  <div class="min-w-0 flex-1 mr-3">
    <div class="text-xs text-gray-400 uppercase tracking-wide">{slotName}</div>
  </div>
  {#if editable}
    <button
      class="rounded-lg border border-gray-600 bg-gray-700/50 px-3 py-1.5 text-sm transition-colors hover:border-gray-500 hover:bg-gray-700 {currentLink ? 'text-gray-100' : 'text-gray-500 italic'}"
      onclick={() => { showPicker = !showPicker; }}
    >
      {currentLink ? currentLink.name : 'Unassigned'}
    </button>
  {:else}
    <span class="text-sm text-gray-400">{currentLink ? currentLink.name : 'Unassigned'}</span>
  {/if}
</div>

{#if showPicker}
  <div class="mx-3 mb-2 rounded-lg border border-gray-600 bg-gray-800 p-2 max-h-48 overflow-y-auto">
    <button
      class="w-full rounded px-2 py-1.5 text-left text-sm text-gray-400 hover:bg-gray-700 hover:text-gray-200"
      onclick={() => { onAssign(0); showPicker = false; }}
    >
      None
    </button>
    {#each filteredLinks as link (link.id)}
      <button
        class="w-full rounded px-2 py-1.5 text-left text-sm transition-colors {link.id === currentLinkId ? 'bg-cyan-500/20 text-cyan-300' : 'text-gray-200 hover:bg-gray-700'}"
        onclick={() => handleSelect(link)}
      >
        <span class="truncate">{link.name}</span>
        <span class="ml-1.5 text-[10px] text-gray-500">{link.category}</span>
      </button>
    {/each}
  </div>
{/if}

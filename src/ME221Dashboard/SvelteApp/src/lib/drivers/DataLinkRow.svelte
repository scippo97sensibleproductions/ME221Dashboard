<script lang="ts">
  import { IconChevronDown, IconCheck, IconSearch } from '@tabler/icons-svelte';
  import type { DataLinkDefinition } from '../HybridBridgeTypes';

  let { slotName, currentLinkId, editable = true, dataLinks, duplicateOf = '', onAssign }: {
    slotType: 'output' | 'input';
    slotIndex: number;
    slotName: string;
    currentLinkId: number;
    editable?: boolean;
    dataLinks: DataLinkDefinition[];
    duplicateOf?: string;
    onAssign: (linkId: number) => void;
  } = $props();

  let showPicker = $state(false);
  let searchQuery = $state('');

  const currentLink = $derived(dataLinks.find(dl => dl.id === currentLinkId));

  const filteredLinks = $derived(
    searchQuery.trim()
      ? dataLinks.filter(dl =>
          dl.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
          dl.category.toLowerCase().includes(searchQuery.toLowerCase()))
      : dataLinks
  );

  function togglePicker() {
    if (!editable) return;
    showPicker = !showPicker;
    if (showPicker) searchQuery = '';
  }

  function handleSelect(linkId: number) {
    onAssign(linkId);
    showPicker = false;
  }
</script>

<div class="relative border-b border-metro-border-subtle py-2 pl-3 pr-2" class:bg-metro-card-hover={showPicker}>
  <div class="flex items-center justify-between gap-3">
    <div class="min-w-0 flex-1">
      <div class="text-[10px] font-semibold uppercase tracking-wider text-metro-text-secondary">{slotName}</div>
      {#if duplicateOf}
        <div class="text-[10px] text-yellow-400">Also assigned to {duplicateOf}</div>
      {/if}
    </div>
    {#if editable}
      <button
        class="flex h-8 items-center gap-1 border border-metro-input-border bg-metro-input-bg px-2 text-[13px] transition-colors hover:border-metro-green"
        onclick={togglePicker}
        aria-haspopup="listbox"
        aria-expanded={showPicker}
      >
        <span class="max-w-44 truncate text-metro-text">{currentLink ? currentLink.name : 'Unassigned'}</span>
        <IconChevronDown size={14} class={showPicker ? 'rotate-180 text-metro-green' : 'text-metro-text-muted'} />
      </button>
    {:else}
      <span class="text-[13px] text-metro-text-muted">{currentLink ? currentLink.name : 'Unassigned'}</span>
    {/if}
  </div>

  {#if showPicker}
    <div class="fixed inset-0 z-30" onclick={togglePicker} role="presentation"></div>
    <div class="absolute left-3 right-3 z-40 mt-1 border border-metro-border bg-metro-card">
      {#if dataLinks.length > 8}
        <div class="relative border-b border-metro-border-subtle">
          <IconSearch size={13} class="absolute left-2 top-1/2 -translate-y-1/2 text-metro-text-muted" />
          <input
            type="text"
            placeholder="Search links..."
            bind:value={searchQuery}
            class="h-8 w-full bg-metro-input-bg py-1 pl-7 pr-2 text-[12px] text-metro-text outline-none placeholder:text-metro-text-muted"
          />
        </div>
      {/if}
      <div class="max-h-64 overflow-y-auto" role="listbox">
        <button
          class="flex w-full items-center gap-2 px-2.5 py-2 text-left text-[13px] text-metro-text-muted transition-colors hover:bg-metro-card-hover"
          onclick={() => handleSelect(0)}
          role="option"
          aria-selected={currentLinkId === 0}
        >
          <span class="flex-1">— Unassigned —</span>
          {#if currentLinkId === 0}
            <IconCheck size={14} class="text-metro-green" />
          {/if}
        </button>
        {#each filteredLinks as link (link.id)}
          <button
            class="flex w-full items-center gap-2 px-2.5 py-2 text-left text-[13px] transition-colors hover:bg-metro-card-hover
              {link.id === currentLinkId ? 'bg-metro-green/15 text-metro-green' : 'text-metro-text'}"
            onclick={() => handleSelect(link.id)}
            role="option"
            aria-selected={link.id === currentLinkId}
          >
            <span class="flex-1 truncate">{link.name}</span>
            <span class="shrink-0 text-[10px] text-metro-text-muted">{link.category}</span>
            {#if link.id === currentLinkId}
              <IconCheck size={14} />
            {/if}
          </button>
        {:else}
          <div class="px-2.5 py-2 text-[12px] text-metro-text-muted">No links match</div>
        {/each}
      </div>
    </div>
  {/if}
</div>

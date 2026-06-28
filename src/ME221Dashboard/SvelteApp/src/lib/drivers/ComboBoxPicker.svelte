<script lang="ts">
  import { Modal } from 'flowbite-svelte';
  import { IconSearch, IconCheck } from '@tabler/icons-svelte';
  import type { ComboOption } from '../lib/HybridBridgeTypes';

  let { open, title, options, currentValue, onSelect, onClose }: {
    open: boolean;
    title: string;
    options: ComboOption[];
    currentValue: number;
    onSelect: (value: number) => void;
    onClose: () => void;
  } = $props();

  let searchQuery = $state('');

  let filteredOptions = $derived(
    searchQuery.trim()
      ? options.filter(o => o.name.toLowerCase().includes(searchQuery.toLowerCase()))
      : options
  );

  function handleSelect(option: ComboOption) {
    onSelect(option.id);
    onClose();
  }
</script>

<Modal bind:open size="sm" placement="center" outsideclose={true} class="backdrop:bg-gray-900/80" ontoggle={(e) => { if (e.newState === 'closed') onClose(); }}>
  <h3 class="mb-3 text-base font-semibold text-gray-100">{title}</h3>

  <div class="relative mb-3">
    <IconSearch size={14} class="absolute left-3 top-1/2 -translate-y-1/2 text-gray-500" />
    <input
      type="text"
      placeholder="Search options..."
      bind:value={searchQuery}
      class="w-full rounded-lg border border-gray-600 bg-gray-700 py-2 pl-9 pr-3 text-sm text-gray-100 placeholder-gray-400 outline-none focus:border-cyan-500"
    />
  </div>

  <div class="max-h-64 overflow-y-auto -mx-2">
    {#each filteredOptions as option (option.id)}
      <button
        class="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left text-sm transition-colors {option.id === currentValue ? 'bg-cyan-500/15 text-cyan-300' : 'text-gray-200 hover:bg-gray-700'}"
        onclick={() => handleSelect(option)}
      >
        <span class="flex-1">{option.name}</span>
        {#if option.id === currentValue}
          <IconCheck size={14} class="text-cyan-400" />
        {/if}
      </button>
    {:else}
      <div class="py-4 text-center text-sm text-gray-500">No options match</div>
    {/each}
  </div>
</Modal>

<script lang="ts">
  import { IconTable } from '@tabler/icons-svelte';
  import type { TableDefinition } from '../tables/types';

  interface Props {
    table: TableDefinition;
    onSelect: (tableId: number) => void;
  }

  let { table, onSelect }: Props = $props();

  let isOneDimensional = $derived(table.rows === 1 || table.cols === 1);
</script>

<button
  class="flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-left transition-colors hover:bg-gray-700/40 border border-transparent hover:border-gray-600/40"
  onclick={() => onSelect(table.id)}
>
  <div class="flex h-8 w-12 shrink-0 items-center justify-center rounded border border-gray-700 bg-gray-800/50">
    <IconTable size={14} class="text-gray-500" />
  </div>
  <div class="min-w-0 flex-1">
    <div class="text-xs font-medium text-gray-100 truncate">{table.name}</div>
    <div class="text-[10px] text-gray-500 mt-0.5">
      {table.rows}×{table.cols}
      {#if isOneDimensional}
        {#if table.input0Name}
          · {table.input0Name}
        {/if}
      {:else}
        · {table.input0Name || '?'} × {table.input1Name || '?'}
      {/if}
    </div>
  </div>
</button>
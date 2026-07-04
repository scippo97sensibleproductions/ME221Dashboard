<script lang="ts">
  import type { TableDefinition } from '../lib/tables/types';
  import { IconCheck } from '@tabler/icons-svelte';

  let { tables, selectedTableIds, toggleTable }: {
    tables: { def: TableDefinition; isSelected: boolean }[];
    selectedTableIds: Set<number>;
    toggleTable: (id: number) => void;
  } = $props();
</script>

<div class="flex-1 overflow-y-auto lg:min-h-0">
  {#if tables.length === 0}
    <div class="flex h-full items-center justify-center">
      <p class="text-[13px]" style="color: var(--metro-text-muted);">No tables match your filter</p>
    </div>
  {:else}
    <div class="space-y-2">
      {#each tables as { def: table, isSelected } (table.id)}
        <div
          style={isSelected
            ? 'background-color: var(--metro-card); border: 1px solid var(--metro-purple);'
            : 'background-color: var(--metro-card); border: 1px solid var(--metro-border);'}
        >
          <div class="flex flex-col gap-2 px-3 py-2.5 sm:flex-row sm:items-center sm:gap-3">
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-1.5">
                <span class="truncate text-[13px] font-semibold" style="color: var(--metro-text);">
                  {table.name}
                </span>
                {#if table.enabled}
                  <span class="shrink-0" style="color: var(--metro-green);" title="Active in ECU"><IconCheck size={12} /></span>
                {/if}
              </div>
              <div class="mt-0.5 flex flex-wrap items-center gap-1.5">
                <span class="px-2 py-0.5 text-[9px] font-bold uppercase tracking-wider"
                      style="background-color: var(--metro-green); color: var(--metro-text-on-accent);">
                  {table.tableType}
                </span>
                <span class="text-[11px]" style="color: var(--metro-text-muted);">{table.category}</span>
                {#if table.outputName}
                  <span class="text-[11px]" style="color: var(--metro-text-muted);">→ {table.outputName}</span>
                {/if}
              </div>
            </div>
            <div class="flex shrink-0 items-center gap-2">
              {#if isSelected}
                <button
                  class="flex-1 px-3 py-2 text-[12px] font-medium transition-colors duration-150 sm:flex-none sm:px-2 sm:py-1"
                  style="background-color: var(--metro-purple); color: var(--metro-text-on-accent);"
                  onclick={() => toggleTable(table.id)}
                >
                  Selected
                </button>
              {:else}
                <button
                  class="metro-hover-border flex-1 px-3 py-2 text-[12px] font-medium transition-colors duration-150 sm:flex-none sm:px-2 sm:py-1"
                  style="background-color: transparent; border: 1px solid var(--metro-input-border); color: var(--metro-text-secondary);"
                  onclick={() => toggleTable(table.id)}
                >
                  Select
                </button>
              {/if}
            </div>
          </div>
        </div>
      {/each}
    </div>
  {/if}
</div>

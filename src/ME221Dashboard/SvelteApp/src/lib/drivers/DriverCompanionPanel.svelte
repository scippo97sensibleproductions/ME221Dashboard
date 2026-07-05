<script lang="ts">
  import type { DriverDefinition, DataLinkDefinition } from '../HybridBridgeTypes';
  import DataLinkRow from './DataLinkRow.svelte';
  import RelatedLinkRow from './RelatedLinkRow.svelte';
  import TableWidget from '../tables/TableWidget.svelte';

  interface Props {
    driverDefinition: DriverDefinition;
    outputLinkIds: number[];
    inputLinkIds: number[];
    dataLinks: DataLinkDefinition[];
    tableDefinitions: { id: number; name: string; category: string; rows: number; cols: number; tableType: string }[];
    onOutputLinkChange: (slotIndex: number, newLinkId: number) => void;
    onInputLinkChange: (slotIndex: number, newLinkId: number) => void;
    onNavigate: (page: string, params?: Record<string, unknown>) => void;
  }

  let {
    driverDefinition,
    outputLinkIds,
    inputLinkIds,
    dataLinks,
    tableDefinitions,
    onOutputLinkChange,
    onInputLinkChange,
    onNavigate,
  }: Props = $props();

  type TabId = 'inputs' | 'outputs' | 'tables' | 'links';
  let activeTab = $state<TabId>('inputs');

  let relatedTables = $derived(
    tableDefinitions.filter(t => t.category === driverDefinition.category)
  );

  let relatedLinks = $derived(
    dataLinks.filter(l => l.category === driverDefinition.category)
  );

  let hasOutputs = $derived(driverDefinition.numberOfOutputs > 0);
  let hasInputs = $derived(driverDefinition.numberOfInputs > 0);
  let hasRelatedTables = $derived(relatedTables.length > 0);
  let hasRelatedLinks = $derived(relatedLinks.length > 0);

  const tabs: { id: TabId; label: string; count: number }[] = $derived([
    { id: 'inputs', label: 'Inputs', count: driverDefinition.numberOfInputs },
    { id: 'outputs', label: 'Outputs', count: driverDefinition.numberOfOutputs },
    { id: 'tables', label: 'Tables', count: relatedTables.length },
    { id: 'links', label: 'Links', count: relatedLinks.length },
  ]);

  function handleTableTap(tableId: number) {
    onNavigate('tableEditor', { tableId });
  }
</script>

<div class="flex flex-col h-full bg-gray-800/20">
  <!-- Tab selector -->
  <div class="flex border-b border-gray-700/30 shrink-0 overflow-x-auto gap-0.5 px-1">
    {#each tabs as tab}
      <button
        class="flex items-center gap-1.5 px-3.5 py-2.5 text-xs font-medium transition-colors border-b-2 whitespace-nowrap
          {activeTab === tab.id
            ? 'border-emerald-400 text-emerald-300'
            : 'border-transparent text-gray-500 hover:text-gray-300'}"
        onclick={() => { activeTab = tab.id; }}
      >
        {tab.label}
        {#if tab.count > 0}
          <span class="rounded-full px-1.5 py-px text-[10px] font-bold leading-none
            {activeTab === tab.id
              ? 'bg-emerald-500/20 text-emerald-300'
              : 'bg-gray-700 text-gray-500'}">
            {tab.count}
          </span>
        {/if}
      </button>
    {/each}
  </div>

  <!-- Tab content -->
  <div class="flex-1 min-h-0 {activeTab === 'tables' ? 'overflow-hidden' : 'overflow-y-auto'} p-3 space-y-1">
    {#if activeTab === 'inputs'}
      {#if hasInputs}
        {#each inputLinkIds as linkId, i}
          <DataLinkRow
            slotType="input"
            slotIndex={i}
            slotName={driverDefinition.inputNames[i] || `Input ${i + 1}`}
            currentLinkId={linkId}
            editable={driverDefinition.editableInputs}
            {dataLinks}
            onAssign={(newId) => onInputLinkChange(i, newId)}
          />
        {/each}
      {:else}
        <div class="py-6 text-center text-xs text-gray-500">
          No input slots for this driver.
        </div>
      {/if}
    {:else if activeTab === 'outputs'}
      {#if hasOutputs}
        {#each outputLinkIds as linkId, i}
          <DataLinkRow
            slotType="output"
            slotIndex={i}
            slotName={driverDefinition.outputNames[i] || `Output ${i + 1}`}
            currentLinkId={linkId}
            editable={driverDefinition.editableOutputs}
            {dataLinks}
            onAssign={(newId) => onOutputLinkChange(i, newId)}
          />
        {/each}
      {:else}
        <div class="py-6 text-center text-xs text-gray-500">
          No output slots for this driver.
        </div>
      {/if}
    {:else if activeTab === 'tables'}
      {#if hasRelatedTables}
        <div class="flex flex-col gap-3 h-full">
          {#each relatedTables as table (table.id)}
            {@const is1D = table.rows <= 1 || table.tableType.startsWith('T1x')}
            <div class="w-full {is1D ? 'h-16' : 'flex-1 min-h-0'}">
              <TableWidget
                tableId={table.id}
                tableName={table.name}
                onTap={handleTableTap}
                showDimensionBadge={false}
                maxFontSize={22}
              />
            </div>
          {/each}
        </div>
      {:else}
        <div class="py-6 text-center text-xs text-gray-500">
          No related tables in this category.
        </div>
      {/if}
    {:else if activeTab === 'links'}
      {#if hasRelatedLinks}
        {#each relatedLinks as link (link.id)}
          <RelatedLinkRow {link} />
        {/each}
      {:else}
        <div class="py-6 text-center text-xs text-gray-500">
          No related data links in this category.
        </div>
      {/if}
    {/if}
  </div>
</div>
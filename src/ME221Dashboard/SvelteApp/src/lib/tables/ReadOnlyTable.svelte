<script lang="ts">
  import type { TableDefinition, TableData, ColorScheme } from './types';
  import { is1DTable } from './types';
  import TableGrid from './TableGrid.svelte';
  import CurveEditor from './CurveEditor.svelte';

  let { tableDef, currentData, importData, axisMode = 'import', colorScheme = 'thermal' }: {
    tableDef: TableDefinition;
    currentData: TableData;
    importData: TableData;
    axisMode?: 'import' | 'current';
    colorScheme?: ColorScheme;
  } = $props();

  // Determine axes + output based on axis mode
  const displayData = $derived.by(() => {
    if (axisMode === 'current') {
      // Use current ECU axes, import values
      return {
        enabled: importData.enabled,
        input0: [...currentData.input0],
        input1: [...currentData.input1],
        output: [...importData.output]
      };
    }
    // Default: import mode
    return importData;
  });

  const displayDef = $derived.by(() => {
    if (axisMode === 'current') {
      return { ...tableDef };
    }
    return tableDef;
  });

  const minVal = $derived(Math.min(...displayData.output));
  const maxVal = $derived(Math.max(...displayData.output));
</script>

<div class="overflow-auto rounded border" style="border-color: var(--metro-border, #333); background-color: var(--metro-bg, #1a1a2e); max-height: 400px;">
  {#if is1DTable(tableDef)}
    <CurveEditor
      tableDef={displayDef}
      tableData={displayData}
      selectedCol={-1}
      opColRange={null}
      {minVal}
      {maxVal}
      anchor={null}
      selection={null}
      selectionType="output"
      dirtyCells={new Set()}
      dirtyInput0={new Set()}
      diffMode={true}
      originalData={currentData}
      {colorScheme}
      liveOutputValue={null}
      onCellClick={() => {}}
      onAxis0Click={() => {}}
      onAnchorSet={() => {}}
      onSelectionComplete={() => {}}
      onSelectionClear={() => {}}
      onContextMenu={() => {}}
    />
  {:else}
    <TableGrid
      tableDef={displayDef}
      tableData={displayData}
      selectedRow={-1}
      selectedCol={-1}
      editMode="output"
      opRowRange={null}
      opColRange={null}
      dirtyCells={new Set()}
      dirtyInput0={new Set()}
      dirtyInput1={new Set()}
      {minVal}
      {maxVal}
      anchor={null}
      selection={null}
      selectionType="output"
      diffMode={true}
      originalData={currentData}
      {colorScheme}
      showContours={false}
      liveOutputValue={null}
      opHistory={[]}
      onCellClick={() => {}}
      onAxis0Click={() => {}}
      onAxis1Click={() => {}}
      onAnchorSet={() => {}}
      onSelectionComplete={() => {}}
      onSelectionClear={() => {}}
      onContextMenu={() => {}}
    />
  {/if}
</div>

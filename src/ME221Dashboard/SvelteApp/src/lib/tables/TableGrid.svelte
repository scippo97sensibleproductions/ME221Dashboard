<script lang="ts">
  import type { TableDefinition, TableData } from './types';
  import { cellKey, getOutputValue, heatColor } from './types';

  let { tableDef, tableData, selectedRow, selectedCol, editMode, opRow, opCol, dirtyCells, dirtyInput0, dirtyInput1, minVal, maxVal, anchor, selection, selectionType = 'output', diffMode = false, originalData = null, onCellClick, onAxis0Click, onAxis1Click, onAnchorSet, onSelectionComplete, onSelectionClear }: {
    tableDef: TableDefinition;
    tableData: TableData;
    selectedRow: number;
    selectedCol: number;
    editMode: 'output' | 'input0' | 'input1';
    opRow: number;
    opCol: number;
    dirtyCells: Set<string>;
    dirtyInput0: Set<number>;
    dirtyInput1: Set<number>;
    minVal: number;
    maxVal: number;
    anchor: { row: number; col: number } | null;
    selection: { startRow: number; startCol: number; endRow: number; endCol: number } | null;
    selectionType?: 'output' | 'input0' | 'input1';
    diffMode?: boolean;
    originalData?: TableData | null;
    onCellClick: (row: number, col: number) => void;
    onAxis0Click: (col: number) => void;
    onAxis1Click: (row: number) => void;
    onAnchorSet: (row: number, col: number, type?: 'output' | 'input0' | 'input1') => void;
    onSelectionComplete: (row: number, col: number) => void;
    onSelectionClear: () => void;
  } = $props();

  let cellW = $derived(isMobile ? 42 : 64);
  let gridStyle = $derived(isMobile
    ? `grid-template-columns: 40px repeat(${tableDef.cols}, ${cellW}px); grid-template-rows: 24px repeat(${tableDef.rows}, 1fr); font-size: 8px; height: 100%;`
    : `grid-template-columns: 48px repeat(${tableDef.cols}, ${cellW}px); grid-template-rows: 28px repeat(${tableDef.rows}, 28px); font-size: 10px;`);

  let isMobile = $state(false);
  let mql: MediaQueryList | null = null;

  function checkMobile() {
    isMobile = mql?.matches ?? false;
  }

  $effect(() => {
    mql = window.matchMedia('(max-width: 768px)');
    checkMobile();
    mql.addEventListener('change', checkMobile);
    return () => mql?.removeEventListener('change', checkMobile);
  });

  let longPressTimer = 0;
  let longPressTriggered = false;
  let longPressStartX: number | null = null;
  let longPressStartY: number | null = null;
  let selectionJustCompleted = false;

  let sMinR = $derived(selection ? Math.min(selection.startRow, selection.endRow) : -1);
  let sMaxR = $derived(selection ? Math.max(selection.startRow, selection.endRow) : -1);
  let sMinC = $derived(selection ? Math.min(selection.startCol, selection.endCol) : -1);
  let sMaxC = $derived(selection ? Math.max(selection.startCol, selection.endCol) : -1);
  let hasSelection = $derived(selection !== null);

  function highlightOutput(r: number, c: number): boolean {
    if (!hasSelection || selectionType !== 'output') return false;
    return r >= sMinR && r <= sMaxR && c >= sMinC && c <= sMaxC;
  }

  function highlightColHeader(c: number): boolean {
    if (!hasSelection || selectionType !== 'input0') return false;
    return c >= sMinC && c <= sMaxC;
  }

  function highlightRowHeader(r: number): boolean {
    if (!hasSelection || selectionType !== 'input1') return false;
    return r >= sMinR && r <= sMaxR;
  }

  function handlePointerDown(e: PointerEvent, r: number, c: number, type: 'output' | 'input0' | 'input1') {
    longPressTriggered = false;
    selectionJustCompleted = false;
    longPressStartX = e.clientX;
    longPressStartY = e.clientY;
    longPressTimer = window.setTimeout(() => {
      longPressTriggered = true;
      if (anchor && !selection) {
        onSelectionComplete(r, c);
      } else {
        onAnchorSet(r, c, type);
      }
    }, 500);
  }

  function handlePointerUp(e: PointerEvent, r: number, c: number) {
    clearTimeout(longPressTimer);
    if (longPressTriggered) { e.preventDefault(); return; }
    if (anchor && !selection) {
      onSelectionComplete(r, c);
      selectionJustCompleted = true;
      e.preventDefault();
    }
  }

  function handlePointerCancel() { clearTimeout(longPressTimer); }
  function handlePointerMove(e: PointerEvent) {
    if (longPressStartX !== null && longPressStartY !== null) {
      const dx = e.clientX - longPressStartX;
      const dy = e.clientY - longPressStartY;
      if (dx * dx + dy * dy > 100) {
        clearTimeout(longPressTimer);
      }
    }
  }

  function handleCellClick(r: number, c: number, type: 'output' | 'input0' | 'input1') {
    if (longPressTriggered || selectionJustCompleted) return;
    if (anchor && !selection) {
      onSelectionComplete(r, c);
    } else {
      if (selection) onSelectionClear();
      if (type === 'input0') onAxis0Click(c);
      else if (type === 'input1') onAxis1Click(r);
      else onCellClick(r, c);
    }
  }
</script>

<style>
  [role="grid"] * {
    user-select: none;
    -webkit-user-select: none;
    -webkit-touch-callout: none;
    touch-action: manipulation;
  }
</style>

<div class="h-full overflow-auto select-none" role="grid" style="touch-action: pan-x pan-y;">
  <div class="min-h-full {isMobile ? 'h-full p-2' : 'flex items-center justify-center p-4'}">
      <div
        class="grid w-fit"
        style="{gridStyle}"
      >
        <!-- Corner -->
        <div
          class="sticky left-0 top-0 z-30 flex items-center justify-center px-0.5 text-[6px] sm:text-[7px] leading-tight overflow-hidden"
          style="border: 1px solid var(--metro-border); background-color: var(--metro-card); color: var(--metro-text-muted);"
          onclick={onSelectionClear}
          onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onSelectionClear(); } }}
          role="columnheader"
          tabindex="-1"
        >
          {tableDef.input1Name}&rarr;<br>&darr;{tableDef.input0Name}
        </div>

        <!-- Column headers -->
        {#each tableData.input0 as colVal, c (c)}
          {@const isAnc = selectionType === 'input0' && anchor?.row === 0 && anchor?.col === c}
          {@const inSel = highlightColHeader(c)}
          {@const isCurrent = c === selectedCol && editMode === 'input0'}
          <div
            class="sticky top-0 z-20 flex cursor-pointer items-center justify-center text-center transition-colors duration-150
                   {isAnc ? 'z-[25]' :
                    inSel ? '' :
                    isCurrent ? '' :
                    dirtyInput0.has(c) ? '' : ''}
                   hover:text-white"
            style="border: 1px solid var(--metro-border); background-color: {isAnc ? 'rgba(216,59,1,0.3)' : inSel ? 'rgba(216,59,1,0.25)' : 'var(--metro-sidebar)'}; color: {isAnc || inSel || isCurrent ? 'var(--metro-orange)' : dirtyInput0.has(c) ? 'var(--metro-yellow)' : 'var(--metro-text-secondary)'};"
            role="columnheader"
            tabindex="-1"
            onclick={() => handleCellClick(0, c, 'input0')}
            onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); handleCellClick(0, c, 'input0'); } }}
            onpointerdown={(e) => handlePointerDown(e, 0, c, 'input0')}
            onpointerup={(e) => handlePointerUp(e, 0, c)}
            onpointermove={handlePointerMove}
            onpointercancel={handlePointerCancel}
          >
            {typeof colVal === 'number' ? colVal.toFixed(2) : colVal.toLocaleString()}
          </div>
        {/each}

        <!-- Row groups -->
        {#each tableData.input1 as rowVal, r (r)}
          {@const isAnc = selectionType === 'input1' && anchor?.row === r && anchor?.col === 0}
          {@const inSel = highlightRowHeader(r)}
          {@const isCurrent = r === selectedRow && editMode === 'input1'}
          <div
            class="sticky left-0 z-20 flex min-w-0 cursor-pointer items-center justify-center overflow-hidden pr-1 text-right transition-colors duration-150
                   {isAnc ? 'z-[25]' :
                    inSel ? '' :
                    isCurrent ? '' :
                    dirtyInput1.has(r) ? '' : ''}
                   hover:text-white"
            style="border: 1px solid var(--metro-border); background-color: {isAnc ? 'rgba(216,59,1,0.3)' : inSel ? 'rgba(216,59,1,0.25)' : 'var(--metro-sidebar)'}; color: {isAnc || inSel || isCurrent ? 'var(--metro-orange)' : dirtyInput1.has(r) ? 'var(--metro-yellow)' : 'var(--metro-text-secondary)'};"
            role="rowheader"
            tabindex="-1"
            onclick={() => handleCellClick(r, 0, 'input1')}
            onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); handleCellClick(r, 0, 'input1'); } }}
            onpointerdown={(e) => handlePointerDown(e, r, 0, 'input1')}
            onpointerup={(e) => handlePointerUp(e, r, 0)}
            onpointermove={handlePointerMove}
            onpointercancel={handlePointerCancel}
          >
            {typeof rowVal === 'number' ? rowVal.toFixed(2) : rowVal}
          </div>

          {#each tableData.input0 as _, c (c)}
            {@const val = getOutputValue(tableData, r, c, tableDef.cols)}
            {@const origVal = diffMode && originalData ? getOutputValue(originalData, r, c, tableDef.cols) : val}
            {@const delta = diffMode ? val - origVal : 0}
            {@const color = diffMode
              ? (Math.abs(delta) < 0.001 ? 'rgb(100, 100, 100)' : delta > 0 ? 'rgb(34, 139, 80)' : 'rgb(200, 50, 50)')
              : heatColor(val, minVal, maxVal)}
            {@const isCurrentCell = r === selectedRow && c === selectedCol && editMode === 'output'}
            {@const isDirty = dirtyCells.has(cellKey(r, c))}
            {@const isOpCell = r === opRow && c === opCol}
            {@const isAnc = selectionType === 'output' && anchor?.row === r && anchor?.col === c}
            {@const inSel = highlightOutput(r, c)}
            <div
              class="relative flex cursor-pointer items-center justify-center text-center font-medium transition-colors duration-150
                     {isAnc ? 'z-[2]' : ''}
                     {isCurrentCell && !inSel ? 'z-[2]' : ''}
                     {r === opRow ? 'border-t-2 border-t-white/20' : ''}
                     {c === opCol ? 'border-l-2 border-l-white/20' : ''}"
              style="border: 1px solid var(--metro-border); background: {isAnc ? 'rgba(216,59,1,0.3)' : inSel ? 'rgba(216,59,1,0.18)' : color}; {isCurrentCell && !inSel ? 'outline: 2px solid var(--metro-orange); outline-offset: -2px;' : ''} {isDirty && !diffMode && !inSel ? 'outline: 2px solid var(--metro-yellow); outline-offset: -2px;' : ''} {inSel && !isAnc ? 'outline: 2px inset var(--metro-orange); outline-offset: -2px;' : ''}"
              role="gridcell"
              tabindex="-1"
              onclick={() => handleCellClick(r, c, 'output')}
              onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); handleCellClick(r, c, 'output'); } }}
              onpointerdown={(e) => handlePointerDown(e, r, c, 'output')}
              onpointerup={(e) => handlePointerUp(e, r, c)}
              onpointermove={handlePointerMove}
              onpointercancel={handlePointerCancel}
            >
              <span class="pointer-events-none" style="color: {diffMode && Math.abs(delta) > 0.001 ? '#fff' : inSel ? 'var(--metro-orange)' : ''};">
                {diffMode && originalData ? (delta > 0 ? '+' : '') + delta.toFixed(2) : val.toFixed(2)}
              </span>
              {#if isAnc}
                <div class="pointer-events-none absolute bottom-0.5 left-1/2 h-1 w-1 -translate-x-1/2 rounded-full" style="background-color: var(--metro-orange);"></div>
              {/if}
              {#if isOpCell}
                <div class="pointer-events-none absolute -left-1 -top-1 h-2 w-2 rounded-full bg-white" style="filter: drop-shadow(0 0 4px rgba(255,255,255,0.5));"></div>
              {/if}
            </div>
          {/each}
        {/each}
      </div>
    </div>
</div>

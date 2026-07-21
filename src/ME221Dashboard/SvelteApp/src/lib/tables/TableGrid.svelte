<script lang="ts">
  import type { TableDefinition, TableData, ColorScheme, InterpolationRange } from './types';
  import type { OperatingPointSample } from '../stores/LiveDataStore.svelte';
  import { cellKey, getOutputValue, heatColor, formatValueAdaptive, DataType, rangeOpacity, fromRaw, findInterpolationRange } from './types';

  let { tableDef, tableData, selectedRow, selectedCol, editMode, opRowRange, opColRange, dirtyCells, dirtyInput0, dirtyInput1, minVal, maxVal, anchor, selection, selectionType = 'output', diffMode = false, originalData = null, colorScheme = 'thermal', showContours = false, liveOutputValue = null, opHistory = [], onCellClick, onAxis0Click, onAxis1Click, onAnchorSet, onSelectionComplete, onSelectionClear, onContextMenu }: {
    tableDef: TableDefinition;
    tableData: TableData;
    selectedRow: number;
    selectedCol: number;
    editMode: 'output' | 'input0' | 'input1';
    opRowRange: InterpolationRange | null;
    opColRange: InterpolationRange | null;
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
    colorScheme?: ColorScheme;
    showContours?: boolean;
    liveOutputValue?: number | null;
    opHistory?: OperatingPointSample[];
    onCellClick: (row: number, col: number) => void;
    onAxis0Click: (col: number) => void;
    onAxis1Click: (row: number) => void;
    onAnchorSet: (row: number, col: number, type?: 'output' | 'input0' | 'input1') => void;
    onSelectionComplete: (row: number, col: number) => void;
    onSelectionClear: () => void;
    onContextMenu?: (e: MouseEvent, row: number, col: number, type: 'output' | 'input0' | 'input1') => void;
  } = $props();

  let cellW = $derived(isMobile ? 42 : 64);
  let gridStyle = $derived(isMobile
    ? `grid-template-columns: 40px repeat(${tableDef.cols}, ${cellW}px); grid-template-rows: 24px repeat(${tableDef.rows}, 1fr); font-size: 8px; height: 100%;`
    : `grid-template-columns: 48px repeat(${tableDef.cols}, ${cellW}px); grid-template-rows: 28px repeat(${tableDef.rows}, 28px); font-size: 10px;`);

  // Max absolute delta for diff mode gradient normalization
  let maxDelta = $derived.by(() => {
    if (!diffMode || !originalData) return 1;
    let mx = 0;
    for (let r = 0; r < tableDef.rows; r++) {
      for (let c = 0; c < tableDef.cols; c++) {
        const cur = getOutputValue(originalData, r, c, tableDef.cols);
        const imp = getOutputValue(tableData, r, c, tableDef.cols);
        const d = Math.abs(imp - cur);
        if (d > mx) mx = d;
      }
    }
    return mx > 0.001 ? mx : 1;
  });

  function diffColor(delta: number, maxD: number): string {
    if (Math.abs(delta) < 0.001) return 'rgb(100, 100, 100)';
    const t = Math.min(1, Math.abs(delta) / maxD);
    // Smooth curve: low changes are subtle, high changes are vivid
    const intensity = Math.sqrt(t);
    if (delta > 0) {
      const g = Math.round(80 + intensity * 159);
      return `rgb(30, ${g}, 60)`;
    } else {
      const r = Math.round(120 + intensity * 135);
      return `rgb(${r}, 40, 40)`;
    }
  }

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

  // Contour line computation
  const CONTOUR_LEVELS = 6;
  const axisW = $derived(isMobile ? 40 : 48);
  const headerH = $derived(isMobile ? 24 : 28);

  function lerp(v1: number, v2: number, level: number): number {
    const d = v2 - v1;
    if (Math.abs(d) < 1e-10) return 0.5;
    return (level - v1) / d;
  }

  let contourPaths = $derived.by(() => {
    if (!showContours || !tableData || !tableDef) return [];
    const rows = tableDef.rows;
    const cols = tableDef.cols;
    if (rows < 2 || cols < 2) return [];

    const grid: number[][] = [];
    for (let r = 0; r < rows; r++) {
      const row: number[] = [];
      for (let c = 0; c < cols; c++) {
        row.push(getOutputValue(tableData, r, c, cols));
      }
      grid.push(row);
    }

    const levels: number[] = [];
    for (let i = 1; i <= CONTOUR_LEVELS; i++) {
      levels.push(minVal + (maxVal - minVal) * (i / (CONTOUR_LEVELS + 1)));
    }

    const paths: string[] = [];
    for (const level of levels) {
      for (let r = 0; r < rows - 1; r++) {
        for (let c = 0; c < cols - 1; c++) {
          const tl = grid[r][c];
          const tr = grid[r][c + 1];
          const br = grid[r + 1][c + 1];
          const bl = grid[r + 1][c];

          let ci = 0;
          if (tl >= level) ci |= 8;
          if (tr >= level) ci |= 4;
          if (br >= level) ci |= 2;
          if (bl >= level) ci |= 1;

          if (ci === 0 || ci === 15) continue;

          const top    = { x: c + lerp(tl, tr, level), y: r };
          const right  = { x: c + 1, y: r + lerp(tr, br, level) };
          const bottom = { x: c + lerp(bl, br, level), y: r + 1 };
          const left   = { x: c, y: r + lerp(tl, bl, level) };

          const segs: [typeof top, typeof top][] = [];
          switch (ci) {
            case 1:  segs.push([left, bottom]); break;
            case 2:  segs.push([bottom, right]); break;
            case 3:  segs.push([left, right]); break;
            case 4:  segs.push([top, right]); break;
            case 5:  segs.push([left, top], [bottom, right]); break;
            case 6:  segs.push([top, bottom]); break;
            case 7:  segs.push([left, top]); break;
            case 8:  segs.push([top, left]); break;
            case 9:  segs.push([top, bottom]); break;
            case 10: segs.push([top, right], [left, bottom]); break;
            case 11: segs.push([top, right]); break;
            case 12: segs.push([left, right]); break;
            case 13: segs.push([bottom, right]); break;
            case 14: segs.push([left, bottom]); break;
          }

          for (const [a, b] of segs) {
            const x1 = axisW + a.x * cellW;
            const y1 = headerH + a.y * (isMobile ? 28 : 28);
            const x2 = axisW + b.x * cellW;
            const y2 = headerH + b.y * (isMobile ? 28 : 28);
            paths.push(`M${x1.toFixed(1)},${y1.toFixed(1)}L${x2.toFixed(1)},${y2.toFixed(1)}`);
          }
        }
      }
    }
    return paths;
  });

  const contourSvgW = $derived(axisW + tableDef.cols * cellW);
  const contourSvgH = $derived(headerH + tableDef.rows * (isMobile ? 28 : 28));

  const MAX_GHOST_DOTS = 50;

  let ghostTrailDots = $derived.by(() => {
    if (!opHistory || opHistory.length === 0 || !tableData) return [];
    const step = Math.max(1, Math.floor(opHistory.length / MAX_GHOST_DOTS));
    const dots: { x: number; y: number; opacity: number }[] = [];
    const len = opHistory.length;
    for (let i = 0; i < len; i += step) {
      const sample = opHistory[i];
      const raw0 = tableDef.input0LinkId ? sample.values[String(tableDef.input0LinkId)] : null;
      if (raw0 == null) continue;
      const x = fromRaw(raw0, tableDef.input0UnitType ?? 0);
      const colRange = findInterpolationRange(x, tableData.input0);
      let colFrac = colRange.lower + (colRange.lower === colRange.upper ? 0.5 : colRange.fraction);
      colFrac = Math.max(0, Math.min(tableDef.cols, colFrac));
      let rowFrac = 0.5;
      if (tableDef.input1LinkId && tableDef.input1LinkId !== 0) {
        const raw1 = sample.values[String(tableDef.input1LinkId)];
        if (raw1 == null) continue;
        const y = fromRaw(raw1, tableDef.input1UnitType ?? 0);
        const rowRange = findInterpolationRange(y, tableData.input1);
        rowFrac = rowRange.lower + (rowRange.lower === rowRange.upper ? 0.5 : rowRange.fraction);
        rowFrac = Math.max(0, Math.min(tableDef.rows, rowFrac));
      }
      const dotX = axisW + colFrac * cellW;
      const dotY = headerH + rowFrac * (isMobile ? 28 : 28);
      const t = len > 1 ? i / (len - 1) : 1;
      const opacity = 0.1 + 0.9 * t;
      dots.push({ x: dotX, y: dotY, opacity });
    }
    return dots;
  });

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

  function handleCellClick(r: number, c: number, type: 'output' | 'input0' | 'input1', e?: MouseEvent) {
    if (longPressTriggered || selectionJustCompleted) return;

    // Shift+Click: extend selection from anchor (or current cell) to clicked cell
    if (e?.shiftKey) {
      if (!anchor) {
        onAnchorSet(selectedRow ?? 0, selectedCol ?? 0, selectionType);
      }
      if (anchor) {
        onSelectionComplete(r, c);
      }
      return;
    }

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
        class="grid w-fit relative"
        style="{gridStyle}"
      >
        {#if showContours && contourPaths.length > 0}
          <svg
            class="pointer-events-none absolute"
            style="z-index: 5;"
            width={contourSvgW}
            height={contourSvgH}
          >
            {#each contourPaths as d}
              <path {d} fill="none" stroke="rgba(255,255,255,0.45)" stroke-width="1" stroke-dasharray="4,3" />
            {/each}
          </svg>
        {/if}
        {#if ghostTrailDots.length > 0}
          <div class="pointer-events-none absolute" style="z-index: 6; left: 0; top: 0; width: {contourSvgW}px; height: {contourSvgH}px;">
            {#each ghostTrailDots as dot}
              <div
                class="absolute rounded-full"
                style="left: {dot.x - 2}px; top: {dot.y - 2}px; width: 4px; height: 4px; background: rgba(255, 255, 255, {dot.opacity.toFixed(2)});"
                aria-hidden="true"
              ></div>
            {/each}
          </div>
        {/if}
        <!-- Corner -->
        <div
          class="sticky left-0 top-0 z-30 flex items-center justify-center px-0.5 text-[6px] sm:text-[7px] leading-tight overflow-hidden"
          style="border: 1px solid var(--metro-border); background-color: var(--metro-card); color: var(--metro-text-muted);"
          onclick={onSelectionClear}
          onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onSelectionClear(); } }}
          role="columnheader"
          tabindex="-1"
        >
          {tableDef.input1Name}{tableDef.input1Unit ? ` (${tableDef.input1Unit})` : ''}&rarr;<br>&darr;{tableDef.input0Name}{tableDef.input0Unit ? ` (${tableDef.input0Unit})` : ''}
        </div>

        <!-- Column headers -->
        {#each tableData.input0 as colVal, c (c)}
          {@const isAnc = selectionType === 'input0' && anchor?.row === 0 && anchor?.col === c}
          {@const inSel = highlightColHeader(c)}
          {@const isCurrent = c === selectedCol && editMode === 'input0'}
          {@const opColOp = opColRange ? rangeOpacity(opColRange, c) : 0}
          {@const isInRange = opColOp > 0}
          <div
            class="sticky top-0 z-20 flex cursor-pointer items-center justify-center text-center transition-colors duration-150
                   {isAnc ? 'z-[25]' :
                    inSel ? '' :
                    isCurrent ? '' :
                    dirtyInput0.has(c) ? '' : ''}
                   hover:text-white"
            style="border: 1px solid var(--metro-border); background-color: {isInRange ? `rgba(255,255,255,${(0.04 + 0.06 * opColOp).toFixed(2)})` : isAnc ? 'rgba(216,59,1,0.3)' : inSel ? 'rgba(216,59,1,0.25)' : 'var(--metro-sidebar)'}; color: {isInRange ? '#fff' : isAnc || inSel || isCurrent ? 'var(--metro-orange)' : dirtyInput0.has(c) ? 'var(--metro-yellow)' : 'var(--metro-text-secondary)'};"
            role="columnheader"
            tabindex="-1"
            onclick={(e) => handleCellClick(0, c, 'input0', e)}
            oncontextmenu={(e) => onContextMenu?.(e, 0, c, 'input0')}
            onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); handleCellClick(0, c, 'input0'); } }}
            onpointerdown={(e) => handlePointerDown(e, 0, c, 'input0')}
            onpointerup={(e) => handlePointerUp(e, 0, c)}
            onpointermove={handlePointerMove}
            onpointercancel={handlePointerCancel}
          >
            {typeof colVal === 'number' ? formatValueAdaptive(colVal, tableDef.input0DataType) : colVal.toLocaleString()}
          </div>
        {/each}

        <!-- Row groups -->
        {#each tableData.input1 as rowVal, r (r)}
          {@const isAnc = selectionType === 'input1' && anchor?.row === r && anchor?.col === 0}
          {@const inSel = highlightRowHeader(r)}
          {@const isCurrent = r === selectedRow && editMode === 'input1'}
          {@const opRowOp = opRowRange ? rangeOpacity(opRowRange, r) : 0}
          {@const isInRange = opRowOp > 0}
          <div
            class="sticky left-0 z-20 flex min-w-0 cursor-pointer items-center justify-center overflow-hidden pr-1 text-right transition-colors duration-150
                   {isAnc ? 'z-[25]' :
                    inSel ? '' :
                    isCurrent ? '' :
                    dirtyInput1.has(r) ? '' : ''}
                   hover:text-white"
            style="border: 1px solid var(--metro-border); background-color: {isInRange ? `rgba(255,255,255,${(0.04 + 0.06 * opRowOp).toFixed(2)})` : isAnc ? 'rgba(216,59,1,0.3)' : inSel ? 'rgba(216,59,1,0.25)' : 'var(--metro-sidebar)'}; color: {isInRange ? '#fff' : isAnc || inSel || isCurrent ? 'var(--metro-orange)' : dirtyInput1.has(r) ? 'var(--metro-yellow)' : 'var(--metro-text-secondary)'};"
            role="rowheader"
            tabindex="-1"
            onclick={(e) => handleCellClick(r, 0, 'input1', e)}
            oncontextmenu={(e) => onContextMenu?.(e, r, 0, 'input1')}
            onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); handleCellClick(r, 0, 'input1'); } }}
            onpointerdown={(e) => handlePointerDown(e, r, 0, 'input1')}
            onpointerup={(e) => handlePointerUp(e, r, 0)}
            onpointermove={handlePointerMove}
            onpointercancel={handlePointerCancel}
          >
            {typeof rowVal === 'number' ? formatValueAdaptive(rowVal, tableDef.input1DataType) : rowVal}
          </div>

          {#each tableData.input0 as _, c (c)}
            {@const val = getOutputValue(tableData, r, c, tableDef.cols)}
            {@const origVal = diffMode && originalData ? getOutputValue(originalData, r, c, tableDef.cols) : val}
            {@const delta = diffMode ? val - origVal : 0}
            {@const color = diffMode
              ? diffColor(delta, maxDelta)
              : heatColor(val, minVal, maxVal, colorScheme)}
            {@const isCurrentCell = r === selectedRow && c === selectedCol && editMode === 'output'}
            {@const isDirty = dirtyCells.has(cellKey(r, c))}
            {@const opRowOp = opRowRange ? rangeOpacity(opRowRange, r) : 0}
            {@const opColOp = opColRange ? rangeOpacity(opColRange, c) : 0}
            {@const opCellOpacity = Math.min(opRowOp, opColOp)}
            {@const isInRange = opCellOpacity > 0}
            {@const isRowInRange = opRowOp > 0}
            {@const isColInRange = opColOp > 0}
            {@const borderAlpha = (0.15 + 0.7 * opCellOpacity).toFixed(2)}
            {@const outlineAlpha = (0.2 + 0.7 * opCellOpacity).toFixed(2)}
            {@const tintAlpha = (0.1 * Math.max(opRowOp, opColOp)).toFixed(2)}
            {@const isAnc = selectionType === 'output' && anchor?.row === r && anchor?.col === c}
            {@const inSel = highlightOutput(r, c)}
            <div
              class="relative flex cursor-pointer items-center justify-center text-center font-medium transition-colors duration-150
                     {isAnc ? 'z-[2]' : ''}
                     {isCurrentCell && !inSel ? 'z-[2]' : ''}"
              style="border: 1px solid {isInRange ? `rgba(255,255,255,${borderAlpha})` : isRowInRange || isColInRange ? 'rgba(255,255,255,0.12)' : 'var(--metro-border)'}; background: {isAnc ? 'rgba(216,59,1,0.3)' : inSel ? 'rgba(216,59,1,0.18)' : isRowInRange || isColInRange ? `color-mix(in srgb, ${color} ${Math.round((1 - parseFloat(tintAlpha)) * 100)}%, rgba(255,255,255,${tintAlpha}))` : color}; {isInRange ? `outline: 2px solid rgba(255,255,255,${outlineAlpha}); outline-offset: -2px; z-index: 3;` : ''} {isCurrentCell && !inSel ? 'outline: 2px solid var(--metro-orange); outline-offset: -2px;' : ''} {isDirty && !diffMode && !inSel && !isInRange ? 'outline: 2px solid var(--metro-yellow); outline-offset: -2px;' : ''} {inSel && !isAnc ? 'outline: 2px inset var(--metro-orange); outline-offset: -2px;' : ''}"
              role="gridcell"
              tabindex="-1"
              onclick={(e) => handleCellClick(r, c, 'output', e)}
              oncontextmenu={(e) => onContextMenu?.(e, r, c, 'output')}
              onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); handleCellClick(r, c, 'output'); } }}
              onpointerdown={(e) => handlePointerDown(e, r, c, 'output')}
              onpointerup={(e) => handlePointerUp(e, r, c)}
              onpointermove={handlePointerMove}
              onpointercancel={handlePointerCancel}
            >
              <span class="pointer-events-none" style="color: {diffMode && Math.abs(delta) > 0.001 ? '#fff' : inSel ? 'var(--metro-orange)' : isInRange ? '#fff' : ''};">
                {diffMode && originalData ? (delta > 0 ? '+' : '') + formatValueAdaptive(delta, tableDef.outputDataType) : formatValueAdaptive(val, tableDef.outputDataType)}
              </span>
              {#if isAnc}
                <div class="pointer-events-none absolute bottom-0.5 left-1/2 h-1 w-1 -translate-x-1/2 rounded-full" style="background-color: var(--metro-orange);"></div>
              {/if}
            </div>
          {/each}
        {/each}
      {#if opColRange && opRowRange && liveOutputValue !== null}
        {@const dotX = axisW + (opColRange.lower + (opColRange.lower === opColRange.upper ? 0.5 : opColRange.fraction)) * cellW}
        {@const dotY = headerH + (opRowRange.lower + (opRowRange.lower === opRowRange.upper ? 0.5 : opRowRange.fraction)) * (isMobile ? 24 : 28)}
        <div class="pointer-events-none absolute rounded-full" style="left: {dotX - 5}px; top: {dotY - 5}px; width: 10px; height: 10px; background: #ffffff; box-shadow: 0 0 6px rgba(255,255,255,0.95), 0 0 0 2px rgba(0,0,0,0.4); z-index: 20;" aria-hidden="true"></div>
      {/if}
    </div>
  </div>
</div>

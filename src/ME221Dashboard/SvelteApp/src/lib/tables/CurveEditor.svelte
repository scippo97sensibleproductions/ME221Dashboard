<script lang="ts">
  import type { TableDefinition, TableData, ColorScheme, InterpolationRange } from './types';
  import { cellKey, heatColor, formatValueAdaptive, DataType, rangeOpacity } from './types';
  import { IconDownload } from '@tabler/icons-svelte';
  import { HybridBridge } from '../HybridBridge';

  let { tableDef, tableData, selectedCol, opColRange, minVal, maxVal, anchor, selection, selectionType = 'output', dirtyCells, dirtyInput0, diffMode = false, originalData = null, colorScheme = 'thermal', liveOutputValue = null, onCellClick, onAxis0Click, onAnchorSet, onSelectionComplete, onSelectionClear, onContextMenu }: {
    tableDef: TableDefinition;
    tableData: TableData;
    selectedCol: number;
    opColRange: InterpolationRange | null;
    minVal: number;
    maxVal: number;
    anchor: { row: number; col: number } | null;
    selection: { startRow: number; startCol: number; endRow: number; endCol: number } | null;
    selectionType?: 'output' | 'input0';
    dirtyCells: Set<string>;
    dirtyInput0: Set<number>;
    diffMode?: boolean;
    originalData?: TableData | null;
    colorScheme?: ColorScheme;
    liveOutputValue?: number | null;
    onCellClick: (row: number, col: number) => void;
    onAxis0Click: (col: number) => void;
    onAnchorSet: (row: number, col: number, type?: 'output' | 'input0') => void;
    onSelectionComplete: (row: number, col: number) => void;
    onSelectionClear: () => void;
    onContextMenu?: (e: MouseEvent, row: number, col: number, type: 'output' | 'input0') => void;
  } = $props();

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

  // Max absolute delta for diff mode gradient normalization
  let maxDelta = $derived.by(() => {
    if (!diffMode || !originalData) return 1;
    let mx = 0;
    const len = Math.min(tableData.output.length, originalData.output.length);
    for (let i = 0; i < len; i++) {
      const d = Math.abs(tableData.output[i] - originalData.output[i]);
      if (d > mx) mx = d;
    }
    return mx > 0.001 ? mx : 1;
  });

  function diffColor(delta: number, maxD: number): string {
    if (Math.abs(delta) < 0.001) return 'rgb(100, 100, 100)';
    const t = Math.min(1, Math.abs(delta) / maxD);
    const intensity = Math.sqrt(t);
    if (delta > 0) {
      const g = Math.round(80 + intensity * 159);
      return `rgb(30, ${g}, 60)`;
    } else {
      const r = Math.round(120 + intensity * 135);
      return `rgb(${r}, 40, 40)`;
    }
  }

  let containerEl = $state<HTMLDivElement>();
  let svgEl = $state<SVGSVGElement>();
  let w = $state(0);
  let h = $state(0);

  $effect(() => {
    if (!containerEl) return;
    const ro = new ResizeObserver(entries => {
      for (const entry of entries) {
        const { width, height } = entry.contentRect;
        if (width > 0 && height > 0) {
          w = width;
          h = height;
        }
      }
    });
    ro.observe(containerEl);
    return () => ro.disconnect();
  });

  const pad = { top: 20, right: 20, bottom: 30, left: 50 };
  let cw = $derived(w - pad.left - pad.right);
  let ch = $derived(h - pad.top - pad.bottom);

  let axis = $derived(tableData.input0);
  let values = $derived.by(() => {
    const result = [];
    for (let i = 0; i < axis.length; i++) {
      result.push({ x: axis[i], y: tableData.output[i] });
    }
    return result;
  });

  let origValues = $derived.by(() => {
    if (!diffMode || !originalData) return null;
    const result = [];
    for (let i = 0; i < axis.length; i++) {
      result.push({ x: originalData.input0[i], y: originalData.output[i] });
    }
    return result;
  });

  let xMin = $derived(axis.length > 0 ? axis[0] : 0);
  let xMax = $derived(axis.length > 0 ? axis[axis.length - 1] : 100);

  let yMin = $derived.by(() => {
    let mn = Infinity;
    for (const v of values) if (v.y < mn) mn = v.y;
    const range = mn === Infinity ? 10 : mn;
    return range - (range === 0 ? 5 : Math.abs(range) * 0.05);
  });

  let yMax = $derived.by(() => {
    let mx = -Infinity;
    for (const v of values) if (v.y > mx) mx = v.y;
    const range = mx === -Infinity ? 100 : mx;
    return range + (range === 0 ? 5 : Math.abs(range) * 0.05);
  });

  function x2px(x: number): number {
    return pad.left + ((x - xMin) / (xMax - xMin || 1)) * cw;
  }

  function y2px(y: number): number {
    return pad.top + ch - ((y - yMin) / (yMax - yMin || 1)) * ch;
  }

  let pathD = $derived.by(() => {
    if (values.length === 0) return '';
    let d = `M ${x2px(values[0].x)} ${y2px(values[0].y)}`;
    for (let i = 1; i < values.length; i++) {
      d += ` L ${x2px(values[i].x)} ${y2px(values[i].y)}`;
    }
    return d;
  });

  let origPathD = $derived.by(() => {
    if (!origValues || origValues.length === 0) return '';
    let d = `M ${x2px(origValues[0].x)} ${y2px(origValues[0].y)}`;
    for (let i = 1; i < origValues.length; i++) {
      d += ` L ${x2px(origValues[i].x)} ${y2px(origValues[i].y)}`;
    }
    return d;
  });

  let yTicks = $derived.by(() => {
    const mn = yMin;
    const mx = yMax;
    const ticks = [];
    const count = 5;
    for (let i = 0; i <= count; i++) {
      const val = mn + (i / count) * (mx - mn);
      ticks.push({ val, px: y2px(val) });
    }
    return ticks;
  });

  let hoverIndex = $state(-1);

  function exportChartAsPng() {
    if (!svgEl || !containerEl) return;
    const scale = 2;
    const cw = w * scale;
    const chPx = h * scale;
    const canvas = document.createElement('canvas');
    canvas.width = cw;
    canvas.height = chPx;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.scale(scale, scale);

    // Resolve CSS variables
    const cs = getComputedStyle(document.documentElement);
    const metroBg = cs.getPropertyValue('--metro-bg').trim() || '#0A0A0A';
    const metroBorder = cs.getPropertyValue('--metro-border').trim() || '#333333';
    const metroText = cs.getPropertyValue('--metro-text').trim() || '#FFFFFF';
    const metroTextSec = cs.getPropertyValue('--metro-text-secondary').trim() || '#A0A0A0';
    const metroTextMuted = cs.getPropertyValue('--metro-text-muted').trim() || '#666666';
    const metroOrange = cs.getPropertyValue('--metro-orange').trim() || '#D83B01';
    const metroYellow = cs.getPropertyValue('--metro-yellow').trim() || '#E5A100';
    const metroCard = cs.getPropertyValue('--metro-card').trim() || '#1A1A1A';

    // Background
    ctx.fillStyle = metroBg;
    ctx.fillRect(0, 0, w, h);

    // Chart area
    const pL = pad.left, pT = pad.top, pR = pad.right, pB = pad.bottom;
    const cW = w - pL - pR;
    const cH = h - pT - pB;

    // Y-axis grid lines + labels
    ctx.textAlign = 'right';
    ctx.textBaseline = 'middle';
    for (const tick of yTicks) {
      ctx.strokeStyle = metroBorder;
      ctx.lineWidth = 0.5;
      ctx.beginPath();
      ctx.moveTo(pL, tick.px);
      ctx.lineTo(w - pR, tick.px);
      ctx.stroke();

      ctx.fillStyle = metroTextSec;
      ctx.font = '11px ui-monospace, monospace';
      ctx.fillText(tick.val.toFixed(1), pL - 8, tick.px + 1);
    }

    // X-axis labels
    ctx.textAlign = 'center';
    ctx.textBaseline = 'top';
    for (let i = 0; i < values.length; i++) {
      const pt = values[i];
      const px = pL + (pt.x - axis[0]) / (axis[axis.length - 1] - axis[0]) * cW;
      const isCurrent = i === selectedCol;
      ctx.fillStyle = isCurrent ? metroOrange : metroTextSec;
      ctx.font = '11px ui-monospace, monospace';
      ctx.fillText(typeof pt.x === 'number' ? pt.x.toFixed(0) : String(pt.x), px, pT + cH + 6);
    }

    // Operating point line
    if (opColRange && opColRange.lower < axis.length) {
      const opX = pL + (axis[opColRange.lower] - axis[0]) / (axis[axis.length - 1] - axis[0]) * cW;
      ctx.strokeStyle = 'rgba(255,255,255,0.12)';
      ctx.lineWidth = 1;
      ctx.setLineDash([4, 3]);
      ctx.beginPath();
      ctx.moveTo(opX, pT);
      ctx.lineTo(opX, pT + cH);
      ctx.stroke();
      ctx.setLineDash([]);
    }

    // Diff mode original curve
    if (diffMode && origValues && origValues.length > 1) {
      ctx.strokeStyle = metroTextMuted;
      ctx.lineWidth = 1.5;
      ctx.setLineDash([6, 3]);
      ctx.beginPath();
      for (let i = 0; i < origValues.length; i++) {
        const px = pL + (origValues[i].x - axis[0]) / (axis[axis.length - 1] - axis[0]) * cW;
        const py = pT + cH - (origValues[i].y - yMin) / (yMax - yMin) * cH;
        if (i === 0) ctx.moveTo(px, py); else ctx.lineTo(px, py);
      }
      ctx.stroke();
      ctx.setLineDash([]);
    }

    // Curve path
    if (values.length > 1) {
      ctx.strokeStyle = metroOrange;
      ctx.lineWidth = 2.5;
      ctx.lineJoin = 'round';
      ctx.lineCap = 'round';
      ctx.beginPath();
      for (let i = 0; i < values.length; i++) {
        const px = pL + (values[i].x - axis[0]) / (axis[axis.length - 1] - axis[0]) * cW;
        const py = pT + cH - (values[i].y - yMin) / (yMax - yMin) * cH;
        if (i === 0) ctx.moveTo(px, py); else ctx.lineTo(px, py);
      }
      ctx.stroke();
    }

    // Data points
    for (let i = 0; i < values.length; i++) {
      const pt = values[i];
      const px = pL + (pt.x - axis[0]) / (axis[axis.length - 1] - axis[0]) * cW;
      const py = pT + cH - (pt.y - yMin) / (yMax - yMin) * cH;
      const isCurrent = i === selectedCol;
      const opOpacity = opColRange ? rangeOpacity(opColRange, i) : 0;
      const isInRange = opOpacity > 0;
      const inSel = isSelectedOutput(i);
      const isDirty = dirtyCells.has(cellKey(0, i));
      const r = isCurrent ? 7 : isInRange ? 5 : inSel ? 6 : 4;

      // Dirty indicator
      if (isDirty && !isCurrent && !inSel) {
        ctx.fillStyle = metroYellow;
        ctx.beginPath();
        ctx.arc(px, py + 10, 2, 0, Math.PI * 2);
        ctx.fill();
      }

      // Point
      ctx.fillStyle = isCurrent ? metroOrange : isInRange ? `rgba(255,255,255,${0.4 + 0.6 * opOpacity})` : inSel ? metroOrange : '#B83201';
      ctx.beginPath();
      ctx.arc(px, py, r, 0, Math.PI * 2);
      ctx.fill();

      if (isCurrent) {
        ctx.strokeStyle = '#ffffff';
        ctx.lineWidth = 2.5;
        ctx.stroke();
      } else if (isInRange) {
        ctx.strokeStyle = `rgba(255,255,255,${0.3 + 0.4 * opOpacity})`;
        ctx.lineWidth = 1.5;
        ctx.stroke();
      }
    }

    // Operating point indicator
    if (opColRange && opColRange.lower < values.length && opColRange.upper < values.length) {
      const fracX = opColRange.lower === opColRange.upper
        ? axis[opColRange.lower]
        : axis[opColRange.lower] + opColRange.fraction * (axis[opColRange.upper] - axis[opColRange.lower]);
      const lowerY = values[opColRange.lower]?.y ?? 0;
      const upperY = values[opColRange.upper]?.y ?? 0;
      const fracY = lowerY + opColRange.fraction * (upperY - lowerY);
      const opX = pL + (fracX - axis[0]) / (axis[axis.length - 1] - axis[0]) * cW;
      const opY = pT + cH - (fracY - yMin) / (yMax - yMin) * cH;
      ctx.fillStyle = '#ffffff';
      ctx.shadowColor = 'rgba(255,255,255,0.8)';
      ctx.shadowBlur = 6;
      ctx.beginPath();
      ctx.arc(opX, opY, 5, 0, Math.PI * 2);
      ctx.fill();
      ctx.shadowBlur = 0;
    }

    // Y-axis label
    ctx.save();
    ctx.fillStyle = metroTextMuted;
    ctx.font = '10px ui-monospace, monospace';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.translate(12, pT + cH / 2);
    ctx.rotate(-Math.PI / 2);
    ctx.fillText(tableDef.outputName, 0, 0);
    ctx.restore();

    // Title
    ctx.fillStyle = metroText;
    ctx.font = 'bold 13px ui-sans-serif, system-ui, sans-serif';
    ctx.textAlign = 'left';
    ctx.textBaseline = 'top';
    ctx.fillText(tableDef.name, pL, 6);

    canvas.toBlob(async (blob) => {
      if (!blob) return;
      const reader = new FileReader();
      reader.onload = async () => {
        const base64 = (reader.result as string).split(',')[1];
        const result = await HybridBridge.saveBinaryFile(`${tableDef.name}_chart.png`, base64, '.png');
        if (!result.success && result.error !== 'Save cancelled') {
          console.error('Export PNG failed:', result.error);
        }
      };
      reader.readAsDataURL(blob);
    }, 'image/png');
  }

  let cellW = $derived(isMobile ? 42 : 64);
  let gridStyle = $derived(
    `grid-template-columns: ${isMobile ? '40px' : '48px'} repeat(${tableDef.cols}, ${cellW}px); grid-template-rows: ${isMobile ? '24px' : '28px'} ${isMobile ? '24px' : '28px'}; font-size: ${isMobile ? '8px' : '10px'};`
  );

  let sMinC = $derived(selection ? Math.min(selection.startCol, selection.endCol) : -1);
  let sMaxC = $derived(selection ? Math.max(selection.startCol, selection.endCol) : -1);
  let hasSelection = $derived(selection !== null);

  function isSelectedOutput(c: number): boolean {
    if (!hasSelection || selectionType !== 'output') return false;
    return c >= sMinC && c <= sMaxC;
  }

  function isSelectedAxis(c: number): boolean {
    if (!hasSelection || selectionType !== 'input0') return false;
    return c >= sMinC && c <= sMaxC;
  }

  let longPressTimer = 0;
  let longPressTriggered = false;
  let longPressStartX: number | null = null;
  let longPressStartY: number | null = null;
  let selectionJustCompleted = false;

  function handlePointerDown(e: PointerEvent, c: number, type: 'output' | 'input0') {
    longPressTriggered = false;
    selectionJustCompleted = false;
    longPressStartX = e.clientX;
    longPressStartY = e.clientY;
    longPressTimer = window.setTimeout(() => {
      longPressTriggered = true;
      if (anchor && !selection) {
        onSelectionComplete(0, c);
      } else {
        onAnchorSet(0, c, type);
      }
    }, 500);
  }

  function handlePointerUp(e: PointerEvent, c: number) {
    clearTimeout(longPressTimer);
    if (longPressTriggered) { e.preventDefault(); return; }
    if (anchor && !selection) {
      onSelectionComplete(0, c);
      selectionJustCompleted = true;
      e.preventDefault();
    }
  }

  function handlePointerMove(e: PointerEvent) {
    if (longPressStartX !== null && longPressStartY !== null) {
      const dx = e.clientX - longPressStartX;
      const dy = e.clientY - longPressStartY;
      if (dx * dx + dy * dy > 100) {
        clearTimeout(longPressTimer);
      }
    }
  }

  function handlePointerCancel() {
    clearTimeout(longPressTimer);
  }

  function handleCellTap(c: number, type: 'output' | 'input0') {
    if (longPressTriggered || selectionJustCompleted) return;
    if (anchor && !selection) {
      onSelectionComplete(0, c);
    } else {
      if (selection) onSelectionClear();
      if (type === 'input0') onAxis0Click(c);
      else onCellClick(0, c);
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

<div class="flex h-full flex-col" role="grid">
  <!-- Data Grid -->
  <div class="shrink-0" style="border-bottom: 1px solid var(--metro-border);">
    <div class="overflow-hidden">
      <div class="grid w-fit" style={gridStyle}>
        <!-- Corner -->
        <div
          class="sticky left-0 top-0 z-30 flex items-center justify-center px-0.5 text-[6px] sm:text-[7px] leading-tight overflow-hidden"
          style="border: 1px solid var(--metro-border); background-color: var(--metro-card); color: var(--metro-text-muted);"
          role="columnheader"
          tabindex="-1"
        >
          {tableDef.input0Name}
        </div>

        <!-- Axis cells -->
        {#each tableData.input0 as colVal, c (c)}
          {@const isAnc = selectionType === 'input0' && anchor?.row === 0 && anchor?.col === c}
          {@const inSel = isSelectedAxis(c)}
          {@const isCurrent = c === selectedCol && selectionType === 'input0'}
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
            onclick={() => handleCellTap(c, 'input0')}
            oncontextmenu={(e) => onContextMenu?.(e, 0, c, 'input0')}
            onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); handleCellTap(c, 'input0'); } }}
            onpointerdown={(e) => handlePointerDown(e, c, 'input0')}
            onpointerup={(e) => handlePointerUp(e, c)}
            onpointermove={handlePointerMove}
            onpointercancel={handlePointerCancel}
          >
            {typeof colVal === 'number' ? formatValueAdaptive(colVal, tableDef.input0DataType) : colVal.toLocaleString()}
          </div>
        {/each}

        <!-- Output row label -->
        <div
          class="sticky left-0 z-20 flex min-w-0 cursor-default items-center justify-center overflow-hidden pr-1 text-right"
          style="border: 1px solid var(--metro-border); background-color: var(--metro-sidebar); color: var(--metro-text-secondary);"
          role="rowheader"
          tabindex="-1"
        >
          {tableDef.outputName}{tableDef.outputUnit ? ` (${tableDef.outputUnit})` : ''}
        </div>

        <!-- Output cells -->
        {#each tableData.output as val, c (c)}
          {@const origVal = diffMode && originalData ? originalData.output[c] : val}
          {@const delta = diffMode ? val - origVal : 0}
          {@const color = diffMode
            ? diffColor(delta, maxDelta)
            : heatColor(val, minVal, maxVal, colorScheme)}
          {@const isAnc = selectionType === 'output' && anchor?.row === 0 && anchor?.col === c}
          {@const inSel = isSelectedOutput(c)}
          {@const isCurrent = c === selectedCol && selectionType === 'output'}
          {@const isDirty = dirtyCells.has(cellKey(0, c))}
          {@const opOpacity = opColRange ? rangeOpacity(opColRange, c) : 0}
          {@const isInRange = opOpacity > 0}
          {@const borderAlpha = (0.15 + 0.7 * opOpacity).toFixed(2)}
          <div
            class="relative flex cursor-pointer items-center justify-center text-center font-medium transition-colors duration-150
                   {isAnc ? 'z-[2]' : ''}
                   {isCurrent && !inSel ? 'z-[2]' : ''}
                   {isInRange ? 'border-t-2 border-t-white/20' : ''}"
            style="border: 1px solid var(--metro-border); background: {isAnc ? 'rgba(216,59,1,0.3)' : inSel ? 'rgba(216,59,1,0.18)' : color}; {isCurrent && !inSel ? 'outline: 2px solid var(--metro-orange); outline-offset: -2px;' : ''} {isDirty && !inSel ? 'outline: 2px solid var(--metro-yellow); outline-offset: -2px;' : ''} {inSel && !isAnc ? 'outline: 2px inset var(--metro-orange); outline-offset: -2px;' : ''} {isInRange ? `outline: 2px solid rgba(255,255,255,${borderAlpha}); outline-offset: -2px; z-index: 1;` : ''}"
            role="gridcell"
            tabindex="-1"
            onclick={() => handleCellTap(c, 'output')}
            oncontextmenu={(e) => onContextMenu?.(e, 0, c, 'output')}
            onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); handleCellTap(c, 'output'); } }}
            onpointerdown={(e) => handlePointerDown(e, c, 'output')}
            onpointerup={(e) => handlePointerUp(e, c)}
            onpointermove={handlePointerMove}
            onpointercancel={handlePointerCancel}
          >
            <span class="pointer-events-none" style="color: {diffMode && Math.abs(delta) > 0.001 ? '#fff' : inSel ? 'var(--metro-orange)' : ''};">
              {diffMode && originalData ? (delta > 0 ? '+' : '') + formatValueAdaptive(delta, tableDef.outputDataType) : formatValueAdaptive(val, tableDef.outputDataType)}
            </span>
            {#if isAnc}
              <div class="pointer-events-none absolute bottom-0.5 left-1/2 h-1 w-1 -translate-x-1/2 rounded-full" style="background-color: var(--metro-orange);"></div>
            {/if}
            {#if isInRange && opColRange?.lower === c && liveOutputValue !== null}
              {@const liveDelta = liveOutputValue - val}
              {@const absDelta = Math.abs(liveDelta)}
              <div class="pointer-events-none absolute inset-x-0 -bottom-4 flex items-center justify-center">
                <span class="rounded px-1 text-[8px] font-bold tabular-nums whitespace-nowrap"
                  style="background-color: {absDelta < 2 ? 'rgba(34,139,80,0.9)' : absDelta < 10 ? 'rgba(216,59,1,0.9)' : 'rgba(200,50,50,0.9)'}; color: #fff;">
                  {liveOutputValue.toFixed(1)} ({liveDelta > 0 ? '+' : ''}{liveDelta.toFixed(1)})
                </span>
              </div>
            {/if}
          </div>
        {/each}
      </div>
    </div>
  </div>

  <!-- SVG Chart -->
  <div bind:this={containerEl} class="flex-1 min-h-0 relative">
    {#if w > 0 && h > 0}
      <button
        class="absolute right-2 top-2 z-10 flex h-7 items-center gap-1 border px-2 text-[10px] font-bold uppercase tracking-wider transition-colors duration-150"
        style="background-color: var(--metro-card); border-color: var(--metro-border); color: var(--metro-text-secondary);"
        onclick={exportChartAsPng}
      >
        <IconDownload size={12} />
        Export PNG
      </button>
      <svg bind:this={svgEl} width={w} height={h} viewBox="0 0 {w} {h}" class="block">
        {#each yTicks as tick}
          <line x1={pad.left} y1={tick.px} x2={w - pad.right} y2={tick.px}
                stroke="var(--metro-border)" stroke-width="0.5" />
          <text x={pad.left - 8} y={tick.px + 4} text-anchor="end"
                fill="var(--metro-text-secondary)" font-size="11" font-family="ui-monospace, monospace">
            {tick.val.toFixed(1)}
          </text>
        {/each}

        {#if opColRange && opColRange.lower !== opColRange.upper && opColRange.lower < axis.length && opColRange.upper < axis.length}
          <rect x={x2px(axis[opColRange.lower])} y={pad.top}
                width={x2px(axis[opColRange.upper]) - x2px(axis[opColRange.lower])}
                height={ch}
                fill="rgba(255,255,255,0.06)" />
          <line x1={x2px(axis[opColRange.lower])} y1={pad.top}
                x2={x2px(axis[opColRange.lower])} y2={pad.top + ch}
                stroke="rgba(255,255,255,0.15)" stroke-width="1" stroke-dasharray="4 3" />
          <line x1={x2px(axis[opColRange.upper])} y1={pad.top}
                x2={x2px(axis[opColRange.upper])} y2={pad.top + ch}
                stroke="rgba(255,255,255,0.15)" stroke-width="1" stroke-dasharray="4 3" />
        {:else if opColRange && opColRange.lower < axis.length}
          <line x1={x2px(axis[opColRange.lower])} y1={pad.top}
                x2={x2px(axis[opColRange.lower])} y2={pad.top + ch}
                stroke="rgba(255,255,255,0.12)" stroke-width="1" stroke-dasharray="4 3" />
        {/if}

        {#if diffMode && origPathD}
          <path d={origPathD} fill="none" stroke="var(--metro-text-muted)" stroke-width="1.5"
                stroke-dasharray="6 3" />
        {/if}

        {#if pathD}
          <path d={pathD} fill="none" stroke="var(--metro-orange)" stroke-width="2.5"
                stroke-linejoin="round" stroke-linecap="round" />
        {/if}

        {#each values as point, i}
          {@const isCurrent = i === selectedCol}
          {@const opOpacity = opColRange ? rangeOpacity(opColRange, i) : 0}
          {@const isInRange = opOpacity > 0}
          {@const inSel = isSelectedOutput(i)}
          {@const isDirty = dirtyCells.has(cellKey(0, i))}

          <circle
            cx={x2px(point.x)} cy={y2px(point.y)}
            r={isCurrent ? 7 : isInRange ? 5 : inSel ? 6 : 4}
            class="pointer-events-none transition-all"
            fill={isCurrent ? '#D83B01' : isInRange ? `rgba(255,255,255,${0.4 + 0.6 * opOpacity})` : inSel ? '#D83B01' : '#B83201'}
            stroke={isCurrent ? '#ffffff' : isInRange ? `rgba(255,255,255,${0.3 + 0.4 * opOpacity})` : 'none'}
            stroke-width={isCurrent ? 2.5 : isInRange ? 1.5 : 0}
            style={isInRange ? 'filter: drop-shadow(0 0 4px rgba(255,255,255,0.6));' : ''}
          />

          {#if isDirty && !isCurrent && !inSel}
            <circle cx={x2px(point.x)} cy={y2px(point.y) + 10} r={2}
                    fill="var(--metro-yellow)" class="pointer-events-none" />
          {/if}

          <circle
            cx={x2px(point.x)} cy={y2px(point.y)} r={14}
            fill="transparent"
            role="img"
            aria-label="Data point {point.x}"
            onmouseenter={() => hoverIndex = i}
            onmouseleave={() => hoverIndex = -1}
          />

          <text
            x={x2px(point.x)} y={pad.top + ch + 18}
            text-anchor="middle"
            fill={isCurrent ? 'var(--metro-orange)' : isSelectedAxis(i) ? 'var(--metro-orange)' : 'var(--metro-text-secondary)'}
            font-size="11"
            font-family="ui-monospace, monospace"
          >
            {typeof point.x === 'number' ? point.x.toFixed(0) : point.x}
          </text>
        {/each}

        {#if opColRange && opColRange.lower < values.length && opColRange.upper < values.length}
          {@const fracX = opColRange.lower === opColRange.upper
            ? axis[opColRange.lower]
            : axis[opColRange.lower] + opColRange.fraction * (axis[opColRange.upper] - axis[opColRange.lower])}
          {@const lowerY = values[opColRange.lower]?.y ?? 0}
          {@const upperY = values[opColRange.upper]?.y ?? 0}
          {@const fracY = lowerY + opColRange.fraction * (upperY - lowerY)}
          <circle
            cx={x2px(fracX)}
            cy={y2px(fracY)}
            r="5"
            fill="#ffffff"
            class="pointer-events-none"
            style="filter: drop-shadow(0 0 6px rgba(255,255,255,0.8));"
          />
        {/if}

        <text x={12} y={pad.top + ch / 2} text-anchor="middle"
              fill="var(--metro-text-muted)" font-size="10" transform="rotate(-90, 12, {pad.top + ch / 2})">
          {tableDef.outputName}
        </text>
      </svg>

      {#if hoverIndex >= 0 && hoverIndex < values.length}
        {@const pt = values[hoverIndex]}
        {@const px = x2px(pt.x)}
        {@const py = y2px(pt.y)}
        <div
          class="pointer-events-none absolute z-10 border px-2.5 py-1.5 text-[11px]"
          style="left: {Math.min(px + 12, w - 140)}px; top: {Math.max(py - 44, 4)}px; background-color: var(--metro-card); border-color: var(--metro-border);"
        >
          <div class="font-medium" style="color: var(--metro-orange);">{tableDef.input0Name}: {pt.x.toLocaleString()}</div>
          <div class="text-white">{tableDef.outputName}: {pt.y.toFixed(2)}</div>
        </div>
      {/if}
    {/if}
  </div>
</div>

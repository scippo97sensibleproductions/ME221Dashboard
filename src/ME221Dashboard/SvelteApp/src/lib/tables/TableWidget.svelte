<script lang="ts">
  import { onMount } from 'svelte';
  import { HybridBridge } from '../HybridBridge';
  import { liveDataStore } from '../stores/LiveDataStore.svelte';
  import {
    cellKey,
    getOutputValue,
    heatColor,
    getDataRange,
    findNearestIndex,
  } from './types';
  import { IconSettings } from '@tabler/icons-svelte';
  import type { TableDefinition, TableData } from './types';

  let { tableId, tableName, onTap, onSettings, colorScheme = 'thermal', showLabels = true, showDimensionBadge = true, maxFontSize }: {
    tableId: number;
    tableName: string;
    onTap: (tableId: number) => void;
    onSettings?: (tableId: number) => void;
    colorScheme?: string;
    showLabels?: boolean;
    showDimensionBadge?: boolean;
    maxFontSize?: number;
  } = $props();

  let tableDef = $state<TableDefinition | null>(null);
  let tableData = $state<TableData | null>(null);
  let loading = $state(true);
  let error = $state<string | null>(null);
  let widgetEl: HTMLDivElement | undefined = $state();
  let widgetW = $state(0);
  let widgetH = $state(0);

  // Operating point (live)
  let opRow = $state(-1);
  let opCol = $state(-1);
  let liveOutputVal = $state<number | null>(null);

  async function loadData() {
    loading = true;
    error = null;
    try {
      const [defsResult, dataResult] = await Promise.all([
        HybridBridge.getTableDefinitions(),
        HybridBridge.readTableData(tableId),
      ]);

      if (!dataResult || dataResult.success === false) {
        error = dataResult?.error ?? 'Failed to read table data from ECU';
        loading = false;
        return;
      }
      if (!dataResult.output || dataResult.output.length === 0) {
        error = 'Table data is empty';
        loading = false;
        return;
      }

      const def = defsResult.tables?.find((t: any) => t.id === tableId) as TableDefinition | undefined;
      if (!def) {
        error = `Table definition for #${tableId} not found`;
        loading = false;
        return;
      }

      tableDef = def;
      tableData = {
        enabled: dataResult.enabled ?? false,
        input0: dataResult.input0 ?? [],
        input1: dataResult.input1 ?? [],
        output: dataResult.output,
      };
    } catch (err: any) {
      error = err?.message ?? String(err);
    } finally {
      loading = false;
    }
  }

  function applyLatestLive() {
    if (!tableDef) return;
    const v = liveDataStore.values;
    if (tableDef.input0LinkId != null) {
      const x = v[String(tableDef.input0LinkId)];
      if (x != null && tableData && tableData.input0.length > 0) {
        opCol = findNearestIndex(x, tableData.input0);
      }
    }
    if (tableDef.input1LinkId != null) {
      const y = v[String(tableDef.input1LinkId)];
      if (y != null && tableData && tableData.input1.length > 1) {
        opRow = findNearestIndex(y, tableData.input1);
      }
    }
    if (tableDef.outputLinkId != null) {
      const out = v[String(tableDef.outputLinkId)];
      liveOutputVal = out ?? null;
    }
  }

  // When data arrives from any source (other widgets, derived computation, GPS),
  // refresh the operating-point display.
  $effect(() => {
    // Touch reactive count to subscribe to per-frame updates
    void liveDataStore.frameCount;
    applyLatestLive();
  });

  // Initial load
  onMount(() => {
    loadData();
  });

  // ResizeObserver via $effect (idiomatic Svelte 5, properly reactive)
  $effect(() => {
    if (!widgetEl) return;
    const ro = new ResizeObserver(entries => {
      for (const entry of entries) {
        widgetW = entry.contentRect.width;
        widgetH = entry.contentRect.height;
      }
    });
    ro.observe(widgetEl);
    return () => ro.disconnect();
  });

  let is1D = $derived.by(() => {
    if (!tableDef) return false;
    if (tableDef.tableType === 'T1x16' || tableDef.tableType === 'T1x32') return true;
    return tableDef.rows <= 1;
  });
  let range = $derived(tableData ? getDataRange(tableData.output) : { min: 0, max: 100 });

  // Pick cell dimensions to fit
  let layout = $derived.by(() => {
    if (!tableDef || !tableData) {
      return { rows: 1, cols: 1, cellW: 0, cellH: 0, labelW: 0, labelH: 0, gridW: 0, gridH: 0 };
    }
    const rows = tableDef.rows;
    const cols = tableDef.cols;
    const headerH = 26; // table-name row at top
    const labelW = is1D ? 0 : 28;
    const labelH = is1D ? 0 : 20;
    const availW = Math.max(20, widgetW - labelW - 4);
    const availH = Math.max(20, widgetH - headerH - labelH - 4);
    const cellW = Math.max(2, availW / cols);
    const cellH = Math.max(2, is1D ? availH : availH / rows);
    const result = {
      rows,
      cols,
      cellW,
      cellH,
      labelW,
      labelH,
      headerH,
      gridW: cellW * cols,
      gridH: cellH * (is1D ? 1 : rows),
    };
    return result;
  });

  // Font sizes: monospace chars are ~0.6×fontSize wide. For N chars to fit in cellW:
  // fontSize = (cellW - padding) / (N × 0.6). Using N=5 worst case → multiplier ~0.33.
  // Using N=4 average → multiplier ~0.42. Splitting the difference at 0.38.
  let cellFont2D = $derived(maxFontSize != null
    ? Math.min(Math.floor(Math.min((layout.cellW - 4) * 0.38, (layout.cellH - 4) * 0.85)), maxFontSize)
    : Math.floor(Math.min((layout.cellW - 4) * 0.38, (layout.cellH - 4) * 0.85)));
  let cellFont1D = $derived(maxFontSize != null
    ? Math.min(Math.floor((layout.cellW - 4) * 0.38), maxFontSize)
    : Math.floor((layout.cellW - 4) * 0.38));
  let showCellLabels2D = $derived(cellFont2D >= 6);
  let showCellLabels1D = $derived(cellFont1D >= 6);
  let axisFontSize = $derived(Math.max(8, Math.floor(Math.min(layout.cellW * 0.7, 11))));
  let showAxisLabelsThreshold = $derived(widgetW >= 60);

  function formatVal(v: number, cols: number): string {
    if (!isFinite(v)) return '—';
    const abs = Math.abs(v);
    if (abs >= 100) return v.toFixed(0);
    if (abs >= 10) return v.toFixed(1);
    return v.toFixed(2);
  }

  function formatAxis(v: number): string {
    if (!isFinite(v)) return '';
    const abs = Math.abs(v);
    if (abs >= 100) return v.toFixed(0);
    if (abs >= 10) return v.toFixed(1);
    return v.toFixed(2);
  }

  // Click/drag detection
  let pointerDownX = 0;
  let pointerDownY = 0;
  let pointerDownAt = 0;
  let movedDuringPress = false;
  const CLICK_SLOP = 6;
  const CLICK_TIME = 400;

  function handlePointerDown(e: PointerEvent) {
    pointerDownX = e.clientX;
    pointerDownY = e.clientY;
    pointerDownAt = performance.now();
    movedDuringPress = false;
  }

  function handlePointerMove(e: PointerEvent) {
    if (e.buttons === 0) return;
    const dx = Math.abs(e.clientX - pointerDownX);
    const dy = Math.abs(e.clientY - pointerDownY);
    if (dx > CLICK_SLOP || dy > CLICK_SLOP) movedDuringPress = true;
  }

  function handleClick(e: MouseEvent) {
    // If buttons (settings, retry) were the click target, the parent DashboardPage wrapper
    // has its own pointer capture; we only fire onTap when no movement happened on our own surface.
    if (movedDuringPress) return;
    const dt = performance.now() - pointerDownAt;
    if (dt > CLICK_TIME) return;
    onTap(tableId);
  }

  function handleSettingsClick(e: MouseEvent) {
    e.stopPropagation();
    e.preventDefault();
    if (onSettings) onSettings(tableId);
  }

  function handleSettingsPointerDown(e: PointerEvent) {
    e.stopPropagation();
  }

  function handleRetry(e: MouseEvent) {
    e.stopPropagation();
    e.preventDefault();
    loadData();
  }

  function handleRetryPointerDown(e: PointerEvent) {
    e.stopPropagation();
  }
</script>

<div
  bind:this={widgetEl}
  class="flex h-full w-full cursor-grab select-none flex-col overflow-hidden border"
  style="border-color: var(--metro-border); background-color: var(--metro-card);"
  onpointerdown={handlePointerDown}
  onpointermove={handlePointerMove}
  onclick={handleClick}
>
  <!-- Header -->
  <div class="flex shrink-0 items-center justify-between border-b px-2 py-1" style="border-color: var(--metro-border); height: {layout.headerH}px;">
    <span class="truncate text-[11px] font-bold uppercase tracking-wider {liveOutputVal != null ? 'text-white' : ''}" style="color: {liveOutputVal != null ? '#fff' : 'var(--metro-text-secondary)'};">
      {tableName}{#if liveOutputVal != null}<span class="ml-1 text-[11px] tabular-nums" style="color: var(--metro-orange);">{formatAxis(liveOutputVal)}</span>{/if}
    </span>
    <div class="flex items-center gap-1.5">
      {#if onSettings}
        <button
          class="rounded p-0.5 text-gray-400 transition-colors hover:bg-gray-700 hover:text-gray-200"
          onclick={handleSettingsClick}
          onpointerdown={handleSettingsPointerDown}
          aria-label="Settings"
          type="button"
        >
          <IconSettings size={12} />
        </button>
      {/if}
      {#if showDimensionBadge && tableDef}
        <span class="text-[10px]" style="color: var(--metro-text-muted);">{tableDef.rows}×{tableDef.cols}</span>
      {/if}
    </div>
  </div>

  <!-- Body -->
  <div class="relative flex-1 overflow-hidden">
    {#if loading}
      <div class="flex h-full w-full flex-col items-center justify-center gap-1">
        <span class="inline-block h-3 w-3 animate-spin rounded-full border-2" style="border-color: var(--metro-border); border-top-color: var(--metro-purple);"></span>
        <span class="text-[10px]" style="color: var(--metro-text-muted);">Loading...</span>
      </div>
    {:else if error}
      <div class="flex h-full w-full flex-col items-center justify-center gap-1 px-3 text-center">
        <span class="text-[10px]" style="color: var(--metro-red);">Load failed</span>
        <span class="line-clamp-3 text-[9px]" style="color: var(--metro-text-muted);">{error}</span>
        <button
          type="button"
          class="mt-1 px-2 py-0.5 text-[10px]"
          style="background-color: var(--metro-purple); color: var(--metro-text-on-accent);"
          onclick={handleRetry}
          onpointerdown={handleRetryPointerDown}
        >
          Retry
        </button>
      </div>
    {:else if !tableDef || !tableData}
      <div class="flex h-full w-full items-center justify-center">
        <span class="text-[10px]" style="color: var(--metro-text-muted);">No data</span>
      </div>
    {:else if is1D}
      <!-- 1D table: heatmap row with axis labels and operating point dot -->
      <div class="absolute left-0 right-0" style="top: {layout.labelH}px; height: {layout.gridH}px;">
        <table
          class="border-collapse"
          style="border: 1px solid var(--metro-border); table-layout: fixed; width: {layout.cellW * layout.cols}px; height: {layout.gridH}px;"
        >
          <tbody>
            <tr style="height: {layout.gridH}px;">
              {#each tableData.output as val, c}
                {@const color = heatColor(val, range.min, range.max, colorScheme)}
                {@const isOp = c === opCol}
                <td
                  class="relative overflow-hidden"
                  style="padding: 0; width: {layout.cellW}px; height: {layout.gridH}px; background: {color};
                         border: 1px solid {isOp ? 'rgba(255,255,255,0.85)' : 'var(--metro-border)'};
                         outline: {isOp ? '2px solid rgba(255,255,255,0.95)' : 'none'}; outline-offset: -2px;"
                >
                  {#if showLabels && showCellLabels1D}
                    <span class="absolute inset-0 flex items-center justify-center font-mono tabular-nums leading-none whitespace-nowrap"
                          style="left: 2px; right: 2px; color: #000; font-size: {cellFont1D}px;">
                      {formatVal(val, layout.cols)}
                    </span>
                  {/if}
                  {#if isOp && liveOutputVal != null}
                    <div
                      class="pointer-events-none absolute inset-0 flex items-center justify-center"
                      aria-hidden="true"
                    >
                      <div
                        class="rounded-full"
                        style="width: 10px; height: 10px; background: #ffffff; box-shadow: 0 0 6px rgba(255,255,255,0.95), 0 0 0 2px rgba(0,0,0,0.4);"
                      ></div>
                    </div>
                  {/if}
                </td>
              {/each}
            </tr>
          </tbody>
        </table>
        <!-- Axis labels below -->
        {#if showAxisLabelsThreshold && tableData.input0.length > 0}
          <div class="absolute left-0 right-0 mt-0.5" style="height: {layout.labelH}px;">
            <div class="flex" style="width: {layout.cellW * layout.cols}px;">
              <div class="flex-1 text-center font-mono tabular-nums truncate" style="color: var(--metro-text-primary); font-size: {axisFontSize}px;">
                 {formatAxis(tableData.input0[0])}
               </div>
               <div class="flex-1 text-center font-mono tabular-nums truncate" style="color: var(--metro-text-primary); font-size: {axisFontSize}px;">
                 {formatAxis(tableData.input0[Math.floor(layout.cols / 2)])}
               </div>
               <div class="flex-1 text-center font-mono tabular-nums truncate" style="color: var(--metro-text-primary); font-size: {axisFontSize}px;">
                 {formatAxis(tableData.input0[layout.cols - 1])}
               </div>
            </div>
          </div>
        {/if}
      </div>
    {:else}
      <!-- 2D table: full grid with column headers, row headers, and operating point cell -->
      <div
        class="absolute"
        style="left: {layout.labelW}px; top: {layout.labelH}px; width: {layout.gridW}px; height: {layout.gridH}px;"
      >
        <table
          class="border-collapse"
          style="border: 1px solid var(--metro-border); table-layout: fixed; width: {layout.gridW}px; height: {layout.gridH}px;"
        >
          <tbody>
            {#each Array.from({ length: layout.rows }, (_, r) => r) as r}
              <tr style="height: {layout.cellH}px;">
                {#each Array.from({ length: layout.cols }, (_, c) => c) as c}
                  {@const val = getOutputValue(tableData, r, c, layout.cols)}
                  {@const color = heatColor(val, range.min, range.max, colorScheme)}
                  {@const isOpCell = r === opRow && c === opCol}
                  {@const isOpRow = r === opRow}
                  {@const isOpCol = c === opCol}
                  <td
                    class="relative"
                    style="padding: 0; width: {layout.cellW}px; height: {layout.cellH}px;
                           background: {isOpRow || isOpCol ? `color-mix(in srgb, ${color} 85%, rgba(255,255,255,0.12))` : color};
                           border: 1px solid {isOpCell ? 'rgba(255,255,255,0.95)' : isOpRow || isOpCol ? 'rgba(255,255,255,0.18)' : 'var(--metro-border)'};
                           outline: {isOpCell ? '2px solid rgba(255,255,255,0.95)' : 'none'}; outline-offset: -2px;"
                  >
                    {#if showLabels && showCellLabels2D}
                      <span class="absolute inset-0 flex items-center justify-center font-mono tabular-nums leading-none"
                            style="font-size: {cellFont2D}px; color: #000;">
                        {formatVal(val, layout.cols)}
                      </span>
                    {/if}
                    {#if isOpCell && liveOutputVal != null}
                      <div class="pointer-events-none absolute inset-0 flex items-center justify-center">
                        <div
                          class="rounded-full"
                          style="width: 10px; height: 10px; background: #ffffff; box-shadow: 0 0 6px rgba(255,255,255,0.95), 0 0 0 2px rgba(0,0,0,0.4);"
                        ></div>
                      </div>
                    {/if}
                  </td>
                {/each}
              </tr>
            {/each}
          </tbody>
        </table>
      </div>

      <!-- Column headers (top): input0 axis -->
      {#if showAxisLabelsThreshold && tableData.input0.length > 0}
        <div class="absolute" style="left: {layout.labelW}px; top: 0; width: {layout.gridW}px; height: {layout.labelH}px;">
          <div class="flex" style="width: {layout.gridW}px; height: {layout.labelH}px;">
            {#each Array.from({ length: Math.min(layout.cols, 5) }, (_, i) => i) as ti}
              {@const colIdx = Math.round((ti / Math.max(1, Math.min(layout.cols, 5) - 1)) * (layout.cols - 1))}
              <div class="flex-1 text-center text-[9px] font-mono tabular-nums truncate"
                   style="color: var(--metro-text-primary); font-size: 9px; line-height: {layout.labelH}px;">
                {formatAxis(tableData.input0[colIdx])}
              </div>
            {/each}
          </div>
        </div>
      {/if}

      <!-- Row labels (left): input1 axis -->
      {#if showAxisLabelsThreshold && tableData.input1.length > 1}
        <div class="absolute" style="left: 0; top: {layout.labelH}px; width: {layout.labelW}px; height: {layout.gridH}px;">
          <div class="flex flex-col" style="width: {layout.labelW}px; height: {layout.gridH}px;">
            {#each Array.from({ length: Math.min(layout.rows, 5) }, (_, i) => i) as ti}
              {@const rowIdx = Math.round((ti / Math.max(1, Math.min(layout.rows, 5) - 1)) * (layout.rows - 1))}
              <div class="flex-1 text-right text-[9px] font-mono tabular-nums truncate pr-1"
                   style="color: var(--metro-text-primary); font-size: 9px; line-height: {layout.cellH}px;">
                {formatAxis(tableData.input1[rowIdx])}
              </div>
            {/each}
          </div>
        </div>
      {/if}
    {/if}
  </div>
</div>

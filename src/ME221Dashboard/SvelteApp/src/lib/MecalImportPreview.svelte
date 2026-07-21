<script lang="ts">
  import { IconX, IconSearch, IconTable, IconAdjustments, IconCheck, IconArrowLeft, IconPackage, IconChevronDown, IconChevronRight } from '@tabler/icons-svelte';
  import { HybridBridge } from './HybridBridge';
  import type { TableDefinition, TableData } from './tables/types';
  import { is1DTable } from './tables/types';
  import ReadOnlyTable from './tables/ReadOnlyTable.svelte';

  let {
    open = $bindable(false),
    fileContent,
    onApply,
    onCancel,
  }: {
    open: boolean;
    fileContent: string;
    onApply: () => void;
    onCancel: () => void;
  } = $props();

  type TablePreview = {
    id: number; name: string; category: string; tableType: string;
    cols: number; rows: number; existsInEcu: boolean;
    input0Name: string; input1Name: string; outputName: string;
    currentInput0Name: string | null; currentInput1Name: string | null; currentOutputName: string | null;
    import: TableData;
    current: TableData | null;
  };
  type DriverPreview = {
    id: number; name: string; category: string;
    numberOfConfigs: number; existsInEcu: boolean;
  };

  let loading = $state(true);
  let error = $state<string | null>(null);
  let metadata = $state<{ productName: string; modelName: string; version: string } | null>(null);
  let tables = $state<TablePreview[]>([]);
  let drivers = $state<DriverPreview[]>([]);
  let dataLinkCount = $state(0);

  let selectedTableIds = $state(new Set<number>());
  let selectedDriverIds = $state(new Set<number>());

  type TableOverride = {
    blend: number;
    axisMode: 'import' | 'current';
    scale: number;
    offset: number;
    clampMin: number | null;
    clampMax: number | null;
  };
  let tableOverrides = $state<Map<number, TableOverride>>(new Map());
  const defaultOverride: TableOverride = { blend: 1, axisMode: 'import', scale: 1, offset: 0, clampMin: null, clampMax: null };

  function getOverride(id: number): TableOverride {
    return tableOverrides.get(id) ?? { ...defaultOverride };
  }

  function setOverride(id: number, patch: Partial<TableOverride>) {
    const existing = getOverride(id);
    const next = new Map(tableOverrides);
    next.set(id, { ...existing, ...patch });
    tableOverrides = next;
  }

  let activeTab = $state<'tables' | 'drivers'>('tables');
  let searchText = $state('');
  let categoryFilter = $state('All');
  let expandedTableId = $state<number | null>(null);
  let applying = $state(false);
  let applyResult = $state<{ success: boolean; message: string } | null>(null);

  const tableCategories = $derived.by(() => {
    const cats = new Set<string>();
    for (const t of tables) cats.add(t.category);
    return ['All', ...Array.from(cats).sort()];
  });

  const driverCategories = $derived.by(() => {
    const cats = new Set<string>();
    for (const d of drivers) cats.add(d.category);
    return ['All', ...Array.from(cats).sort()];
  });

  const filteredTables = $derived.by(() => {
    let list = tables;
    if (categoryFilter !== 'All') list = list.filter((t) => t.category === categoryFilter);
    if (searchText.trim()) {
      const q = searchText.toLowerCase();
      list = list.filter((t) => t.name.toLowerCase().includes(q) || t.category.toLowerCase().includes(q));
    }
    return list;
  });

  const filteredDrivers = $derived.by(() => {
    let list = drivers;
    if (categoryFilter !== 'All') list = list.filter((d) => d.category === categoryFilter);
    if (searchText.trim()) {
      const q = searchText.toLowerCase();
      list = list.filter((d) => d.name.toLowerCase().includes(q) || d.category.toLowerCase().includes(q));
    }
    return list;
  });

  const categories = $derived(activeTab === 'tables' ? tableCategories : driverCategories);

  function countChangedCells(table: TablePreview): number {
    const cur = table.current?.output;
    if (!cur) return 0;
    const imp = table.import.output;
    let count = 0;
    const len = Math.min(cur.length, imp.length);
    for (let i = 0; i < len; i++) {
      if (Math.abs(cur[i] - imp[i]) > 0.0001) count++;
    }
    return count;
  }

  function buildTableDef(table: TablePreview, axisMode: string): TableDefinition {
    const useCurrent = axisMode === 'current';
    return {
      id: table.id,
      name: table.name,
      category: table.category,
      viewInTree: true,
      enabled: true,
      tableType: table.tableType,
      cols: table.cols,
      rows: table.rows,
      input0LinkId: 0,
      input1LinkId: 0,
      outputLinkId: 0,
      input0Name: useCurrent ? (table.currentInput0Name ?? table.input0Name) : table.input0Name,
      input1Name: useCurrent ? (table.currentInput1Name ?? table.input1Name) : table.input1Name,
      outputName: useCurrent ? (table.currentOutputName ?? table.outputName) : table.outputName,
      incrementValue: 0.1,
      defaultValue: null,
      input0Unit: '',
      input0UnitType: 0,
      input0DataType: 0,
      input1Unit: '',
      input1UnitType: 0,
      input1DataType: 0,
      outputUnit: '',
      outputUnitType: 0,
      outputDataType: 0,
    };
  }

  async function loadData() {
    loading = true;
    error = null;
    applyResult = null;
    try {
      const result = await HybridBridge.getMecalImportPreview(fileContent);
      if (!result.success) {
        error = result.error || 'Failed to parse file';
        return;
      }
      metadata = result.metadata ?? null;
      tables = (result.tables as TablePreview[]) ?? [];
      drivers = (result.drivers as DriverPreview[]) ?? [];
      dataLinkCount = result.dataLinkCount ?? 0;

      selectedTableIds = new Set(tables.filter((t) => t.existsInEcu).map((t) => t.id));
      selectedDriverIds = new Set(drivers.filter((d) => d.existsInEcu).map((d) => d.id));
      tableOverrides = new Map();
    } catch (e: any) {
      error = e.message;
    } finally {
      loading = false;
    }
  }

  $effect(() => {
    if (open && fileContent) loadData();
  });

  function toggleTable(id: number) {
    const next = new Set(selectedTableIds);
    if (next.has(id)) next.delete(id); else next.add(id);
    selectedTableIds = next;
  }

  function toggleDriver(id: number) {
    const next = new Set(selectedDriverIds);
    if (next.has(id)) next.delete(id); else next.add(id);
    selectedDriverIds = next;
  }

  function selectAll() {
    if (activeTab === 'tables') selectedTableIds = new Set(filteredTables.map((t) => t.id));
    else selectedDriverIds = new Set(filteredDrivers.map((d) => d.id));
  }

  function deselectAll() {
    if (activeTab === 'tables') selectedTableIds = new Set();
    else selectedDriverIds = new Set();
  }

  function expandTable(id: number) {
    expandedTableId = expandedTableId === id ? null : id;
  }

  async function handleApply() {
    applying = true;
    applyResult = null;
    try {
      // Build modified table data for tables with overrides
      const tablesToWrite: Array<{ tableId: number; input0: number[]; input1: number[]; output: number[] }> = [];

      for (const table of tables) {
        if (!selectedTableIds.has(table.id)) continue;
        if (!table.current) continue;

        const ov = getOverride(table.id);
        const is1D = is1DTable({ tableType: table.tableType } as any);

        // Determine output values based on blend
        let output: number[];
        if (ov.blend >= 0.999) {
          output = [...table.import.output];
        } else if (ov.blend <= 0.001) {
          output = [...table.current.output];
        } else {
          // Blend between current and import
          const len = Math.max(table.current.output.length, table.import.output.length);
          output = new Array(len);
          for (let i = 0; i < len; i++) {
            const c = table.current.output[i] ?? 0;
            const v = table.import.output[i] ?? 0;
            output[i] = c + (v - c) * ov.blend;
          }
        }

        // Apply scale and offset
        if (ov.scale !== 1 || ov.offset !== 0) {
          output = output.map((v) => {
            let v2 = v * ov.scale + ov.offset;
            if (ov.clampMin != null && v2 < ov.clampMin) v2 = ov.clampMin;
            if (ov.clampMax != null && v2 > ov.clampMax) v2 = ov.clampMax;
            return Math.round(v2 * 10000) / 10000;
          });
        }

        // Determine axes based on axisMode
        let input0: number[];
        let input1: number[];

        if (ov.axisMode === 'current') {
          input0 = [...table.current.input0];
          input1 = [...table.current.input1];
        } else {
          input0 = [...table.import.input0];
          input1 = [...table.import.input1];
        }

        tablesToWrite.push({ tableId: table.id, input0, input1, output });
      }

      // Write modified tables
      let tablesWritten = 0;
      let tablesFailed = 0;
      if (tablesToWrite.length > 0) {
        const result = await HybridBridge.writeTableDataBatch(tablesToWrite);
        if (result.success) {
          tablesWritten = result.written ?? 0;
          tablesFailed = result.failed ?? 0;
        } else {
          tablesFailed = tablesToWrite.length;
        }
      }

      // Also import drivers via the original method if any are selected
      let driversWritten = 0;
      let driversFailed = 0;
      if (selectedDriverIds.size > 0) {
        // For now, use the original import for drivers (overrides only apply to tables)
        const driverResult = await HybridBridge.applyMecalImport(
          fileContent, [], Array.from(selectedDriverIds),
        );
        if (driverResult.success) {
          driversWritten = driverResult.driversWritten ?? 0;
          driversFailed = driverResult.driversFailed ?? 0;
        } else {
          driversFailed = selectedDriverIds.size;
        }
      }

      const parts: string[] = [];
      if (tablesWritten > 0) parts.push(`${tablesWritten} tables`);
      if (driversWritten > 0) parts.push(`${driversWritten} drivers`);
      if (tablesFailed > 0) parts.push(`${tablesFailed} tables failed`);
      if (driversFailed > 0) parts.push(`${driversFailed} drivers failed`);

      if (tablesFailed + driversFailed > 0 && tablesWritten + driversWritten === 0) {
        applyResult = { success: false, message: parts.join(', ') || 'Import failed' };
      } else {
        applyResult = { success: true, message: parts.length > 0 ? `Imported: ${parts.join(', ')}` : 'Import complete' };
        setTimeout(() => onApply(), 1500);
      }
    } catch (e: any) {
      applyResult = { success: false, message: e.message };
    } finally {
      applying = false;
    }
  }

  function switchTab(tab: 'tables' | 'drivers') {
    activeTab = tab;
    searchText = '';
    categoryFilter = 'All';
  }
</script>

{#if open}
  <div class="fixed inset-0 z-50 flex flex-col" style="background-color: var(--metro-bg, #1a1a2e);">
    <!-- Header -->
    <div class="flex items-center gap-3 border-b px-4 py-3 shrink-0" style="border-color: var(--metro-border, #333);">
      <button
        class="p-1 rounded transition-colors hover:bg-white/10"
        style="color: var(--metro-text-secondary, #A0A0A0);"
        onclick={() => { open = false; onCancel(); }}
      >
        <IconArrowLeft size={20} />
      </button>
      <div class="flex-1 min-w-0">
        <h1 class="text-base font-semibold truncate" style="color: var(--metro-text, #fff);">Import Calibration</h1>
        {#if metadata}
          <div class="text-[11px] truncate" style="color: var(--metro-text-secondary, #A0A0A0);">
            {metadata.productName} / {metadata.modelName} v{metadata.version}
          </div>
        {/if}
      </div>
      {#if !loading && !applyResult}
        <button
          class="flex items-center gap-1.5 rounded px-3 py-1.5 text-xs font-medium transition-colors shrink-0"
          style="background-color: var(--metro-green, #107C10); color: #fff;"
          disabled={applying || (selectedTableIds.size === 0 && selectedDriverIds.size === 0)}
          onclick={handleApply}
        >
          {#if applying} Applying... {:else}
            <IconCheck size={14} />
            Apply ({selectedTableIds.size}T {selectedDriverIds.size}D)
          {/if}
        </button>
      {/if}
    </div>

    {#if loading}
      <div class="flex-1 flex items-center justify-center">
        <div class="text-sm" style="color: var(--metro-text-secondary, #A0A0A0);">Reading ECU data...</div>
      </div>
    {:else if error}
      <div class="flex-1 flex items-center justify-center p-6">
        <div class="text-center space-y-3">
          <div class="text-sm" style="color: #f87171;">{error}</div>
          <button class="rounded px-3 py-1.5 text-xs" style="background-color: var(--metro-bg-hover, #2a2a4a); color: var(--metro-text, #fff);" onclick={() => { open = false; onCancel(); }}>Close</button>
        </div>
      </div>
    {:else if applyResult}
      <div class="flex-1 flex items-center justify-center p-6">
        <div class="text-center space-y-3 max-w-sm">
          <div class="text-sm" style="color: {applyResult.success ? '#4ade80' : '#f87171'};">{applyResult.message}</div>
          {#if !applyResult.success}
            <button class="rounded px-3 py-1.5 text-xs" style="background-color: var(--metro-bg-hover, #2a2a4a); color: var(--metro-text, #fff);" onclick={() => { applyResult = null; }}>Try Again</button>
          {/if}
        </div>
      </div>
    {:else}
      <!-- Summary badges -->
      <div class="flex gap-2 px-4 py-2 shrink-0 overflow-x-auto">
        <span class="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium" style="background-color: rgba(0,120,215,0.15); color: #60a5fa;">
          <IconTable size={12} /> {tables.length} tables
        </span>
        <span class="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium" style="background-color: rgba(107,44,145,0.15); color: #c084fc;">
          <IconAdjustments size={12} /> {drivers.length} drivers
        </span>
        <span class="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium" style="background-color: rgba(16,124,16,0.15); color: #4ade80;">
          <IconPackage size={12} /> {dataLinkCount} datalinks
        </span>
      </div>

      <!-- Tabs -->
      <div class="flex border-b px-4 shrink-0" style="border-color: var(--metro-border, #333);">
        <button class="px-3 py-2 text-xs font-medium transition-colors border-b-2" style="border-color: {activeTab === 'tables' ? 'var(--metro-blue, #0078D7)' : 'transparent'}; color: {activeTab === 'tables' ? 'var(--metro-text, #fff)' : 'var(--metro-text-secondary, #A0A0A0)'};" onclick={() => switchTab('tables')}>Tables ({tables.length})</button>
        <button class="px-3 py-2 text-xs font-medium transition-colors border-b-2" style="border-color: {activeTab === 'drivers' ? 'var(--metro-purple, #6B2C91)' : 'transparent'}; color: {activeTab === 'drivers' ? 'var(--metro-text, #fff)' : 'var(--metro-text-secondary, #A0A0A0)'};" onclick={() => switchTab('drivers')}>Drivers ({drivers.length})</button>
      </div>

      <!-- Search + filters -->
      <div class="px-4 py-2 space-y-2 shrink-0">
        <div class="relative">
          <IconSearch size={14} class="absolute left-2 top-1/2 -translate-y-1/2" style="color: var(--metro-text-muted, #666);" />
          <input type="text" placeholder="Search..." bind:value={searchText} class="w-full rounded border pl-7 pr-2 py-1.5 text-xs" style="border-color: var(--metro-border, #333); background-color: var(--metro-bg-hover, #2a2a4a); color: var(--metro-text, #fff);" />
        </div>
        {#if categories.length > 1}
          <div class="flex gap-1 overflow-x-auto pb-1 -mx-1 px-1">
            {#each categories as cat}
              <button class="px-2 py-0.5 text-[10px] rounded-full whitespace-nowrap transition-colors shrink-0" style="background-color: {categoryFilter === cat ? 'var(--metro-blue, #0078D7)' : 'var(--metro-bg-hover, #2a2a4a)'}; color: {categoryFilter === cat ? '#fff' : 'var(--metro-text-secondary, #A0A0A0)'};" onclick={() => (categoryFilter = cat)}>{cat}</button>
            {/each}
          </div>
        {/if}
        <div class="flex items-center justify-between">
          <span class="text-[10px]" style="color: var(--metro-text-muted, #666);">{selectedTableIds.size} of {filteredTables.length} tables selected</span>
          <div class="flex gap-1">
            <button class="rounded px-2 py-0.5 text-[10px]" style="background-color: var(--metro-bg-hover, #2a2a4a); color: var(--metro-text-secondary, #A0A0A0);" onclick={selectAll}>All</button>
            <button class="rounded px-2 py-0.5 text-[10px]" style="background-color: var(--metro-bg-hover, #2a2a4a); color: var(--metro-text-secondary, #A0A0A0);" onclick={deselectAll}>None</button>
          </div>
        </div>
      </div>

      <!-- Content -->
      <div class="flex-1 overflow-y-auto px-4 pb-4">
        {#if activeTab === 'tables'}
          {#if filteredTables.length === 0}
            <div class="py-8 text-center text-xs" style="color: var(--metro-text-muted, #666);">No tables match</div>
          {:else}
            {#each filteredTables as table (table.id)}
              {@const isSelected = selectedTableIds.has(table.id)}
              {@const isExpanded = expandedTableId === table.id}
              {@const changed = countChangedCells(table)}
              {@const hasCurrent = table.current != null}
              {@const override = getOverride(table.id)}

              <div class="rounded-lg mb-2 transition-all" style="border: 1px solid {isSelected ? 'rgba(0,120,215,0.3)' : 'var(--metro-border, #333)'}; background-color: {isSelected ? 'rgba(0,120,215,0.05)' : 'var(--metro-card, #16213e)'};">
                <!-- Card header -->
                <div class="flex items-center gap-2 px-3 py-2 cursor-pointer" onclick={() => expandTable(table.id)}>
                  <button
                    class="w-5 h-5 rounded flex items-center justify-center shrink-0 transition-colors"
                    style="background-color: {isSelected ? 'var(--metro-blue, #0078D7)' : 'var(--metro-bg-hover, #2a2a4a)'};"
                    onclick={(e) => { e.stopPropagation(); toggleTable(table.id); }}
                  >
                    {#if isSelected}<IconCheck size={12} color="#fff" />{/if}
                  </button>
                  <div class="flex-1 min-w-0">
                    <div class="flex items-center gap-2">
                      <span class="text-xs font-medium truncate" style="color: var(--metro-text, #fff);">{table.name}</span>
                      {#if !hasCurrent}
                        <span class="text-[9px] rounded px-1 py-0.5 shrink-0" style="background-color: rgba(232,17,35,0.15); color: #f87171;">new</span>
                      {:else if changed > 0}
                        <span class="text-[9px] rounded px-1 py-0.5 shrink-0" style="background-color: rgba(250,204,21,0.15); color: #facc15;">{changed} changed</span>
                      {:else}
                        <span class="text-[9px] rounded px-1 py-0.5 shrink-0" style="background-color: rgba(16,124,16,0.15); color: #4ade80;">identical</span>
                      {/if}
                    </div>
                    <div class="text-[10px]" style="color: var(--metro-text-secondary, #A0A0A0);">{table.category} &middot; {table.tableType} &middot; {table.cols}&times;{table.rows}</div>
                  </div>
                  <div class="shrink-0" style="color: var(--metro-text-muted, #666);">
                    {#if isExpanded}<IconChevronDown size={16} />{:else}<IconChevronRight size={16} />{/if}
                  </div>
                </div>

                <!-- Expanded detail -->
                {#if isExpanded && hasCurrent}
                  <div class="border-t space-y-3" style="border-color: var(--metro-border, #333);">
                    <!-- Axis info bar -->
                    <div class="px-3 pt-3 grid grid-cols-3 gap-2 text-[10px]">
                      <div><span style="color: var(--metro-text-muted, #666);">X:</span> <span style="color: var(--metro-text-secondary, #A0A0A0);">{override.axisMode === 'current' ? (table.currentInput0Name ?? table.input0Name) : table.input0Name}</span></div>
                      <div><span style="color: var(--metro-text-muted, #666);">Y:</span> <span style="color: var(--metro-text-secondary, #A0A0A0);">{override.axisMode === 'current' ? (table.currentInput1Name ?? table.input1Name) : table.input1Name}</span></div>
                      <div><span style="color: var(--metro-text-muted, #666);">Out:</span> <span style="color: var(--metro-text-secondary, #A0A0A0);">{override.axisMode === 'current' ? (table.currentOutputName ?? table.outputName) : table.outputName}</span></div>
                    </div>

                    <!-- Real table in diff mode -->
                    <div class="px-3">
                      <ReadOnlyTable
                        tableDef={buildTableDef(table, override.axisMode)}
                        currentData={table.current!}
                        importData={table.import}
                        axisMode={override.axisMode}
                      />
                    </div>

                    <!-- Controls -->
                    <div class="px-3 pb-3 space-y-3">
                      <!-- Axis selection -->
                      <div class="space-y-1">
                        <div class="text-[10px] font-medium" style="color: var(--metro-text-secondary, #A0A0A0);">Axis Handling</div>
                        <div class="flex gap-1">
                          {#each [{ value: 'import', label: 'Import axes' }, { value: 'current', label: 'ECU axes' }] as opt}
                            <button
                              class="rounded px-2 py-1 text-[10px] transition-colors flex-1"
                              style="background-color: {override.axisMode === opt.value ? 'var(--metro-blue, #0078D7)' : 'var(--metro-bg-hover, #2a2a4a)'}; color: {override.axisMode === opt.value ? '#fff' : 'var(--metro-text-secondary, #A0A0A0)'};"
                              onclick={() => setOverride(table.id, { axisMode: opt.value as any })}
                            >{opt.label}</button>
                          {/each}
                        </div>
                      </div>

                      <!-- Blend slider -->
                      <div class="space-y-1">
                        <div class="flex items-center justify-between">
                          <span class="text-[10px] font-medium" style="color: var(--metro-text-secondary, #A0A0A0);">Blend</span>
                          <span class="text-[10px] tabular-nums" style="color: var(--metro-text-muted, #666);">{Math.round(override.blend * 100)}% import</span>
                        </div>
                        <input
                          type="range" min="0" max="1" step="0.01" value={override.blend}
                          oninput={(e) => setOverride(table.id, { blend: parseFloat((e.target as HTMLInputElement).value) })}
                          class="w-full h-1.5 rounded-full appearance-none cursor-pointer"
                          style="background: linear-gradient(to right, var(--metro-bg-hover, #2a2a4a) 0%, var(--metro-blue, #0078D7) {override.blend * 100}%, var(--metro-bg-hover, #2a2a4a) {override.blend * 100}%);"
                        />
                        <div class="flex justify-between text-[8px]" style="color: var(--metro-text-muted, #666);">
                          <span>Keep current</span><span>Full import</span>
                        </div>
                      </div>

                      <!-- Scale + Offset -->
                      <div class="grid grid-cols-2 gap-2">
                        <label class="flex flex-col gap-0.5">
                          <span class="text-[10px]" style="color: var(--metro-text-muted, #666);">Scale</span>
                          <input type="number" step="0.01" value={override.scale} oninput={(e) => setOverride(table.id, { scale: parseFloat((e.target as HTMLInputElement).value) || 1 })} class="rounded border px-2 py-1 text-[11px]" style="border-color: var(--metro-border, #333); background-color: var(--metro-bg, #1a1a2e); color: var(--metro-text, #fff);" />
                        </label>
                        <label class="flex flex-col gap-0.5">
                          <span class="text-[10px]" style="color: var(--metro-text-muted, #666);">Offset</span>
                          <input type="number" step="0.1" value={override.offset} oninput={(e) => setOverride(table.id, { offset: parseFloat((e.target as HTMLInputElement).value) || 0 })} class="rounded border px-2 py-1 text-[11px]" style="border-color: var(--metro-border, #333); background-color: var(--metro-bg, #1a1a2e); color: var(--metro-text, #fff);" />
                        </label>
                      </div>

                      <!-- Quick presets -->
                      <div class="flex gap-1 flex-wrap">
                        <button class="rounded px-2 py-0.5 text-[9px]" style="background-color: var(--metro-bg-hover, #2a2a4a); color: var(--metro-text-secondary, #A0A0A0);" onclick={() => setOverride(table.id, { blend: 0.5, scale: 1, offset: 0 })}>50/50</button>
                        <button class="rounded px-2 py-0.5 text-[9px]" style="background-color: var(--metro-bg-hover, #2a2a4a); color: var(--metro-text-secondary, #A0A0A0);" onclick={() => setOverride(table.id, { blend: 1, scale: 1.1, offset: 0 })}>+10%</button>
                        <button class="rounded px-2 py-0.5 text-[9px]" style="background-color: var(--metro-bg-hover, #2a2a4a); color: var(--metro-text-secondary, #A0A0A0);" onclick={() => setOverride(table.id, { blend: 1, scale: 0.9, offset: 0 })}>-10%</button>
                        <button class="rounded px-2 py-0.5 text-[9px]" style="background-color: var(--metro-bg-hover, #2a2a4a); color: var(--metro-text-secondary, #A0A0A0);" onclick={() => setOverride(table.id, { blend: 1, scale: 1, offset: 1 })}>+1</button>
                        <button class="rounded px-2 py-0.5 text-[9px]" style="background-color: var(--metro-bg-hover, #2a2a4a); color: var(--metro-text-secondary, #A0A0A0);" onclick={() => setOverride(table.id, { ...defaultOverride })}>Reset</button>
                      </div>
                    </div>
                  </div>
                {:else if isExpanded && !hasCurrent}
                  <div class="border-t px-3 py-3 text-[10px]" style="border-color: var(--metro-border, #333); color: var(--metro-text-muted, #666);">
                    Table not found on ECU — will be written as new.
                  </div>
                {/if}
              </div>
            {/each}
          {/if}
        {:else}
          {#if filteredDrivers.length === 0}
            <div class="py-8 text-center text-xs" style="color: var(--metro-text-muted, #666);">No drivers match</div>
          {:else}
            {#each filteredDrivers as driver (driver.id)}
              {@const isSelected = selectedDriverIds.has(driver.id)}
              <button
                class="w-full flex items-center gap-3 rounded-lg px-3 py-2.5 mb-1.5 text-left transition-all"
                style="background-color: {isSelected ? 'rgba(107,44,145,0.1)' : 'var(--metro-card, #16213e)'}; border: 1px solid {isSelected ? 'rgba(107,44,145,0.3)' : 'var(--metro-border, #333)'};"
                onclick={() => toggleDriver(driver.id)}
              >
                <div class="w-5 h-5 rounded flex items-center justify-center shrink-0 transition-colors" style="background-color: {isSelected ? 'var(--metro-purple, #6B2C91)' : 'var(--metro-bg-hover, #2a2a4a)'};">
                  {#if isSelected}<IconCheck size={12} color="#fff" />{/if}
                </div>
                <div class="flex-1 min-w-0">
                  <div class="text-xs font-medium truncate" style="color: var(--metro-text, #fff);">{driver.name}</div>
                  <div class="text-[10px]" style="color: var(--metro-text-secondary, #A0A0A0);">{driver.category} &middot; {driver.numberOfConfigs} configs</div>
                </div>
                {#if driver.existsInEcu}
                  <span class="text-[9px] rounded-full px-1.5 py-0.5 shrink-0" style="background-color: rgba(16,124,16,0.15); color: #4ade80;">on ECU</span>
                {:else}
                  <span class="text-[9px] rounded-full px-1.5 py-0.5 shrink-0" style="background-color: rgba(232,17,35,0.15); color: #f87171;">new</span>
                {/if}
              </button>
            {/each}
          {/if}
        {/if}
      </div>
    {/if}
  </div>
{/if}

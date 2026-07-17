<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { HybridBridge, type AvailableSensor, type AvailableSensorsResult, type SensorCustomization } from '../lib/HybridBridge';
  import type { TableDefinition } from '../lib/tables/types';
  import type { DashboardTableEntry } from '../lib/HybridBridgeTypes';
  import { IconSearch, IconX, IconArrowRight, IconSettings, IconTable } from '@tabler/icons-svelte';
  import SensorCategoryFilter from './SensorCategoryFilter.svelte';
  import SensorCardList from './SensorCardList.svelte';
  import TableCardList from './TableCardList.svelte';

  let { onNavigate, dashboardName = 'default', onDashboardCreated }: {
    onNavigate: (page: string) => void;
    dashboardName?: string;
    onDashboardCreated?: (name: string) => void;
  } = $props();

  // ─── State ────────────────────────────────────────────────────────────────

  let sensors = $state<AvailableSensor[]>([]);
  let activeTab = $state<'sensors' | 'tables'>('sensors');
  let availableTables = $state<TableDefinition[]>([]);
  let selectedTableIds = $state<Set<number>>(new Set());
  let tableSortBy = $state<'name' | 'category' | 'dimensions'>('name');
  let selectedCategory = $state<string | null>(null);
  let tableSelectedCategory = $state<string | null>(null);
  let searchText = $state('');
  let tableSearchText = $state('');
  let currentPage = $state(1);
  const pageSize = 8;
  let loading = $state(true);
  let saving = $state(false);
  let error = $state<string | null>(null);
  let expandCustomizationId = $state<number | null>(null);
  let backgroundImagePath = $state<string | null>(null);
  let bgPicking = $state(false);

  // Customization edit state (per sensor, stored by id)
  let edits = $state<Record<number, {
    customName: string;
    customUnit: string;
    minRange: string;
    maxRange: string;
    minRangeBypass: boolean;
    maxRangeBypass: boolean;
  }>>({});

  // ─── Derived (auto-computed, no manual cascade) ──────────────────────────

  let selectedCount = $derived(sensors.filter(s => s.isSelected).length);
  let totalCount = $derived(sensors.length);

  // Categories derived from sensors — no manual rebuild on toggle
  let categories = $derived.by(() => {
    const catMap = new Map<string, { total: number; selected: number }>();
    for (const s of sensors) {
      const cat = catMap.get(s.category) || { total: 0, selected: 0 };
      cat.total++;
      if (s.isSelected) cat.selected++;
      catMap.set(s.category, cat);
    }
    return [
      { name: 'All', total: sensors.length, selected: selectedCount },
      ...Array.from(catMap.entries())
              .sort((a, b) => (b[1].selected > 0 ? 1 : 0) - (a[1].selected > 0 ? 1 : 0) || a[0].localeCompare(b[0]))
              .map(([name, counts]) => ({ name, ...counts }))
    ];
  });

  // Filtered list derived from sensors + category + search
  let filteredSensors = $derived.by(() => {
    let filtered = sensors;

    if (selectedCategory && selectedCategory !== 'All') {
      filtered = filtered.filter(s => s.category === selectedCategory);
    }

    if (searchText.trim()) {
      const q = searchText.toLowerCase();
      filtered = filtered.filter(s =>
              s.name.toLowerCase().includes(q) ||
              s.category.toLowerCase().includes(q) ||
              s.unit.toLowerCase().includes(q) ||
              String(s.id).includes(q)
      );
    }

    // Selected first
    return [...filtered].sort((a, b) => (a.isSelected === b.isSelected ? 0 : a.isSelected ? -1 : 1));
  });

  // Total pages derived from filtered count
  let totalPages = $derived(Math.max(1, Math.ceil(filteredSensors.length / pageSize)));

  // ─── Tables-tab filtering ───────────────────────────────────────────────
  let tableCategories = $derived.by(() => {
    const catMap = new Map<string, { total: number; selected: number }>();
    for (const t of availableTables) {
      const c = catMap.get(t.category) || { total: 0, selected: 0 };
      c.total++;
      if (selectedTableIds.has(t.id)) c.selected++;
      catMap.set(t.category, c);
    }
    return [
      { name: 'All', total: availableTables.length, selected: selectedTableIds.size },
      ...Array.from(catMap.entries())
              .sort((a, b) => (b[1].selected > 0 ? 1 : 0) - (a[1].selected > 0 ? 1 : 0) || a[0].localeCompare(b[0]))
              .map(([name, counts]) => ({ name, ...counts }))
    ];
  });

  let filteredAvailableTables = $derived.by(() => {
    let result = [...availableTables];
    if (tableSelectedCategory && tableSelectedCategory !== 'All') {
      result = result.filter(t => t.category === tableSelectedCategory);
    }
    if (tableSearchText.trim()) {
      const q = tableSearchText.toLowerCase();
      result = result.filter(t =>
        t.name.toLowerCase().includes(q) ||
        t.category.toLowerCase().includes(q) ||
        (t.input0Name ?? '').toLowerCase().includes(q) ||
        (t.outputName ?? '').toLowerCase().includes(q)
      );
    }
    result.sort((a, b) => {
      if (tableSortBy === 'name') return a.name.localeCompare(b.name);
      if (tableSortBy === 'category') return a.category.localeCompare(b.category) || a.name.localeCompare(b.name);
      if (tableSortBy === 'dimensions') return (a.rows * a.cols) - (b.rows * b.cols) || a.name.localeCompare(b.name);
      return 0;
    });
    // Selected first, then sort
    result.sort((a, b) => {
      const aSel = selectedTableIds.has(a.id);
      const bSel = selectedTableIds.has(b.id);
      return (aSel === bSel ? 0 : aSel ? -1 : 1);
    });
    return result;
  });

  let tableSelectedCount = $derived(selectedTableIds.size);
  let tableTotalCount = $derived(availableTables.length);

  // Clamp currentPage when totalPages changes
  $effect(() => {
    if (currentPage > totalPages) currentPage = totalPages;
  });

  // Reset page when filter changes
  $effect(() => {
    void selectedCategory;
    void searchText;
    void tableSelectedCategory;
    void tableSearchText;
    currentPage = 1;
  });

  // Paged list derived from filtered + page
  let pagedSensors = $derived.by(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredSensors.slice(start, start + pageSize);
  });

  // ─── Load data ────────────────────────────────────────────────────────────

  async function loadSensors() {
    loading = true;
    error = null;
    try {
      const result: AvailableSensorsResult = await HybridBridge.getAvailableSensors(dashboardName);
      if (!mounted) return;
      if (result.error) {
        error = result.error;
        return;
      }
      sensors = result.sensors;
      backgroundImagePath = result.backgroundImagePath ?? null;

      // Load available tables
      const tableResult = await HybridBridge.getTableDefinitions();
      if (!mounted) return;
      availableTables = tableResult.tables ?? [];

      // Load existing table selections from dashboard config
      const dashConfig = await HybridBridge.getDashboardConfig(dashboardName);
      if (!mounted) return;
      if (dashConfig.tables) {
        selectedTableIds = new Set(dashConfig.tables.map(t => t.tableId));
      }
    } catch (err) {
      if (!mounted) return;
      error = String(err);
    } finally {
      if (mounted) loading = false;
    }
  }

  let debounceTimer: ReturnType<typeof setTimeout> | null = null;
  let mounted = false;

  function onSearchInput(e: Event) {
    const target = e.target as HTMLInputElement;
    if (debounceTimer) clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => {
      searchText = target.value;
    }, 300);
  }

  function onTableSearchInput(e: Event) {
    const target = e.target as HTMLInputElement;
    if (debounceTimer) clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => {
      tableSearchText = target.value;
    }, 300);
  }

  function selectCategory(name: string) {
    selectedCategory = name === 'All' ? null : name;
  }
  function selectTableCategory(name: string) {
    tableSelectedCategory = name === 'All' ? null : name;
  }

  // ─── Selection ────────────────────────────────────────────────────────────

  function toggleSensor(id: number) {
    // Direct mutation — $derived categories/filters recompute automatically
    const s = sensors.find(s => s.id === id);
    if (s) s.isSelected = !s.isSelected;
    sensors = sensors; // trigger reactivity
  }

  // ─── Customization ────────────────────────────────────────────────────────

  function toggleCustomization(id: number) {
    if (expandCustomizationId === id) {
      expandCustomizationId = null;
      return;
    }
    expandCustomizationId = id;
    const s = sensors.find(s => s.id === id);
    if (s) {
      edits[id] = {
        customName: s.customization?.customName ?? '',
        customUnit: s.customization?.customUnit ?? '',
        minRange: s.customization?.minRange != null ? String(s.customization.minRange) : '',
        maxRange: s.customization?.maxRange != null ? String(s.customization.maxRange) : '',
        minRangeBypass: s.customization?.minRangeBypass ?? false,
        maxRangeBypass: s.customization?.maxRangeBypass ?? false,
      };
    }
  }

  function saveCustomization(id: number) {
    const e = edits[id];
    if (!e) return;
    const minVal = e.minRange ? parseFloat(e.minRange) : null;
    const maxVal = e.maxRange ? parseFloat(e.maxRange) : null;
    const hasName = e.customName.trim().length > 0;
    const hasUnit = e.customUnit.trim().length > 0;
    const hasBypass = e.minRangeBypass || e.maxRangeBypass;

    const cust: SensorCustomization | null = (hasName || hasUnit || minVal != null || maxVal != null || hasBypass)
            ? {
              customName: hasName ? e.customName.trim() : null,
              customUnit: hasUnit ? e.customUnit.trim() : null,
              minRange: minVal,
              maxRange: maxVal,
              minRangeBypass: e.minRangeBypass,
              maxRangeBypass: e.maxRangeBypass,
            }
            : null;

    const s = sensors.find(s => s.id === id);
    if (s) s.customization = cust;
    sensors = sensors;
    expandCustomizationId = null;
  }

  function clearCustomization(id: number) {
    const s = sensors.find(s => s.id === id);
    if (s) s.customization = null;
    delete edits[id];
    sensors = sensors;
    expandCustomizationId = null;
  }

  // ─── Table selection ──────────────────────────────────────────────────────

  function toggleTable(id: number) {
    const next = new Set(selectedTableIds);
    if (next.has(id)) {
      next.delete(id);
    } else {
      next.add(id);
    }
    selectedTableIds = next;
  }

  // ─── Save ─────────────────────────────────────────────────────────────────

  async function handleSave() {
    saving = true;
    try {
      const selectedIds = sensors.filter(s => s.isSelected).map(s => s.id);
      const customizations: Record<string, SensorCustomization> = {};
      for (const s of sensors) {
        if (s.customization) {
          customizations[String(s.id)] = s.customization;
        }
      }
      const result = await HybridBridge.saveSensorSelection({
        dashboardName,
        selectedIds,
        customizations,
        backgroundImagePath,
      });
      if (!result.success) {
        error = result.error || 'Save failed';
        return;
      }
      // Save table selections — first table on this dashboard auto-fits for legibility.
      const tables: DashboardTableEntry[] = [];
      const isFirst = selectedTableIds.size === 1;
      let idx = 0;
      for (const tid of selectedTableIds) {
        const entry: DashboardTableEntry = isFirst
          ? { tableId: tid, fractionX: 0.025, fractionY: 0.075, widthFraction: 0.95, heightFraction: 0.85, zIndex: 0 }
          : { tableId: tid, fractionX: 0.1 + idx * 0.2, fractionY: 0.1, widthFraction: 0.2, heightFraction: 0.3, zIndex: 0 };
        tables.push(entry);
        idx++;
      }
      await HybridBridge.saveDashboardTables(dashboardName, tables);
      onNavigate('dashboard');
    } catch (err) {
      error = String(err);
    } finally {
      saving = false;
    }
  }

  // ─── Background Image ───────────────────────────────────────────────────

  async function pickBackground() {
    bgPicking = true;
    try {
      const result = await HybridBridge.pickDashboardBackground();
      if (result.picked && result.path) {
        backgroundImagePath = result.path;
      }
    } catch (err) {
      error = String(err);
    } finally {
      bgPicking = false;
    }
  }

  function removeBackground() {
    backgroundImagePath = null;
  }

  // ─── Lifecycle ────────────────────────────────────────────────────────────

  onMount(() => {
    mounted = true;
    loadSensors();
  });

  onDestroy(() => {
    mounted = false;
    if (debounceTimer) {
      clearTimeout(debounceTimer);
      debounceTimer = null;
    }
  });
</script>

<div class="flex w-full flex-col lg:h-full">
  <!-- Header -->
  <div class="mb-4 flex shrink-0 items-center gap-3">
    <div class="flex h-10 w-10 shrink-0 items-center justify-center" style="background-color: var(--metro-purple);">
      <IconSettings size={20} style="color: var(--metro-text-on-accent);" />
    </div>
    <div>
      <h2 class="text-[20px] font-extrabold uppercase tracking-[-0.5px]" style="color: var(--metro-text);">Configure Sensors</h2>
      <p class="text-[11px]" style="color: var(--metro-text-secondary);">Dashboard: {dashboardName} — Select and customize gauges</p>
    </div>
  </div>

  <!-- Tab bar -->
  <div class="mb-4 flex shrink-0 gap-1 border-b" style="border-color: var(--metro-border);">
    <button
      class="flex items-center gap-1.5 border-b-2 px-4 py-2 text-[12px] font-bold uppercase tracking-wider transition-colors duration-150"
      style="border-color: {activeTab === 'sensors' ? 'var(--metro-orange)' : 'transparent'}; color: {activeTab === 'sensors' ? 'var(--metro-orange)' : 'var(--metro-text-muted)'};"
      onclick={() => { activeTab = 'sensors'; }}
    >
      <IconSettings size={14} />
      Sensors
    </button>
    <button
      class="flex items-center gap-1.5 border-b-2 px-4 py-2 text-[12px] font-bold uppercase tracking-wider transition-colors duration-150"
      style="border-color: {activeTab === 'tables' ? 'var(--metro-orange)' : 'transparent'}; color: {activeTab === 'tables' ? 'var(--metro-orange)' : 'var(--metro-text-muted)'};"
      onclick={() => { activeTab = 'tables'; }}
    >
      <IconTable size={14} />
      Tables
      {#if selectedTableIds.size > 0}
        <span class="ml-1 rounded-full px-1.5 py-0.5 text-[9px] font-bold" style="background-color: var(--metro-orange); color: #fff;">{selectedTableIds.size}</span>
      {/if}
    </button>
  </div>

  <!-- Background image picker -->
  <div class="mb-4 shrink-0 p-4" style="background-color: var(--metro-card); border: 1px solid var(--metro-border);">
    <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <p class="text-[13px] font-extrabold uppercase tracking-wider" style="color: var(--metro-text-secondary);">Dashboard Background</p>
        {#if backgroundImagePath}
          <p class="mt-1 text-[12px] truncate max-w-[300px]" style="color: var(--metro-text-secondary);">{backgroundImagePath.split(/[/\\]/).pop()}</p>
        {:else}
          <p class="mt-1 text-[12px]" style="color: var(--metro-text-muted);">No background set</p>
        {/if}
      </div>
      <div class="flex gap-2">
        <button
                class="metro-hover-bg flex-1 px-3 py-2 text-[12px] font-medium transition-colors duration-150 disabled:opacity-50 sm:flex-none sm:py-1.5"
                style="background-color: var(--metro-input-bg); border: 1px solid var(--metro-input-border); color: var(--metro-text-secondary);"
                onclick={pickBackground}
                disabled={bgPicking}
        >
          {bgPicking ? 'Picking…' : 'Pick Image'}
        </button>
        {#if backgroundImagePath}
          <button
                  class="metro-btn-danger flex-1 px-3 py-2 text-[12px] font-medium sm:flex-none sm:py-1.5"
                  onclick={removeBackground}
          >
            Remove
          </button>
        {/if}
      </div>
    </div>
  </div>

  <!-- Main area -->
  <div class="relative flex flex-col gap-4 lg:min-h-0 lg:flex-1 lg:flex-row lg:overflow-hidden">
    <!-- Loading overlay -->
    {#if loading}
      <div class="absolute inset-0 z-10 flex items-center justify-center" style="background-color: rgba(10, 10, 10, 0.8);">
        <div class="flex flex-col items-center gap-2">
          <span class="inline-block h-6 w-6 animate-spin rounded-full border-2 border-[#444]" style="border-top-color: var(--metro-purple);"></span>
          <span class="text-[12px]" style="color: var(--metro-text-secondary);">Loading sensors…</span>
        </div>
      </div>
    {/if}

    <!-- Error state -->
    {#if error && !loading}
      <div class="absolute inset-0 z-10 flex items-center justify-center" style="background-color: rgba(10, 10, 10, 0.8);">
        <div class="flex flex-col items-center gap-2">
          <p class="text-[13px]" style="color: var(--metro-red);">{error}</p>
          <button
                  class="metro-btn-primary px-3 py-1.5 text-[12px]"
                  onclick={loadSensors}
          >Retry</button>
        </div>
      </div>
    {/if}

    <!-- Category filter -->
    {#if activeTab === 'sensors'}
      <SensorCategoryFilter
        {categories}
        selectedCategory={selectedCategory ?? 'All'}
        onSelect={selectCategory}
      />
    {:else}
      <SensorCategoryFilter
        categories={tableCategories}
        selectedCategory={tableSelectedCategory ?? 'All'}
        onSelect={selectTableCategory}
      />
    {/if}

    <!-- Main content -->
    <div class="flex flex-1 flex-col lg:min-h-0 lg:overflow-hidden">
      {#if activeTab === 'sensors'}
        <!-- Search -->
        <div class="mb-3 flex shrink-0 flex-col gap-2 sm:flex-row sm:items-center">
          <div class="relative flex-1">
            <IconSearch size={14} class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2" style="color: var(--metro-text-muted);" />
            <input
                    type="text"
                    placeholder="Search sensors..."
                    value={searchText}
                    oninput={onSearchInput}
                    class="w-full py-2.5 pl-9 pr-3 text-[13px] outline-none transition-colors duration-150"
                    style="background-color: var(--metro-input-bg); border: 1px solid var(--metro-input-border); color: var(--metro-text);"
                    onfocus={(e) => { e.currentTarget.style.borderColor = 'var(--metro-purple)'; }}
                    onblur={(e) => { e.currentTarget.style.borderColor = 'var(--metro-input-border)'; }}
            />
            {#if searchText}
              <button
                      class="metro-hover-text absolute right-2 top-1/2 -translate-y-1/2 transition-colors duration-150"
                      style="color: var(--metro-text-muted);"
                      onclick={() => { searchText = ''; }}
              >
                <IconX size={14} />
              </button>
            {/if}
          </div>
        </div>

        <!-- Sensor list -->
        <SensorCardList
          sensors={pagedSensors}
          {expandCustomizationId}
          {edits}
          {toggleSensor}
          {toggleCustomization}
          {saveCustomization}
          {clearCustomization}
        />

        <!-- Pagination -->
        {#if totalPages > 1}
          <div class="mt-3 flex shrink-0 items-center justify-center gap-3">
            <button
                    class="metro-hover-text px-4 py-2 text-[12px] font-medium transition-colors duration-150 disabled:opacity-40"
                    style="color: var(--metro-text-secondary);"
                    disabled={currentPage <= 1}
                    onclick={() => { currentPage--; }}
            >
              Prev
            </button>
            <span class="text-[11px]" style="color: var(--metro-text-muted);">Page {currentPage} of {totalPages}</span>
            <button
                    class="metro-hover-text px-4 py-2 text-[12px] font-medium transition-colors duration-150 disabled:opacity-40"
                    style="color: var(--metro-text-secondary);"
                    disabled={currentPage >= totalPages}
                    onclick={() => { currentPage++; }}
            >
              Next
            </button>
          </div>
        {/if}
      {:else}
        <!-- Tables tab -->
        <div class="flex flex-1 flex-col lg:min-h-0 lg:overflow-hidden">
          <p class="mb-3 shrink-0 text-[12px]" style="color: var(--metro-text-secondary);">Select tables to show on the dashboard. Tapping a table widget opens the full editor.</p>

          <!-- Search -->
          <div class="mb-3 flex shrink-0 flex-col gap-2 sm:flex-row sm:items-center">
            <div class="relative flex-1">
              <IconSearch size={14} class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2" style="color: var(--metro-text-muted);" />
              <input
                type="text"
                placeholder="Search tables..."
                value={tableSearchText}
                oninput={onTableSearchInput}
                class="w-full py-2.5 pl-9 pr-3 text-[13px] outline-none transition-colors duration-150"
                style="background-color: var(--metro-input-bg); border: 1px solid var(--metro-input-border); color: var(--metro-text);"
                onfocus={(e) => { e.currentTarget.style.borderColor = 'var(--metro-purple)'; }}
                onblur={(e) => { e.currentTarget.style.borderColor = 'var(--metro-input-border)'; }}
              />
              {#if tableSearchText}
                <button
                  class="metro-hover-text absolute right-2 top-1/2 -translate-y-1/2 transition-colors duration-150"
                  style="color: var(--metro-text-muted);"
                  onclick={() => { tableSearchText = ''; }}
                >
                  <IconX size={14} />
                </button>
              {/if}
            </div>
          </div>

          <!-- Table list -->
          {#if availableTables.length === 0}
            <p class="py-6 text-center text-[12px]" style="color: var(--metro-text-muted);">No tables available</p>
          {:else}
            <TableCardList
              tables={filteredAvailableTables.map(t => ({ def: t, isSelected: selectedTableIds.has(t.id) }))}
              {selectedTableIds}
              {toggleTable}
            />
          {/if}
        </div>
      {/if}
    </div>
  </div>

  <!-- Footer -->
  <div class="mt-4 flex shrink-0 flex-col gap-2 sm:flex-row sm:items-center sm:justify-between p-4" style="background-color: var(--metro-card); border: 1px solid var(--metro-border);">
    <span class="text-[13px] font-semibold text-center sm:text-left" style="color: var(--metro-text-secondary);">
      Selected: {activeTab === 'sensors' ? selectedCount : tableSelectedCount} / {activeTab === 'sensors' ? totalCount : tableTotalCount}
    </span>
    <button
            class="metro-btn-primary w-full sm:w-auto px-4 py-2 text-[13px] font-bold uppercase tracking-wider transition-all duration-150 disabled:opacity-50"
            onclick={handleSave}
            disabled={saving}
    >
      {#if saving}
        <span class="flex items-center justify-center gap-2">
          <span class="inline-block h-3 w-3 animate-spin rounded-full border-2 border-white/30 border-t-white"></span>
          Saving…
        </span>
      {:else}
        <span class="flex items-center justify-center gap-2">
          Save & Go to Dashboard
          <IconArrowRight size={14} />
        </span>
      {/if}
    </button>
  </div>
</div>

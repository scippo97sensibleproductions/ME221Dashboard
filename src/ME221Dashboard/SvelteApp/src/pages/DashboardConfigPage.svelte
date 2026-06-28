<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { HybridBridge, type AvailableSensor, type AvailableSensorsResult, type SensorCustomization } from '../lib/HybridBridge';
  import { IconSearch, IconX, IconArrowRight, IconSettings } from '@tabler/icons-svelte';
  import SensorCategoryFilter from './SensorCategoryFilter.svelte';
  import SensorCardList from './SensorCardList.svelte';

  let { onNavigate, dashboardName = 'default', onDashboardCreated }: {
    onNavigate: (page: string) => void;
    dashboardName?: string;
    onDashboardCreated?: (name: string) => void;
  } = $props();

  // ─── State ────────────────────────────────────────────────────────────────

  let sensors = $state<AvailableSensor[]>([]);
  let selectedCategory = $state<string | null>(null);
  let searchText = $state('');
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

  // Clamp currentPage when totalPages changes
  $effect(() => {
    if (currentPage > totalPages) currentPage = totalPages;
  });

  // Reset page when filter changes
  $effect(() => {
    // Touch both to create dependency
    void selectedCategory;
    void searchText;
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
      if (result.error) {
        error = result.error;
        return;
      }
      sensors = result.sensors;
      backgroundImagePath = result.backgroundImagePath ?? null;
    } catch (err) {
      error = String(err);
    } finally {
      loading = false;
    }
  }

  let debounceTimer: ReturnType<typeof setTimeout> | null = null;

  function onSearchInput(e: Event) {
    const target = e.target as HTMLInputElement;
    if (debounceTimer) clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => {
      searchText = target.value;
    }, 300);
  }

  function selectCategory(name: string) {
    selectedCategory = name === 'All' ? null : name;
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

    const cust: SensorCustomization | null = (hasName || hasUnit || minVal != null || maxVal != null)
            ? {
              customName: hasName ? e.customName.trim() : null,
              customUnit: hasUnit ? e.customUnit.trim() : null,
              minRange: minVal,
              maxRange: maxVal,
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
      if (result.success) {
        onNavigate('dashboard');
      } else {
        error = result.error || 'Save failed';
      }
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
    loadSensors();
  });

  onDestroy(() => {
    if (debounceTimer) clearTimeout(debounceTimer);
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
    <SensorCategoryFilter
      {categories}
      selectedCategory={selectedCategory ?? 'All'}
      onSelect={selectCategory}
    />

    <!-- Main content -->
    <div class="flex flex-1 flex-col lg:min-h-0 lg:overflow-hidden">
      <!-- Search + mobile categories -->
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
    </div>
  </div>

  <!-- Footer -->
  <div class="mt-4 flex shrink-0 flex-col gap-2 sm:flex-row sm:items-center sm:justify-between p-4" style="background-color: var(--metro-card); border: 1px solid var(--metro-border);">
    <span class="text-[13px] font-semibold text-center sm:text-left" style="color: var(--metro-text-secondary);">
      Selected: {selectedCount} / {totalCount}
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

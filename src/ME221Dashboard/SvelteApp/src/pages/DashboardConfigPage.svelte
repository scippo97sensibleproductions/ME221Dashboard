<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { SvelteMap, SvelteSet } from 'svelte/reactivity';
  import { Modal, Button } from 'flowbite-svelte';
  import { HybridBridge, type AvailableSensor, type AvailableSensorsResult, type SensorCustomization } from '../lib/HybridBridge';
  import type { TableDefinition } from '../lib/tables/types';
  import type { DashboardTableEntry } from '../lib/HybridBridgeTypes';
  import { IconSearch, IconX, IconArrowRight, IconSettings, IconTable, IconCar, IconGauge } from '@tabler/icons-svelte';
  import SensorCategoryFilter from './SensorCategoryFilter.svelte';
  import SensorCardList from './SensorCardList.svelte';
  import TableCardList from './TableCardList.svelte';
  import NumberInput from '../lib/NumberInput.svelte';
  import ClampNotice from '../lib/gauges/ClampNotice.svelte';
  import { navigationGate } from '../lib/navigationGate.svelte';
  import {
    isShiftPointValid,
    isFloorFieldDisabled,
    isFloorEditValid,
    isFloorHeld,
    isSuggestionVisible,
    suggestedFloor,
    isEffectivelyInactiveFloor,
    computeClampedFloor,
    resolveClampDip,
    SHIFTER_COPY,
    formatShifterCopy,
    FLOOR_MIN,
    type ShifterSessionValues,
  } from '../lib/shift/shifterConfig';

  let { onNavigate, dashboardName = 'default', onOpenVehicleConfig, onRegisterShifterApi, onShifterDirtyChange }: {
    onNavigate: (page: string) => void;
    dashboardName?: string;
    onDashboardCreated?: (name: string) => void;
    onOpenVehicleConfig?: () => void;
    /** Registers the single save/discard/dirty API with App (U8 dirty gate). */
    onRegisterShifterApi?: (api: {
      save: () => Promise<boolean>;
      discard: () => Promise<void>;
      isDirty: () => boolean;
    }) => void;
    onShifterDirtyChange?: (dirty: boolean) => void;
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
  let shifterModalOpen = $state(false);
  let shifterSaveError = $state<string | null>(null);
  const shifterModal = navigationGate.registerModal('shifterSettings');

  $effect(() => {
    if (shifterModalOpen) shifterModal.open();
    else shifterModal.close();
  });
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
    const catMap = new SvelteMap<string, { total: number; selected: number }>();
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
    const catMap = new SvelteMap<string, { total: number; selected: number }>();
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
    const next = new SvelteSet(selectedTableIds);
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
      // Save table selections — preserve existing positions, only default new ones.
      const existingTableMap = new SvelteMap<number, DashboardTableEntry>();
      const existingConfig = await HybridBridge.getDashboardConfig(dashboardName);
      if (existingConfig.tables) {
        for (const t of existingConfig.tables) existingTableMap.set(t.tableId, t);
      }
      const tables: DashboardTableEntry[] = [];
      let newIdx = 0;
      for (const tid of selectedTableIds) {
        const existing = existingTableMap.get(tid);
        if (existing) {
          tables.push({ ...existing, tableId: tid });
        } else {
          const entry: DashboardTableEntry = selectedTableIds.size === 1
            ? { tableId: tid, fractionX: 0.025, fractionY: 0.075, widthFraction: 0.95, heightFraction: 0.85, zIndex: 0 }
            : { tableId: tid, fractionX: 0.1 + newIdx * 0.2, fractionY: 0.1, widthFraction: 0.2, heightFraction: 0.3, zIndex: 0 };
          tables.push(entry);
          newIdx++;
        }
      }
      await HybridBridge.saveDashboardTables(dashboardName, tables);
      onNavigate('gaugeBuilder');
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

  // ─── Shifter settings (R9, R19 lifecycle) ────────────────────────────────
  // Form session: deep-copies the payload on load (never binds the _configCache
  // object) so edits cannot poison the cache. The single save routine persists
  // on the section Save button and the App dialog's "Save and leave".
  let shifterSession = $state<ShifterSessionValues | null>(null);
  let lastPersistedShifter = $state<ShifterSessionValues>({ shiftPointRpm: 0, downshiftFloorRpm: 0 });
  let shiftPointError = $state<string | null>(null);
  let floorError = $state<string | null>(null);
  let shiftPointDraft = $state<string | null>(null);
  let floorDraft = $state<string | null>(null);
  // Held-state machinery (R19): the dip is frozen at establishment and never
  // re-captured by in-hold edits.
  let sessionDip = $state<number | null>(null);
  // Suggestion machinery: page-mount suppression keyed by dashboard; re-offer
  // on shift-point commit; "Suggest again" re-offers after dismissal.
  let suggestionDismissed = $state(false);
  let suggestionPending = $state(false);
  // Clamped-info notices: persist until any shifter/ramp edit or the producing
  // bound no longer holds (U8).
  let clampedNotice = $state<string | null>(null);
  let inactiveNotice = $state<string | null>(null);

  const floorHeld = $derived(
    shifterSession !== null && isFloorHeld(shifterSession.shiftPointRpm, shifterSession.downshiftFloorRpm)
  );

  const shifterDirty = $derived.by(() => {
    if (shifterSession === null) return false;
    const s = shifterSession;
    const p = lastPersistedShifter;
    if (s.shiftPointRpm !== p.shiftPointRpm || s.downshiftFloorRpm !== p.downshiftFloorRpm) return true;
    // Typed-but-uncommitted text counts as dirty (R19).
    if (shiftPointDraft !== null && shiftPointDraft !== String(s.shiftPointRpm)) return true;
    if (floorDraft !== null && floorDraft !== String(s.downshiftFloorRpm)) return true;
    return false;
  });

  const clampPreview = $derived.by(() => {
    if (!shifterSession || !floorHeld) return null;
    const dip = resolveClampDip(sessionDip, persistedSpacing());
    return computeClampedFloor(shifterSession.shiftPointRpm, dip);
  });

  const suggestion = $derived.by(() => {
    if (!shifterSession || suggestionDismissed || !suggestionPending) return null;
    if (!isSuggestionVisible(shifterSession.shiftPointRpm)) return null;
    return suggestedFloor(shifterSession.shiftPointRpm);
  });

  // Muted ▼-disabled states (R19): floor unset, or the shift point sitting in
  // the hidden band (shift point − 1500 ≤ IDLE_FLOOR). The floor column omits
  // the label when the field itself is disabled (its box already shows the hint).
  const mutedLabel = $derived.by(() => {
    if (!shifterSession) return null;
    const s = shifterSession;
    if (s.downshiftFloorRpm <= 0) return SHIFTER_COPY.floorUnset;
    if (!isSuggestionVisible(s.shiftPointRpm)) return SHIFTER_COPY.adviceUnavailable;
    return null;
  });

  const floorMutedLabel = $derived.by(() => {
    if (!shifterSession || isFloorFieldDisabled(shifterSession.shiftPointRpm)) return null;
    return mutedLabel;
  });

  const showSuggestAgain = $derived(
    shifterSession !== null && suggestionDismissed && shifterSession.downshiftFloorRpm <= 0
  );

  // Compact card summary for the collapsed section (the editor lives in a modal).
  const shifterSummary = $derived.by(() => {
    if (!shifterSession) return 'Loading…';
    const sp = Math.round(shifterSession.shiftPointRpm);
    const fp = Math.round(shifterSession.downshiftFloorRpm);
    if (sp <= 0) return 'Shift light not configured';
    return fp > 0
      ? `Shift at ${sp} rpm · Downshift floor ${fp} rpm`
      : `Shift at ${sp} rpm · No downshift floor`;
  });

  // Persisted spacing (shiftPoint − floor from the loaded baseline) — the
  // fallback dip when no session dip was established.
  function persistedSpacing(): number | null {
    const s = lastPersistedShifter;
    if (s.shiftPointRpm > 0 && s.downshiftFloorRpm > 0) {
      return s.shiftPointRpm - s.downshiftFloorRpm;
    }
    return null;
  }

  async function loadShifterConfig() {
    try {
      const vc = await HybridBridge.getVehicleConfig();
      lastPersistedShifter = {
        shiftPointRpm: vc.shifter?.shiftPointRpm ?? 0,
        downshiftFloorRpm: vc.shifter?.downshiftFloorRpm ?? 0,
      };
      shifterSession = { ...lastPersistedShifter };
    } catch {
      shifterSession = { shiftPointRpm: 0, downshiftFloorRpm: 0 };
      lastPersistedShifter = { ...shifterSession };
    }
    // Clamp-to-minimum (R19): any stored floor at or below IDLE_FLOOR (zero
    // excluded) re-surfaces the effectively-inactive notice + "Clear floor".
    if (isEffectivelyInactiveFloor(lastPersistedShifter.downshiftFloorRpm)) {
      inactiveNotice = SHIFTER_COPY.effectivelyInactive;
    }
  }

  async function persistShifter(session: ShifterSessionValues): Promise<boolean> {
    try {
      const vc = await HybridBridge.getVehicleConfig();
      const result = await HybridBridge.setVehicleConfig({
        ...vc,
        shifter: { shiftPointRpm: session.shiftPointRpm, downshiftFloorRpm: session.downshiftFloorRpm },
      });
      if (!result?.success) {
        shifterSaveError = result?.error ?? 'Failed to save shifter settings';
        return false;
      }
      shifterSaveError = null;
      return true;
    } catch (err) {
      shifterSaveError = String(err);
      return false;
    }
  }

  /** The single save routine (shared with the App dialog's "Save and leave").
   *  Force-commits any in-field text first (commit-reject runs before persist),
   *  applies the on-save clamp while held, persists, and updates the baseline.
   *  Returns false when persistence failed — the dirty gate then stays armed
   *  and the App keeps the user on the page instead of navigating away. */
  async function saveShifter(): Promise<boolean> {
    if (!shifterSession) return false;
    commitPendingDrafts();
    if (floorHeld) {
      const dip = resolveClampDip(sessionDip, persistedSpacing());
      const clamped = computeClampedFloor(shifterSession.shiftPointRpm, dip);
      const s = { ...shifterSession, downshiftFloorRpm: clamped };
      shifterSession = s;
      clampedNotice = isEffectivelyInactiveFloor(clamped)
        ? SHIFTER_COPY.effectivelyInactive
        : formatShifterCopy(SHIFTER_COPY.floorClamped, clamped);
    } else {
      clampedNotice = null;
    }
    if (inactiveNotice && !isEffectivelyInactiveFloor(shifterSession.downshiftFloorRpm)) {
      inactiveNotice = null;
    }
    const ok = await persistShifter(shifterSession);
    if (!ok) return false;
    lastPersistedShifter = { ...shifterSession };
    suggestionPending = false;
    suggestionDismissed = false;
    return true;
  }

  /** "Discard": refetch via getVehicleConfig — invalidating _configCache first,
   *  never merging (R19). */
  async function discardShifter(): Promise<void> {
    HybridBridge.invalidateVehicleConfigCache();
    shifterSession = null;
    shiftPointError = null;
    floorError = null;
    shiftPointDraft = null;
    floorDraft = null;
    clampedNotice = null;
    sessionDip = null;
    suggestionPending = false;
    suggestionDismissed = false;
    await loadShifterConfig();
  }

  function registerShifterApi() {
    onRegisterShifterApi?.({
      save: saveShifter,
      discard: discardShifter,
      isDirty: () => shifterDirty,
    });
  }

  function commitPendingDrafts() {
    if (shifterSession === null) return;
    if (shiftPointDraft !== null && shiftPointDraft !== String(shifterSession.shiftPointRpm)) {
      const v = parseFloat(shiftPointDraft);
      if (!isNaN(v)) commitShiftPoint(v);
    }
    if (floorDraft !== null && floorDraft !== String(shifterSession.downshiftFloorRpm)) {
      const v = parseFloat(floorDraft);
      if (!isNaN(v)) commitFloor(v);
    }
    shiftPointDraft = null;
    floorDraft = null;
  }

  function clearNoticesOnEdit() {
    clampedNotice = null;
    inactiveNotice = null;
  }

  function commitShiftPoint(v: number) {
    if (!shifterSession) return;
    if (!isShiftPointValid(v)) {
      // Live-reject (R19): revert ONLY the edited field to the last valid
      // value with an inline error — never the whole session, which would
      // discard valid unsaved edits to the other field.
      shiftPointError = SHIFTER_COPY.valueOutOfRange;
      shifterSession = { ...shifterSession, shiftPointRpm: lastPersistedShifter.shiftPointRpm };
      return;
    }
    shiftPointError = null;
    const prev = shifterSession;
    const next = { ...prev, shiftPointRpm: v };
    shifterSession = next;
    clearNoticesOnEdit();

    // Held entry: a shift-point change at or below an existing floor (R19).
    // The dip is frozen at establishment and kept for the whole session —
    // re-raising the shift point above the floor (release) never re-captures
    // it (R19).
    if (isFloorHeld(v, next.downshiftFloorRpm) && sessionDip === null) {
      // Establishment-time spacing: the PRE-edit spacing is used — the new
      // shift point may already sit below the floor, which would make the
      // new-minus-floor difference negative.
      sessionDip = prev.downshiftFloorRpm > 0 && prev.shiftPointRpm > prev.downshiftFloorRpm
        ? prev.shiftPointRpm - prev.downshiftFloorRpm
        : null;
    }

    // Zero→nonzero transition: re-enable the floor field and fire the first
    // re-derive suggestion in the same interaction (R19).
    if (prev.shiftPointRpm <= 0 && v > 0 && next.downshiftFloorRpm <= 0) {
      suggestionPending = true;
    } else if (suggestionPending) {
      // Re-offer on shift-point commit; the draft re-derives live.
      suggestionDismissed = false;
    }
  }

  function commitFloor(v: number) {
    if (!shifterSession) return;
    if (!isFloorEditValid(v, shifterSession.shiftPointRpm, floorHeld)) {
      floorError = SHIFTER_COPY.valueOutOfRange;
      shifterSession = { ...shifterSession, downshiftFloorRpm: lastPersistedShifter.downshiftFloorRpm };
      return;
    }
    floorError = null;
    clearNoticesOnEdit();
    const next = { ...shifterSession, downshiftFloorRpm: v };
    shifterSession = next;
    // A manual floor entry dismisses a pending draft (R19); a valid floor edit
    // at or below the new shift point releases the hold.
    if (v > 0) {
      suggestionPending = false;
      suggestionDismissed = false;
      // Establishment-time dip capture (manual edit or accept). While held the
      // dip stays frozen at establishment — in-hold floor edits must not
      // re-capture it (and a held value can sit at/above the shift point,
      // which would freeze a non-positive spacing).
      if (next.shiftPointRpm > 0 && !isFloorHeld(next.shiftPointRpm, v)) {
        sessionDip = next.shiftPointRpm - v;
      }
    }
  }

  function acceptSuggestion() {
    if (!shifterSession || suggestion === null) return;
    floorError = null;
    const next = { ...shifterSession, downshiftFloorRpm: suggestion };
    shifterSession = next;
    sessionDip = next.shiftPointRpm - suggestion;
    suggestionPending = false;
    suggestionDismissed = false;
    clearNoticesOnEdit();
  }

  function dismissSuggestion() {
    suggestionPending = false;
    suggestionDismissed = true;
  }

  function clearFloor() {
    if (!shifterSession) return;
    clearNoticesOnEdit();
    shifterSession = { ...shifterSession, downshiftFloorRpm: 0 };
    // "Clear floor" re-offers the suggestion immediately in-session (the floor
    // was never dismissed — R19).
    suggestionDismissed = false;
    suggestionPending = true;
  }

  // ─── Lifecycle ────────────────────────────────────────────────────────────

  // Dirty-state registration with the App dirty gate (U8): the gate is armed
  // while the form session or uncommitted text diverges from the baseline.
  $effect(() => {
    onShifterDirtyChange?.(shifterDirty);
  });

  onMount(() => {
    mounted = true;
    void loadShifterConfig();
    registerShifterApi();
  });

  // Reload sensors/customizations whenever the dashboard name changes. The page
  // is keyed by activeDashboard in App.svelte, but the name may resolve AFTER
  // the first mount (the active dashboard comes from the backend config). a
  // stale 'default' load would show another dashboard's data and lose the
  // per-dashboard customizations (e.g. gear range 0–6).
  let lastLoadedDashboard: string | null = null;
  $effect(() => {
    if (!mounted) return;
    const name = dashboardName;
    if (name === lastLoadedDashboard) return;
    lastLoadedDashboard = name;
    loadSensors();
  });

  onDestroy(() => {
    mounted = false;
    if (debounceTimer) {
      clearTimeout(debounceTimer);
      debounceTimer = null;
    }
    // Disarm the dirty gate on unmount: a destroyed page must not leave the
    // 'dirty-form' reason armed (silently blocking all navigation) nor leave a
    // dangling dialog behind (the App closes it when the gate disarms).
    onShifterDirtyChange?.(false);
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

  <!-- Vehicle + shifter configuration (R8, R9) — compact card; the shift-light
       editor lives in a modal so the sensor grid keeps the page real estate. -->
  <div class="mb-4 shrink-0 p-4" style="background-color: var(--metro-card); border: 1px solid var(--metro-border);">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div class="flex min-w-0 flex-col gap-0.5">
        <p class="text-[13px] font-extrabold uppercase tracking-wider" style="color: var(--metro-text-secondary);">Vehicle & Shifter</p>
        <p class="truncate text-[11px]" style="color: var(--metro-text-muted);" role="status">{shifterSummary}</p>
        {#if shifterDirty}
          <p class="flex items-center gap-1.5 text-[10px] font-bold uppercase tracking-wider" style="color: var(--metro-orange);" role="status">
            <span class="inline-block h-1.5 w-1.5 rounded-full" style="background-color: var(--metro-orange);"></span>
            Unsaved changes
          </p>
        {/if}
      </div>
      <div class="flex shrink-0 gap-2">
        <button
          class="metro-hover-bg px-3 py-2 text-[12px] font-medium transition-colors duration-150"
          style="background-color: var(--metro-input-bg); border: 1px solid var(--metro-input-border); color: var(--metro-text-secondary);"
          onclick={() => onOpenVehicleConfig?.()}
        >
          <span class="flex items-center gap-2"><IconCar size={14} /> Vehicle Config</span>
        </button>
        <button
          class="metro-btn-primary px-3 py-2 text-[12px] font-bold uppercase tracking-wider"
          onclick={() => { shifterModalOpen = true; }}
        >
          <span class="flex items-center gap-2"><IconGauge size={14} /> Shift Light</span>
        </button>
      </div>
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
    <div class="flex items-center gap-2 w-full sm:w-auto">
      <button
              class="metro-btn-secondary flex-1 sm:flex-none px-4 py-2 text-[13px] font-bold uppercase tracking-wider transition-all duration-150"
              onclick={() => onNavigate('dashboard')}
      >
        Back to Dashboard
      </button>
      <button
              class="metro-btn-primary flex-1 sm:flex-none px-4 py-2 text-[13px] font-bold uppercase tracking-wider transition-all duration-150 disabled:opacity-50"
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
            Save & Build Gauges
            <IconArrowRight size={14} />
          </span>
        {/if}
      </button>
    </div>
  </div>
</div>

<!-- Shift-light editor modal (R19) — the single save routine stays page-owned
     so the App dirty gate keeps working; closing with unsaved changes keeps the
     gate armed (the card shows the "Unsaved changes" chip). -->
{#if shifterModalOpen}
  <Modal bind:open={shifterModalOpen} size="md" placement="center" outsideclose={true} class="backdrop:bg-gray-900/80">
    {#snippet header()}
      <div class="flex w-full items-center justify-between">
        <h2 class="text-base font-semibold text-gray-100">Shift Light</h2>
        <p class="text-xs text-gray-500">Dashboard: {dashboardName}</p>
      </div>
    {/snippet}

    <div class="flex flex-col gap-4">
      {#if shifterSaveError}
        <div class="rounded border px-3 py-2 text-[12px]" style="border-color: rgba(232,17,35,0.4); background-color: rgba(232,17,35,0.1); color: var(--metro-red);" role="alert">
          {shifterSaveError}
        </div>
      {/if}

      <!-- Shift point -->
      <div>
        <div class="mb-1.5 flex items-center justify-between">
          <p class="text-[11px] font-bold uppercase tracking-wider" style="color: var(--metro-text-secondary);">Shift Point</p>
          {#if shifterSession}
            <span class="text-[11px] font-mono" style="color: var(--metro-orange);">{Math.round(shifterSession.shiftPointRpm)} rpm</span>
          {/if}
        </div>
        {#if shifterSession}
          <NumberInput
            value={shifterSession.shiftPointRpm}
            step={100}
            unit="rpm"
            onchange={commitShiftPoint}
            error={shiftPointError}
            onErrorAutoHide={() => { shiftPointError = null; }}
            forceCommitOnNudge
            ondraft={(t) => { shiftPointDraft = t; }}
          />
        {:else}
          <div class="flex items-center gap-2 text-[12px]" style="color: var(--metro-text-muted);">
            <span class="inline-block h-3 w-3 animate-spin rounded-full border border-[#444] border-t-[#888]"></span>
            Loading…
          </div>
        {/if}
        <p class="mt-1 text-[10px]" style="color: var(--metro-text-muted);">200–9000 rpm. The bar flashes and ▲ lights here.</p>
      </div>

      <!-- Downshift floor -->
      <div>
        <div class="mb-1.5 flex items-center justify-between">
          <p class="text-[11px] font-bold uppercase tracking-wider" style="color: var(--metro-text-secondary);">Downshift Floor</p>
          {#if shifterSession}
            <span class="text-[11px] font-mono" style="color: var(--metro-orange);">{Math.round(shifterSession.downshiftFloorRpm)} rpm</span>
          {/if}
        </div>
        {#if shifterSession}
          {#if isFloorFieldDisabled(shifterSession.shiftPointRpm)}
            <div class="flex min-h-[44px] items-center rounded-lg border px-3 text-[12px]" style="border-color: var(--metro-input-border); background-color: var(--metro-input-bg); color: var(--metro-text-muted);">
              {SHIFTER_COPY.floorFieldHint}
            </div>
          {:else}
            <NumberInput
              value={shifterSession.downshiftFloorRpm}
              step={100}
              unit="rpm"
              onchange={commitFloor}
              error={floorError}
              onErrorAutoHide={() => { floorError = null; }}
              forceCommitOnNudge
              ondraft={(t) => { floorDraft = t; }}
            />
          {/if}
        {:else}
          <div class="flex items-center gap-2 text-[12px]" style="color: var(--metro-text-muted);">
            <span class="inline-block h-3 w-3 animate-spin rounded-full border border-[#444] border-t-[#888]"></span>
            Loading…
          </div>
        {/if}

        <!-- Held-state indicator with clamp preview (R19) -->
        {#if floorHeld}
          <div class="mt-1.5 flex items-start gap-2 rounded border px-2.5 py-1.5 text-[11px]" style="border-color: rgba(245,159,0,0.35); background-color: rgba(245,159,0,0.08); color: #f5a623;" role="status">
            <span class="flex-1">
              Floor held above shift point — will clamp to {clampPreview != null ? Math.round(clampPreview) : '—'} on save
            </span>
          </div>
        {/if}

        <!-- Clamped-info notices (persist until any shifter/ramp edit, U8) -->
        {#if clampedNotice}
          <div class="mt-1.5">
            <ClampNotice variant="clamped-info-persist" message={clampedNotice} />
          </div>
        {/if}
        {#if inactiveNotice}
          <div class="mt-1.5">
            <ClampNotice variant="clamped-info-persist" message={inactiveNotice}>
              {#snippet actions()}
                <button
                  class="rounded px-2 py-0.5 text-[10px] font-bold transition-colors"
                  style="background-color: rgba(245,159,0,0.15); color: #f5a623;"
                  onclick={clearFloor}
                >{SHIFTER_COPY.clearFloor}</button>
              {/snippet}
            </ClampNotice>
          </div>
        {/if}

        <p class="mt-1 text-[10px]" style="color: var(--metro-text-muted);" role="status" aria-label={floorMutedLabel ?? undefined}>
          {#if floorMutedLabel}
            {floorMutedLabel}
          {:else}
            ▼ lights when RPM drops through the floor. Minimum {FLOOR_MIN} rpm.
          {/if}
        </p>
      </div>

      <!-- Suggestion overlay (R19): re-derived floor from an unset state -->
      {#if suggestion !== null && shifterSession}
        <div class="flex flex-col gap-2 rounded border px-3 py-2.5" style="border-color: rgba(0,120,215,0.4); background-color: rgba(0,120,215,0.08);" role="dialog" aria-label="Shift point suggestion">
          <p class="text-[12px]" style="color: var(--metro-text-secondary);">
            {formatShifterCopy(SHIFTER_COPY.suggestionBody, suggestion)}
          </p>
          <div class="flex gap-2">
            <button
              class="metro-btn-primary px-3 py-1.5 text-[11px] font-bold uppercase tracking-wider"
              onclick={acceptSuggestion}
            >Accept</button>
            <button
              class="metro-btn-secondary px-3 py-1.5 text-[11px] font-bold uppercase tracking-wider"
              onclick={dismissSuggestion}
            >Dismiss</button>
          </div>
        </div>
      {/if}
    </div>

    {#snippet footer()}
      <div class="flex w-full flex-wrap items-center justify-between gap-2">
        <p class="flex items-center gap-1.5 text-[11px]" style="color: var(--metro-text-muted);" role="status" aria-label={mutedLabel ?? undefined}>
          {#if mutedLabel}
            {mutedLabel}
          {/if}
          {#if showSuggestAgain}
            <button
              class="rounded px-2 py-0.5 text-[10px] font-bold transition-colors"
              style="background-color: var(--metro-input-bg); color: var(--metro-blue, #0078D7);"
              onclick={() => { suggestionDismissed = false; suggestionPending = true; }}
            >Suggest again</button>
          {/if}
        </p>
        <div class="flex gap-2">
          <Button color="alternative" class="!border-gray-600 !bg-gray-700 !text-gray-300 hover:!bg-gray-600" onclick={() => { shifterModalOpen = false; }}>
            Close
          </Button>
          <Button
            class="!bg-cyan-600 hover:!bg-cyan-500 !text-white border-cyan-600"
            onclick={() => void saveShifter()}
            disabled={shifterSession === null || !shifterDirty}
          >Save</Button>
        </div>
      </div>
    {/snippet}
  </Modal>
{/if}

<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { toast } from '../lib/toasts.svelte';
  import { IconChevronLeft, IconChevronDown, IconChevronRight, IconBookmark, IconRotate } from '@tabler/icons-svelte';
  import { HybridBridge } from '../lib/HybridBridge';
  import type { DataLinkWarningSetting, SaveWarningDatalinkPayload, WarningLevel, WarningPoint } from '../lib/HybridBridgeTypes';
  import { warningEvaluator } from '../lib/stores/warningEvaluator';
  import { navigationGate } from '../lib/navigationGate.svelte';
  import { warningToasts } from '../lib/warningToasts';
  import { SvelteSet } from 'svelte/reactivity';
  import LevelEditor from '../lib/warnings/LevelEditor.svelte';
  import PointEditor from '../lib/warnings/PointEditor.svelte';
  import BatchSheet from '../lib/warnings/BatchSheet.svelte';
  import { createLevelUndo } from '../lib/warnings/levelUndo';
  import { createBatchLedger } from '../lib/warnings/batchLedger';
  import { createDraft } from '../lib/warnings/draftRetention';

  let { onNavigate }: {
    onNavigate: (page: string, params?: Record<string, unknown>) => void;
  } = $props();

  // ─── State ──────────────────────────────────────────────────────────────

  let settings = $state<DataLinkWarningSetting[]>([]);
  let delayMs = $state(500);
  let delayInput = $state('');
  let loading = $state(true);
  let loadError = $state<string | null>(null);
  let searchQuery = $state('');
  let selectedCategory = $state<string | null>(null);
  let expandedRows = new SvelteSet<number>();
  let rowWriteError = $state<Record<number, string>>({});
  let rowWritePending = new SvelteSet<number>();
  let batchSheetOpen = $state(false);
  let batchTitle = $state('');
  let batchScope = $state('');
  let batchError = $state<string | null>(null);
  let batchAction = $state<'enable' | 'disable' | 'defaults' | null>(null);
  let undoRow = $state<number | null>(null);
  let restoreConfirmId = $state<number | null>(null);
  let restoreMarkerClause = $state<string | null>(null);
  let noticeText = $state<Record<string, string>>({});

  // One live-region element per notice type (aria-atomic=false semantics)
  const NOTICE_TYPES = ['duplicate', 'clamp', 'write', 'expiry', 'batch', 'load'] as const;
  let lastNotice: Record<string, string> = {};

  function announce(type: string, text: string) {
    if (!text) return;
    lastNotice = { ...lastNotice, [type]: text };
    noticeText = { ...lastNotice };
  }

  // ─── Pure state machines ────────────────────────────────────────────────

  const levelUndo = createLevelUndo({ now: () => Date.now() });

  const batchLedger = createBatchLedger({
    now: () => Date.now(),
    persist: async (entries) => {
      await HybridBridge.saveBatchLedger(entries.map(e => ({ dataId: e.dataId, outcome: e.outcome, timestamp: e.timestamp })));
    },
  });

  const delayDraft = createDraft<number>({
    validate: (v) => {
      if (!Number.isFinite(v)) return 'Enter a number.';
      return null;
    },
    commit: async (v) => {
      const clamped = Math.min(60000, Math.max(0, v));
      const result = await HybridBridge.saveWarningDelay(clamped);
      if (result.success) {
        delayMs = clamped;
        warningEvaluator.pushDelay(clamped);
        return true;
      }
      return false;
    },
  });

  // ─── Derived ────────────────────────────────────────────────────────────

  let filteredSettings = $derived.by(() => {
    let result = [...settings];
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      result = result.filter(s => s.name.toLowerCase().includes(q) || s.category.toLowerCase().includes(q) || String(s.dataId).includes(q));
    }
    if (selectedCategory) {
      result = result.filter(s => s.category === selectedCategory);
    }
    result.sort((a, b) => a.name.localeCompare(b.name));
    return result;
  });

  let categories = $derived.by(() => {
    const cats: Record<string, boolean> = {};
    for (const s of settings) if (s.category) cats[s.category] = true;
    return Object.keys(cats).sort();
  });

  let enabledCount = $derived(settings.filter(s => s.enabled).length);
  let hasCalibration = $derived(settings.length > 0);

  // ─── Write pipeline (R14/KTD2) ──────────────────────────────────────────

  function suspendKindFor(payload: SaveWarningDatalinkPayload): 'edit' | 'disable' | 're-enable' {
    if (payload.writeKind !== 'enable-toggle') return 'edit';
    return payload.enabled ? 're-enable' : 'disable';
  }

  async function reRead(now: boolean): Promise<Awaited<ReturnType<typeof HybridBridge.getWarningSettings>> | null> {
    try {
      const payload = await HybridBridge.getWarningSettings();
      warningEvaluator.refresh(payload);
      if (now) settings = payload.settings;
      return payload;
    } catch {
      return null;
    }
  }

  let pendingResolveKind: Record<number, 'resume' | 'restart'> = {};

  async function commitDatalink(dataId: number, payload: SaveWarningDatalinkPayload): Promise<{ ok: boolean; result?: Awaited<ReturnType<typeof HybridBridge.saveWarningDatalink>> }> {
    rowWritePending.add(dataId);
    warningEvaluator.invalidatePending(dataId, suspendKindFor(payload));
    const result = await HybridBridge.saveWarningDatalink(payload);
    rowWritePending.delete(dataId);
    if (!result.success) {
      warningEvaluator.resolveAfterWrite(dataId, 'resume');
      return { ok: false, result };
    }
    pendingResolveKind[dataId] = payload.writeKind === 'enable-toggle' && payload.enabled ? 'restart' : 'resume';
    const fresh = await reRead(true);
    if (!fresh) {
      // KTD2 (b): the transition stays frozen (no activation, no toast) until
      // a retried re-read resolves it against the fresh snapshot.
      rowWriteError = { ...rowWriteError, [dataId]: 'Saved, but the re-read failed. Retry to refresh.' };
      return { ok: true, result };
    }
    warningEvaluator.resolveAfterWrite(dataId, pendingResolveKind[dataId]);
    delete pendingResolveKind[dataId];
    return { ok: true, result };
  }

  /** Display-only writes (name/color/autolog/flash): no invalidation, debounced re-read. */
  async function commitDisplay(dataId: number, payload: SaveWarningDatalinkPayload): Promise<boolean> {
    const result = await HybridBridge.saveWarningDatalink(payload);
    if (!result.success) {
      failWrite(dataId, 'Failed to save. Retry or discard.');
      return false;
    }
    settings = settings.map(s => (s.dataId === dataId ? { ...s, levels: payload.levels } : s));
    void debouncedReRead();
    return true;
  }

  let debounceTimer: ReturnType<typeof setTimeout> | null = null;

  function debouncedReRead() {
    if (debounceTimer) clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => {
      debounceTimer = null;
      void reRead(false);
    }, 500);
  }

  function failWrite(dataId: number, message: string) {
    rowWriteError = { ...rowWriteError, [dataId]: message };
    announce('write', `${settings.find(s => s.dataId === dataId)?.name ?? dataId}: ${message}`);
  }

  async function retryWrite(dataId: number) {
    const errs = { ...rowWriteError };
    delete errs[dataId];
    rowWriteError = errs;
    const fresh = await reRead(true);
    if (fresh && pendingResolveKind[dataId]) {
      warningEvaluator.resolveAfterWrite(dataId, pendingResolveKind[dataId]);
      delete pendingResolveKind[dataId];
    }
  }

  function discardWrite(dataId: number) {
    const errs = { ...rowWriteError };
    delete errs[dataId];
    rowWriteError = errs;
  }

  // ─── Per-datalink handlers ──────────────────────────────────────────────

  function commitLevels(dataId: number, levels: WarningLevel[]): Promise<boolean> {
    const setting = settings.find(s => s.dataId === dataId);
    if (!setting) return Promise.resolve(false);
    const payload: SaveWarningDatalinkPayload = {
      dataId,
      levels,
      points: setting.points,
      enabled: setting.enabled,
      writeKind: 'points-levels-edit',
    };
    return commitDatalink(dataId, payload).then(({ ok }) => {
      if (!ok) failWrite(dataId, 'Failed to save levels. Retry or discard.');
      return ok;
    });
  }

  /** Display-only level edits (name/color/autolog/flash) — R4 flip still applies. */
  function displayLevels(dataId: number, levels: WarningLevel[]): Promise<boolean> {
    const setting = settings.find(s => s.dataId === dataId);
    if (!setting) return Promise.resolve(false);
    const payload: SaveWarningDatalinkPayload = {
      dataId,
      levels,
      points: setting.points,
      enabled: setting.enabled,
      writeKind: 'points-levels-edit',
    };
    return commitDisplay(dataId, payload);
  }

  function commitPoints(dataId: number, points: WarningPoint[]): Promise<boolean> {
    const setting = settings.find(s => s.dataId === dataId);
    if (!setting) return Promise.resolve(false);
    const payload: SaveWarningDatalinkPayload = {
      dataId,
      levels: setting.levels,
      points,
      enabled: setting.enabled,
      writeKind: 'points-levels-edit',
    };
    return commitDatalink(dataId, payload).then(({ ok }) => {
      if (ok) {
        // A point edit between delete and undo keeps Custom (R9/KTD4)
        if (levelUndo.has(dataId)) levelUndo.recordInterveningEdit(dataId);
      } else {
        failWrite(dataId, 'Failed to save points. Retry or discard.');
      }
      return ok;
    });
  }

  async function toggleEnabled(dataId: number) {
    const setting = settings.find(s => s.dataId === dataId);
    if (!setting) return;
    const payload: SaveWarningDatalinkPayload = {
      dataId,
      levels: setting.levels,
      points: setting.points,
      enabled: !setting.enabled,
      writeKind: 'enable-toggle',
    };
    const { ok } = await commitDatalink(dataId, payload);
    if (!ok) {
      failWrite(dataId, 'Failed to toggle. Retry or discard.');
    }
    // On success, commitDatalink's re-read already replaced `settings` with
    // the backend snapshot (which includes the toggle) — no local flip here,
    // or the change would be applied twice and cancel itself out.
  }

  // ─── Level delete + R9 undo ─────────────────────────────────────────────

  async function deleteLevel(dataId: number, levelId: string) {
    const setting = settings.find(s => s.dataId === dataId);
    if (!setting) return;
    const level = setting.levels.find(l => l.id === levelId);
    if (!level) return;
    const removedPoints = setting.points.filter(p => p.levelId === levelId);
    levelUndo.begin({
      dataId,
      levels: [{
        name: level.name,
        color: level.color,
        autolog: level.autolog,
        flash: level.flash,
        order: level.order,
      }],
      points: removedPoints.map(p => ({ value: p.value, direction: p.direction, levelId: p.levelId, enabled: p.enabled })),
      preDeleteStatus: setting.status === 'Custom' ? 'Custom' : 'Typical',
      markerSet: setting.migratedBoundsMarkerSet,
      markerLevelId: setting.migratedBoundsMarkerLevelId,
      markerDeleted: setting.migratedBoundsMarkerLevelId === levelId,
    });
    const ok = await commitLevels(dataId, setting.levels.filter(l => l.id !== levelId));
    if (ok) undoRow = dataId;
    const notices = { ...shownExpiryNotices };
    delete notices[dataId];
    shownExpiryNotices = notices;
  }

  let shownExpiryNotices = $state<Record<number, boolean>>({});

  $effect(() => {
    for (const setting of settings) {
      if (levelUndo.hasExpiryNotice(setting.dataId)) {
        shownExpiryNotices = { ...shownExpiryNotices, [setting.dataId]: true };
        levelUndo.clearExpiryNotice(setting.dataId);
      }
    }
  });

  async function performUndo(dataId: number) {
    const payloadInfo = levelUndo.buildRestorePayload(dataId);
    const snapshot = levelUndo.consume(dataId);
    if (!snapshot || !payloadInfo) return;
    const setting = settings.find(s => s.dataId === dataId);
    if (!setting) return;
    const restoredLevels: WarningLevel[] = [
      ...setting.levels.map(l => ({ ...l })),
      {
        id: '',
        name: snapshot.levels[0]?.name ?? 'warning',
        color: snapshot.levels[0]?.color ?? '#f59e0b',
        autolog: snapshot.levels[0]?.autolog ?? false,
        flash: snapshot.levels[0]?.flash ?? false,
        order: snapshot.levels[0]?.order ?? setting.levels.length,
      },
    ];
    const restoredPoints: WarningPoint[] = [
      ...setting.points.map(p => ({ ...p })),
      ...snapshot.points.map(p => ({ id: '', value: p.value, direction: p.direction, levelId: restoredLevels[restoredLevels.length - 1].id, enabled: p.enabled })),
    ];
    const payload: SaveWarningDatalinkPayload = {
      dataId,
      levels: restoredLevels,
      points: restoredPoints,
      enabled: setting.enabled,
      writeKind: 'undo-restore',
      preDeleteStatus: payloadInfo.preDeleteStatus,
      interveningEdit: payloadInfo.interveningEdit,
      markerSet: payloadInfo.markerSet,
      markerLevelName: payloadInfo.markerLevelName,
    };
    const { ok } = await commitDatalink(dataId, payload);
    if (ok) {
      undoRow = null;
      const notices = { ...shownExpiryNotices };
      delete notices[dataId];
      shownExpiryNotices = notices;
      announce('load', 'Level restored.');
    } else {
      failWrite(dataId, 'Undo failed. Retry.');
      levelUndo.freeze(dataId, true);
      rowWriteError = { ...rowWriteError, [dataId]: 'Undo failed. Retry.' };
    }
  }

  // ─── Batch presets (R15) ────────────────────────────────────────────────

  function openBatch(action: 'enable' | 'disable' | 'defaults') {
    batchError = null;
    batchAction = action;
    if (action === 'enable') {
      batchTitle = 'Enable warning monitoring';
      batchScope = batchLedger.getState().bannerVisible
        ? `Retry the ${batchLedger.failedDatalinks().length} failed datalink(s).`
        : `Enable all ${filteredSettings.length} data link(s) matching the current filters.`;
    } else if (action === 'disable') {
      batchTitle = 'Disable warning monitoring';
      batchScope = batchLedger.getState().bannerVisible
        ? `Retry the ${batchLedger.failedDatalinks().length} failed datalink(s).`
        : `Disable all ${filteredSettings.length} data link(s) matching the current filters.`;
    } else {
      batchTitle = 'Restore ECU defaults';
      batchScope = 'Applies DEF defaults to Typical-status data links only. Custom data links are left untouched.';
    }
    batchSheetOpen = true;
  }

  async function runBatch() {
    if (!batchAction) return;
    batchLedger.begin();
    navigationGate.setBlocked('batch-in-flight', true);
    batchError = null;

    const target = batchAction === 'enable' || batchAction === 'disable'
      ? batchLedger.targetDatalinks(filteredSettings.map(s => s.dataId))
      : filteredSettings.filter(s => s.status === 'Typical').map(s => s.dataId);

    if (target.length === 0) {
      batchLedger.cancel();
      batchSheetOpen = false;
      announce('batch', 'No data links to apply.');
      return;
    }

    if (batchAction === 'enable' || batchAction === 'disable') {
      const enabled = batchAction === 'enable';
      for (const dataId of target) {
        const setting = settings.find(s => s.dataId === dataId);
        if (!setting) {
          batchLedger.recordOutcome(dataId, 'skipped');
          continue;
        }
        const payload: SaveWarningDatalinkPayload = {
          dataId,
          levels: setting.levels,
          points: setting.points,
          enabled,
          writeKind: 'enable-toggle',
        };
        const { ok } = await commitDatalink(dataId, payload);
        batchLedger.recordOutcome(dataId, ok ? 'success' : 'failed');
      }
    } else {
      const defaults = await HybridBridge.getDefXmlDefaults();
      const defaultsById = new Map(defaults.map(d => [d.dataId, d]));
      for (const dataId of target) {
        const setting = settings.find(s => s.dataId === dataId);
        const def = defaultsById.get(dataId);
        if (!setting || !def || def.levels.length === 0) {
          batchLedger.recordOutcome(dataId, 'skipped');
          continue;
        }
        const hasWarningRole = def.levels.some(l => l.name.toLowerCase() === 'warning');
        const payload: SaveWarningDatalinkPayload = {
          dataId,
          levels: def.levels,
          points: def.points,
          enabled: setting.enabled,
          writeKind: 'preset-restore',
          markerSet: hasWarningRole,
        };
        const { ok } = await commitDatalink(dataId, payload);
        batchLedger.recordOutcome(dataId, ok ? 'success' : 'failed');
        if (ok) {
          settings = settings.map(s => (s.dataId === dataId ? { ...s, levels: def.levels, points: def.points, status: 'Typical' } : s));
        }
      }
    }

    batchLedger.complete();
    navigationGate.setBlocked('batch-in-flight', false);
    batchSheetOpen = false;
    await debouncedReRead();
    const failed = batchLedger.failedDatalinks();
    if (failed.length > 0) {
      const names = failed.map(id => settings.find(s => s.dataId === id)?.name ?? String(id)).join(', ');
      announce('batch', `Failed: ${names}`);
    }
  }

  // ─── Per-datalink Restore (R15/R18 marker clause) ───────────────────────

  let defaultsById = $state<Map<number, DataLinkWarningSetting>>(new Map());

  async function loadDefaults() {
    try {
      const defaults = await HybridBridge.getDefXmlDefaults();
      defaultsById = new Map(defaults.map(d => [d.dataId, d]));
    } catch {
      defaultsById = new Map();
    }
  }

  async function askRestore(dataId: number) {
    const setting = settings.find(s => s.dataId === dataId);
    if (!setting) return;
    const def = defaultsById.get(dataId);
    if (!def || def.levels.length === 0) {
      toast('No ECU defaults available for this data link.', 'info');
      return;
    }
    restoreMarkerClause = null;
    if (setting.migratedBoundsMarkerSet) {
      const hasWarningRole = def.levels.some(l => l.name.toLowerCase() === 'warning');
      restoreMarkerClause = hasWarningRole
        ? 'The migrated marker is set: the restored "warning" level keeps autolog enabled.'
        : 'The migrated marker persists until you change autolog on any level of this data link.';
    }
    restoreConfirmId = dataId;
  }

  async function performRestore(dataId: number) {
    restoreConfirmId = null;
    const setting = settings.find(s => s.dataId === dataId);
    if (!setting) return;
    const def = defaultsById.get(dataId);
    if (!def || def.levels.length === 0) return;
    const hasWarningRole = def.levels.some(l => l.name.toLowerCase() === 'warning');
    const payload: SaveWarningDatalinkPayload = {
      dataId,
      levels: def.levels,
      points: def.points,
      enabled: setting.enabled,
      writeKind: 'preset-restore',
      markerSet: hasWarningRole,
    };
    const { ok } = await commitDatalink(dataId, payload);
    if (ok) {
      settings = settings.map(s => (s.dataId === dataId ? { ...s, levels: def.levels, points: def.points, status: 'Typical' } : s));
      toast('Defaults restored.', 'success');
    } else {
      failWrite(dataId, 'Failed to restore defaults. Retry or discard.');
    }
  }

  // ─── Delay draft (R15) ──────────────────────────────────────────────────

  async function commitDelay(raw: string) {
    const value = raw === '' ? Number.NaN : Number(raw);
    delayDraft.start(Number.isFinite(value) ? value : Number.NaN);
    const outcome = await delayDraft.submit();
    if (outcome === 'rejected') {
      delayInput = String(delayMs);
      announce('load', 'Enter a number for the delay.');
    } else if (outcome === 'failed') {
      announce('load', 'Failed to save the delay.');
      delayInput = String(delayMs);
      delayDraft.discard();
    } else if (outcome === 'committed') {
      delayInput = String(delayMs);
    }
  }

  // ─── Filter/collapse with-holding (R16) ─────────────────────────────────

  let navigationBlocked = $derived(Object.keys(rowWriteError).length > 0 || rowWritePending.size > 0);

  $effect(() => {
    if (navigationBlocked) navigationGate.setBlocked('retained-draft', true);
    else navigationGate.setBlocked('retained-draft', false);
  });

  function applySearch(value: string) {
    if (Object.keys(rowWriteError).length > 0) return; // withheld — control reverts to last applied
    searchQuery = value;
  }

  function toggleExpand(dataId: number) {
    if (Object.keys(rowWriteError).length > 0) return; // collapse withheld for retained failed drafts
    if (expandedRows.has(dataId)) {
      expandedRows.delete(dataId);
      const notices = { ...shownExpiryNotices };
      delete notices[dataId];
      shownExpiryNotices = notices;
    } else {
      expandedRows.add(dataId);
    }
  }

  // ─── Load ───────────────────────────────────────────────────────────────

  async function loadSettings() {
    loading = true;
    loadError = null;
    try {
      const payload = await HybridBridge.getWarningSettings();
      settings = payload.settings;
      if (delayDraft.isPending()) {
        // Defer: a pending delay draft is never overwritten by a successful load
      } else {
        delayMs = payload.delayMs;
        delayInput = String(payload.delayMs);
      }
      warningEvaluator.refresh(payload);
      warningToasts.refreshDisplayLookup();
      if (payload.pointsRemovedIds && payload.pointsRemovedIds.length > 0) {
        const names = payload.pointsRemovedIds
          .map(id => settings.find(s => s.dataId === id)?.name ?? String(id))
          .join(', ');
        announce('load', `Points removed while applying defaults: ${names}`);
      }
      await loadQueuedNotices();
    } catch (e) {
      loadError = 'Failed to load warning settings.';
      console.error('Failed to load warning settings:', e);
    } finally {
      loading = false;
    }
  }

  /** R15/KTD6: surface queued banners + undo-expiry notices left by a previous session. */
  async function loadQueuedNotices() {
    try {
      const queued = await HybridBridge.getWarningQueuedNotices();
      if (queued.undoExpiryNotices.length === 0 && queued.banners.length === 0) return;
      for (const n of queued.undoExpiryNotices) {
        if (settings.some(s => s.dataId === n.dataId)) {
          shownExpiryNotices = { ...shownExpiryNotices, [n.dataId]: true };
        }
      }
      if (queued.banners.length > 0) {
        batchLedger.begin();
        for (const banner of queued.banners) {
          for (const id of banner.dataIds) batchLedger.recordOutcome(id, 'failed');
        }
        batchLedger.complete();
      }
      await HybridBridge.saveWarningQueuedNotices({ banners: [], undoExpiryNotices: [] });
      announce('batch', 'Some data links failed in a previous session. Retry or dismiss.');
    } catch {
      // in-memory fallback — queued notices stay for the next attempt
    }
  }

  /** R15/KTD6: persist pending expiry notices + the batch banner before unmount. */
  function persistQueuedNotices() {
    const expiryNotices: { dataId: number; timestamp: number }[] = levelUndo
      .takeExpiryNotices()
      .map(dataId => ({ dataId, timestamp: Date.now() }));
    const banners = batchLedger.getState().bannerVisible
      ? [{
          dataIds: batchLedger.failedDatalinks(),
          kind: 'batch-partial-failure',
          message: 'Some data links failed to update.',
          timestamp: Date.now(),
        }]
      : [];
    if (expiryNotices.length > 0 || banners.length > 0) {
      HybridBridge.saveWarningQueuedNotices({ banners, undoExpiryNotices: expiryNotices }).catch(() => {});
    }
  }

  function retryLoad() {
    void loadSettings();
  }

  onMount(() => {
    void loadDefaults();
    void loadSettings();
  });

  onDestroy(() => {
    if (debounceTimer) clearTimeout(debounceTimer);
    persistQueuedNotices();
    navigationGate.setBlocked('retained-draft', false);
  });
</script>

<div class="mx-auto max-w-4xl">
  <div class="mb-4 flex items-center gap-3">
    <button
      class="flex h-8 w-8 items-center justify-center rounded-lg text-gray-400 transition-colors hover:bg-gray-700 hover:text-gray-200"
      onclick={() => onNavigate('dashboard')}
    >
      <IconChevronLeft size={18} />
    </button>
    <h1 class="text-xl font-bold text-gray-100">Warning Centre</h1>
    {#if enabledCount > 0}
      <span class="rounded-full bg-amber-500/20 px-2 py-0.5 text-xs font-medium text-amber-300">
        {enabledCount} active
      </span>
    {/if}
  </div>

  <!-- Preset buttons + Delay -->
  <div class="mb-4 flex flex-wrap items-center gap-2">
    <button
      class="flex items-center gap-1.5 rounded-lg border border-gray-700 bg-gray-800 px-3 py-1.5 text-xs font-medium text-gray-300 transition-colors hover:border-amber-500 hover:text-amber-300 disabled:opacity-40"
      onclick={() => openBatch('defaults')}
      disabled={!hasCalibration}
    >
      <IconBookmark size={14} />
      ECU Defaults
    </button>
    <button
      class="rounded-lg border border-gray-700 bg-gray-800 px-3 py-1.5 text-xs font-medium text-gray-300 transition-colors hover:border-green-500 hover:text-green-300 disabled:opacity-40"
      onclick={() => openBatch('enable')}
      disabled={!hasCalibration}
    >
      Enable All
    </button>
    <button
      class="rounded-lg border border-gray-700 bg-gray-800 px-3 py-1.5 text-xs font-medium text-gray-300 transition-colors hover:border-gray-500 hover:text-gray-300 disabled:opacity-40"
      onclick={() => openBatch('disable')}
      disabled={!hasCalibration}
    >
      None
    </button>

    <div class="ml-auto flex items-center gap-2">
      <span class="text-xs text-gray-500">Delay:</span>
      <input
        type="number"
        step="50"
        placeholder={loading ? '…' : String(delayMs)}
        value={delayInput}
        oninput={(e) => { delayInput = (e.target as HTMLInputElement).value; }}
        onblur={() => { if (delayInput !== '' && delayInput !== String(delayMs)) void commitDelay(delayInput); }}
        onkeydown={(e) => { if (e.key === 'Enter') { if (delayInput !== '' && delayInput !== String(delayMs)) void commitDelay(delayInput); } }}
        class="w-20 rounded-lg border border-gray-700 bg-gray-800 px-2 py-1 text-xs text-gray-200 outline-none focus:border-amber-500 focus:ring-1 focus:ring-amber-500"
        aria-label="Warning delay in milliseconds"
      />
      <span class="text-xs text-gray-500">ms</span>
    </div>
  </div>

  {#if batchLedger.getState().bannerVisible}
    <div
      class="mb-3 flex items-center gap-2 rounded border px-3 py-2 text-xs"
      style="border-color: rgba(239,68,68,0.4); color: #fca5a5; background-color: rgba(239,68,68,0.08);"
      role="status"
    >
      <span>Some data links failed. Retry only those, or dismiss to apply to all.</span>
      <button class="ml-auto rounded px-2 py-1 font-medium hover:bg-red-900/40" onclick={() => openBatch(batchAction ?? 'enable')}>
        Retry
      </button>
      <button class="rounded px-2 py-1 hover:bg-red-900/40" onclick={() => batchLedger.dismissBanner()}>
        Dismiss
      </button>
    </div>
  {/if}

  <!-- Search + category chips -->
  <div class="mb-3 flex flex-wrap items-center gap-2">
    <input
      type="text"
      placeholder="Search data links..."
      value={searchQuery}
      oninput={(e) => applySearch((e.target as HTMLInputElement).value)}
      class="w-full rounded-lg border border-gray-700 bg-gray-800 py-2 px-3 text-sm text-gray-100 placeholder-gray-500 outline-none focus:border-amber-500 focus:ring-1 focus:ring-amber-500 sm:w-64"
    />
    {#each categories as cat (cat)}
      <button
        class="rounded-full px-2.5 py-0.5 text-xs font-medium transition-colors {selectedCategory === cat ? 'bg-amber-500/20 text-amber-300' : 'bg-gray-800 text-gray-400 hover:bg-gray-700 hover:text-gray-200'}"
        onclick={() => {
          if (Object.keys(rowWriteError).length > 0) return;
          selectedCategory = selectedCategory === cat ? null : cat;
        }}
      >
        {cat}
      </button>
    {/each}
  </div>

  {#if loading}
    <div class="flex items-center justify-center py-12">
      <span class="inline-block h-6 w-6 animate-spin rounded-full border-2 border-gray-500 border-t-amber-400"></span>
    </div>
  {:else if loadError}
    <div class="flex flex-col items-center gap-3 py-12 text-center">
      <p class="text-sm text-red-400">{loadError}</p>
      <button class="rounded-lg border border-gray-700 bg-gray-800 px-3 py-1.5 text-xs text-gray-300 hover:border-amber-500" onclick={retryLoad}>
        Retry
      </button>
    </div>
  {:else if settings.length === 0}
    <div class="py-12 text-center text-sm text-gray-500">
      <p>No calibration loaded.</p>
      <p class="mt-1 text-xs text-gray-600">Load a calibration to configure warning thresholds. The delay above still applies globally.</p>
    </div>
  {:else if filteredSettings.length === 0}
    <div class="py-12 text-center text-sm text-gray-500">
      No data links match your filters.
    </div>
  {:else}
    <div class="rounded-lg border border-gray-700">
      {#each filteredSettings as setting (setting.dataId)}
        <div class="border-b border-gray-700/50 last:border-b-0">
          <div class="flex min-h-11 items-center gap-2 px-3 py-1.5">
            <label class="relative inline-flex cursor-pointer items-center">
              <input
                type="checkbox"
                checked={setting.enabled}
                disabled={rowWritePending.has(setting.dataId)}
                class="peer sr-only"
                onchange={() => void toggleEnabled(setting.dataId)}
              />
              <div class="h-5 w-9 rounded-full bg-gray-700 after:absolute after:left-[2px] after:top-[2px] after:h-4 after:w-4 after:rounded-full after:border after:border-gray-600 after:bg-gray-400 after:transition-all peer-checked:bg-amber-500 peer-checked:after:translate-x-full peer-checked:after:border-white peer-checked:after:bg-white disabled:opacity-40"></div>
            </label>
            <span class="min-w-0 flex-1 truncate text-xs font-medium text-gray-200">{setting.name}</span>
            <span class="rounded-full px-1.5 py-0.5 text-[9px] font-bold {setting.status === 'Custom' ? 'bg-amber-500/20 text-amber-300' : 'bg-blue-500/20 text-blue-300'}">
              {setting.enabled ? (setting.status === 'Custom' ? 'Custom' : 'Typical') : 'Off'}
            </span>
            <button
              class="flex h-8 items-center gap-1 rounded px-2 text-[11px] transition-colors hover:bg-gray-800 disabled:opacity-40 disabled:hover:bg-transparent"
              style="color: var(--metro-text-muted);"
              onclick={() => void askRestore(setting.dataId)}
              disabled={!defaultsById.has(setting.dataId)}
              title={defaultsById.has(setting.dataId) ? `Restore ECU defaults for ${setting.name}` : 'No ECU defaults defined for this data link'}
              aria-label="Restore ECU defaults for {setting.name}"
            >
              <IconRotate size={12} />
              Defaults
            </button>
            <button
              class="flex h-8 w-8 items-center justify-center rounded transition-colors hover:bg-gray-800"
              style="color: var(--metro-text-muted);"
              onclick={() => toggleExpand(setting.dataId)}
              aria-expanded={expandedRows.has(setting.dataId)}
              aria-label="Edit thresholds for {setting.name}"
            >
              {#if expandedRows.has(setting.dataId)}
                <IconChevronDown size={14} />
              {:else}
                <IconChevronRight size={14} />
              {/if}
            </button>
          </div>

          {#if rowWriteError[setting.dataId]}
            <div
              class="mx-3 mb-2 flex items-center gap-2 rounded border px-2 py-1.5 text-[11px]"
              style="border-color: rgba(239,68,68,0.4); color: #fca5a5; background-color: rgba(239,68,68,0.08);"
              role="status"
            >
              <span class="min-w-0 flex-1">{rowWriteError[setting.dataId]}</span>
              <button class="rounded px-2 py-1 font-medium hover:bg-red-900/40" onclick={() => void retryWrite(setting.dataId)}>
                Retry
              </button>
              <button class="rounded px-2 py-1 hover:bg-red-900/40" onclick={() => discardWrite(setting.dataId)}>
                Discard
              </button>
            </div>
          {/if}

          {#if shownExpiryNotices[setting.dataId]}
            <p class="mx-3 mb-2 rounded border px-2 py-1.5 text-[11px]" style="border-color: var(--metro-border); color: var(--metro-text-muted);">
              The undo window expired.
            </p>
          {/if}

          {#if expandedRows.has(setting.dataId)}
            <div class="space-y-3 px-3 pb-3">
              {#if undoRow === setting.dataId && levelUndo.has(setting.dataId)}
                <div class="flex items-center gap-2 rounded border px-2 py-1.5 text-[11px]" style="border-color: rgba(245,158,11,0.4); color: #fcd34d;">
                  <span class="min-w-0 flex-1">{levelUndo.scopeLabel(setting.dataId)}</span>
                  <button class="rounded px-2 py-1 font-medium hover:bg-amber-900/40" onclick={() => void performUndo(setting.dataId)}>
                    Undo
                  </button>
                  <button class="rounded px-2 py-1 hover:bg-amber-900/40" onclick={() => { undoRow = null; levelUndo.consume(setting.dataId); }}>
                    Dismiss
                  </button>
                </div>
              {/if}
              <LevelEditor
                levels={setting.levels}
                onChange={(levels) => void commitLevels(setting.dataId, levels)}
                onDisplayChange={(levels) => void displayLevels(setting.dataId, levels)}
                onDeleteLevel={(levelId) => void deleteLevel(setting.dataId, levelId)}
                onMutate={() => { levelUndo.expireByMutation(setting.dataId); }}
              />
              <PointEditor
                points={setting.points}
                levels={setting.levels}
                minValue={null}
                maxValue={null}
                onChange={(points) => commitPoints(setting.dataId, points)}
              />
            </div>
          {/if}
        </div>
      {/each}
    </div>

    <div class="mt-3 text-right text-xs text-gray-500">
      {filteredSettings.length} of {settings.length} data links
    </div>
  {/if}

  <!-- Notice live regions (one per notice type) -->
  {#each NOTICE_TYPES as type (type)}
    <span class="sr-only" aria-live="polite">{noticeText[type] ?? ''}</span>
  {/each}
</div>

<!-- Batch confirmation sheet -->
<BatchSheet
  open={batchSheetOpen}
  title={batchTitle}
  scopeText={batchScope}
  confirmLabel={batchAction === 'defaults' ? 'Restore' : 'Apply'}
  inFlight={batchLedger.getState().inFlight}
  error={batchError}
  onConfirm={() => void runBatch()}
  onCancel={() => { if (!batchLedger.getState().inFlight) batchSheetOpen = false; }}
/>

<!-- Per-datalink restore confirmation -->
{#if restoreConfirmId !== null}
  <div
    class="fixed inset-0 z-[70] flex items-center justify-center bg-black/60"
    role="dialog"
    aria-modal="true"
    tabindex="-1"
    onclick={(e) => { if (e.target === e.currentTarget) restoreConfirmId = null; }}
    onkeydown={(e) => { if (e.key === 'Escape') restoreConfirmId = null; }}
  >
    <div class="w-72 rounded border p-4" style="background-color: var(--metro-card); border-color: var(--metro-border);">
      <p class="text-sm font-medium" style="color: var(--metro-text);">Restore ECU defaults?</p>
      <p class="mt-1 text-xs" style="color: var(--metro-text-muted);">
        Replaces this data link's levels with the DEF defaults.
        {#if restoreMarkerClause}{restoreMarkerClause}{/if}
      </p>
      <div class="mt-3 flex justify-end gap-2">
        <button
          class="h-11 rounded px-3 text-xs transition-colors hover:bg-gray-800"
          style="color: var(--metro-text-secondary);"
          onclick={() => { restoreConfirmId = null; }}
        >
          Cancel
        </button>
        <button
          class="h-11 rounded px-3 text-xs font-medium text-white transition-colors hover:bg-amber-600"
          style="background-color: var(--metro-accent);"
          onclick={() => void performRestore(restoreConfirmId!)}
        >
          Restore
        </button>
      </div>
    </div>
  </div>
{/if}

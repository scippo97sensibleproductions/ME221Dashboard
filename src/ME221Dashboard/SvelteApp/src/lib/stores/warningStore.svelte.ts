import type { WarningHistoryEntry } from '../HybridBridgeTypes';
export type { WarningHistoryEntry } from '../HybridBridgeTypes';
import { HybridBridge } from '../HybridBridge';
import { SvelteMap } from 'svelte/reactivity';
import { liveDataStore } from './LiveDataStore.svelte';
import {
  warningEvaluator,
  furthestPoint,
  type ActivationEvent,
  type DropEvent,
  type EvaluatorEvent,
  type WarningEvaluator,
} from './warningEvaluator';

export interface ActiveWarning {
  dataId: number;
  name: string;
  unit: string;
  category: string;
  value: number;
  levelId: string;
  levelName: string;
  color: string;
  threshold: number;
  thresholdType: 'min' | 'max';
  triggeredAt: number;
}

const MAX_HISTORY = 200;
const SAVE_DEBOUNCE_MS = 2000;

let activeWarnings = $state<Map<number, ActiveWarning>>(new Map());
let warningHistory = $state<WarningHistoryEntry[]>([]);
let historyCounter = 0;
let panelOpen = $state(false);
let saveTimer: ReturnType<typeof setTimeout> | null = null;
let loaded = false;
let pendingSave: WarningHistoryEntry[] | null = null;
let openEntryByDataId = new SvelteMap<number, { entry: WarningHistoryEntry; levelId: string }>();

let evaluator: WarningEvaluator = warningEvaluator;
let unsubscribeEvents: (() => void) | null = null;

function scheduleSave() {
  if (saveTimer) clearTimeout(saveTimer);
  saveTimer = setTimeout(() => {
    saveTimer = null;
    void persistHistory();
  }, SAVE_DEBOUNCE_MS);
}

async function persistHistory() {
  try {
    if (pendingSave !== null) {
      await HybridBridge.saveWarningHistory(pendingSave);
      pendingSave = null;
    }
    await HybridBridge.saveWarningHistory(warningHistory);
  } catch (e) {
    pendingSave = pendingSave ?? warningHistory.slice();
    console.error('[WARN] Failed to save warning history:', e);
  }
}

function levelInfo(dataId: number, levelId: string): { name: string; color: string; autolog: boolean } | null {
  const level = evaluator.getLevel(dataId, levelId);
  if (!level) return null;
  return { name: level.name, color: level.color, autolog: level.autolog };
}

function datalinkDisplay(dataId: number): { name: string; unit: string; category: string } {
  const snapshot = evaluator.getSnapshot();
  const dl = snapshot.settings.get(dataId);
  return { name: dl?.name ?? '', unit: dl?.unit ?? '', category: dl?.category ?? '' };
}

function openSnapshot(dataId: number, levelId: string, value: number, triggeredAt: number) {
  const info = levelInfo(dataId, levelId);
  const dl = datalinkDisplay(dataId);
  if (openEntryByDataId.has(dataId)) return;
  const entry: WarningHistoryEntry = {
    id: ++historyCounter,
    dataId,
    name: dl.name,
    unit: dl.unit,
    category: dl.category,
    value,
    severity: info?.name ?? 'warning',
    threshold: 0,
    thresholdType: 'max',
    triggeredAt,
    clearedAt: null,
  };
  const active = activeWarnings.get(dataId);
  if (active) {
    entry.threshold = active.threshold;
    entry.thresholdType = active.thresholdType;
  }
  warningHistory = [entry, ...warningHistory].slice(0, MAX_HISTORY);
  openEntryByDataId = new SvelteMap(openEntryByDataId).set(dataId, { entry, levelId });
}

function closeOpenEntry(dataId: number, clearedAt: number) {
  const open = openEntryByDataId.get(dataId);
  if (!open) return;
  const next = new SvelteMap(openEntryByDataId);
  next.delete(dataId);
  openEntryByDataId = next;
  warningHistory = warningHistory.map(h =>
    h.id === open.entry.id ? { ...h, clearedAt } : h
  );
}

function upsertActive(dataId: number, levelId: string, value: number, threshold: number, thresholdType: 'min' | 'max', triggeredAt: number) {
  const info = levelInfo(dataId, levelId);
  const dl = datalinkDisplay(dataId);
  if (!info) return;
  const existing = activeWarnings.get(dataId);
  const warning: ActiveWarning = {
    dataId,
    name: dl.name,
    unit: dl.unit,
    category: dl.category,
    value,
    levelId,
    levelName: info.name,
    color: info.color,
    threshold: threshold ?? existing?.threshold ?? 0,
    thresholdType: thresholdType ?? existing?.thresholdType ?? 'max',
    triggeredAt,
  };
  activeWarnings = new SvelteMap(activeWarnings).set(dataId, warning);
}

function onActivation(event: ActivationEvent) {
  const { dataId, levelId, value, threshold, thresholdType, viaRecompute } = event;
  const now = Date.now();
  const info = levelInfo(dataId, levelId);
  upsertActive(dataId, levelId, value, threshold, thresholdType, now);

  const open = openEntryByDataId.get(dataId);
  if (open && open.levelId !== levelId) {
    closeOpenEntry(dataId, now);
  }
  if (!viaRecompute && info?.autolog && !openEntryByDataId.has(dataId)) {
    openSnapshot(dataId, levelId, value, now);
  }
  scheduleSave();
}

function onDrop(event: DropEvent) {
  const { dataId, toLevelId } = event;
  const now = Date.now();
  closeOpenEntry(dataId, now);
  if (toLevelId !== null) {
    const info = levelInfo(dataId, toLevelId);
    const live = liveDataStore.values[String(dataId)];
    const value = live ?? activeWarnings.get(dataId)?.value ?? 0;
    const snapshot = evaluator.getSnapshot();
    const dl = snapshot.settings.get(dataId);
    const points = dl?.points.filter(p => p.levelId === toLevelId && p.enabled) ?? [];
    const furthest = furthestPoint(points, value);
    upsertActive(dataId, toLevelId, value, furthest.threshold, furthest.thresholdType, now);
    if (info?.autolog && !openEntryByDataId.has(dataId)) {
      openSnapshot(dataId, toLevelId, value, now);
    }
  } else {
    const next = new SvelteMap(activeWarnings);
    next.delete(dataId);
    activeWarnings = next;
  }
  scheduleSave();
}

function handleEvents(events: EvaluatorEvent[]) {
  for (const event of events) {
    if (event.type === 'activation') onActivation(event.activation);
    else onDrop(event.drop);
  }
}

function attach() {
  unsubscribeEvents?.();
  unsubscribeEvents = evaluator.subscribe(handleEvents);
}

attach();

class WarningStore {
  get activeWarnings(): Map<number, ActiveWarning> {
    return activeWarnings;
  }

  get activeWarningCount(): number {
    return activeWarnings.size;
  }

  get history(): WarningHistoryEntry[] {
    return warningHistory;
  }

  get isPanelOpen(): boolean {
    return panelOpen;
  }

  togglePanel() {
    panelOpen = !panelOpen;
  }

  openPanel() {
    panelOpen = true;
  }

  closePanel() {
    panelOpen = false;
  }

  /** Load persisted warning history from file. Call once on app mount. */
  async loadHistory() {
    if (loaded) return;
    try {
      const result = await HybridBridge.getWarningHistory();
      if (Array.isArray(result)) {
        warningHistory = result;
        if (result.length > 0) {
          historyCounter = Math.max(...result.map(e => e.id), 0);
        }
      }
    } catch (e) {
      console.error('[WARN] Failed to load warning history:', e);
    }
    loaded = true;
  }

  /**
   * R12 toggle-moment snapshot: called synchronously right after a successful
   * autolog-toggle write, so an ongoing activation that has no open entry yet
   * (autolog was off at activation) snapshots at the toggle moment even if the
   * debounced re-read lands after the level deactivates.
   */
  snapshotToggle(dataId: number, levelId: string) {
    const active = activeWarnings.get(dataId);
    if (!active || active.levelId !== levelId) return;
    if (openEntryByDataId.has(dataId)) return;
    const value = liveDataStore.values[String(dataId)] ?? active.value;
    openSnapshot(dataId, levelId, value, Date.now());
    scheduleSave();
  }

  /**
   * Interim close actions for the pre-U5 panel: in-place clearedAt closes,
   * never separate entries (R12). Removed with the U5 panel rebuild.
   */
  clearWarning(dataId: number) {
    closeOpenEntry(dataId, Date.now());
    const next = new SvelteMap(activeWarnings);
    next.delete(dataId);
    activeWarnings = next;
    scheduleSave();
  }

  clearAllWarnings() {
    for (const dataId of activeWarnings.keys()) closeOpenEntry(dataId, Date.now());
    activeWarnings = new Map();
    scheduleSave();
  }

  reset() {
    if (saveTimer) clearTimeout(saveTimer);
    saveTimer = null;
    activeWarnings = new Map();
    warningHistory = [];
    openEntryByDataId = new SvelteMap();
    pendingSave = null;
    panelOpen = false;
    loaded = false;
  }

  /** Test hook: attach a fresh factory evaluator instance. */
  __attachEvaluator(e: WarningEvaluator) {
    unsubscribeEvents?.();
    evaluator = e;
    attach();
  }

  __detachEvaluator() {
    unsubscribeEvents?.();
    unsubscribeEvents = null;
    evaluator = warningEvaluator;
  }
}

export const warningStore = new WarningStore();

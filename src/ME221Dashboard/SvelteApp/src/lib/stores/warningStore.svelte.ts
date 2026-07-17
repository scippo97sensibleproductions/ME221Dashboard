import type { DataLinkWarningSetting, WarningHistoryEntry } from '../HybridBridgeTypes';
import { HybridBridge } from '../HybridBridge';

export type WarningSeverity = 'warning' | 'critical';

export interface ActiveWarning {
  dataId: number;
  name: string;
  unit: string;
  category: string;
  value: number;
  severity: WarningSeverity;
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
let previousStates = new Map<number, 'none' | 'warning' | 'critical'>();
let saveTimer: ReturnType<typeof setTimeout> | null = null;
let loaded = false;

function scheduleSave() {
  if (saveTimer) clearTimeout(saveTimer);
  saveTimer = setTimeout(() => {
    saveTimer = null;
    HybridBridge.saveWarningHistory(warningHistory).catch(e =>
      console.error('[WARN] Failed to save warning history:', e)
    );
  }, SAVE_DEBOUNCE_MS);
}

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
   * Called from the live data loop. Updates warning state for an entity
   * and fires toast on state transitions (none→warning, none→critical, warning→critical).
   */
  updateWarning(
    dataId: number,
    name: string,
    unit: string,
    category: string,
    value: number,
    newState: 'none' | 'warning' | 'critical',
    warningSettings: DataLinkWarningSetting[] | null
  ): void {
    const prev = previousStates.get(dataId) ?? 'none';
    previousStates.set(dataId, newState);

    if (newState === 'none') {
      // Clear if was active
      if (prev !== 'none') {
        this.#clearWarning(dataId);
      }
      return;
    }

    // Find the threshold that was crossed
    const setting = warningSettings?.find(s => s.dataId === dataId);
    let threshold = 0;
    let thresholdType: 'min' | 'max' = 'max';
    if (setting) {
      if (setting.maxWarning != null && value > setting.maxWarning) {
        threshold = setting.maxWarning;
        thresholdType = 'max';
      } else if (setting.minWarning != null && value < setting.minWarning) {
        threshold = setting.minWarning;
        thresholdType = 'min';
      }
    }

    // State transition → fire toast
    if (prev === 'none' || (prev === 'warning' && newState === 'critical')) {
      this.#addActiveWarning(dataId, name, unit, category, value, newState, threshold, thresholdType);
    } else {
      // Update existing warning with new value
      const existing = activeWarnings.get(dataId);
      if (existing) {
        existing.value = value;
        existing.severity = newState;
      }
    }
  }

  clearWarning(dataId: number) {
    this.#clearWarning(dataId);
  }

  clearAllWarnings() {
    const now = Date.now();
    for (const [dataId, w] of activeWarnings) {
      this.#addToHistory(w, now);
    }
    activeWarnings = new Map();
    scheduleSave();
  }

  reset() {
    activeWarnings = new Map();
    warningHistory = [];
    previousStates.clear();
    panelOpen = false;
    loaded = false;
  }

  #addActiveWarning(
    dataId: number,
    name: string,
    unit: string,
    category: string,
    value: number,
    severity: WarningSeverity,
    threshold: number,
    thresholdType: 'min' | 'max'
  ) {
    const warning: ActiveWarning = {
      dataId, name, unit, category, value, severity, threshold, thresholdType,
      triggeredAt: Date.now(),
    };
    activeWarnings = new Map(activeWarnings).set(dataId, warning);
  }

  #clearWarning(dataId: number) {
    const existing = activeWarnings.get(dataId);
    if (existing) {
      this.#addToHistory(existing, Date.now());
      const next = new Map(activeWarnings);
      next.delete(dataId);
      activeWarnings = next;
      scheduleSave();
    }
  }

  #addToHistory(w: ActiveWarning, clearedAt: number) {
    const entry: WarningHistoryEntry = {
      id: ++historyCounter,
      dataId: w.dataId,
      name: w.name,
      unit: w.unit,
      category: w.category,
      value: w.value,
      severity: w.severity,
      threshold: w.threshold,
      thresholdType: w.thresholdType,
      triggeredAt: w.triggeredAt,
      clearedAt,
    };
    warningHistory = [entry, ...warningHistory].slice(0, MAX_HISTORY);
  }
}

export const warningStore = new WarningStore();

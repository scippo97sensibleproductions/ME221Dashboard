import type { WarningLevel, WarningPoint, WarningSettingsPayload } from '../HybridBridgeTypes';
import { hasCvdConfusablePair } from '../warnings/colorWarnings';

export interface EvaluatorDatalinkSettings {
  enabled: boolean;
  name: string;
  unit: string;
  category: string;
  levels: WarningLevel[];
  points: WarningPoint[];
  hasCvdConfusablePair: boolean;
}

export interface ActivationEvent {
  dataId: number;
  levelId: string;
  toastEligible: boolean;
  viaRecompute: boolean;
  value: number;
  threshold: number;
  thresholdType: 'min' | 'max';
}

export interface DropEvent {
  dataId: number;
  fromLevelId: string;
  toLevelId: string | null;
}

export type EvaluatorEvent =
  | { type: 'activation'; activation: ActivationEvent }
  | { type: 'drop'; drop: DropEvent };

export interface EvaluatorDatalinkState {
  activeLevelId: string | null;
  activeLevelName: string | null;
  activeLevelColor: string | null;
  hasCvdConfusablePair: boolean;
}

interface PendingTransition {
  crossingTime: number;
  deadline: number;
  targetLevelId: string;
  delayMsAtCrossing: number;
  remainingAtSuspend: number | null;
  suspendKind: 'edit' | 'disable' | 're-enable' | null;
}

interface DatalinkState {
  activeLevelId: string | null;
  pending: PendingTransition | null;
  reArmAt: number | null;
}

interface DatalinkRuntime {
  dataId: number;
  settings: EvaluatorDatalinkSettings;
  rankByLevelId: Map<string, number>;
  state: DatalinkState;
}

/**
 * R20: the crossed point of the level furthest beyond its threshold in the
 * trigger direction — largest threshold for max-direction points, smallest
 * for min-direction points, list-position tie-break. Max-direction wins when
 * both directions are crossed within the level.
 */
export function furthestPoint(points: WarningPoint[], value: number): { threshold: number; thresholdType: 'min' | 'max' } {
  let bestMax: { threshold: number; index: number } | null = null;
  let bestMin: { threshold: number; index: number } | null = null;
  for (let i = 0; i < points.length; i++) {
    const p = points[i];
    if (!p.enabled) continue;
    if (p.direction === 'max' && value > p.value) {
      if (bestMax === null || p.value > bestMax.threshold || (p.value === bestMax.threshold && i < bestMax.index)) {
        bestMax = { threshold: p.value, index: i };
      }
    } else if (p.direction === 'min' && value < p.value) {
      if (bestMin === null || p.value < bestMin.threshold || (p.value === bestMin.threshold && i < bestMin.index)) {
        bestMin = { threshold: p.value, index: i };
      }
    }
  }
  if (bestMax !== null) return { threshold: bestMax.threshold, thresholdType: 'max' };
  if (bestMin !== null) return { threshold: bestMin.threshold, thresholdType: 'min' };
  return { threshold: 0, thresholdType: 'max' };
}

function levelsEqual(a: WarningLevel[], b: WarningLevel[]): boolean {
  if (a.length !== b.length) return false;
  for (let i = 0; i < a.length; i++) {
    const x = a[i];
    const y = b[i];
    // Evaluation-relevant fields only: display-only changes (name, color,
    // autolog, flash) must NOT trigger the immediate recompute path.
    if (x.id !== y.id || x.order !== y.order) {
      return false;
    }
  }
  return true;
}

function pointsEqual(a: WarningPoint[], b: WarningPoint[]): boolean {
  if (a.length !== b.length) return false;
  for (let i = 0; i < a.length; i++) {
    const x = a[i];
    const y = b[i];
    if (x.id !== y.id || x.value !== y.value || x.direction !== y.direction || x.levelId !== y.levelId || x.enabled !== y.enabled) {
      return false;
    }
  }
  return true;
}

function settingsEqual(a: EvaluatorDatalinkSettings, b: EvaluatorDatalinkSettings): boolean {
  return a.enabled === b.enabled && levelsEqual(a.levels, b.levels) && pointsEqual(a.points, b.points);
}

function sortedRanks(levels: WarningLevel[]): Map<string, number> {
  const sorted = [...levels].sort((a, b) => a.order - b.order || 0);
  const map = new Map<string, number>();
  sorted.forEach((l, i) => map.set(l.id, i));
  return map;
}

export interface WarningEvaluator {
  step(now: number, values: Record<string, number | null>): void;
  refresh(payload: WarningSettingsPayload, now?: number): void;
  pushDelay(delayMs: number, now?: number): void;
  invalidatePending(dataId: number, kind: 'edit' | 'disable' | 're-enable'): void;
  resolveAfterWrite(dataId: number, outcome: 'resume' | 'cancel' | 'restart', now?: number): void;
  getState(dataId: number): EvaluatorDatalinkState;
  getLevel(dataId: number, levelId: string): WarningLevel | undefined;
  getSnapshot(): { settings: Map<number, EvaluatorDatalinkSettings>; delayMs: number };
  subscribe(listener: (events: EvaluatorEvent[]) => void): () => void;
  reset(now?: number): void;
  setPaused(now: number, paused: boolean): void;
}

export function createWarningEvaluator(now: () => number): WarningEvaluator {
  const datalinks = new Map<number, DatalinkRuntime>();
  const lastValues = new Map<number, number | null>();
  let delayMs = 500;
  const listeners: ((events: EvaluatorEvent[]) => void)[] = [];
  let pendingEvents: EvaluatorEvent[] = [];
  let pausedAt: number | null = null;

  function flush(): void {
    if (pendingEvents.length === 0) return;
    const events = pendingEvents;
    pendingEvents = [];
    for (const listener of listeners) listener(events);
  }

  function rankOf(dl: DatalinkRuntime, levelId: string | null): number {
    if (levelId === null) return -1;
    return dl.rankByLevelId.get(levelId) ?? -1;
  }

  function levelById(dl: DatalinkRuntime, levelId: string): WarningLevel | undefined {
    return dl.settings.levels.find(l => l.id === levelId);
  }

  function crossedPoints(dl: DatalinkRuntime, value: number): WarningPoint[] {
    const crossed: WarningPoint[] = [];
    if (!dl.settings.enabled) return crossed;
    const levelIds = new Set(dl.settings.levels.map(l => l.id));
    for (const p of dl.settings.points) {
      if (!p.enabled) continue;
      if (!levelIds.has(p.levelId)) continue;
      if (p.direction === 'max' ? value > p.value : value < p.value) crossed.push(p);
    }
    return crossed;
  }

  function highestCrossed(dl: DatalinkRuntime, value: number): { rank: number; levelId: string | null } {
    let best = -1;
    let bestId: string | null = null;
    for (const p of crossedPoints(dl, value)) {
      const r = rankOf(dl, p.levelId);
      if (r > best) {
        best = r;
        bestId = p.levelId;
      }
    }
    return { rank: best, levelId: bestId };
  }

  function dropTo(dl: DatalinkRuntime, toLevelId: string | null, eventTime: number, setReArm: boolean): void {
    const from = dl.state.activeLevelId;
    if (from === null && toLevelId === null) return;
    if (from === toLevelId) return;
    dl.state.activeLevelId = toLevelId;
    if (setReArm) dl.state.reArmAt = eventTime;
    if (from !== null) {
      pendingEvents.push({ type: 'drop', drop: { dataId: dl.dataId, fromLevelId: from, toLevelId } });
    }
  }

  function fireActivation(dl: DatalinkRuntime, levelId: string, value: number, toastEligible: boolean, viaRecompute: boolean): void {
    const crossed = crossedPoints(dl, value).filter(p => p.levelId === levelId);
    const furthest = furthestPoint(crossed, value);
    pendingEvents.push({
      type: 'activation',
      activation: {
        dataId: dl.dataId,
        levelId,
        toastEligible,
        viaRecompute,
        value,
        threshold: furthest.threshold,
        thresholdType: furthest.thresholdType,
      },
    });
  }

  function recomputeImmediate(dl: DatalinkRuntime, nowTime: number): void {
    const value = lastValues.get(dl.dataId) ?? null;
    const active = dl.state.activeLevelId;
    const activeVanished = active !== null && !dl.rankByLevelId.has(active);
    const { rank, levelId } = value === null ? { rank: -1, levelId: null } : highestCrossed(dl, value);
    const activeRank = active !== null && dl.rankByLevelId.has(active) ? rankOf(dl, active) : -1;

    if (activeVanished) {
      dl.state.pending = null;
      dropTo(dl, levelId, nowTime, true);
      return;
    }
    if (value === null) {
      dl.state.pending = null;
      dropTo(dl, null, nowTime, true);
      return;
    }
    if (levelId !== null && rank > activeRank) {
      dl.state.pending = null;
      dl.state.activeLevelId = levelId;
      fireActivation(dl, levelId, value, false, true);
    } else if (rank < activeRank) {
      // Write-driven recompute that lowers/clears the active state: re-arm at
      // the recompute moment (R14's point-removal/disable path).
      dl.state.pending = null;
      dropTo(dl, levelId, nowTime, true);
    } else {
      dl.state.pending = null;
    }
  }

  function createPending(dl: DatalinkRuntime, targetLevelId: string, crossingTime: number): void {
    dl.state.pending = {
      crossingTime,
      deadline: crossingTime + delayMs,
      targetLevelId,
      delayMsAtCrossing: delayMs,
      remainingAtSuspend: null,
      suspendKind: null,
    };
  }

  return {
    step(stepNow: number, values: Record<string, number | null>): void {
      for (const dl of datalinks.values()) {
        const dataId = dl.dataId;
        const value = values[String(dataId)] ?? null;
        lastValues.set(dataId, value);
        const suspended = dl.state.pending !== null && dl.state.pending.suspendKind !== null;

        // Value-driven clears and de-escalations run even while a transition is
        // suspended (KTD2: evaluation continues on the last re-read snapshot;
        // drops are value-driven, not settings-driven).
        if (value === null) {
          if (!suspended && dl.state.pending !== null) {
            dl.state.pending = null;
            dl.state.reArmAt = stepNow;
          }
          dropTo(dl, null, stepNow, true);
          continue;
        }

        const { rank, levelId } = highestCrossed(dl, value);
        const activeRank = rankOf(dl, dl.state.activeLevelId);

        if (dl.state.activeLevelId !== null && (levelId === null || rank < activeRank)) {
          dropTo(dl, levelId, stepNow, true);
        }

        if (suspended) continue;

        const pending = dl.state.pending;

        if (pending !== null) {
          if (levelId === null) {
            dl.state.pending = null;
            dl.state.reArmAt = stepNow;
          } else if (rank > rankOf(dl, pending.targetLevelId)) {
            pending.targetLevelId = levelId; // join the earliest timer (R14)
          } else if (rank < rankOf(dl, pending.targetLevelId)) {
            // revert to the highest still-crossed point; timer restarts (R14)
            pending.targetLevelId = levelId;
            pending.deadline = stepNow + delayMs;
            pending.crossingTime = stepNow;
            if (rank <= rankOf(dl, dl.state.activeLevelId)) {
              dl.state.pending = null;
            }
          }
        } else if (dl.state.activeLevelId === null) {
          if (levelId !== null) {
            createPending(dl, levelId, stepNow);
          }
        } else if (rank > activeRank) {
          createPending(dl, levelId!, stepNow);
        }

        const p = dl.state.pending;
        if (p !== null && p.suspendKind === null && stepNow >= p.deadline) {
          const current = highestCrossed(dl, value);
          if (current.levelId === null) {
            dl.state.pending = null;
            dl.state.reArmAt = stepNow;
          } else {
            const eligible = p.crossingTime >= (dl.state.reArmAt ?? -Infinity) + p.delayMsAtCrossing;
            dl.state.activeLevelId = current.levelId;
            dl.state.pending = null;
            fireActivation(dl, current.levelId, value, eligible, false);
          }
        }
      }
      flush();
    },

    refresh(payload: WarningSettingsPayload, refreshNow?: number): void {
      const t = refreshNow ?? now();
      const next = new Map<number, DatalinkRuntime>();
      const seen = new Set<number>();

      for (const s of payload.settings) {
        seen.add(s.dataId);
        const settings: EvaluatorDatalinkSettings = {
          enabled: s.enabled,
          name: s.name,
          unit: s.unit,
          category: s.category,
          levels: s.levels,
          points: s.points,
          hasCvdConfusablePair: hasCvdConfusablePair(s.levels),
        };
        const existing = datalinks.get(s.dataId);

        if (existing !== undefined) {
          const suspended = existing.state.pending !== null && existing.state.pending.suspendKind !== null;
          const changed = !settingsEqual(existing.settings, settings);
          existing.settings = settings;
          existing.rankByLevelId = sortedRanks(settings.levels);
          if (!suspended && changed) {
            recomputeImmediate(existing, t);
          }
          next.set(s.dataId, existing);
          continue;
        }

        next.set(s.dataId, {
          dataId: s.dataId,
          settings,
          rankByLevelId: sortedRanks(settings.levels),
          state: { activeLevelId: null, pending: null, reArmAt: null },
        });
      }

      for (const [dataId, dl] of datalinks) {
        if (seen.has(dataId)) continue;
        dropTo(dl, null, t, false);
      }

      datalinks.clear();
      for (const [k, v] of next) datalinks.set(k, v);
      delayMs = payload.delayMs;
      flush();
    },

    pushDelay(newDelay: number, pushNow?: number): void {
      const t = pushNow ?? now();
      for (const dl of datalinks.values()) {
        const p = dl.state.pending;
        if (p === null || p.suspendKind !== null) continue;
        const elapsed = t - p.crossingTime;
        p.deadline = t + Math.max(0, newDelay - elapsed);
      }
      delayMs = newDelay;
    },

    invalidatePending(dataId: number, kind: 'edit' | 'disable' | 're-enable'): void {
      const dl = datalinks.get(dataId);
      if (dl === undefined) return;
      const p = dl.state.pending;
      if (p === null) return;
      if (p.suspendKind !== null) {
        // A re-enable write landing while a suspension exists upgrades the
        // resolution to the discard+restart branch (KTD2).
        if (kind === 're-enable') p.suspendKind = kind;
        return;
      }
      p.suspendKind = kind;
      p.remainingAtSuspend = p.deadline - now();
    },

    resolveAfterWrite(dataId: number, outcome: 'resume' | 'cancel' | 'restart', resolveNow?: number): void {
      const t = resolveNow ?? now();
      const dl = datalinks.get(dataId);
      if (dl === undefined) return;
      const p = dl.state.pending;
      if (p === null || p.suspendKind === null) return;

      if (outcome === 'cancel') {
        dl.state.pending = null;
        return;
      }

      const value = lastValues.get(dataId) ?? null;
      const current = value === null ? null : highestCrossed(dl, value).levelId;
      const crossingStillHolds = current !== null;

      if (outcome === 'restart') {
        dl.state.pending = null;
        if (crossingStillHolds) {
          createPending(dl, current!, t);
        }
        return;
      }

      if (!crossingStillHolds) {
        dl.state.pending = null;
        dl.state.reArmAt = t;
        return;
      }
      dl.state.pending = {
        crossingTime: p.crossingTime,
        deadline: t + (p.remainingAtSuspend ?? 0),
        targetLevelId: current!,
        delayMsAtCrossing: p.delayMsAtCrossing,
        remainingAtSuspend: null,
        suspendKind: null,
      };
    },

    getState(dataId: number): EvaluatorDatalinkState {
      const dl = datalinks.get(dataId);
      if (dl === undefined) {
        return { activeLevelId: null, activeLevelName: null, activeLevelColor: null, hasCvdConfusablePair: false };
      }
      const level = dl.state.activeLevelId !== null ? levelById(dl, dl.state.activeLevelId) : undefined;
      return {
        activeLevelId: dl.state.activeLevelId,
        activeLevelName: level?.name ?? null,
        activeLevelColor: level?.color ?? null,
        hasCvdConfusablePair: dl.settings.hasCvdConfusablePair,
      };
    },

    getLevel(dataId: number, levelId: string): WarningLevel | undefined {
      const dl = datalinks.get(dataId);
      if (dl === undefined) return undefined;
      return levelById(dl, levelId);
    },

    getSnapshot(): { settings: Map<number, EvaluatorDatalinkSettings>; delayMs: number } {
      const settings = new Map<number, EvaluatorDatalinkSettings>();
      for (const [dataId, dl] of datalinks) settings.set(dataId, dl.settings);
      return { settings, delayMs };
    },

    subscribe(listener: (events: EvaluatorEvent[]) => void): () => void {
      listeners.push(listener);
      return () => {
        const i = listeners.indexOf(listener);
        if (i >= 0) listeners.splice(i, 1);
      };
    },

    reset(resetNow?: number): void {
      const t = resetNow ?? now();
      for (const dl of datalinks.values()) {
        dl.state.pending = null;
        dl.state.reArmAt = null;
        dropTo(dl, null, t, false);
      }
      flush();
    },

    setPaused(t: number, paused: boolean): void {
      if (paused) {
        if (pausedAt === null) pausedAt = t;
        return;
      }
      if (pausedAt === null) return;
      const shift = t - pausedAt;
      pausedAt = null;
      if (shift <= 0) return;
      for (const dl of datalinks.values()) {
        const p = dl.state.pending;
        if (p === null || p.suspendKind !== null) continue;
        p.deadline += shift;
      }
    },
  };
}

export const warningEvaluator: WarningEvaluator = createWarningEvaluator(() => performance.now());

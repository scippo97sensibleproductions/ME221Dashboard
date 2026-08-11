import { addToast, getToasts, removeToast, setToastDisplayed, updateToast, type Toast } from './toasts.svelte';
import { navigationGate } from './navigationGate.svelte';
import { warningStore } from './stores/warningStore.svelte';
import type { WarningEvaluator } from './stores/warningEvaluator';
import type { ActivationEvent, DropEvent, EvaluatorEvent } from './stores/warningEvaluator';

export interface WarningToastOptions {
  now: () => number;
  durationMs?: number;
  maxStack?: number;
  replayGapMs?: number;
  isPanelOpen: () => boolean;
}

export interface LedgerEntry {
  dataId: number;
  levelId: string;
  channel: 'toast' | 'hidden';
  announcedAt: number;
}

interface PendingToast {
  id: number;
  dataId: number;
  levelId: string;
}

export interface WarningToasts {
  attachEvaluator(evaluator: WarningEvaluator): void;
  refreshDisplayLookup(): void;
  setBackgrounded(paused: boolean, now: number): void;
  revalidate(): void;
  tick(now: number): void;
  getLedger(): LedgerEntry[];
  getAnnouncedText(id: number): string | undefined;
  handleTap(id: number): boolean;
  dismiss(id: number): void;
  firstRunCompleted(): void;
  reset(): void;
}

export function createWarningToasts(options: WarningToastOptions): WarningToasts {
  const durationMs = options.durationMs ?? 3000;
  const maxStack = options.maxStack ?? 3;
  const replayGapMs = options.replayGapMs ?? 1000;

  let evaluator: WarningEvaluator | null = null;
  let unsubscribe: (() => void) | null = null;
  let activeLevelIdByDataId: Record<number, string | null> = {};
  let ledger: LedgerEntry[] = [];
  let announcedTextById = new Map<number, string>();
  let pausedAt: number | null = null;
  let firstRunDone = false;
  let lastSurfacedAt = -Infinity;
  let pendingQueue: PendingToast[] = [];

  function visibleToasts(): Toast[] {
    return getToasts().filter(t => t.meta?.displayed);
  }

  function datalinkName(dataId: number): string {
    return evaluator?.getSnapshot().settings.get(dataId)?.name ?? '';
  }

  function levelName(dataId: number, levelId: string): string {
    return evaluator?.getLevel(dataId, levelId)?.name ?? levelId;
  }

  function messageFor(dataId: number, levelId: string): string {
    return `${datalinkName(dataId)}: ${levelName(dataId, levelId)}`;
  }

  function markAnnounced(id: number, channel: 'toast' | 'hidden'): void {
    const toast = getToasts().find(t => t.id === id);
    if (!toast || !toast.meta || toast.meta.announced) return;
    announcedTextById.set(id, toast.message);
    updateToast(id, { meta: { ...toast.meta, announced: true } });
    ledger.push({ dataId: toast.meta.dataId, levelId: toast.meta.levelId, channel, announcedAt: options.now() });
  }

  function addVisibleToast(dataId: number, levelId: string, evictionClass: 'activation' | 'escalation'): number {
    const id = addToast({
      message: messageFor(dataId, levelId),
      type: 'warning',
      meta: {
        dataId,
        levelId,
        levelName: levelName(dataId, levelId),
        evictionClass,
        deadline: options.now() + durationMs,
        remaining: durationMs,
        announced: false,
        displayed: false,
        firstRunQueued: false,
      },
    });
    setToastDisplayed(id, true);
    markAnnounced(id, 'toast');
    return id;
  }

  function moveToPending(id: number): void {
    setToastDisplayed(id, false);
    const toast = getToasts().find(t => t.id === id);
    if (!toast || !toast.meta) return;
    updateToast(id, { meta: { ...toast.meta, firstRunQueued: true } });
    pendingQueue.push({ id, dataId: toast.meta.dataId, levelId: toast.meta.levelId });
  }

  function pushArrival(dataId: number, levelId: string, evictionClass: 'activation' | 'escalation'): void {
    const stack = visibleToasts();
    if (stack.length < maxStack) {
      addVisibleToast(dataId, levelId, evictionClass);
      return;
    }

    if (evictionClass === 'escalation') {
      const nonEscalation = stack.find(t => t.meta!.evictionClass !== 'escalation');
      if (nonEscalation) {
        if (firstRunDone) {
          removeToast(nonEscalation.id);
        } else {
          moveToPending(nonEscalation.id);
        }
        addVisibleToast(dataId, levelId, evictionClass);
      } else if (!options.isPanelOpen()) {
        // All-escalation stack: announce through the hidden live region once.
        const id = addToast({
          message: messageFor(dataId, levelId),
          type: 'warning',
          meta: {
            dataId,
            levelId,
            levelName: levelName(dataId, levelId),
            evictionClass,
            deadline: options.now() + durationMs,
            remaining: durationMs,
            announced: false,
            displayed: false,
            firstRunQueued: false,
          },
        });
        markAnnounced(id, 'hidden');
      }
    } else {
      // Non-escalation arrival with a full stack: evict the oldest
      // non-escalation (retention priority); fall back to the oldest when the
      // stack holds only escalations.
      const nonEscalation = stack.find(t => t.meta!.evictionClass !== 'escalation');
      const victim = nonEscalation ?? stack[0];
      if (firstRunDone || victim.meta!.evictionClass === 'escalation') {
        removeToast(victim.id);
      } else {
        moveToPending(victim.id);
      }
      addVisibleToast(dataId, levelId, evictionClass);
    }
  }

  function onActivation(event: ActivationEvent): void {
    const previous = activeLevelIdByDataId[event.dataId] ?? null;
    activeLevelIdByDataId[event.dataId] = event.levelId;

    if (event.viaRecompute || !event.toastEligible) {
      // Silent activation: update any visible toast for the datalink to the new
      // level content (no re-announcement), keeping toast-gauge-panel agreement.
      for (const toast of visibleToasts()) {
        if (toast.meta!.dataId !== event.dataId) continue;
        const newName = levelName(event.dataId, event.levelId);
        updateToast(toast.id, {
          message: messageFor(event.dataId, event.levelId),
          meta: { ...toast.meta!, levelId: event.levelId, levelName: newName },
        });
      }
      return;
    }

    const evictionClass: 'activation' | 'escalation' =
      previous !== null && previous !== event.levelId ? 'escalation' : 'activation';

    const existing = visibleToasts().find(t => t.meta!.dataId === event.dataId);
    if (existing) removeToast(existing.id);

    pushArrival(event.dataId, event.levelId, evictionClass);
  }

  function onDrop(event: DropEvent): void {
    activeLevelIdByDataId[event.dataId] = event.toLevelId;
    for (const toast of visibleToasts()) {
      if (toast.meta!.dataId !== event.dataId) continue;
      if (event.toLevelId === null) {
        removeToast(toast.id);
      } else {
        const newName = levelName(event.dataId, event.toLevelId);
        updateToast(toast.id, {
          message: messageFor(event.dataId, event.toLevelId),
          meta: { ...toast.meta!, levelId: event.toLevelId, levelName: newName },
        });
      }
    }
  }

  function surfaceQueued(now: number): void {
    if (!firstRunDone || pendingQueue.length === 0) return;
    if (now - lastSurfacedAt < replayGapMs) return;
    if (visibleToasts().length >= maxStack) return;
    const item = pendingQueue[0];
    const toast = getToasts().find(t => t.id === item.id);
    if (!toast || !toast.meta) {
      pendingQueue = pendingQueue.slice(1);
      return;
    }
    // Never surface a queued toast whose datalink is no longer active at the
    // toast's level (the drop already happened while it was queued).
    const current = evaluator?.getState(item.dataId).activeLevelId ?? null;
    if (current !== item.levelId) {
      removeToast(item.id);
      pendingQueue = pendingQueue.slice(1);
      return;
    }
    pendingQueue = pendingQueue.slice(1);
    setToastDisplayed(item.id, true);
    const fresh = getToasts().find(t => t.id === item.id);
    if (!fresh || !fresh.meta) return;
    updateToast(item.id, {
      message: messageFor(item.dataId, item.levelId),
      meta: { ...fresh.meta, firstRunQueued: false, deadline: now + durationMs },
    });
    if (!fresh.meta.announced) markAnnounced(item.id, 'toast');
    lastSurfacedAt = now;
  }

  return {
    attachEvaluator(evaluatorInstance: WarningEvaluator): void {
      unsubscribe?.();
      evaluator = evaluatorInstance;
      activeLevelIdByDataId = {};
      for (const dataId of evaluatorInstance.getSnapshot().settings.keys()) {
        activeLevelIdByDataId[dataId] = evaluatorInstance.getState(dataId).activeLevelId;
      }
      unsubscribe = evaluatorInstance.subscribe((events: EvaluatorEvent[]) => {
        for (const event of events) {
          if (event.type === 'activation') onActivation(event.activation);
          else onDrop(event.drop);
        }
      });
    },

    refreshDisplayLookup(): void {
      for (const toast of getToasts()) {
        if (!toast.meta || !toast.meta.displayed) continue;
        const newName = levelName(toast.meta.dataId, toast.meta.levelId);
        if (newName !== toast.meta.levelName) {
          updateToast(toast.id, {
            message: messageFor(toast.meta.dataId, toast.meta.levelId),
            meta: { ...toast.meta, levelName: newName },
          });
        }
      }
    },

    setBackgrounded(paused: boolean, now: number): void {
      if (paused) {
        if (pausedAt === null) pausedAt = now;
        return;
      }
      if (pausedAt === null) return;
      const shift = now - pausedAt;
      pausedAt = null;
      if (shift <= 0) return;
      for (const toast of getToasts()) {
        if (!toast.meta || !toast.meta.displayed) continue;
        updateToast(toast.id, { meta: { ...toast.meta, deadline: toast.meta.deadline + shift } });
      }
    },

    revalidate(): void {
      if (!evaluator) return;
      for (const toast of visibleToasts()) {
        const state = evaluator.getState(toast.meta!.dataId);
        if (state.activeLevelId !== toast.meta!.levelId) {
          removeToast(toast.id);
        }
      }
    },

    tick(now: number): void {
      if (pausedAt === null) {
        for (const toast of visibleToasts()) {
          if (now >= toast.meta!.deadline) removeToast(toast.id);
        }
        // Hidden-region announcements expire like visible toasts (bounded set)
        for (const toast of getToasts()) {
          if (!toast.meta || toast.meta.displayed || toast.meta.firstRunQueued) continue;
          if (!toast.meta.announced) continue;
          if (now >= toast.meta.deadline) removeToast(toast.id);
        }
      }
      surfaceQueued(now);
    },

    getLedger(): LedgerEntry[] {
      return ledger;
    },

    getAnnouncedText(id: number): string | undefined {
      return announcedTextById.get(id);
    },

    handleTap(id: number): boolean {
      if (navigationGate.isNavigationBlocked()) return false;
      const toast = getToasts().find(t => t.id === id);
      if (!toast || !toast.meta) return false;
      removeToast(id);
      return true;
    },

    dismiss(id: number): void {
      removeToast(id);
    },

    firstRunCompleted(): void {
      firstRunDone = true;
    },

    reset(): void {
      for (const toast of getToasts()) removeToast(toast.id);
      announcedTextById = new Map();
      ledger = [];
      pendingQueue = [];
      activeLevelIdByDataId = {};
      pausedAt = null;
      firstRunDone = false;
      lastSurfacedAt = -Infinity;
    },
  };
}

export const warningToasts: WarningToasts = createWarningToasts({
  now: () => performance.now(),
  isPanelOpen: () => warningStore.isPanelOpen,
});

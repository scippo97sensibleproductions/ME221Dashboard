<script lang="ts">
  import { onDestroy } from 'svelte';
  import { IconAlertTriangle, IconX, IconClock } from '@tabler/icons-svelte';
  import { warningStore } from './stores/warningStore.svelte';
  import { warningEvaluator } from './stores/warningEvaluator';
  import { warningToasts } from './warningToasts';
  import { navigationGate } from './navigationGate.svelte';
  import { createPanelAnnouncements, type PanelRow } from './panelAnnouncements';
  import { getToasts } from './toasts.svelte';

  let { dashboardName, page = 'dashboard', onNavigate = () => {} }: {
    dashboardName: string;
    page?: string;
    onNavigate?: (page: string) => void;
  } = $props();

  const panelListId = $derived(`warning-panel-list-${dashboardName.replace(/[^a-zA-Z0-9-]/g, '-')}`);

  const announcements = createPanelAnnouncements({
    getLedger: () => warningToasts.getLedger(),
    hasVisibleToast: (dataId) => getToasts().some(t => t.meta?.displayed && t.meta.dataId === dataId),
    datalinkName: (dataId) => warningEvaluator.getSnapshot().settings.get(dataId)?.name ?? `Datalink ${dataId}`,
  });

  let activeList = $derived(
    Array.from(warningStore.activeWarnings.values()).sort((a, b) => b.triggeredAt - a.triggeredAt)
  );
  let count = $derived(warningStore.activeWarningCount);
  let isOpen = $derived(warningStore.isPanelOpen);
  let showHistory = $state(false);
  let panelAnnouncementText = $state('');
  let badgeAnnouncementText = $state('');
  let _tick = $state(0);
  let _tickInterval: ReturnType<typeof setInterval> | null = null;

  // Relative timestamps re-evaluate every 5s while the panel is open.
  $effect(() => {
    if (isOpen) {
      _tickInterval = setInterval(() => { _tick++; }, 5000);
    } else if (_tickInterval) {
      clearInterval(_tickInterval);
      _tickInterval = null;
    }
    return () => { if (_tickInterval) clearInterval(_tickInterval); };
  });

  function rows(): PanelRow[] {
    return Array.from(warningStore.activeWarnings.values()).map(w => ({
      dataId: w.dataId,
      levelId: w.levelId,
      levelName: w.levelName,
      name: w.name,
    }));
  }

  function emit(region: 'panel' | 'badge', texts: string[]) {
    if (texts.length === 0) return;
    if (region === 'panel') panelAnnouncementText = texts.join('. ');
    else badgeAnnouncementText = texts.join('. ');
  }

  $effect(() => {
    void count;
    emit('badge', announcements.onCountChange(count, isOpen));
  });

  $effect(() => {
    if (isOpen) {
      emit('panel', announcements.onOpen(rows()));
    }
  });

  const unsubscribeEvents = warningEvaluator.subscribe(events => {
    for (const event of events) {
      if (event.type === 'activation' && !event.activation.viaRecompute) {
        const dl = warningEvaluator.getSnapshot().settings.get(event.activation.dataId);
        const level = warningEvaluator.getLevel(event.activation.dataId, event.activation.levelId);
        emit('panel', announcements.onActivation({
          dataId: event.activation.dataId,
          levelId: event.activation.levelId,
          levelName: level?.name ?? event.activation.levelId,
          name: dl?.name ?? `Datalink ${event.activation.dataId}`,
        }, isOpen));
      } else if (event.type === 'drop') {
        emit('panel', announcements.onDrop(event.drop, isOpen));
      }
    }
  });

  function badgeTap() {
    if (navigationGate.isNavigationBlocked()) return;
    if (page === 'dashboard') {
      warningStore.togglePanel();
    } else {
      warningStore.openPanel();
      onNavigate('dashboard');
    }
  }

  onDestroy(() => {
    unsubscribeEvents();
    warningStore.closePanel();
  });

  function formatTimeAgo(ts: number): string {
    void _tick;
    const sec = Math.floor((Date.now() - ts) / 1000);
    if (sec < 5) return 'just now';
    if (sec < 60) return `${sec}s ago`;
    const min = Math.floor(sec / 60);
    if (min < 60) return `${min}m ago`;
    return `${Math.floor(min / 60)}h ago`;
  }

  function formatTime(ts: number): string {
    return new Date(ts).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  }
</script>

<!-- Floating badge -->
<button
  class="fixed bottom-20 right-4 z-30 flex h-10 items-center gap-1.5 rounded-full px-3 text-xs font-bold transition-all duration-200"
  style="background-color: {count > 0 ? (activeList[0]?.color ?? '#f59e0b') : 'var(--metro-card)'}; color: {count > 0 ? '#fff' : 'var(--metro-text-secondary)'}; border: 1px solid {count > 0 ? 'transparent' : 'var(--metro-border)'};"
  onclick={badgeTap}
  aria-label="Warnings: {count} active"
  aria-expanded={isOpen}
  aria-controls={isOpen ? panelListId : undefined}
>
  <IconAlertTriangle size={16} />
  {#if count > 0}
    <span>{count}</span>
  {/if}
</button>
<span class="sr-only" aria-live="polite">{badgeAnnouncementText}</span>

<!-- Backdrop + slide-in panel -->
{#if isOpen}
  <div
    class="fixed inset-0 z-30 bg-black/40 transition-opacity"
    onclick={() => warningStore.closePanel()}
    onkeydown={(e) => { if (e.key === 'Escape') warningStore.closePanel(); }}
    role="button"
    tabindex="-1"
  ></div>

  <div
    class="fixed right-0 top-0 z-40 flex max-h-[65vh] w-80 flex-col border-l shadow-2xl transition-transform duration-200"
    style="background-color: var(--metro-bg); border-color: var(--metro-border);"
  >
    <div class="flex items-center justify-between border-b px-4 py-3" style="border-color: var(--metro-border);">
      <div class="flex items-center gap-2">
        <IconAlertTriangle size={18} style="color: #f59e0b;" />
        <span class="text-sm font-bold" style="color: var(--metro-text);">Warnings</span>
        {#if count > 0}
          <span class="rounded-full bg-amber-500/20 px-2 py-0.5 text-[10px] font-bold text-amber-300">{count}</span>
        {/if}
      </div>
      <button
        class="rounded p-1 transition-colors hover:bg-gray-700"
        style="color: var(--metro-text-muted);"
        onclick={() => warningStore.closePanel()}
        aria-label="Close warnings panel"
      >
        <IconX size={16} />
      </button>
    </div>

    <div class="flex border-b" style="border-color: var(--metro-border);">
      <button
        class="flex-1 px-3 py-2 text-xs font-medium transition-colors"
        style="color: {!showHistory ? 'var(--metro-text)' : 'var(--metro-text-muted)'}; border-bottom: 2px solid {!showHistory ? '#f59e0b' : 'transparent'};"
        onclick={() => { showHistory = false; }}
      >
        Active ({count})
      </button>
      <button
        class="flex-1 px-3 py-2 text-xs font-medium transition-colors"
        style="color: {showHistory ? 'var(--metro-text)' : 'var(--metro-text-muted)'}; border-bottom: 2px solid {showHistory ? '#f59e0b' : 'transparent'};"
        onclick={() => { showHistory = true; }}
      >
        History
      </button>
    </div>

    <div class="flex-1 overflow-y-auto">
      {#if !showHistory}
        {#if activeList.length === 0}
          <div class="flex flex-col items-center justify-center py-16 text-center">
            <IconAlertTriangle size={32} style="color: var(--metro-text-muted); opacity: 0.3;" />
            <p class="mt-3 text-xs" style="color: var(--metro-text-muted);">No active warnings</p>
            <p class="mt-1 text-[10px]" style="color: var(--metro-text-muted); opacity: 0.6;">Warnings appear here when values cross thresholds</p>
          </div>
        {:else}
          <ul id={panelListId} class="max-h-[50vh] overflow-y-auto">
            {#each activeList as w (w.dataId)}
              <li
                id="warning-row-{w.dataId}"
                class="flex items-start gap-3 border-b px-4 py-3"
                style="border-color: var(--metro-border); border-left: 3px solid {w.color};"
              >
                <div class="mt-0.5 flex-1 min-w-0">
                  <div class="flex items-center gap-2">
                    <span class="text-xs font-medium" style="color: var(--metro-text);">{w.name}</span>
                    <span
                      class="rounded-full px-1.5 py-0.5 text-[9px] font-bold"
                      style="background-color: {w.color}33; color: {w.color};"
                    >
                      {w.levelName}
                    </span>
                  </div>
                  <div class="mt-0.5 flex items-baseline gap-1 text-[11px]">
                    <span class="font-mono font-bold" style="color: {w.color};">
                      {w.value.toFixed(1)}{w.unit ? ` ${w.unit}` : ''}
                    </span>
                    <span style="color: var(--metro-text-muted);">
                      {w.thresholdType === 'max' ? '>' : '<'}
                      {w.threshold.toFixed(1)}{w.unit ? ` ${w.unit}` : ''}
                    </span>
                  </div>
                  <div class="mt-1 flex items-center gap-1 text-[10px]" style="color: var(--metro-text-muted);">
                    <IconClock size={10} />
                    {formatTimeAgo(w.triggeredAt)}
                    {#if w.category}
                      <span class="ml-1 rounded bg-gray-800 px-1 py-0.5">{w.category}</span>
                    {/if}
                  </div>
                </div>
              </li>
            {/each}
          </ul>
        {/if}
      {:else}
        {#if warningStore.history.length === 0}
          <div class="flex flex-col items-center justify-center py-16 text-center">
            <IconClock size={32} style="color: var(--metro-text-muted); opacity: 0.3;" />
            <p class="mt-3 text-xs" style="color: var(--metro-text-muted);">No warning history</p>
          </div>
        {:else}
          {#each warningStore.history as h (h.id)}
            <div
              class="flex items-start gap-3 border-b px-4 py-2.5"
              style="border-color: var(--metro-border); opacity: 0.7;"
            >
              <div class="flex-1 min-w-0">
                <div class="flex items-center gap-2">
                  <span class="text-[11px] font-medium" style="color: var(--metro-text-secondary);">{h.name}</span>
                  <span class="rounded-full px-1.5 py-0.5 text-[9px] font-bold" style="background-color: rgba(245,158,11,0.15); color: #fcd34d;">
                    {h.severity}
                  </span>
                </div>
                <div class="mt-0.5 text-[10px]" style="color: var(--metro-text-muted);">
                  {h.value.toFixed(1)}{h.unit ? ` ${h.unit}` : ''} — {formatTime(h.triggeredAt)}
                  {#if h.clearedAt}
                    → {formatTime(h.clearedAt)}
                  {/if}
                </div>
              </div>
            </div>
          {/each}
        {/if}
      {/if}
    </div>
  </div>
{/if}
<span class="sr-only" aria-live="polite">{panelAnnouncementText}</span>

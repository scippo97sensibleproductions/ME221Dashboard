<script lang="ts">
  import { IconAlertTriangle, IconX, IconTrash, IconClock } from '@tabler/icons-svelte';
  import { warningStore, type ActiveWarning, type WarningHistoryEntry } from './stores/warningStore.svelte';

  let activeList = $derived(
    Array.from(warningStore.activeWarnings.values())
      .sort((a, b) => (a.severity === 'critical' ? 0 : 1) - (b.severity === 'critical' ? 0 : 1) || b.triggeredAt - a.triggeredAt)
  );

  let count = $derived(warningStore.activeWarningCount);
  let isOpen = $derived(warningStore.isPanelOpen);
  let showHistory = $state(false);

  function formatTimeAgo(ts: number): string {
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

  let _tickInterval: ReturnType<typeof setInterval> | null = null;
  let _tick = $state(0);

  $effect(() => {
    if (isOpen) {
      _tickInterval = setInterval(() => { _tick++; }, 5000);
    } else if (_tickInterval) {
      clearInterval(_tickInterval);
      _tickInterval = null;
    }
    return () => { if (_tickInterval) clearInterval(_tickInterval); };
  });
</script>

<!-- Floating trigger button -->
<button
  class="fixed bottom-20 right-4 z-40 flex h-10 items-center gap-1.5 rounded-full px-3 text-xs font-bold shadow-lg transition-all duration-200 {count > 0 ? 'animate-pulse' : ''}"
  style="background-color: {count > 0 ? (activeList[0]?.severity === 'critical' ? '#ef4444' : '#f59e0b') : 'var(--metro-card)'}; color: {count > 0 ? '#fff' : 'var(--metro-text-secondary)'}; border: 1px solid {count > 0 ? 'transparent' : 'var(--metro-border)'};"
  onclick={() => warningStore.togglePanel()}
  aria-label="Warnings: {count} active"
>
  <IconAlertTriangle size={16} />
  {#if count > 0}
    <span>{count}</span>
  {/if}
</button>

<!-- Backdrop + slide-in panel -->
{#if isOpen}
  <!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
  <div
    class="fixed inset-0 z-40 bg-black/40 transition-opacity"
    onclick={() => warningStore.closePanel()}
    onkeydown={(e) => { if (e.key === 'Escape') warningStore.closePanel(); }}
    role="button"
    tabindex="-1"
  ></div>

  <div
    class="fixed right-0 top-0 z-50 flex h-full w-80 flex-col border-l shadow-2xl transition-transform duration-200"
    style="background-color: var(--metro-bg); border-color: var(--metro-border);"
  >
    <!-- Header -->
    <div class="flex items-center justify-between border-b px-4 py-3" style="border-color: var(--metro-border);">
      <div class="flex items-center gap-2">
        <IconAlertTriangle size={18} style="color: #f59e0b;" />
        <span class="text-sm font-bold" style="color: var(--metro-text);">Warnings</span>
        {#if count > 0}
          <span class="rounded-full bg-amber-500/20 px-2 py-0.5 text-[10px] font-bold text-amber-300">{count}</span>
        {/if}
      </div>
      <div class="flex items-center gap-1">
        {#if count > 0}
          <button
            class="rounded p-1 transition-colors hover:bg-gray-700"
            style="color: var(--metro-text-muted);"
            onclick={() => warningStore.clearAllWarnings()}
            title="Clear all warnings"
          >
            <IconTrash size={14} />
          </button>
        {/if}
        <button
          class="rounded p-1 transition-colors hover:bg-gray-700"
          style="color: var(--metro-text-muted);"
          onclick={() => warningStore.closePanel()}
        >
          <IconX size={16} />
        </button>
      </div>
    </div>

    <!-- Tab bar -->
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

    <!-- Content -->
    <div class="flex-1 overflow-y-auto">
      {#if !showHistory}
        <!-- Active warnings -->
        {#if activeList.length === 0}
          <div class="flex flex-col items-center justify-center py-16 text-center">
            <IconAlertTriangle size={32} style="color: var(--metro-text-muted); opacity: 0.3;" />
            <p class="mt-3 text-xs" style="color: var(--metro-text-muted);">No active warnings</p>
            <p class="mt-1 text-[10px]" style="color: var(--metro-text-muted); opacity: 0.6;">Warnings appear here when values cross thresholds</p>
          </div>
        {:else}
          {#each activeList as w (w.dataId)}
            {@const time = _tick}
            <div
              class="flex items-start gap-3 border-b px-4 py-3 transition-colors hover:bg-gray-800/30"
              style="border-color: var(--metro-border); border-left: 3px solid {w.severity === 'critical' ? '#ef4444' : '#f59e0b'};"
            >
              <div class="mt-0.5 flex-1 min-w-0">
                <div class="flex items-center gap-2">
                  <span class="text-xs font-medium" style="color: var(--metro-text);">{w.name}</span>
                  <span
                    class="rounded-full px-1.5 py-0.5 text-[9px] font-bold"
                    style="background-color: {w.severity === 'critical' ? 'rgba(239,68,68,0.2)' : 'rgba(245,158,11,0.2)'}; color: {w.severity === 'critical' ? '#fca5a5' : '#fcd34d'};"
                  >
                    {w.severity === 'critical' ? 'CRITICAL' : 'WARN'}
                  </span>
                </div>
                <div class="mt-0.5 flex items-baseline gap-1 text-[11px]">
                  <span class="font-mono font-bold" style="color: {w.severity === 'critical' ? '#ef4444' : '#f59e0b'};">
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
              <button
                class="mt-0.5 rounded p-1 transition-colors hover:bg-gray-700"
                style="color: var(--metro-text-muted);"
                onclick={() => warningStore.clearWarning(w.dataId)}
                title="Dismiss"
              >
                <IconX size={12} />
              </button>
            </div>
          {/each}
        {/if}
      {:else}
        <!-- History -->
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
                  <span
                    class="rounded-full px-1.5 py-0.5 text-[9px] font-bold"
                    style="background-color: {h.severity === 'critical' ? 'rgba(239,68,68,0.15)' : 'rgba(245,158,11,0.15)'}; color: {h.severity === 'critical' ? '#fca5a5' : '#fcd34d'};"
                  >
                    {h.severity === 'critical' ? 'CRITICAL' : 'WARN'}
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

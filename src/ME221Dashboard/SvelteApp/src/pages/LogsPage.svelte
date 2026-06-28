<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { HybridBridge, type LogEntryEvent } from '../lib/HybridBridge';
  import { IconMessage, IconCopy, IconTrash, IconPlayerPlay, IconPlayerPause } from '@tabler/icons-svelte';

  let { onNavigate }: { onNavigate: (page: string) => void } = $props();

  let logs = $state<LogEntryEvent[]>([]);
  let filterLevel = $state('All');
  let filterCategory = $state('');
  let filterText = $state('');
  let autoScroll = $state(true);
  let paused = $state(false);
  let scrollEl: HTMLDivElement | null = $state(null);
  let unsubscribe: (() => void) | null = null;

  const levels = ['All', 'Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical'];
  const levelColors: Record<string, string> = {
    Trace: 'var(--metro-text-muted)',
    Debug: 'var(--metro-text-secondary)',
    Information: 'var(--metro-green)',
    Warning: 'var(--metro-yellow)',
    Error: 'var(--metro-red)',
    Critical: 'var(--metro-red)',
  };
  const levelBg: Record<string, string> = {
    Trace: '#111111',
    Debug: '#141414',
    Information: '',
    Warning: '#1A1600',
    Error: '#1A0A0A',
    Critical: '#1A0A0A',
  };

  let filteredLogs = $derived(logs.filter(l => {
    if (filterLevel !== 'All' && l.level !== filterLevel) return false;
    if (filterCategory && !l.category.toLowerCase().includes(filterCategory.toLowerCase())) return false;
    if (filterText && !l.message.toLowerCase().includes(filterText.toLowerCase())) return false;
    return true;
  }));

  function scrollToBottom() {
    if (autoScroll && scrollEl) {
      scrollEl.scrollTop = scrollEl.scrollHeight;
    }
  }

  function handleScroll() {
    if (!scrollEl) return;
    const atBottom = scrollEl.scrollHeight - scrollEl.scrollTop - scrollEl.clientHeight < 40;
    autoScroll = atBottom;
  }

  function truncateCategory(cat: string): string {
    const parts = cat.split('.');
    return parts[parts.length - 1];
  }

  onMount(async () => {
    const existing = await HybridBridge.getRecentLogs(500);
    if (existing.entries) {
      logs = existing.entries;
    }
    await HybridBridge.startLogStreaming();

    unsubscribe = HybridBridge.onMessage((event) => {
      if (event.event === 'logEntry') {
        const entry = event as LogEntryEvent;
        logs = [...logs.slice(-499), entry];
        requestAnimationFrame(scrollToBottom);
      }
    });

    requestAnimationFrame(scrollToBottom);
  });

  onDestroy(async () => {
    unsubscribe?.();
    await HybridBridge.stopLogStreaming();
  });

  async function clearLogs() {
    await HybridBridge.clearLogs();
    logs = [];
  }

  function copyAllLogs() {
    const text = filteredLogs.map(l => {
      let line = `${l.timestamp} [${l.level}] ${truncateCategory(l.category)}: ${l.message}`;
      if (l.exception) line += `\n${l.exception}`;
      return line;
    }).join('\n');
    navigator.clipboard.writeText(text).catch(() => {});
  }
</script>

<div class="flex h-full flex-col" style="background-color: var(--metro-bg);">
  <!-- Header -->
  <div class="flex shrink-0 items-center justify-between px-4 py-3" style="    border-bottom: 1px solid var(--metro-border);">
    <div class="flex items-center gap-3">
          <div class="flex items-center gap-3" style="border-left: 4px solid var(--metro-red); padding-left: 12px;">
            <IconMessage size="18" style="color: var(--metro-red);" />
        <h1 class="text-[16px] font-extrabold uppercase tracking-wider text-white" style="line-height: 1.2;">Logs</h1>
      </div>
      <span class="text-white font-bold uppercase" style="background-color: var(--metro-border); font-size: 9px; letter-spacing: 0.5px; padding: 2px 8px; line-height: 1; color: var(--metro-text-secondary);">
        {filteredLogs.length} entries
      </span>
    </div>
    <div class="flex items-center gap-1.5">
      <button
        class="metro-hover-bg flex items-center gap-1.5 px-3 py-1.5 text-[11px] font-bold uppercase tracking-wider transition-colors duration-150"
        style="background-color: {paused ? 'var(--metro-orange)' : 'var(--metro-border)'}; color: {paused ? 'var(--metro-text-on-accent)' : 'var(--metro-text-secondary)'};"
        onclick={() => { paused = !paused; if (!paused) requestAnimationFrame(scrollToBottom); }}
      >
        {#if paused}
          <IconPlayerPlay size="12" />
          Paused
        {:else}
          <IconPlayerPause size="12" />
          Live
        {/if}
      </button>
      <button
        class="metro-hover-bg flex items-center gap-1.5 px-3 py-1.5 text-[11px] font-bold uppercase tracking-wider transition-colors duration-150"
        style="background-color: var(--metro-border); color: var(--metro-text-secondary);"
        onclick={copyAllLogs}
      >
        <IconCopy size="12" />
        Copy All
      </button>
      <button
        class="metro-hover-bg flex items-center gap-1.5 px-3 py-1.5 text-[11px] font-bold uppercase tracking-wider transition-colors duration-150"
        style="background-color: var(--metro-border); color: var(--metro-text-secondary);"
        onclick={clearLogs}
      >
        <IconTrash size="12" />
        Clear
      </button>
    </div>
  </div>

  <!-- Filters -->
  <div class="flex shrink-0 flex-wrap items-center gap-2 px-4 py-2" style="background-color: #141414; border-bottom: 1px solid var(--metro-border-subtle);">
    <select
      class="text-white px-2 py-1.5 text-[11px] focus:outline-none appearance-none transition-colors duration-150"
      style="background-color: var(--metro-card); border: 1px solid var(--metro-border); border-radius: 2px;"
      bind:value={filterLevel}
      onfocus={(e) => { e.currentTarget.style.borderColor = 'var(--metro-blue)'; }}
      onblur={(e) => { e.currentTarget.style.borderColor = 'var(--metro-border)'; }}
    >
      {#each levels as level}
        <option value={level}>{level}</option>
      {/each}
    </select>
    <input
      type="text"
      placeholder="Category..."
      class="w-36 text-white px-2 py-1.5 text-[11px] focus:outline-none transition-colors duration-150"
      style="background-color: var(--metro-card); border: 1px solid var(--metro-border); border-radius: 2px;"
      bind:value={filterCategory}
      onfocus={(e) => { e.currentTarget.style.borderColor = 'var(--metro-blue)'; }}
      onblur={(e) => { e.currentTarget.style.borderColor = 'var(--metro-border)'; }}
    />
    <input
      type="text"
      placeholder="Search message..."
      class="w-48 text-white px-2 py-1.5 text-[11px] focus:outline-none transition-colors duration-150"
      style="background-color: var(--metro-card); border: 1px solid var(--metro-border); border-radius: 2px;"
      bind:value={filterText}
      onfocus={(e) => { e.currentTarget.style.borderColor = 'var(--metro-blue)'; }}
      onblur={(e) => { e.currentTarget.style.borderColor = 'var(--metro-border)'; }}
    />
  </div>

  <!-- Log entries -->
  <div
    bind:this={scrollEl}
    onscroll={handleScroll}
    class="flex-1 overflow-y-auto font-mono text-[11px]"
    style="line-height: 1.6;"
  >
    {#if filteredLogs.length === 0}
      <div class="flex h-full items-center justify-center" style="color: var(--metro-text-muted);">
        No log entries{filterLevel !== 'All' || filterCategory || filterText ? ' matching filters' : ''}.
      </div>
    {:else}
      {#each filteredLogs as log, i (i)}
        {@const bgColor = levelBg[log.level] ?? ''}
        <div
          class="flex gap-2 px-4 py-0.5"
          style="border-bottom: 1px solid var(--metro-border-subtle); {bgColor ? `background-color: ${bgColor};` : ''}"
        >
          <span class="shrink-0" style="color: #555;">{log.timestamp}</span>
          <span class="shrink-0 w-16 text-right font-bold" style="color: {levelColors[log.level] ?? 'var(--metro-text-secondary)'};">
            {log.level.substring(0, 4)}
          </span>
          <span class="shrink-0 w-28 truncate" style="color: var(--metro-text-muted);" title={log.category}>
            {truncateCategory(log.category)}
          </span>
          <span class="flex-1 break-all" style="color: #CCC;">{log.message}</span>
        </div>
        {#if log.exception}
          <div class="px-4 py-1 pl-[10.5rem]" style="border-bottom: 1px solid var(--metro-border-subtle); background-color: #1A0A0A;">
            <pre class="whitespace-pre-wrap break-all text-[10px]" style="color: var(--metro-red); opacity: 0.8;">{log.exception}</pre>
          </div>
        {/if}
      {/each}
    {/if}
  </div>
</div>

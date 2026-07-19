<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { HybridBridge, type LogEntryEvent } from '../lib/HybridBridge';
  import {
    IconMessage, IconCopy, IconTrash, IconPlayerPlay, IconPlayerPause,
    IconBookmark, IconBookmarkFilled, IconX, IconFilter, IconFileExport,
    IconChevronUp, IconChevronDown, IconUpload, IconSparkles,
  } from '@tabler/icons-svelte';
  import { toast } from '../lib/toasts.svelte';
  import {
    type LogEntry, type LogMarker, type LevelStats, type ChannelStats,
    type SearchMode, LEVEL_COLORS, LEVEL_BG, ALL_LEVELS,
    computeLevelStats, computeChannelStats, truncateCategory, formatTimestamp,
  } from '../lib/LogViewerTypes';
  import { parseCsvLog } from '../lib/logImporter';
  import {
    detectEvents, type DetectedEvent, type EventType,
    EVENT_ICONS, EVENT_COLORS,
  } from '../lib/logEventDetector';

  let { onNavigate }: { onNavigate: (page: string) => void } = $props();

  // ── State ──────────────────────────────────────────────────────────────
  let logs = $state<LogEntry[]>([]);
  let filterLevel = $state('All');
  let filterCategory = $state('');
  let filterText = $state('');
  let searchMode = $state<SearchMode>('text');
  let autoScroll = $state(true);
  let paused = $state(false);
  let scrollEl: HTMLDivElement | null = $state(null);
  let unsubscribe: (() => void) | null = null;
  let mounted = false;
  let streamingStarted = false;

  // Markers
  let markerA = $state<LogMarker | null>(null);
  let markerB = $state<LogMarker | null>(null);
  let nextMarkerId = $state<'A' | 'B'>('A');

  // Stats panel toggle
  let statsOpen = $state(true);

  // Event detection
  let detectedEvents = $state<DetectedEvent[]>([]);
  let filterEventType = $state<EventType | 'All'>('All');
  let importSummary = $state<{ total: number; skipped: number } | null>(null);
  let fileInputEl: HTMLInputElement | null = null;

  // ── Derived ────────────────────────────────────────────────────────────
  let filteredLogs = $derived.by(() => {
    let result = logs.map((l, i) => ({ ...l, originalIndex: i }));
    if (filterLevel !== 'All') {
      result = result.filter(l => l.level === filterLevel);
    }
    if (filterCategory) {
      const catLower = filterCategory.toLowerCase();
      result = result.filter(l => l.category.toLowerCase().includes(catLower));
    }
    if (filterText) {
      if (searchMode === 'regex') {
        try {
          const re = new RegExp(filterText, 'i');
          result = result.filter(l => re.test(l.message));
        } catch {
          result = [];
        }
      } else {
        const textLower = filterText.toLowerCase();
        result = result.filter(l => l.message.toLowerCase().includes(textLower));
      }
    }
    if (filterEventType !== 'All') {
      const eventIndices = new Set(
        detectedEvents.filter(e => e.type === filterEventType).map(e => e.entryIndex)
      );
      result = result.filter(l => eventIndices.has(l.originalIndex!));
    }
    return result;
  });

  let levelStats = $derived(computeLevelStats(logs));

  let rangeEntries = $derived.by(() => {
    if (markerA && markerB) {
      const start = Math.min(markerA.entryIndex, markerB.entryIndex);
      const end = Math.max(markerA.entryIndex, markerB.entryIndex);
      return filteredLogs.slice(start, end + 1);
    }
    return filteredLogs;
  });

  let channelStats = $derived(computeChannelStats(rangeEntries));

  // Diff comparison: full log stats vs range stats
  let fullLogStats = $derived(computeChannelStats(filteredLogs));
  let diffView = $state(false);
  let diffChannelStats = $derived.by(() => {
    if (!fullLogStats || !channelStats || !markerA || !markerB) return null;
    return {
      avg: channelStats.average - fullLogStats.average,
      min: channelStats.min - fullLogStats.min,
      max: channelStats.max - fullLogStats.max,
      rangeCount: rangeEntries.length,
      totalCount: filteredLogs.length,
      rangePct: filteredLogs.length > 0 ? (rangeEntries.length / filteredLogs.length * 100) : 0,
    };
  });

  // Events for filtered logs — build a Set of indices with events
  let eventIndexSet = $derived(new Set(detectedEvents.map(e => e.entryIndex)));

  // Event type counts
  let eventTypeCounts = $derived.by(() => {
    const counts: Record<string, number> = {};
    for (const ev of detectedEvents) {
      counts[ev.type] = (counts[ev.type] || 0) + 1;
    }
    return counts;
  });

  function getEventsAtIndex(index: number): DetectedEvent[] {
    return detectedEvents.filter(e => e.entryIndex === index);
  }

  // ── Scroll ─────────────────────────────────────────────────────────────
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

  // ── Markers ────────────────────────────────────────────────────────────
  function placeMarker(logIndex: number) {
    const entry = filteredLogs[logIndex];
    if (!entry) return;
    const marker: LogMarker = {
      id: nextMarkerId,
      entryIndex: logIndex,
      timestamp: entry.timestamp,
    };
    if (nextMarkerId === 'A') {
      markerA = marker;
      nextMarkerId = 'B';
    } else {
      markerB = marker;
      nextMarkerId = 'A';
    }
  }

  function removeMarker(id: 'A' | 'B') {
    if (id === 'A') markerA = null;
    else markerB = null;
    nextMarkerId = id;
  }

  function isMarkerLine(logIndex: number): 'A' | 'B' | null {
    if (markerA && markerA.entryIndex === logIndex) return 'A';
    if (markerB && markerB.entryIndex === logIndex) return 'B';
    return null;
  }

  function inMarkerRange(logIndex: number): boolean {
    if (!markerA || !markerB) return false;
    const lo = Math.min(markerA.entryIndex, markerB.entryIndex);
    const hi = Math.max(markerA.entryIndex, markerB.entryIndex);
    return logIndex >= lo && logIndex <= hi;
  }

  // ── Actions ────────────────────────────────────────────────────────────
  function scrollToMarker(id: 'A' | 'B') {
    const marker = id === 'A' ? markerA : markerB;
    if (!marker || !scrollEl) return;
    const rowHeight = 24;
    scrollEl.scrollTop = marker.entryIndex * rowHeight;
  }

  async function clearLogs() {
    logs = [];
    markerA = null;
    markerB = null;
    nextMarkerId = 'A';
    detectedEvents = [];
    importSummary = null;
    await HybridBridge.clearLogs();
  }

  function copyAllLogs() {
    const text = filteredLogs.map(l => {
      let line = `${l.timestamp} [${l.level}] ${truncateCategory(l.category)}: ${l.message}`;
      if (l.exception) line += `\n${l.exception}`;
      return line;
    }).join('\n');
    navigator.clipboard.writeText(text).catch(() => {});
    toast('Copied to clipboard', 'success');
  }

  async function exportLogs() {
    const lines = ['timestamp,level,category,message,exception'];
    for (const l of filteredLogs) {
      const escape = (s: string) => `"${(s ?? '').replace(/"/g, '""')}"`;
      lines.push([
        escape(l.timestamp), escape(l.level), escape(l.category),
        escape(l.message), escape(l.exception ?? ''),
      ].join(','));
    }
    const csv = lines.join('\n');
    const filename = `logs-${new Date().toISOString().slice(0, 19).replace(/:/g, '-')}`;
    const result = await HybridBridge.saveFile(filename, csv, '.csv');
    if (result.success) {
      toast(`Exported ${filteredLogs.length} entries`, 'success');
    } else if (result.error !== 'Save cancelled') {
      toast(`Export failed: ${result.error}`, 'error');
    }
  }

  // ── CSV Import ────────────────────────────────────────────────────────
  function triggerImport() {
    fileInputEl?.click();
  }

  function handleFileSelect(e: Event) {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    input.value = '';

    const reader = new FileReader();
    reader.onload = () => {
      const content = reader.result as string;
      const result = parseCsvLog(content);
      importSummary = { total: result.totalRows, skipped: result.skippedCount };
      logs = [...logs, ...result.entries];
      detectAndUpdateEvents();
      toast(
        `Imported ${result.entries.length} entries` +
        (result.skippedCount > 0 ? ` (${result.skippedCount} skipped)` : ''),
        result.skippedCount > 0 ? 'warning' : 'success'
      );
      requestAnimationFrame(scrollToBottom);
    };
    reader.readAsText(file);
  }

  function detectAndUpdateEvents() {
    detectedEvents = detectEvents(logs);
  }

  // ── Lifecycle ──────────────────────────────────────────────────────────
  onMount(async () => {
    mounted = true;
    try {
      const existing = await HybridBridge.getRecentLogs(500);
      if (!mounted) return;
      if (existing.entries) {
        logs = existing.entries.map(e => ({ ...e, source: 'live' as const }));
      }
      if (!mounted) return;
      await HybridBridge.startLogStreaming();
      if (!mounted) {
        HybridBridge.stopLogStreaming().catch(() => {});
        return;
      }
      streamingStarted = true;
    } catch (err) {
      if (!mounted) return;
      console.warn('Failed to start log streaming:', err);
      return;
    }

    unsubscribe = HybridBridge.onMessage((event) => {
      if (!mounted || paused) return;
      if (event.event === 'logEntry') {
        const entry = event as LogEntryEvent;
        const logEntry: LogEntry = { ...entry, source: 'live' };
        logs = [...logs.slice(-499), logEntry];
        requestAnimationFrame(scrollToBottom);
      }
    });

    requestAnimationFrame(scrollToBottom);
  });

  onDestroy(() => {
    mounted = false;
    if (unsubscribe) {
      unsubscribe();
      unsubscribe = null;
    }
    if (streamingStarted) {
      HybridBridge.stopLogStreaming().catch(() => {});
      streamingStarted = false;
    }
  });
</script>

<div class="flex h-full flex-col" style="background-color: var(--metro-bg);">
  <!-- Header -->
  <div class="flex shrink-0 flex-col gap-2 px-4 py-3 sm:flex-row sm:items-center sm:justify-between sm:gap-3" style="border-bottom: 1px solid var(--metro-border);">
    <div class="flex flex-wrap items-center gap-3">
      <div class="flex items-center gap-3" style="border-left: 4px solid var(--metro-red); padding-left: 12px;">
        <IconMessage size="18" style="color: var(--metro-red);" />
        <h1 class="text-[16px] font-extrabold uppercase tracking-wider text-white" style="line-height: 1.2;">Logs</h1>
      </div>
      <span class="text-white font-bold uppercase" style="background-color: var(--metro-border); font-size: 9px; letter-spacing: 0.5px; padding: 2px 8px; line-height: 1; color: var(--metro-text-secondary);">
        {filteredLogs.length} entries
      </span>
    </div>
    <div class="flex flex-wrap items-center gap-1.5">
      <input
        type="file"
        accept=".csv"
        class="hidden"
        bind:this={fileInputEl}
        onchange={handleFileSelect}
      />
      <button
        class="metro-hover-bg flex items-center gap-1.5 px-3 py-1.5 text-[11px] font-bold uppercase tracking-wider transition-colors duration-150"
        style="background-color: var(--metro-border); color: var(--metro-text-secondary);"
        onclick={triggerImport}
        title="Import CSV log file"
      >
        <IconUpload size="12" />
        Import
      </button>
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
        onclick={exportLogs}
        title="Export logs as CSV"
      >
        <IconFileExport size="12" />
        Export
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

  <!-- Level statistics bar -->
  <div class="flex shrink-0 flex-wrap items-center gap-1.5 px-4 py-2" style="background-color: #111; border-bottom: 1px solid var(--metro-border-subtle);">
    <span class="mr-1 text-[9px] font-bold uppercase tracking-wider" style="color: var(--metro-text-muted);">Levels</span>
    {#each levelStats as stat}
      <button
        class="flex items-center gap-1 rounded px-2 py-0.5 text-[10px] font-bold transition-all duration-150"
        style="background-color: {filterLevel === stat.level ? stat.color : 'var(--metro-card)'}; color: {filterLevel === stat.level ? '#000' : stat.color}; border: 1px solid {filterLevel === stat.level ? stat.color : 'var(--metro-border)'}; opacity: {stat.count === 0 ? 0.4 : 1};"
        onclick={() => { filterLevel = filterLevel === stat.level ? 'All' : stat.level; }}
      >
        {stat.level.substring(0, 4)}
        <span class="text-[9px]" style="opacity: 0.8;">{stat.count}</span>
      </button>
    {/each}
    <div class="ml-2 h-4 w-px" style="background-color: var(--metro-border);"></div>
    <button
      class="flex items-center gap-1 rounded px-2 py-0.5 text-[10px] font-bold transition-all duration-150"
      style="background-color: {filterLevel === 'All' ? 'var(--metro-blue)' : 'var(--metro-card)'}; color: {filterLevel === 'All' ? '#fff' : 'var(--metro-text-secondary)'}; border: 1px solid {filterLevel === 'All' ? 'var(--metro-blue)' : 'var(--metro-border)'};"
      onclick={() => { filterLevel = 'All'; }}
    >
      All
    </button>
  </div>

  <!-- Import summary -->
  {#if importSummary}
    <div class="flex shrink-0 items-center gap-2 px-4 py-1.5" style="background-color: #111; border-bottom: 1px solid var(--metro-border-subtle);">
      <span class="text-[10px] font-bold" style="color: var(--metro-text-secondary);">
        Import: {importSummary.total} rows
      </span>
      {#if importSummary.skipped > 0}
        <span class="text-[10px] font-bold" style="color: var(--metro-orange);">
          ({importSummary.skipped} skipped)
        </span>
      {/if}
      <button
        class="ml-auto text-[10px] font-bold uppercase transition-colors duration-150"
        style="color: var(--metro-text-muted);"
        onclick={() => { importSummary = null; }}
      >
        dismiss
      </button>
    </div>
  {/if}

  <!-- Event filter chips -->
  {#if detectedEvents.length > 0}
    <div class="flex shrink-0 flex-wrap items-center gap-1.5 px-4 py-1.5" style="background-color: #111; border-bottom: 1px solid var(--metro-border-subtle);">
      <IconSparkles size="11" style="color: var(--metro-text-muted);" />
      <span class="mr-1 text-[9px] font-bold uppercase tracking-wider" style="color: var(--metro-text-muted);">Events</span>
      {#each (['spike', 'flatline', 'dropout', 'abnormal'] as EventType[]) as eventType}
        {@const count = eventTypeCounts[eventType] ?? 0}
        <button
          class="flex items-center gap-1 rounded px-2 py-0.5 text-[10px] font-bold transition-all duration-150"
          style="background-color: {filterEventType === eventType ? EVENT_COLORS[eventType] : 'var(--metro-card)'}; color: {filterEventType === eventType ? '#fff' : EVENT_COLORS[eventType]}; border: 1px solid {filterEventType === eventType ? EVENT_COLORS[eventType] : 'var(--metro-border)'}; opacity: {count === 0 ? 0.4 : 1};"
          onclick={() => { filterEventType = filterEventType === eventType ? 'All' : eventType; }}
        >
          {EVENT_ICONS[eventType]} {eventType}
          <span class="text-[9px]" style="opacity: 0.8;">{count}</span>
        </button>
      {/each}
      <button
        class="flex items-center gap-1 rounded px-2 py-0.5 text-[10px] font-bold transition-all duration-150"
        style="background-color: {filterEventType === 'All' ? 'var(--metro-blue)' : 'var(--metro-card)'}; color: {filterEventType === 'All' ? '#fff' : 'var(--metro-text-secondary)'}; border: 1px solid {filterEventType === 'All' ? 'var(--metro-blue)' : 'var(--metro-border)'};"
        onclick={() => { filterEventType = 'All'; }}
      >
        All
      </button>
    </div>
  {/if}

  <!-- Markers & Stats panel -->
  {#if markerA || markerB}
    <div class="flex shrink-0 flex-wrap items-center gap-2 px-4 py-2" style="background-color: #0D0D0D; border-bottom: 1px solid var(--metro-border-subtle);">
      <span class="text-[9px] font-bold uppercase tracking-wider" style="color: var(--metro-text-muted);">Markers</span>
      {#if markerA}
        <span class="flex items-center gap-1 rounded px-2 py-0.5 text-[10px] font-bold" style="background-color: rgba(34,139,234,0.15); color: #228BEA; border: 1px solid rgba(34,139,234,0.3);">
          <IconBookmarkFilled size="10" />
          A: {formatTimestamp(markerA.timestamp)}
          <button class="ml-0.5 rounded p-0.5 transition-colors hover:bg-white/10" onclick={() => removeMarker('A')}>
            <IconX size="10" />
          </button>
        </span>
      {:else}
        <span class="text-[10px]" style="color: var(--metro-text-muted);">A: —</span>
      {/if}
      {#if markerB}
        <span class="flex items-center gap-1 rounded px-2 py-0.5 text-[10px] font-bold" style="background-color: rgba(255,107,53,0.15); color: #FF6B35; border: 1px solid rgba(255,107,53,0.3);">
          <IconBookmarkFilled size="10" />
          B: {formatTimestamp(markerB.timestamp)}
          <button class="ml-0.5 rounded p-0.5 transition-colors hover:bg-white/10" onclick={() => removeMarker('B')}>
            <IconX size="10" />
          </button>
        </span>
      {:else}
        <span class="text-[10px]" style="color: var(--metro-text-muted);">B: —</span>
      {/if}
      {#if markerA && markerB}
        <button
          class="flex items-center gap-1 rounded px-2 py-0.5 text-[10px] font-bold transition-colors hover:bg-white/5"
          style="color: var(--metro-text-secondary); border: 1px solid var(--metro-border);"
          onclick={() => { statsOpen = !statsOpen; }}
        >
          {#if statsOpen}
            <IconChevronUp size="10" />
          {:else}
            <IconChevronDown size="10" />
          {/if}
          Stats
        </button>
        <button
          class="flex items-center gap-1 rounded px-2 py-0.5 text-[10px] font-bold transition-colors hover:bg-white/5"
          style="color: {diffView ? 'var(--metro-cyan)' : 'var(--metro-text-secondary)'}; border: 1px solid {diffView ? 'var(--metro-cyan)' : 'var(--metro-border)'};"
          onclick={() => { diffView = !diffView; statsOpen = diffView ? false : statsOpen; }}
        >
          Diff
        </button>
        <button
          class="flex items-center gap-1 rounded px-2 py-0.5 text-[10px] font-bold transition-colors hover:bg-white/5"
          style="color: var(--metro-text-secondary); border: 1px solid var(--metro-border);"
          onclick={() => { scrollToMarker('A'); }}
        >
          Go A
        </button>
        <button
          class="flex items-center gap-1 rounded px-2 py-0.5 text-[10px] font-bold transition-colors hover:bg-white/5"
          style="color: var(--metro-text-secondary); border: 1px solid var(--metro-border);"
          onclick={() => { scrollToMarker('B'); }}
        >
          Go B
        </button>
      {/if}
    </div>
  {/if}

  <!-- Statistics panel -->
  {#if statsOpen && channelStats && markerA && markerB}
    <div class="flex shrink-0 flex-wrap items-center gap-3 px-4 py-2" style="background-color: #0A0A0A; border-bottom: 1px solid var(--metro-border-subtle);">
      <span class="text-[9px] font-bold uppercase tracking-wider" style="color: var(--metro-text-muted);">Range Stats</span>
      <div class="flex items-center gap-1 text-[10px]">
        <span style="color: var(--metro-text-muted);">Min:</span>
        <span class="font-bold" style="color: var(--metro-green);">{channelStats.min.toFixed(2)}</span>
      </div>
      <div class="flex items-center gap-1 text-[10px]">
        <span style="color: var(--metro-text-muted);">Max:</span>
        <span class="font-bold" style="color: var(--metro-red);">{channelStats.max.toFixed(2)}</span>
      </div>
      <div class="flex items-center gap-1 text-[10px]">
        <span style="color: var(--metro-text-muted);">Avg:</span>
        <span class="font-bold" style="color: var(--metro-yellow);">{channelStats.average.toFixed(2)}</span>
      </div>
      <div class="flex items-center gap-1 text-[10px]">
        <span style="color: var(--metro-text-muted);">Delta:</span>
        <span class="font-bold" style="color: {channelStats.delta >= 0 ? 'var(--metro-green)' : 'var(--metro-red)'};">{channelStats.delta >= 0 ? '+' : ''}{channelStats.delta.toFixed(2)}</span>
      </div>
      <div class="flex items-center gap-1 text-[10px]">
        <span style="color: var(--metro-text-muted);">Rate:</span>
        <span class="font-bold" style="color: var(--metro-blue);">{channelStats.rateOfChange.toFixed(2)}/s</span>
      </div>
      <div class="flex items-center gap-1 text-[10px]">
        <span style="color: var(--metro-text-muted);">Entries:</span>
        <span class="font-bold" style="color: var(--metro-text-secondary);">{rangeEntries.length}</span>
      </div>
    </div>
  {/if}

  <!-- Diff comparison panel -->
  {#if diffView && diffChannelStats && markerA && markerB}
    <div class="flex shrink-0 flex-wrap items-center gap-3 px-4 py-2" style="background-color: #0A0D12; border-bottom: 1px solid var(--metro-border-subtle);">
      <span class="text-[9px] font-bold uppercase tracking-wider" style="color: var(--metro-cyan);">Diff</span>
      <div class="flex items-center gap-1 text-[10px]">
        <span style="color: var(--metro-text-muted);">Range:</span>
        <span class="font-bold" style="color: var(--metro-text-secondary);">{diffChannelStats.rangeCount} entries ({diffChannelStats.rangePct.toFixed(1)}%)</span>
      </div>
      <div class="flex items-center gap-1 text-[10px]">
        <span style="color: var(--metro-text-muted);">Avg Δ:</span>
        <span class="font-bold" style="color: {diffChannelStats.avg >= 0 ? 'var(--metro-green)' : 'var(--metro-red)'};">{diffChannelStats.avg >= 0 ? '+' : ''}{diffChannelStats.avg.toFixed(2)}</span>
      </div>
      <div class="flex items-center gap-1 text-[10px]">
        <span style="color: var(--metro-text-muted);">Min Δ:</span>
        <span class="font-bold" style="color: {diffChannelStats.min >= 0 ? 'var(--metro-green)' : 'var(--metro-red)'};">{diffChannelStats.min >= 0 ? '+' : ''}{diffChannelStats.min.toFixed(2)}</span>
      </div>
      <div class="flex items-center gap-1 text-[10px]">
        <span style="color: var(--metro-text-muted);">Max Δ:</span>
        <span class="font-bold" style="color: {diffChannelStats.max >= 0 ? 'var(--metro-green)' : 'var(--metro-red)'};">{diffChannelStats.max >= 0 ? '+' : ''}{diffChannelStats.max.toFixed(2)}</span>
      </div>
      <span class="text-[9px]" style="color: var(--metro-text-muted);">vs full log ({filteredLogs.length} entries)</span>
    </div>
  {/if}

  <!-- Filters -->
  <div class="flex shrink-0 flex-wrap items-center gap-2 px-4 py-2" style="background-color: #141414; border-bottom: 1px solid var(--metro-border-subtle);">
    <select
      class="text-white px-2 py-1.5 text-[11px] focus:outline-none appearance-none transition-colors duration-150"
      style="background-color: var(--metro-card); border: 1px solid var(--metro-border); border-radius: 2px;"
      bind:value={filterLevel}
      onfocus={(e) => { e.currentTarget.style.borderColor = 'var(--metro-blue)'; }}
      onblur={(e) => { e.currentTarget.style.borderColor = 'var(--metro-border)'; }}
    >
      <option value="All">All Levels</option>
      {#each ALL_LEVELS as level}
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
    <div class="flex items-center" style="border: 1px solid var(--metro-border); border-radius: 2px; background-color: var(--metro-card);">
      <input
        type="text"
        placeholder={searchMode === 'regex' ? 'Regex pattern...' : 'Search message...'}
        class="w-48 text-white px-2 py-1.5 text-[11px] focus:outline-none transition-colors duration-150"
        style="background-color: transparent; border: none;"
        bind:value={filterText}
        onfocus={(e) => { e.currentTarget.parentElement!.style.borderColor = 'var(--metro-blue)'; }}
        onblur={(e) => { e.currentTarget.parentElement!.style.borderColor = 'var(--metro-border)'; }}
      />
      <button
        class="flex items-center gap-0.5 px-1.5 py-1 text-[10px] font-bold uppercase transition-colors duration-150"
        style="color: {searchMode === 'regex' ? 'var(--metro-blue)' : 'var(--metro-text-muted)'}; border-left: 1px solid var(--metro-border);"
        onclick={() => { searchMode = searchMode === 'text' ? 'regex' : 'text'; }}
        title={searchMode === 'regex' ? 'Switch to text search' : 'Switch to regex search'}
      >
        <IconFilter size="11" />
        {searchMode === 'regex' ? 'REGEX' : 'TEXT'}
      </button>
    </div>
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
        {@const bgColor = LEVEL_BG[log.level] ?? ''}
        {@const markerId = isMarkerLine(i)}
        {@const inRange = inMarkerRange(i)}
        <div
          class="flex gap-2 px-4 py-0.5 relative"
          style="border-bottom: 1px solid var(--metro-border-subtle); {bgColor ? `background-color: ${bgColor};` : ''} {inRange ? 'background-color: rgba(34,139,234,0.05);' : ''}"
        >
          <!-- Marker indicator line -->
          {#if markerId}
            <div
              class="absolute left-0 top-0 bottom-0 w-0.5"
              style="background-color: {markerId === 'A' ? '#228BEA' : '#FF6B35'};"
            ></div>
            <span
              class="absolute left-1 top-0.5 text-[8px] font-bold"
              style="color: {markerId === 'A' ? '#228BEA' : '#FF6B35'};"
            >{markerId}</span>
          {/if}

          <!-- Marker placement button -->
          <button
            class="shrink-0 flex items-center justify-center w-4 h-4 mt-0.5 rounded transition-colors duration-100"
            style="color: {markerId ? (markerId === 'A' ? '#228BEA' : '#FF6B35') : 'var(--metro-text-muted)'}; opacity: 0.4;"
            onclick={() => placeMarker(i)}
            title="Place marker {nextMarkerId}"
            onmouseenter={(e) => { e.currentTarget.style.opacity = '1'; }}
            onmouseleave={(e) => { e.currentTarget.style.opacity = '0.4'; }}
          >
            {#if markerId}
              <IconBookmarkFilled size="10" />
            {:else}
              <IconBookmark size="10" />
            {/if}
          </button>

          <span class="shrink-0 tabular-nums" style="color: #555; min-width: 88px;">{formatTimestamp(log.timestamp)}</span>
          <span class="shrink-0 w-12 text-right font-bold" style="color: {LEVEL_COLORS[log.level] ?? 'var(--metro-text-secondary)'};">
            {log.level.substring(0, 4)}
          </span>
          <span class="shrink-0 w-20 truncate" style="color: var(--metro-text-muted);" title={log.category}>
            {truncateCategory(log.category)}
          </span>
          <span class="flex-1 break-all" style="color: #CCC;">{log.message}</span>
          {#if eventIndexSet.has(log.originalIndex!) && filterEventType === 'All'}
            {@const evts = getEventsAtIndex(log.originalIndex!)}
            <span class="flex shrink-0 items-center gap-0.5" title={evts.map(e => e.description).join(' | ')}>
              {#each evts as ev}
                <span
                  class="inline-flex items-center justify-center rounded px-1 text-[9px] font-bold leading-none"
                  style="background-color: {EVENT_COLORS[ev.type]}; color: #fff;"
                  title={ev.description}
                >{EVENT_ICONS[ev.type]}</span>
              {/each}
            </span>
          {/if}
        </div>
        {#if log.exception}
          <div class="px-4 py-1 pl-[16rem] sm:pl-[12rem]" style="border-bottom: 1px solid var(--metro-border-subtle); background-color: #1A0A0A;">
            <pre class="whitespace-pre-wrap break-all text-[10px]" style="color: var(--metro-red); opacity: 0.8;">{log.exception}</pre>
          </div>
        {/if}
      {/each}
    {/if}
  </div>
</div>

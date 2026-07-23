<script lang="ts">
  import StreamingLineChart from '../lib/echarts/StreamingLineChart.svelte';
  import SensorPicker from '../lib/monitor/PresetSensorPicker.svelte';
  import EcuInfoPanel from '../lib/monitor/EcuInfoPanel.svelte';
  import { SessionRecorder, type RecordingState } from '../lib/monitor/SessionRecorder';
  import { SessionStore, type RecordedSession, type FreezeFrame } from '../lib/monitor/SessionStore';
  import { computeRangeStats, computeRangeStatsBetween, type RangeStats } from '../lib/monitor/StatsComputer';
  import { detectSensorEvents, SENSOR_EVENT_COLORS, SENSOR_EVENT_ICONS, type SensorEvent, type SensorEventType } from '../lib/monitor/SensorEventDetector';
  import { HybridBridge } from '../lib/HybridBridge';
  import { liveDataStore } from '../lib/stores/LiveDataStore.svelte';
  import type { DataLinkDefinition } from '../lib/HybridBridgeTypes';
  import { getSensorColor } from '../lib/monitor/sensorColors';
  import {
    IconChartLine, IconAdjustments, IconCircleDotted,
    IconTrash, IconBookmark, IconBookmarkFilled, IconX,
    IconFileExport, IconFileImport, IconPlayerPlay, IconPlayerStop, IconStack2,
  } from '@tabler/icons-svelte';

  let {
    onNavigate,
    connectionState,
  }: {
    onNavigate: (page: string) => void;
    connectionState: { state: string };
  } = $props();

  // ── State ──────────────────────────────────────────────────────────────
  let allDataLinks = $state<DataLinkDefinition[]>([]);
  let selectedIds = $state<Set<number>>(new Set());
  let timeWindowSec = $state(10);
  let mobileTab: 'chart' | 'sensors' | 'info' | 'sessions' = $state('chart');
  let isMobile = $state(false);

  // Session state
  let recorderState = $state<RecordingState>(SessionRecorder.state);
  let sessions = $state<RecordedSession[]>(SessionStore.sessions);
  let activeSession = $state<RecordedSession | null>(null);
  let playbackTimeMs = $state(0);
  let isPlaying = $state(false);
  let playbackSpeed = $state(1);
  let playInterval: ReturnType<typeof setInterval> | null = null;
  let timerTick = $state(0);
  let timerInterval: ReturnType<typeof setInterval> | null = null;

  // Stats overlay
  let showStats = $state(true);
  let liveStats = $state<Map<number, RangeStats>>(new Map());
  const _statsAccum = new Map<number, RangeStats>();

  // A/B markers
  let markerA = $state<number | null>(null);
  let markerB = $state<number | null>(null);
  let rangeStats = $state<RangeStats | null>(null);

  // Events
  let detectedEvents = $state<SensorEvent[]>([]);
  let filterEventType = $state<SensorEventType | 'All'>('All');

  // Sessions panel
  let showSessionsPanel = $state(false);
  let sessionsLoading = $state(false);
  let busyAction = $state<string | null>(null);
  let sessionError = $state<string | null>(null);

  // ── Derived ────────────────────────────────────────────────────────────
  const chartSeries = $derived(
    Array.from(selectedIds).map((id) => {
      const dl = allDataLinks.find((d) => d.id === id);
      return {
        id: String(id),
        name: dl?.name ?? `Sensor ${id}`,
        color: getSensorColor(id),
      };
    }),
  );

  const currentData = $derived(
    activeSession?.data
      ? new Map(Object.entries(activeSession.data).map(([k, v]) => [k, v]))
      : undefined,
  );

  // ── Effects ────────────────────────────────────────────────────────────
  $effect(() => {
    const mq = window.matchMedia('(max-width: 767px)');
    isMobile = mq.matches;
    const handler = (e: MediaQueryListEvent) => { isMobile = e.matches; };
    mq.addEventListener('change', handler);
    return () => mq.removeEventListener('change', handler);
  });

  // Persist settings
  $effect(() => {
    try {
      const v = localStorage.getItem('monitor_timeWindow');
      if (v) timeWindowSec = Number(v);
    } catch {}
  });
  $effect(() => {
    localStorage.setItem('monitor_timeWindow', String(timeWindowSec));
  });
  $effect(() => {
    try {
      const stored = localStorage.getItem('monitor_selectedIds');
      if (stored) selectedIds = new Set(JSON.parse(stored));
    } catch {}
  });
  $effect(() => {
    localStorage.setItem('monitor_selectedIds', JSON.stringify([...selectedIds]));
  });

  // Load data links
  $effect(() => {
    HybridBridge.getDataLinks().then((result) => {
      allDataLinks = result.dataLinks;
    }).catch(() => {});
  });

  // Initialize session store (sync with C# backend)
  $effect(() => {
    sessionsLoading = true;
    SessionStore.init().then(() => {
      sessions = SessionStore.sessions;
      sessionsLoading = false;
    }).catch(() => { sessionsLoading = false; });
  });

  // Cleanup intervals on unmount
  $effect(() => {
    return () => {
      stopPlayback();
      stopTimer();
    };
  });

  // Compute live stats
  $effect(() => {
    const frameCount = liveDataStore.frameCount;
    for (const id of selectedIds) {
      const val = liveDataStore.values[id];
      if (val != null) {
        const existing = _statsAccum.get(id);
        if (existing) {
          _statsAccum.set(id, {
            min: Math.min(existing.min, val),
            max: Math.max(existing.max, val),
            avg: (existing.avg * existing.count + val) / (existing.count + 1),
            delta: val - existing.min,
            rateOfChange: existing.rateOfChange,
            count: existing.count + 1,
            durationMs: performance.now(),
          });
        } else {
          _statsAccum.set(id, {
            min: val, max: val, avg: val, delta: 0,
            rateOfChange: 0, count: 1, durationMs: 0,
          });
        }
      }
    }
    liveStats = new Map(_statsAccum);
  });

  // Compute range stats when markers change
  $effect(() => {
    if (markerA != null && markerB != null && activeSession?.data) {
      const lo = Math.min(markerA, markerB);
      const hi = Math.max(markerA, markerB);
      let combined: RangeStats | null = null;
      for (const id of selectedIds) {
        const pts = activeSession.data[id];
        if (!pts) continue;
        const stats = computeRangeStatsBetween(pts, lo, hi);
        if (stats) {
          if (!combined) {
            combined = stats;
          } else {
            combined = {
              min: Math.min(combined.min, stats.min),
              max: Math.max(combined.max, stats.max),
              avg: (combined.avg + stats.avg) / 2,
              delta: combined.delta + stats.delta,
              rateOfChange: combined.rateOfChange + stats.rateOfChange,
              count: combined.count + stats.count,
              durationMs: hi - lo,
            };
          }
        }
      }
      rangeStats = combined;
    } else {
      rangeStats = null;
    }
  });

  // Detect events on session data
  $effect(() => {
    if (!activeSession?.data) { detectedEvents = []; return; }
    const events: SensorEvent[] = [];
    for (const id of selectedIds) {
      const pts = activeSession.data[id];
      const name = activeSession.sensorNames[id] ?? `Sensor ${id}`;
      if (pts && pts.length > 1) {
        events.push(...detectSensorEvents(pts, id, name));
      }
    }
    detectedEvents = events.sort((a, b) => a.timeMs - b.timeMs);
  });

  // ── Session Management ─────────────────────────────────────────────────
  function startTimer() {
    stopTimer();
    timerTick++;
    timerInterval = setInterval(() => timerTick++, 100);
  }
  function stopTimer() {
    if (timerInterval) { clearInterval(timerInterval); timerInterval = null; }
  }

  function handleRecord() {
    if (SessionRecorder.state === 'recording') {
      const recorded = SessionRecorder.getRecordedData();
      const session = SessionStore.save(recorded);
      sessions = SessionStore.sessions;
      activeSession = session;
      playbackTimeMs = recorded.durationMs;
      SessionRecorder.reset();
      recorderState = 'stopped';
      stopTimer();
    } else {
      _statsAccum.clear();
      liveStats = new Map();
      markerA = null;
      markerB = null;
      activeSession = null;
      const nameMap = new Map<number, string>();
      for (const dl of allDataLinks) nameMap.set(dl.id, dl.name);
      SessionRecorder.start([...selectedIds], nameMap);
      recorderState = 'recording';
      startTimer();
    }
  }

  async function loadSession(session: RecordedSession) {
    // If session has no data (loaded from summary), fetch full data from C#
    if (Object.keys(session.data).length === 0) {
      const full = await SessionStore.loadFullSession(session.id);
      if (full) {
        session = full;
      } else {
        sessionError = 'Failed to load session data';
        return;
      }
    }
    activeSession = session;
    playbackTimeMs = session.durationMs;
    showSessionsPanel = false;
    isPlaying = false;
    stopPlayback();
  }

  function deleteSession(id: string) {
    SessionStore.remove(id);
    sessions = SessionStore.sessions;
    if (activeSession?.id === id) {
      activeSession = null;
      playbackTimeMs = 0;
    }
  }

  function renameSession(id: string, name: string) {
    SessionStore.rename(id, name);
    sessions = SessionStore.sessions;
    if (activeSession?.id === id) activeSession = { ...activeSession, name };
  }

  // ── Playback ───────────────────────────────────────────────────────────
  function startPlayback() {
    if (!activeSession) return;
    isPlaying = true;
    const durationMs = activeSession.durationMs;
    const stepMs = 50;
    playInterval = setInterval(() => {
      playbackTimeMs += stepMs * playbackSpeed;
      if (playbackTimeMs >= durationMs) {
        playbackTimeMs = durationMs;
        stopPlayback();
      }
    }, stepMs);
  }

  function stopPlayback() {
    isPlaying = false;
    if (playInterval) { clearInterval(playInterval); playInterval = null; }
  }

  function togglePlayback() {
    if (isPlaying) stopPlayback();
    else startPlayback();
  }

  function seekTo(ms: number) {
    playbackTimeMs = Math.max(0, Math.min(ms, activeSession?.durationMs ?? 0));
  }

  // ── Freeze Frame ───────────────────────────────────────────────────────
  function handleFreezeFrame() {
    SessionRecorder.freezeFrame();
  }

  // ── Export ──────────────────────────────────────────────────────────────
  function handleExport(format: 'csv' | 'yaml') {
    const content = format === 'csv' ? SessionRecorder.toCsv() : SessionRecorder.toYaml();
    HybridBridge.saveFile(`session.${format}`, content);
  }

  function handleSessionExport(session: RecordedSession, format: 'csv' | 'yaml') {
    const lines: string[] = [];
    if (format === 'csv') {
      const ids = session.sensorIds;
      const escapeCsv = (s: string) => s.includes(',') || s.includes('"') ? `"${s.replace(/"/g, '""')}"` : s;
      lines.push(['time_ms', ...ids.map(id => escapeCsv(session.sensorNames[id] ?? String(id)))].join(','));
      const maxLen = Math.max(...ids.map(id => session.data[id]?.length ?? 0));
      for (let i = 0; i < maxLen; i++) {
        const row: string[] = [];
        for (const id of ids) {
          const pts = session.data[id] ?? [];
          const s = pts[i];
          row.push(s ? `${s.t.toFixed(1)},${s.v}` : ',');
        }
        lines.push(row.join(','));
      }
    } else {
      lines.push('session:');
      lines.push(`  name: ${session.name}`);
      lines.push(`  start: ${session.startTime}`);
      lines.push(`  duration_ms: ${session.durationMs}`);
      lines.push('  sensors:');
      for (const id of session.sensorIds) {
        const name = session.sensorNames[id] ?? String(id);
        const pts = session.data[id] ?? [];
        lines.push(`    "${name}":`);
        for (const s of pts) {
          lines.push(`      - t: ${s.t.toFixed(1)}`);
          lines.push(`        v: ${s.v}`);
        }
      }
    }
    HybridBridge.saveFile(`${session.name}.${format}`, lines.join('\n'));
  }

  // ── .mes Export/Import ─────────────────────────────────────────────────
  async function handleImportMes() {
    busyAction = 'import';
    sessionError = null;
    try {
      const result = await SessionStore.importFromMes();
      if (result.success) {
        sessions = SessionStore.sessions;
      } else if (result.error) {
        sessionError = result.error;
      }
    } catch (err: any) {
      sessionError = err?.message ?? 'Import failed';
    } finally {
      busyAction = null;
    }
  }

  async function handleExportSessionMes(session: RecordedSession) {
    busyAction = `export-${session.id}`;
    sessionError = null;
    try {
      const result = await SessionStore.exportToMes(session);
      if (!result.success && result.error) {
        sessionError = result.error;
      }
    } catch (err: any) {
      sessionError = err?.message ?? 'Export failed';
    } finally {
      busyAction = null;
    }
  }

  async function handleExportAllSessionsMes() {
    busyAction = 'export-all';
    sessionError = null;
    try {
      const result = await SessionStore.exportAllToMes();
      if (!result.success && result.error) {
        sessionError = result.error;
      }
    } catch (err: any) {
      sessionError = err?.message ?? 'Export failed';
    } finally {
      busyAction = null;
    }
  }

  // Auto-dismiss error after 5 seconds
  $effect(() => {
    if (sessionError) {
      const t = setTimeout(() => { sessionError = null; }, 5000);
      return () => clearTimeout(t);
    }
  });

  // ── Helpers ────────────────────────────────────────────────────────────
  function formatMs(ms: number): string {
    const s = Math.floor(ms / 1000);
    const m = Math.floor(s / 60);
    const h = Math.floor(m / 60);
    return `${String(h).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  }

  function resetLiveStats() {
    _statsAccum.clear();
    liveStats = new Map();
  }
</script>

<div class="flex flex-col h-full">
  <!-- ── Top Bar ──────────────────────────────────────────────────────── -->
  <div class="flex items-center gap-2 px-3 py-1.5 bg-[#1a1a1a] border-b border-[#333] text-[11px]">
    <!-- Connection status -->
    <div class="flex items-center gap-1.5">
      <div class="w-1.5 h-1.5 rounded-full shrink-0
        {connectionState.state === 'Connected' ? 'bg-green-500' :
         connectionState.state === 'Connecting' ? 'bg-amber-500' : 'bg-red-500'}">
      </div>
      <span class="text-gray-400">{connectionState.state}</span>
    </div>
    <div class="text-gray-600">|</div>

    <!-- Recording controls -->
    {#if !activeSession}
      <button
        class="flex items-center gap-1.5 px-2 py-1 rounded text-[11px] font-bold uppercase tracking-wider transition-colors
          {recorderState === 'recording'
            ? 'bg-red-600/20 text-red-400 hover:bg-red-600/30'
            : 'bg-[#2a2a2a] text-gray-400 hover:bg-[#333]'}"
        onclick={handleRecord}
      >
        {#if recorderState === 'recording'}
          <IconPlayerStop size={12} />
          Stop
        {:else}
          <IconPlayerPlay size={12} />
          Record
        {/if}
      </button>
      {#if recorderState === 'recording'}
        <span class="text-red-400 font-mono tabular-nums">{timerTick ? formatMs(SessionRecorder.durationMs) : '00:00:00'}</span>
        <button
          class="flex items-center gap-1 px-2 py-1 text-[10px] text-sky-400 bg-sky-400/10 rounded hover:bg-sky-400/20 transition-colors"
          onclick={handleFreezeFrame}
          title="Freeze-frame: bookmark this moment"
        >
          <IconBookmark size={11} />
        </button>
      {/if}
    {:else}
      <!-- Playback controls -->
      <button
        class="flex items-center gap-1 px-2 py-1 rounded text-[11px] font-bold uppercase tracking-wider bg-[#2a2a2a] text-gray-400 hover:bg-[#333] transition-colors"
        onclick={() => { activeSession = null; playbackTimeMs = 0; stopPlayback(); }}
      >
        <IconX size={12} />
        Live
      </button>
      <button
        class="flex items-center gap-1 px-2 py-1 rounded text-[11px] transition-colors
          {isPlaying ? 'bg-sky-600/20 text-sky-400' : 'bg-[#2a2a2a] text-gray-400 hover:bg-[#333]'}"
        onclick={togglePlayback}
      >
        {#if isPlaying}
          <IconPlayerStop size={12} />
        {:else}
          <IconPlayerPlay size={12} />
        {/if}
      </button>
      <span class="text-gray-400 font-mono tabular-nums text-[10px]">
        {formatMs(playbackTimeMs)} / {formatMs(activeSession.durationMs)}
      </span>
      <input
        type="range" min="0" max={activeSession.durationMs} step="100"
        value={playbackTimeMs}
        oninput={(e) => seekTo(Number((e.target as HTMLInputElement).value))}
        class="w-32 h-1 accent-sky-500"
      />
      <select
        class="bg-[#222] border border-[#444] rounded px-1 py-0.5 text-[10px] text-gray-300"
        bind:value={playbackSpeed}
      >
        <option value={0.5}>0.5x</option>
        <option value={1}>1x</option>
        <option value={2}>2x</option>
        <option value={5}>5x</option>
      </select>
    {/if}

    <div class="flex-1"></div>

    <!-- Stats toggle -->
    <button
      class="px-2 py-1 rounded text-[10px] font-bold uppercase tracking-wider transition-colors
        {showStats ? 'bg-cyan-600/20 text-cyan-400' : 'bg-[#2a2a2a] text-gray-500'}"
      onclick={() => showStats = !showStats}
    >
      Stats
    </button>

    <!-- A/B Markers -->
    {#if activeSession}
      <div class="flex items-center gap-1">
        <button
          class="flex items-center gap-1 px-2 py-1 rounded text-[10px] font-bold transition-colors
            {markerA != null ? 'bg-blue-600/20 text-blue-400' : 'bg-[#2a2a2a] text-gray-500'}"
          onclick={() => { markerA = markerA != null ? null : playbackTimeMs; }}
        >
          {#if markerA != null}
            <IconBookmarkFilled size={10} />
            A
          {:else}
            <IconBookmark size={10} />
            A
          {/if}
        </button>
        <button
          class="flex items-center gap-1 px-2 py-1 rounded text-[10px] font-bold transition-colors
            {markerB != null ? 'bg-orange-600/20 text-orange-400' : 'bg-[#2a2a2a] text-gray-500'}"
          onclick={() => { markerB = markerB != null ? null : playbackTimeMs; }}
        >
          {#if markerB != null}
            <IconBookmarkFilled size={10} />
            B
          {:else}
            <IconBookmark size={10} />
            B
          {/if}
        </button>
      </div>
    {/if}

    <!-- Time window -->
    <select
      class="bg-[#222] border border-[#444] rounded px-1.5 py-0.5 text-[10px] text-gray-300"
      bind:value={timeWindowSec}
    >
      <option value={5}>5s</option>
      <option value={10}>10s</option>
      <option value={30}>30s</option>
      <option value={60}>1m</option>
      <option value={300}>5m</option>
      <option value={1800}>30m</option>
    </select>

    <!-- Sessions panel toggle -->
    <button
      class="flex items-center gap-1 px-2 py-1 rounded text-[10px] font-bold uppercase tracking-wider transition-colors
        {showSessionsPanel ? 'bg-purple-600/20 text-purple-400' : 'bg-[#2a2a2a] text-gray-500 hover:bg-[#333]'}"
      onclick={() => showSessionsPanel = !showSessionsPanel}
    >
      <IconStack2 size={12} />
      Sessions
      {#if sessions.length > 0}
        <span class="text-[9px] bg-[#333] px-1 rounded">{sessions.length}</span>
      {/if}
    </button>
  </div>

  <!-- ── Range Stats Bar ──────────────────────────────────────────────── -->
  {#if rangeStats && markerA != null && markerB != null}
    <div class="flex items-center gap-3 px-3 py-1.5 bg-[#0a0a0a] border-b border-[#222] text-[10px]">
      <span class="font-bold uppercase tracking-wider text-gray-500">Range</span>
      <div class="flex items-center gap-1">
        <span class="text-gray-500">Min:</span>
        <span class="font-bold text-green-400">{rangeStats.min.toFixed(2)}</span>
      </div>
      <div class="flex items-center gap-1">
        <span class="text-gray-500">Max:</span>
        <span class="font-bold text-red-400">{rangeStats.max.toFixed(2)}</span>
      </div>
      <div class="flex items-center gap-1">
        <span class="text-gray-500">Avg:</span>
        <span class="font-bold text-yellow-400">{rangeStats.avg.toFixed(2)}</span>
      </div>
      <div class="flex items-center gap-1">
        <span class="text-gray-500">Δ:</span>
        <span class="font-bold {rangeStats.delta >= 0 ? 'text-green-400' : 'text-red-400'}">
          {rangeStats.delta >= 0 ? '+' : ''}{rangeStats.delta.toFixed(2)}
        </span>
      </div>
      <div class="flex items-center gap-1">
        <span class="text-gray-500">Rate:</span>
        <span class="font-bold text-blue-400">{rangeStats.rateOfChange.toFixed(2)}/s</span>
      </div>
      <button
        class="ml-auto text-gray-500 hover:text-gray-300 transition-colors"
        onclick={() => { markerA = null; markerB = null; }}
      >
        <IconX size={12} />
      </button>
    </div>
  {/if}

  <!-- ── Event Filter Bar ─────────────────────────────────────────────── -->
  {#if detectedEvents.length > 0}
    <div class="flex items-center gap-1.5 px-3 py-1 bg-[#111] border-b border-[#222] overflow-x-auto">
      <span class="text-[9px] font-bold uppercase tracking-wider text-gray-500 shrink-0">Events</span>
      {#each (['spike', 'flatline', 'dropout'] as SensorEventType[]) as eventType}
        {@const count = detectedEvents.filter(e => e.type === eventType).length}
        <button
          class="flex items-center gap-1 rounded px-1.5 py-0.5 text-[9px] font-bold transition-colors shrink-0
            {filterEventType === eventType
              ? 'text-white'
              : 'bg-[#1a1a1a] text-gray-500 hover:bg-[#222]'}"
          style="{filterEventType === eventType ? `background-color: ${SENSOR_EVENT_COLORS[eventType]}` : ''}"
          onclick={() => filterEventType = filterEventType === eventType ? 'All' : eventType}
        >
          {SENSOR_EVENT_ICONS[eventType]} {eventType}
          <span class="opacity-70">{count}</span>
        </button>
      {/each}
    </div>
  {/if}

  <!-- ── Main Content ─────────────────────────────────────────────────── -->
  {#if isMobile}
    <!-- Mobile: tab-based layout -->
    <div class="flex-1 min-h-0 flex flex-col">
      {#if mobileTab === 'chart'}
        <div class="flex-1 min-h-0 p-1">
          {#if chartSeries.length === 0}
            <div class="flex items-center justify-center h-full text-gray-500 text-xs px-4 text-center">
              Tap <span class="text-sky-400 mx-0.5">Sensors</span> tab to select data links to chart
            </div>
          {:else}
            <div class="relative h-full">
              <StreamingLineChart
                series={chartSeries}
                {timeWindowSec}
                showDataZoom={true}
                mode={activeSession ? 'playback' : 'live'}
                playbackData={currentData}
                {playbackTimeMs}
                {markerA}
                {markerB}
              />
              {#if showStats}
                <div class="absolute top-1 right-1 flex flex-col gap-0.5 z-10">
                  {#each chartSeries as s}
                    {@const stats = liveStats.get(Number(s.id))}
                    {#if stats}
                      <div class="bg-[#1a1a1a]/80 backdrop-blur rounded px-1.5 py-0.5 text-[8px] font-mono flex items-center gap-1">
                        <span class="w-1.5 h-1.5 rounded-full" style="background: {s.color}"></span>
                        <span class="text-gray-400">{stats.min.toFixed(1)}</span>
                        <span class="text-white">{stats.avg.toFixed(1)}</span>
                        <span class="text-gray-400">{stats.max.toFixed(1)}</span>
                      </div>
                    {/if}
                  {/each}
                </div>
              {/if}
            </div>
          {/if}
        </div>
      {:else if mobileTab === 'sensors'}
        <div class="flex-1 min-h-0">
          <SensorPicker dataLinks={allDataLinks} bind:selectedIds />
        </div>
      {:else if mobileTab === 'sessions'}
        <div class="flex-1 min-h-0 overflow-y-auto">
          <!-- Mobile sessions list -->
          <div class="p-2 space-y-1">
            {#if sessionError}
              <div class="px-2 py-1.5 bg-red-900/30 border border-red-500/30 rounded text-[10px] text-red-400">
                {sessionError}
              </div>
            {/if}
            <div class="flex items-center gap-1 mb-2">
              <button
                class="flex items-center gap-1 px-2 py-1 text-[10px] text-purple-400 bg-purple-400/10 rounded hover:bg-purple-400/20 transition-colors disabled:opacity-50"
                onclick={handleImportMes}
                disabled={busyAction === 'import'}
              >
                <IconFileImport size={11} />
                {busyAction === 'import' ? 'Importing...' : 'Import .mes'}
              </button>
              {#if sessions.length > 0}
                <button
                  class="flex items-center gap-1 px-2 py-1 text-[10px] text-sky-400 bg-sky-400/10 rounded hover:bg-sky-400/20 transition-colors disabled:opacity-50"
                  onclick={handleExportAllSessionsMes}
                  disabled={busyAction === 'export-all'}
                >
                  <IconFileExport size={11} />
                  {busyAction === 'export-all' ? 'Exporting...' : 'Export All'}
                </button>
              {/if}
            </div>
            {#if sessionsLoading}
              <div class="text-center text-gray-500 text-xs py-8">Loading...</div>
            {:else if sessions.length === 0}
              <div class="text-center text-gray-500 text-xs py-8">No saved sessions</div>
            {/if}
            {#each sessions as session}
              <div
                class="w-full flex items-center gap-2 px-2 py-2 rounded text-left transition-colors cursor-pointer
                  {activeSession?.id === session.id ? 'bg-purple-600/20 border border-purple-500/30' : 'bg-[#1a1a1a] border border-[#222] hover:bg-[#222]'}"
                onclick={() => loadSession(session)}
                role="button"
                tabindex="0"
                onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') loadSession(session); }}
              >
                <div class="flex-1 min-w-0">
                  <div class="text-xs text-white truncate">{session.name}</div>
                  <div class="text-[10px] text-gray-500">{formatMs(session.durationMs)} · {session.sensorIds.length || session.sensorCount || 0} sensors</div>
                </div>
                <button
                  class="p-1 text-gray-500 hover:text-purple-400 transition-colors disabled:opacity-50"
                  title="Export .mes"
                  disabled={busyAction === `export-${session.id}`}
                  onclick={(e) => { e.stopPropagation(); handleExportSessionMes(session); }}
                >
                  <IconFileExport size={12} />
                </button>
                <button
                  class="p-1 text-gray-500 hover:text-red-400 transition-colors"
                  onclick={(e) => { e.stopPropagation(); deleteSession(session.id); }}
                >
                  <IconTrash size={12} />
                </button>
              </div>
            {/each}
          </div>
        </div>
      {:else}
        <div class="flex-1 min-h-0">
          <EcuInfoPanel />
        </div>
      {/if}
    </div>

    <!-- Mobile tab bar -->
    <div class="flex border-t border-[#333] bg-[#1a1a1a]">
      <button class="flex-1 flex flex-col items-center gap-0.5 py-2 {mobileTab === 'chart' ? 'text-sky-400' : 'text-gray-500'}"
        onclick={() => mobileTab = 'chart'}>
        <IconChartLine size={18} />
        <span class="text-[9px]">Chart</span>
      </button>
      <button class="flex-1 flex flex-col items-center gap-0.5 py-2 {mobileTab === 'sensors' ? 'text-sky-400' : 'text-gray-500'}"
        onclick={() => mobileTab = 'sensors'}>
        <IconAdjustments size={18} />
        <span class="text-[9px]">Sensors</span>
        {#if selectedIds.size > 0}
          <span class="absolute top-1 right-1/4 w-4 h-4 rounded-full bg-sky-600 text-[8px] text-white flex items-center justify-center">{selectedIds.size}</span>
        {/if}
      </button>
      <button class="flex-1 flex flex-col items-center gap-0.5 py-2 {mobileTab === 'sessions' ? 'text-purple-400' : 'text-gray-500'}"
        onclick={() => mobileTab = 'sessions'}>
        <IconStack2 size={18} />
        <span class="text-[9px]">Sessions</span>
      </button>
      <button class="flex-1 flex flex-col items-center gap-0.5 py-2 {mobileTab === 'info' ? 'text-sky-400' : 'text-gray-500'}"
        onclick={() => mobileTab = 'info'}>
        <IconCircleDotted size={18} />
        <span class="text-[9px]">ECU</span>
      </button>
    </div>
  {:else}
    <!-- Desktop: three-panel layout -->
    <div class="flex flex-1 min-h-0 relative">
      <!-- Left: Sensor Picker -->
      <div class="w-64 shrink-0 border-r border-[#333]">
        <SensorPicker dataLinks={allDataLinks} bind:selectedIds />
      </div>

      <!-- Sessions dropdown (absolute positioned) -->
      {#if showSessionsPanel}
        <div class="absolute top-0 left-64 z-30 w-72 max-h-[60vh] bg-[#1a1a1a] border border-[#333] rounded-b-lg shadow-2xl overflow-hidden flex flex-col">
          <div class="flex items-center justify-between px-2 py-1.5 border-b border-[#333]">
            <span class="text-[10px] font-bold uppercase tracking-wider text-gray-500">Sessions</span>
            <div class="flex items-center gap-1">
              <button
                class="p-1 text-gray-500 hover:text-purple-400 transition-colors disabled:opacity-50"
                title="Import .mes"
                disabled={busyAction === 'import'}
                onclick={handleImportMes}
              >
                <IconFileImport size={11} />
              </button>
              {#if sessions.length > 0}
                <button
                  class="p-1 text-gray-500 hover:text-sky-400 transition-colors disabled:opacity-50"
                  title="Export all as .mes"
                  disabled={busyAction === 'export-all'}
                  onclick={handleExportAllSessionsMes}
                >
                  <IconFileExport size={11} />
                </button>
              {/if}
              <button class="text-gray-500 hover:text-gray-300" onclick={() => showSessionsPanel = false}>
                <IconX size={12} />
              </button>
            </div>
          </div>
          <div class="flex-1 overflow-y-auto">
            {#if sessionError}
              <div class="mx-2 mt-1.5 px-2 py-1.5 bg-red-900/30 border border-red-500/30 rounded text-[9px] text-red-400">
                {sessionError}
              </div>
            {/if}
            {#if sessionsLoading}
              <div class="text-center text-gray-500 text-[10px] py-4">Loading...</div>
            {:else if sessions.length === 0}
              <div class="text-center text-gray-500 text-[10px] py-4">No saved sessions</div>
            {/if}
            {#each sessions as session}
              <div
                role="button"
                tabindex="0"
                class="flex items-center gap-2 px-2 py-1.5 text-left cursor-pointer transition-colors border-b border-[#222]
                  {activeSession?.id === session.id ? 'bg-purple-600/10' : 'hover:bg-[#222]'}"
                onclick={() => loadSession(session)}
                onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') loadSession(session); }}
              >
                <div class="flex-1 min-w-0">
                  <input
                    type="text"
                    value={session.name}
                    onchange={(e) => renameSession(session.id, (e.target as HTMLInputElement).value)}
                    class="w-full bg-transparent text-[11px] text-white truncate outline-none hover:bg-[#2a2a2a] px-1 rounded"
                    onclick={(e) => e.stopPropagation()}
                  />
                  <div class="text-[9px] text-gray-500 px-1">
                    {new Date(session.startTime).toLocaleDateString()} · {formatMs(session.durationMs)}
                  </div>
                </div>
                <button
                  class="p-1 text-gray-500 hover:text-sky-400 transition-colors"
                  title="Export CSV"
                  onclick={(e) => { e.stopPropagation(); handleSessionExport(session, 'csv'); }}
                >
                  <IconFileExport size={11} />
                </button>
                <button
                  class="p-1 text-gray-500 hover:text-purple-400 transition-colors disabled:opacity-50"
                  title="Export .mes"
                  disabled={busyAction === `export-${session.id}`}
                  onclick={(e) => { e.stopPropagation(); handleExportSessionMes(session); }}
                >
                  <IconFileExport size={11} />
                </button>
                <button
                  class="p-1 text-gray-500 hover:text-red-400 transition-colors"
                  title="Delete"
                  onclick={(e) => { e.stopPropagation(); deleteSession(session.id); }}
                >
                  <IconTrash size={11} />
                </button>
              </div>
            {/each}
          </div>
        </div>
      {/if}

      <!-- Center: Chart -->
      <div class="flex-1 min-w-0 p-2 flex flex-col">
        {#if chartSeries.length === 0}
          <div class="flex items-center justify-center h-full text-gray-500 text-sm">
            Select sensors from the left panel to chart
          </div>
        {:else}
          <div class="flex-1 min-h-0 relative">
            <StreamingLineChart
              series={chartSeries}
              {timeWindowSec}
              showDataZoom={true}
              mode={activeSession ? 'playback' : 'live'}
              playbackData={currentData}
              {playbackTimeMs}
              {markerA}
              {markerB}
            />
            {#if showStats}
              <div class="absolute top-1 right-1 flex flex-col gap-0.5 z-10">
                {#each chartSeries as s}
                  {@const stats = liveStats.get(Number(s.id))}
                  {#if stats}
                    <div class="bg-[#1a1a1a]/80 backdrop-blur rounded px-1.5 py-0.5 text-[9px] font-mono flex items-center gap-1.5">
                      <span class="w-2 h-2 rounded-full shrink-0" style="background: {s.color}"></span>
                      <span class="text-gray-400">min:{stats.min.toFixed(1)}</span>
                      <span class="text-white font-bold">avg:{stats.avg.toFixed(1)}</span>
                      <span class="text-gray-400">max:{stats.max.toFixed(1)}</span>
                    </div>
                  {/if}
                {/each}
              </div>
            {/if}
          </div>
        {/if}

        <!-- Freeze frames list -->
        {#if activeSession && activeSession.freezeFrames.length > 0}
          <div class="flex items-center gap-1.5 mt-1 px-2 py-1 bg-[#1a1a1a] rounded border border-[#222]">
            <IconBookmark size={11} class="text-sky-400 shrink-0" />
            <span class="text-[9px] text-gray-500 uppercase tracking-wider shrink-0">Freeze Frames</span>
            {#each activeSession.freezeFrames as ff}
              <button
                class="px-1.5 py-0.5 text-[9px] bg-[#222] rounded text-gray-300 hover:bg-[#333] transition-colors"
                onclick={() => seekTo(ff.timeMs)}
              >
                {formatMs(ff.timeMs)}
              </button>
            {/each}
          </div>
        {/if}
      </div>

      <!-- Right: ECU Info -->
      <div class="w-60 shrink-0">
        <EcuInfoPanel />
      </div>
    </div>
  {/if}

  <!-- ── Bottom Bar (export only) ──────────────────────────────────────── -->
  {#if recorderState === 'stopped' || activeSession}
    <div class="flex items-center gap-2 px-3 py-1 bg-[#1a1a1a] border-t border-[#333] text-[10px]">
      <span class="text-gray-500 uppercase tracking-wider font-bold">Export</span>
      <button
        class="px-2 py-1 text-gray-400 bg-[#2a2a2a] rounded hover:bg-[#333] transition-colors"
        onclick={() => {
          if (activeSession) handleSessionExport(activeSession, 'csv');
          else handleExport('csv');
        }}
      >CSV</button>
      <button
        class="px-2 py-1 text-gray-400 bg-[#2a2a2a] rounded hover:bg-[#333] transition-colors"
        onclick={() => {
          if (activeSession) handleSessionExport(activeSession, 'yaml');
          else handleExport('yaml');
        }}
      >YAML</button>
    </div>
  {/if}
</div>

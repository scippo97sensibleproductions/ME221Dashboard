<script lang="ts">
  import StreamingLineChart from '../lib/echarts/StreamingLineChart.svelte';
  import { SessionStore, type RecordedSession } from '../lib/monitor/SessionStore';
  import { getSensorColor } from '../lib/monitor/sensorColors';
  import {
    IconPlayerPlay, IconPlayerStop, IconTrash, IconX,
    IconFileExport, IconFileImport, IconStack2, IconSearch,
  } from '@tabler/icons-svelte';

  let { onNavigate }: { onNavigate: (page: string) => void } = $props();

  function formatMs(ms: number): string {
    const s = Math.floor(ms / 1000);
    const m = Math.floor(s / 60);
    const h = Math.floor(m / 60);
    return `${String(h).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  }

  // ── State ──────────────────────────────────────────────────────────────
  let sessions = $state<RecordedSession[]>(SessionStore.sessions);
  let activeSession = $state<RecordedSession | null>(null);
  let playbackTimeMs = $state(0);
  let isPlaying = $state(false);
  let playbackSpeed = $state(1);
  let playInterval: ReturnType<typeof setInterval> | null = null;
  let searchQuery = $state('');
  let busyAction = $state<string | null>(null);
  let sessionError = $state<string | null>(null);
  let sessionsLoading = $state(false);

  // ── Derived ────────────────────────────────────────────────────────────
  const filteredSessions = $derived(
    searchQuery
      ? sessions.filter(s => s.name.toLowerCase().includes(searchQuery.toLowerCase()))
      : sessions,
  );

  const chartSeries = $derived(
    activeSession
      ? activeSession.sensorIds.map(id => ({
          id: String(id),
          name: activeSession.sensorNames[id] ?? `Sensor ${id}`,
          color: getSensorColor(id),
        }))
      : [],
  );

  const currentData = $derived(
    activeSession?.data
      ? new Map(Object.entries(activeSession.data).map(([k, v]) => [k, v]))
      : undefined,
  );

  // ── Init ───────────────────────────────────────────────────────────────
  $effect(() => {
    sessionsLoading = true;
    SessionStore.init().then(() => {
      sessions = SessionStore.sessions;
      sessionsLoading = false;
    }).catch(() => { sessionsLoading = false; });
  });

  // Cleanup on unmount
  $effect(() => {
    return () => { stopPlayback(); };
  });

  // ── Session Management ─────────────────────────────────────────────────
  async function loadSession(session: RecordedSession) {
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

  // ── Export/Import ──────────────────────────────────────────────────────
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
      if (!result.success && result.error) sessionError = result.error;
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
      if (!result.success && result.error) sessionError = result.error;
    } catch (err: any) {
      sessionError = err?.message ?? 'Export failed';
    } finally {
      busyAction = null;
    }
  }

  // Auto-dismiss error
  $effect(() => {
    if (sessionError) {
      const t = setTimeout(() => { sessionError = null; }, 5000);
      return () => clearTimeout(t);
    }
  });
</script>

<div class="flex flex-col h-full">
  <!-- ── Top Bar ──────────────────────────────────────────────────────── -->
  <div class="flex items-center gap-2 px-3 py-1.5 bg-[#1a1a1a] border-b border-[#333] text-[11px]">
    <IconStack2 size={14} class="text-purple-400" />
    <span class="font-bold uppercase tracking-wider text-gray-400">Sessions</span>
    <span class="text-gray-600">|</span>
    <span class="text-gray-500">{sessions.length} saved</span>

    <div class="flex-1"></div>

    {#if activeSession}
      <!-- Playback controls -->
      <button
        class="flex items-center gap-1 px-2 py-1 rounded text-[11px] font-bold uppercase tracking-wider bg-[#2a2a2a] text-gray-400 hover:bg-[#333] transition-colors"
        onclick={() => { activeSession = null; playbackTimeMs = 0; stopPlayback(); }}
      >
        <IconX size={12} />
        Close
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
  </div>

  <!-- ── Main Content ─────────────────────────────────────────────────── -->
  <div class="flex flex-1 min-h-0">
    <!-- Left: Session List -->
    <div class="w-80 shrink-0 border-r border-[#333] flex flex-col">
      <!-- Search + Actions -->
      <div class="px-2 py-1.5 border-b border-[#222] space-y-1.5">
        <div class="flex items-center gap-1 bg-[#222] rounded px-2 py-1">
          <IconSearch size={12} class="text-gray-500 shrink-0" />
          <input
            type="text"
            placeholder="Search sessions..."
            bind:value={searchQuery}
            class="flex-1 bg-transparent text-[11px] text-white outline-none placeholder:text-gray-600"
          />
        </div>
        <div class="flex items-center gap-1">
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
      </div>

      <!-- Error -->
      {#if sessionError}
        <div class="mx-2 mt-1.5 px-2 py-1.5 bg-red-900/30 border border-red-500/30 rounded text-[9px] text-red-400">
          {sessionError}
        </div>
      {/if}

      <!-- Session List -->
      <div class="flex-1 overflow-y-auto">
        {#if sessionsLoading}
          <div class="text-center text-gray-500 text-[10px] py-8">Loading...</div>
        {:else if filteredSessions.length === 0}
          <div class="text-center text-gray-500 text-[10px] py-8">
            {sessions.length === 0 ? 'No saved sessions' : 'No matching sessions'}
          </div>
        {/if}
        {#each filteredSessions as session (session.id)}
          <div
            class="flex items-center gap-2 px-2 py-1.5 text-left cursor-pointer transition-colors border-b border-[#222]
              {activeSession?.id === session.id ? 'bg-purple-600/10' : 'hover:bg-[#222]'}"
            onclick={() => loadSession(session)}
            role="button"
            tabindex="0"
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
                {new Date(session.startTime).toLocaleDateString()} · {formatMs(session.durationMs)} · {session.sensorIds.length || session.sensorCount || 0} sensors
              </div>
            </div>
            <button
              class="p-1 text-gray-500 hover:text-sky-400 transition-colors disabled:opacity-50"
              title="Export CSV"
              onclick={(e) => { e.stopPropagation(); /* CSV export placeholder */ }}
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

    <!-- Right: Chart / Empty State -->
    <div class="flex-1 min-w-0 p-2 flex flex-col">
      {#if activeSession}
        {#if chartSeries.length === 0}
          <div class="flex items-center justify-center h-full text-gray-500 text-sm">
            No sensor data in this session
          </div>
        {:else}
          <div class="flex-1 min-h-0">
            <StreamingLineChart
              series={chartSeries}
              timeWindowSec={Math.max(10, Math.ceil(activeSession.durationMs / 1000))}
              showDataZoom={true}
              mode="playback"
              playbackData={currentData}
              {playbackTimeMs}
            />
          </div>
        {/if}

        <!-- Freeze frames -->
        {#if activeSession.freezeFrames.length > 0}
          <div class="flex items-center gap-1.5 mt-1 px-2 py-1 bg-[#1a1a1a] rounded border border-[#222]">
            <span class="text-[9px] text-gray-500 uppercase tracking-wider shrink-0">Bookmarks</span>
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
      {:else}
        <div class="flex flex-col items-center justify-center h-full text-gray-500 gap-3">
          <IconStack2 size={48} class="text-gray-700" />
          <div class="text-center">
            <div class="text-sm font-medium">No session selected</div>
            <div class="text-[11px] text-gray-600 mt-1">Select a session from the list to view playback</div>
          </div>
          {#if sessions.length === 0}
            <button
              class="flex items-center gap-1.5 px-3 py-1.5 text-[11px] text-purple-400 bg-purple-400/10 rounded hover:bg-purple-400/20 transition-colors"
              onclick={handleImportMes}
            >
              <IconFileImport size={13} />
              Import .mes file
            </button>
          {/if}
        </div>
      {/if}
    </div>
  </div>
</div>

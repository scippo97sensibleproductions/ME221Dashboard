<script lang="ts">
  import StreamingLineChart from '../lib/charts/StreamingLineChart.svelte';
  import { SessionStore, type RecordedSession } from '../lib/monitor/SessionStore';
  import { getSensorColor } from '../lib/monitor/sensorColors';
  import { HybridBridge } from '../lib/HybridBridge';
  import {
    IconPlayerPlay, IconPlayerStop, IconTrash, IconX,
    IconFileExport, IconFileImport, IconStack2, IconSearch,
    IconDotsVertical, IconDownload, IconFileSpreadsheet, IconClock,
    IconChartLine,
  } from '@tabler/icons-svelte';

  let { onNavigate }: { onNavigate: (page: string) => void } = $props();

  function formatMs(ms: number): string {
    const s = Math.floor(ms / 1000);
    const m = Math.floor(s / 60);
    const h = Math.floor(m / 60);
    return `${String(h).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  }

  function formatDate(iso: string): string {
    const d = new Date(iso);
    const now = new Date();
    const diffMs = now.getTime() - d.getTime();
    const diffDays = Math.floor(diffMs / 86400000);
    if (diffDays === 0) return 'Today';
    if (diffDays === 1) return 'Yesterday';
    if (diffDays < 7) return `${diffDays}d ago`;
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
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
  let openMenuId = $state<string | null>(null);

  // ── Derived ────────────────────────────────────────────────────────────
  const filteredSessions = $derived(
    searchQuery
      ? sessions.filter(s => s.name.toLowerCase().includes(searchQuery.toLowerCase()))
      : sessions,
  );

  const chartSeries = $derived(
    (() => {
      const s = activeSession;
      return s
        ? s.sensorIds.map(id => ({
            id: String(id),
            name: s.sensorNames[id] ?? `Sensor ${id}`,
            color: getSensorColor(id),
          }))
        : [];
    })(),
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

  // Close menu on outside click
  $effect(() => {
    const handler = (e: MouseEvent) => {
      if (openMenuId && !(e.target as HTMLElement).closest('[data-menu]')) {
        openMenuId = null;
      }
    };
    document.addEventListener('click', handler);
    return () => document.removeEventListener('click', handler);
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
    openMenuId = null;
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
  const VD_NAME_MAP: Record<string, string> = {
    'rpm': 'RPM', 'engine speed': 'RPM',
    'throttle position': 'Throttle Position', 'tps': 'Throttle Position',
    'afr': 'AFR', 'wideband': 'AFR', 'lambda': 'AFR',
    'boost': 'Boost', 'map': 'Boost',
    'baro': 'Barometric Pressure',
    'clt': 'Coolant Temp', 'coolant temp': 'Coolant Temp',
    'iat': 'Intake Air Temp', 'intake air temp': 'Intake Air Temp',
    'batt': 'Battery Voltage', 'battery voltage': 'Battery Voltage',
    'vss': 'Vehicle Speed', 'speed': 'Vehicle Speed',
    'ignition': 'Ignition Timing', 'ignition advance': 'Ignition Timing',
    'duty': 'Injector Duty', 'injector duty': 'Injector Duty',
    'fuel rail': 'Fuel Pressure', 'fuel pressure': 'Fuel Pressure',
  };
  function mapVdName(raw: string): string {
    const lower = raw.toLowerCase().trim();
    if (VD_NAME_MAP[lower]) return VD_NAME_MAP[lower];
    for (const [key, val] of Object.entries(VD_NAME_MAP)) {
      if (lower.includes(key)) return val;
    }
    return raw;
  }

  function buildSessionCsv(session: RecordedSession): string {
    const ids = session.sensorIds;
    if (ids.length === 0) return '';
    const escapeCsv = (s: string) => s.includes(',') || s.includes('"') ? `"${s.replace(/"/g, '""')}"` : s;
    const headers = ['time_ms', ...ids.map(id => escapeCsv(session.sensorNames[id] ?? String(id)))];
    const lines: string[] = [headers.join(',')];
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
    return lines.join('\n');
  }

  function buildSessionVdCsv(session: RecordedSession): string {
    const ids = session.sensorIds;
    if (ids.length === 0) return '';
    const escapeCsv = (s: string) => s.includes(',') || s.includes('"') ? `"${s.replace(/"/g, '""')}"` : s;
    const mappedNames = ids.map(id => mapVdName(session.sensorNames[id] ?? String(id)));
    const headers = ['Time', ...mappedNames.map(escapeCsv)];
    const lines: string[] = ['ME221', headers.join(',')];
    const maxLen = Math.max(...ids.map(id => session.data[id]?.length ?? 0));
    for (let i = 0; i < maxLen; i++) {
      const row: string[] = [];
      for (const id of ids) {
        const pts = session.data[id] ?? [];
        const s = pts[i];
        row.push(s ? `${(s.t / 1000).toFixed(3)},${s.v}` : ',');
      }
      lines.push(row.join(','));
    }
    return lines.join('\n');
  }

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
    openMenuId = null;
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

<div class="flex flex-col h-full select-none">
  <!-- ── Top Bar ──────────────────────────────────────────────────────── -->
  <div class="flex items-center gap-3 px-4 py-2 bg-metro-surface border-b border-metro-border">
    <div class="border-l-4 border-l-metro-purple pl-3 flex items-center gap-2">
      <IconStack2 size={16} class="text-metro-purple" />
      <span class="text-[13px] font-extrabold uppercase tracking-wider text-white">Sessions</span>
    </div>
    <span class="bg-metro-purple/20 text-metro-purple text-[10px] font-bold uppercase px-2 py-0.5 rounded">{sessions.length} saved</span>

    <button
      class="metro-btn-secondary flex items-center gap-1.5 px-2.5 py-1 text-[10px]"
      onclick={() => onNavigate('ecuMonitor')}
    >
      <IconChartLine size={14} />
      Monitor
    </button>

    <div class="flex-1"></div>

    {#if activeSession}
      <!-- Playback controls -->
      <button
        class="metro-btn-secondary flex items-center gap-1.5 px-3 py-1.5 text-[11px]"
        onclick={() => { activeSession = null; playbackTimeMs = 0; stopPlayback(); }}
      >
        <IconX size={14} />
        Close
      </button>
      <button
        class="flex items-center gap-1.5 px-3 py-1.5 rounded text-[11px] font-bold uppercase tracking-wider transition-colors duration-150
          {isPlaying ? 'bg-metro-blue text-white' : 'metro-btn-secondary'}"
        onclick={togglePlayback}
      >
        {#if isPlaying}
          <IconPlayerStop size={14} />
          Stop
        {:else}
          <IconPlayerPlay size={14} />
          Play
        {/if}
      </button>
      <span class="text-metro-text-secondary font-mono tabular-nums text-[11px]">
        {formatMs(playbackTimeMs)} / {formatMs(activeSession.durationMs)}
      </span>
      <input
        type="range" min="0" max={activeSession.durationMs} step="100"
        value={playbackTimeMs}
        oninput={(e) => seekTo(Number((e.target as HTMLInputElement).value))}
        class="w-32 h-1 accent-metro-blue"
      />
      <select
        class="metro-input py-1 px-1.5 text-[10px]"
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
    <div class="w-80 shrink-0 border-r border-metro-border flex flex-col bg-metro-surface">
      <!-- Search + Actions -->
      <div class="px-3 py-2 border-b border-metro-border space-y-2">
        <div class="flex items-center gap-2 bg-metro-input-bg border border-metro-input-border rounded px-2 py-1.5">
          <IconSearch size={14} class="text-metro-text-muted shrink-0" />
          <input
            type="text"
            placeholder="Search sessions..."
            bind:value={searchQuery}
            class="flex-1 bg-transparent text-[12px] text-white outline-none placeholder:text-metro-text-muted font-mono"
          />
        </div>
        <div class="flex items-center gap-2">
          <button
            class="metro-btn-secondary flex items-center gap-1.5 px-2.5 py-1 text-[10px] disabled:opacity-50"
            onclick={handleImportMes}
            disabled={busyAction === 'import'}
          >
            <IconFileImport size={12} />
            {busyAction === 'import' ? 'Importing...' : 'Import .mes'}
          </button>
          {#if sessions.length > 0}
            <button
              class="metro-btn-primary flex items-center gap-1.5 px-2.5 py-1 text-[10px] disabled:opacity-50"
              onclick={handleExportAllSessionsMes}
              disabled={busyAction === 'export-all'}
            >
              <IconFileExport size={12} />
              {busyAction === 'export-all' ? 'Exporting...' : 'Export All'}
            </button>
          {/if}
        </div>
      </div>

      <!-- Error -->
      {#if sessionError}
        <div class="mx-3 mt-2 px-2 py-1.5 bg-metro-red/20 border border-metro-red/40 rounded text-[10px] text-metro-red font-bold uppercase tracking-wider">
          {sessionError}
        </div>
      {/if}

      <!-- Session List -->
      <div class="flex-1 overflow-y-auto">
        {#if sessionsLoading}
          <div class="flex flex-col items-center justify-center py-12 gap-2">
            <div class="w-6 h-6 border-2 border-metro-purple/30 border-t-metro-purple rounded-full animate-spin"></div>
            <span class="text-metro-text-muted text-[11px]">Loading sessions...</span>
          </div>
        {:else if filteredSessions.length === 0}
          <div class="flex flex-col items-center justify-center py-12 gap-3">
            <div class="w-12 h-12 rounded bg-metro-purple/10 flex items-center justify-center">
              <IconStack2 size={24} class="text-metro-purple/50" />
            </div>
            <div class="text-center">
              <div class="text-[12px] text-metro-text-secondary font-bold">{sessions.length === 0 ? 'No sessions yet' : 'No matching sessions'}</div>
              <div class="text-[10px] text-metro-text-muted mt-1">{sessions.length === 0 ? 'Record a session to get started' : 'Try a different search term'}</div>
            </div>
          </div>
        {/if}
        {#each filteredSessions as session (session.id)}
          {@const sensorCount = session.sensorIds.length || session.sensorCount || 0}
          {@const isActive = activeSession?.id === session.id}
          <div class="relative" data-menu>
            <div
              class="flex items-center gap-2 px-3 py-2.5 text-left cursor-pointer transition-colors duration-150 border-b border-metro-border-subtle
                {isActive
                  ? 'bg-gradient-to-r from-metro-purple/15 to-transparent border-l-2 border-l-metro-purple'
                  : 'hover:bg-metro-hover border-l-2 border-l-transparent'}"
              onclick={() => loadSession(session)}
              role="button"
              tabindex="0"
              onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') loadSession(session); }}
            >
              <!-- Sensor color dots -->
              <div class="flex flex-col gap-0.5 shrink-0">
                {#each session.sensorIds.slice(0, 3) as sid}
                  <div class="w-1.5 h-1.5 rounded-full" style="background: {getSensorColor(sid)}"></div>
                {/each}
                {#if sensorCount > 3}
                  <div class="text-[8px] text-metro-text-muted text-center">+{sensorCount - 3}</div>
                {/if}
              </div>

              <div class="flex-1 min-w-0">
                <input
                  type="text"
                  value={session.name}
                  onchange={(e) => renameSession(session.id, (e.target as HTMLInputElement).value)}
                  class="w-full bg-transparent text-[12px] text-white truncate outline-none hover:bg-metro-card px-1 rounded font-bold"
                  onclick={(e) => e.stopPropagation()}
                />
                <div class="flex items-center gap-2 mt-0.5 px-1">
                  <span class="text-[10px] text-metro-text-muted flex items-center gap-1">
                    <IconClock size={10} />
                    {formatDate(session.startTime)}
                  </span>
                  <span class="text-metro-border">·</span>
                  <span class="text-[10px] text-metro-text-secondary font-mono">{formatMs(session.durationMs)}</span>
                  <span class="text-metro-border">·</span>
                  <span class="text-[9px] bg-metro-purple/20 text-metro-purple px-1.5 py-0.5 rounded font-bold uppercase">{sensorCount} sensors</span>
                </div>
              </div>
              <button
                class="w-7 h-7 flex items-center justify-center text-metro-text-secondary hover:bg-metro-hover rounded transition-colors duration-150"
                title="Actions"
                onclick={(e) => { e.stopPropagation(); openMenuId = openMenuId === session.id ? null : session.id; }}
              >
                <IconDotsVertical size={14} />
              </button>
            </div>

            <!-- Context Menu -->
            {#if openMenuId === session.id}
              <div class="absolute right-2 top-full z-50 w-48 bg-metro-card border border-metro-border rounded shadow-lg overflow-hidden">
                <button
                  class="w-full flex items-center gap-2 px-3 py-2 text-[11px] text-metro-text-secondary hover:bg-metro-hover hover:text-white transition-colors duration-150 text-left"
                  onclick={(e) => { e.stopPropagation(); HybridBridge.saveFile(`${session.name}.csv`, buildSessionCsv(session)); openMenuId = null; }}
                >
                  <IconFileSpreadsheet size={14} />
                  Export CSV
                </button>
                <button
                  class="w-full flex items-center gap-2 px-3 py-2 text-[11px] text-metro-text-secondary hover:bg-metro-hover hover:text-white transition-colors duration-150 text-left"
                  onclick={(e) => { e.stopPropagation(); HybridBridge.saveFile(`${session.name}.csv`, buildSessionVdCsv(session)); openMenuId = null; }}
                >
                  <IconDownload size={14} />
                  Export for VirtualDyno
                </button>
                <button
                  class="w-full flex items-center gap-2 px-3 py-2 text-[11px] text-metro-text-secondary hover:bg-metro-hover hover:text-white transition-colors duration-150 text-left disabled:opacity-50"
                  disabled={busyAction === `export-${session.id}`}
                  onclick={(e) => { e.stopPropagation(); handleExportSessionMes(session); }}
                >
                  <IconFileExport size={14} />
                  {busyAction === `export-${session.id}` ? 'Exporting...' : 'Export .mes'}
                </button>
                <div class="border-t border-metro-border"></div>
                <button
                  class="w-full flex items-center gap-2 px-3 py-2 text-[11px] text-metro-red hover:bg-metro-red/20 transition-colors duration-150 text-left"
                  onclick={(e) => { e.stopPropagation(); deleteSession(session.id); }}
                >
                  <IconTrash size={14} />
                  Delete
                </button>
              </div>
            {/if}
          </div>
        {/each}
      </div>
    </div>

    <!-- Right: Chart / Empty State -->
    <div class="flex-1 min-w-0 p-3 flex flex-col bg-metro-bg">
      {#if activeSession}
        {#if chartSeries.length === 0}
          <div class="flex items-center justify-center h-full text-metro-text-muted text-[13px]">
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
          <div class="flex items-center gap-2 mt-2 px-3 py-1.5 bg-metro-card border border-metro-border">
            <span class="text-[10px] text-metro-text-muted uppercase tracking-wider font-bold shrink-0">Bookmarks</span>
            {#each activeSession.freezeFrames as ff}
              <button
                class="metro-btn-secondary px-2 py-0.5 text-[10px]"
                onclick={() => seekTo(ff.timeMs)}
              >
                {formatMs(ff.timeMs)}
              </button>
            {/each}
          </div>
        {/if}
      {:else}
        <div class="flex flex-col items-center justify-center h-full gap-4">
          <div class="w-20 h-20 rounded bg-gradient-to-br from-metro-purple/20 to-metro-blue/10 flex items-center justify-center">
            <IconStack2 size={40} class="text-metro-purple/60" />
          </div>
          <div class="text-center">
            <div class="text-[14px] font-bold text-metro-text-secondary">No session selected</div>
            <div class="text-[11px] text-metro-text-muted mt-1">Select a session from the list to view playback</div>
          </div>
          {#if sessions.length === 0}
            <button
              class="metro-btn-primary flex items-center gap-2 px-4 py-2 text-[12px]"
              onclick={handleImportMes}
            >
              <IconFileImport size={14} />
              Import .mes file
            </button>
          {/if}
        </div>
      {/if}
    </div>
  </div>
</div>

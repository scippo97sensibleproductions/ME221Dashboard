<script lang="ts">
  import { SessionRecorder, type RecordingState } from './SessionRecorder';

  let {
    selectedSensorIds = [],
    onExport,
  }: {
    selectedSensorIds: number[];
    onExport?: (format: 'csv' | 'yaml') => void;
  } = $props();

  let tick = $state(0);
  let recorderState = $state<RecordingState>(SessionRecorder.state);
  let finalDurationMs = $state(0);
  let timerInterval: ReturnType<typeof setInterval> | null = null;

  function startTimer() {
    stopTimer();
    tick++;
    timerInterval = setInterval(() => tick++, 100);
  }

  function stopTimer() {
    if (timerInterval !== null) {
      clearInterval(timerInterval);
      timerInterval = null;
    }
  }

  function toggleRecording() {
    if (SessionRecorder.state === 'recording') {
      finalDurationMs = SessionRecorder.durationMs;
      SessionRecorder.stop();
      stopTimer();
    } else {
      SessionRecorder.start(selectedSensorIds);
      startTimer();
    }
    recorderState = SessionRecorder.state;
  }

  function formatDuration(ms: number): string {
    const s = Math.floor(ms / 1000);
    const m = Math.floor(s / 60);
    const h = Math.floor(m / 60);
    return `${String(h).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  }

  function handleExport(format: 'csv' | 'yaml') {
    onExport?.(format);
  }
</script>

<div class="flex items-center gap-3 px-3 py-1.5 bg-[#1a1a1a] border-t border-[#333]">
  <button
    class="flex items-center gap-1.5 px-2 py-1 rounded transition-colors
      {recorderState === 'recording'
        ? 'bg-red-600/20 text-red-400 hover:bg-red-600/30'
        : 'bg-[#2a2a2a] text-gray-400 hover:bg-[#333]'}"
    onclick={toggleRecording}
    title={recorderState === 'recording' ? 'Stop recording session' : 'Start recording session to CSV/YAML'}
  >
    {#if recorderState === 'recording'}
      <div class="w-2.5 h-2.5 bg-red-500 rounded-sm"></div>
      <span class="text-[11px]">Stop</span>
    {:else}
      <div class="w-2.5 h-2.5 bg-red-500 rounded-full"></div>
      <span class="text-[11px]">Record</span>
    {/if}
  </button>

  <div class="text-xs text-gray-400 font-mono tabular-nums min-w-[70px]">
    {#if recorderState === 'recording'}
      {tick ? formatDuration(SessionRecorder.durationMs) : formatDuration(SessionRecorder.durationMs)}
    {:else if recorderState === 'stopped'}
      {formatDuration(finalDurationMs)}
    {:else}
      00:00:00
    {/if}
  </div>

  <div class="text-[10px] text-gray-500">
    {#if recorderState === 'recording'}
      <span class="text-red-400">Recording</span>
    {:else if recorderState === 'stopped'}
      <span class="text-amber-400">Stopped</span>
    {:else}
      Session
    {/if}
  </div>

  <div class="flex-1"></div>

  {#if recorderState !== 'idle'}
    <button
      class="px-2 py-1 text-[10px] text-gray-400 bg-[#2a2a2a] rounded hover:bg-[#333] transition-colors"
      title="Export recorded data as CSV"
      onclick={() => handleExport('csv')}
    >
      Export CSV
    </button>
    <button
      class="px-2 py-1 text-[10px] text-gray-400 bg-[#2a2a2a] rounded hover:bg-[#333] transition-colors"
      title="Export recorded data as YAML"
      onclick={() => handleExport('yaml')}
    >
      Export YAML
    </button>
  {/if}
</div>

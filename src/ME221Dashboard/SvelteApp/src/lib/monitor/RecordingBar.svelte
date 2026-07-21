<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { SessionRecorder, type RecordingState } from './SessionRecorder';
  import type { DataLinkDefinition } from '../HybridBridgeTypes';
  import { IconPlayerPlay, IconPlayerStop, IconCamera } from '@tabler/icons-svelte';

  let {
    selectedSensorIds = [],
    dataLinks = [],
    onExport,
    onFreezeFrame,
  }: {
    selectedSensorIds: number[];
    dataLinks?: DataLinkDefinition[];
    onExport?: (format: 'csv' | 'yaml') => void;
    onFreezeFrame?: () => void;
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

  onMount(() => {
    if (SessionRecorder.state === 'recording') {
      startTimer();
    }
  });

  onDestroy(() => {
    stopTimer();
  });

  function toggleRecording() {
    if (SessionRecorder.state === 'recording') {
      finalDurationMs = SessionRecorder.durationMs;
      SessionRecorder.stop();
      stopTimer();
    } else {
      const nameMap = new Map<number, string>();
      for (const dl of dataLinks) {
        nameMap.set(dl.id, dl.name);
      }
      SessionRecorder.start(selectedSensorIds, nameMap);
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
    class="flex items-center gap-1.5 px-3 py-1.5 rounded transition-colors text-[11px] font-bold uppercase tracking-wider
      {recorderState === 'recording'
        ? 'bg-red-600/20 text-red-400 hover:bg-red-600/30'
        : 'bg-[#2a2a2a] text-gray-400 hover:bg-[#333]'}"
    onclick={toggleRecording}
    title={recorderState === 'recording' ? 'Stop recording session' : 'Start recording session'}
  >
    {#if recorderState === 'recording'}
      <IconPlayerStop size={14} />
      <span>Stop</span>
    {:else}
      <IconPlayerPlay size={14} />
      <span>Record</span>
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

  <div class="text-[10px] font-bold uppercase tracking-wider">
    {#if recorderState === 'recording'}
      <span class="text-red-400">Recording</span>
    {:else if recorderState === 'stopped'}
      <span class="text-amber-400">Stopped</span>
    {:else}
      <span class="text-gray-600">Ready</span>
    {/if}
  </div>

  {#if recorderState === 'recording'}
    <button
      class="flex items-center gap-1 px-2 py-1 text-[10px] text-sky-400 bg-sky-400/10 rounded hover:bg-sky-400/20 transition-colors"
      title="Freeze-frame: bookmark this moment"
      onclick={onFreezeFrame}
    >
      <IconCamera size={12} />
      Freeze
    </button>
  {/if}

  <div class="flex-1"></div>

  {#if recorderState !== 'idle'}
    <button
      class="px-2 py-1 text-[10px] text-gray-400 bg-[#2a2a2a] rounded hover:bg-[#333] transition-colors"
      title="Export recorded data as CSV"
      onclick={() => handleExport('csv')}
    >
      CSV
    </button>
    <button
      class="px-2 py-1 text-[10px] text-gray-400 bg-[#2a2a2a] rounded hover:bg-[#333] transition-colors"
      title="Export recorded data as YAML"
      onclick={() => handleExport('yaml')}
    >
      YAML
    </button>
  {/if}
</div>

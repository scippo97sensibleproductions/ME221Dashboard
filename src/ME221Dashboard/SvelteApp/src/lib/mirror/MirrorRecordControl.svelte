<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { HybridBridge } from '../HybridBridge';
  import { mirrorStore } from '../stores/MirrorStore.svelte';
  import { SessionRecorder, type RecordingState } from '../monitor/SessionRecorder';
  import { IconPlayerPlay, IconPlayerStop } from '@tabler/icons-svelte';

  let recorderState = $state<RecordingState>(SessionRecorder.state);
  let elapsed = $state('00:00:00');
  let notice = $state<string | null>(null);
  let noticeVisible = $state(false);
  let timerInterval: ReturnType<typeof setInterval> | null = null;
  let noticeTimer: ReturnType<typeof setTimeout> | null = null;
  let unsubscribeFeed: (() => void) | null = null;
  let wasConnected = false;

  let show = $derived(mirrorStore.sessionState === 'Connected');

  function syncFromRecorder() {
    recorderState = SessionRecorder.state;
    const next = SessionRecorder.completionNotice;
    if (next) {
      notice = next;
      noticeVisible = true;
      if (noticeTimer) clearTimeout(noticeTimer);
      noticeTimer = setTimeout(() => { noticeVisible = false; }, 6000);
    } else {
      notice = null;
      noticeVisible = false;
      if (noticeTimer) clearTimeout(noticeTimer);
      noticeTimer = null;
    }
  }

  function formatElapsed(): string {
    const s = Math.floor(SessionRecorder.durationMs / 1000);
    const m = Math.floor(s / 60);
    const h = Math.floor(m / 60);
    return `${String(h).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  }

  function startTimer() {
    stopTimer();
    elapsed = formatElapsed();
    timerInterval = setInterval(() => { elapsed = formatElapsed(); }, 250);
  }

  function stopTimer() {
    if (timerInterval !== null) {
      clearInterval(timerInterval);
      timerInterval = null;
    }
  }

  function toggle() {
    if (SessionRecorder.state === 'recording') {
      SessionRecorder.stop();
      stopTimer();
    } else {
      SessionRecorder.startCompanion([]);
      startTimer();
    }
    syncFromRecorder();
  }

  // Session-end finalization (R18): revocation, disconnect or exhaustion
  // finalize the open recording with a completion notice.
  $effect(() => {
    const connected = mirrorStore.sessionState === 'Connected';
    if (connected) {
      wasConnected = true;
    } else if (wasConnected) {
      wasConnected = false;
      if (SessionRecorder.state === 'recording') {
        SessionRecorder.finalize('Session ended');
        stopTimer();
        syncFromRecorder();
      }
    }
  });

  onMount(() => {
    syncFromRecorder();
    if (SessionRecorder.state === 'recording') startTimer();
    unsubscribeFeed = HybridBridge.onMessage((event) => {
      if (event.event !== 'mirrorRecordFeed') return;
      if (SessionRecorder.state !== 'recording') return;
      SessionRecorder.pushFeedFrame(event.payload.seq, event.payload.ts, event.payload.values);
    });
  });

  onDestroy(() => {
    stopTimer();
    if (noticeTimer) clearTimeout(noticeTimer);
    if (SessionRecorder.state === 'recording') {
      SessionRecorder.finalize('Companion session ended');
    }
    unsubscribeFeed?.();
  });
</script>

{#if show}
  <div
    class="fixed bottom-4 right-4 z-40 flex items-center gap-3 px-3 py-2 rounded"
    style="background-color: var(--metro-card); border: 1px solid var(--metro-border);"
  >
    <span class="flex items-center gap-1.5">
      <span
        class="h-2 w-2 rounded-full {recorderState === 'recording' ? 'bg-metro-red' : 'bg-[#333333]'}"
      ></span>
      <span
        class="text-[10px] font-semibold uppercase tracking-[0.5px] {recorderState === 'recording' ? 'text-metro-red' : 'text-metro-text-secondary'}"
      >
        {recorderState === 'recording' ? 'REC' : 'RECORD'}
      </span>
    </span>

    <span class="text-xs font-mono tabular-nums" style="color: var(--metro-text-secondary);">
      {recorderState === 'recording' ? elapsed : '00:00:00'}
    </span>

    <button
      class="flex items-center gap-1 px-2 py-1 rounded text-[10px] font-bold uppercase tracking-wider transition-colors
        {recorderState === 'recording'
          ? 'bg-metro-red text-white hover:bg-[#C50F1F]'
          : 'bg-metro-teal text-white hover:opacity-80'}"
      onclick={toggle}
      title={recorderState === 'recording' ? 'Stop companion recording' : 'Start companion recording'}
    >
      {#if recorderState === 'recording'}
        <IconPlayerStop size={16} />
        <span>Stop</span>
      {:else}
        <IconPlayerPlay size={16} />
        <span>Record</span>
      {/if}
    </button>

    {#if noticeVisible && notice}
      <span
        class="max-w-40 truncate px-2 py-1 rounded text-[10px] font-semibold uppercase tracking-[0.5px]"
        style="color: var(--metro-teal); border: 1px solid var(--metro-teal);"
        title={notice}
      >
        {notice}
      </span>
    {/if}
  </div>
{/if}

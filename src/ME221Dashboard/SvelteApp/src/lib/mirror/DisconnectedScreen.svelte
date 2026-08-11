<script lang="ts">
  import { IconRefresh, IconArrowBackUp } from '@tabler/icons-svelte';

  let { onRetry, onExit }: { onRetry: () => void; onExit: () => void } = $props();
</script>

<!-- R13: exhausted-reconnect state — the host is unreachable. -->
<div
  class="fixed inset-0 z-50 flex flex-col items-center justify-center gap-4 px-4"
  style="background-color: var(--metro-bg);"
  role="alert"
  aria-label="Connection lost"
>
  <div class="flex items-center gap-3 border-l-4 pl-3" style="border-color: var(--metro-orange);">
    <h2 class="text-[13px] font-extrabold uppercase tracking-wider text-white">Connection Lost</h2>
  </div>
  <p class="text-[13px]" style="color: var(--metro-text-secondary);">The host is unreachable. Retry or return to discovery.</p>
  <div class="flex gap-2">
    <button class="metro-btn-primary flex items-center gap-2" onclick={onRetry}>
      <IconRefresh size={16} />
      Retry
    </button>
    <button class="metro-btn-secondary flex items-center gap-2" onclick={onExit}>
      <IconArrowBackUp size={16} />
      Return to Discovery
    </button>
  </div>
</div>

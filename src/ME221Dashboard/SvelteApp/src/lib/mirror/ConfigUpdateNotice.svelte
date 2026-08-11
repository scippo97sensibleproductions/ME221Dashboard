<script lang="ts">
  import { IconRefresh } from '@tabler/icons-svelte';

  let { onReconnect }: { onReconnect: () => void } = $props();
</script>

<!-- R19: the host changed snapshot-scoped config mid-session; reconnect required. -->
<div
  class="fixed inset-0 z-50 flex flex-col items-center justify-center gap-4 px-4"
  style="background-color: var(--metro-bg);"
  role="alert"
  aria-label="Update made"
>
  <div class="flex items-center gap-3 border-l-4 pl-3" style="border-color: var(--metro-yellow);">
    <h2 class="text-[13px] font-extrabold uppercase tracking-wider text-white">Update Made</h2>
  </div>
  <p class="text-[13px]" style="color: var(--metro-text-secondary);">The host changed its configuration. Reconnect to receive the fresh snapshot.</p>
  <button class="metro-btn-primary flex items-center gap-2" onclick={onReconnect}>
    <IconRefresh size={16} />
    Reconnect
  </button>
</div>

<script lang="ts">
  import { IconLoader2 } from '@tabler/icons-svelte';

  let { open, title, scopeText, confirmLabel = 'Apply', inFlight = false, error = null, onConfirm = () => {}, onCancel = () => {} }: {
    open: boolean;
    title: string;
    scopeText: string;
    confirmLabel?: string;
    inFlight?: boolean;
    error?: string | null;
    onConfirm?: () => void;
    onCancel?: () => void;
  } = $props();
</script>

{#if open}
  <div
    class="fixed inset-0 z-[70] flex items-end justify-center bg-black/60"
    role="dialog"
    aria-modal="true"
    tabindex="-1"
    onclick={(e) => { if (e.target === e.currentTarget && !inFlight) onCancel(); }}
    onkeydown={(e) => { if (e.key === 'Escape' && !inFlight) onCancel(); }}
  >
    <div
      class="w-full max-w-md rounded-t border border-b-0 p-4"
      style="background-color: var(--metro-card); border-color: var(--metro-border);"
    >
      <p class="text-sm font-medium" style="color: var(--metro-text);">{title}</p>
      <p class="mt-1 text-xs" style="color: var(--metro-text-muted);">{scopeText}</p>

      {#if error}
        <p class="mt-2 rounded border px-2 py-1.5 text-[11px]" style="border-color: rgba(239,68,68,0.4); color: #fca5a5;">
          {error}
        </p>
      {/if}

      <div class="mt-3 flex justify-end gap-2">
        <button
          class="h-11 rounded px-3 text-xs transition-colors hover:bg-gray-800 disabled:opacity-40"
          style="color: var(--metro-text-secondary);"
          disabled={inFlight}
          onclick={onCancel}
        >
          Cancel
        </button>
        <button
          class="flex h-11 items-center gap-1.5 rounded px-3 text-xs font-medium text-white transition-colors disabled:opacity-60"
          style="background-color: var(--metro-accent);"
          disabled={inFlight}
          onclick={onConfirm}
        >
          {#if inFlight}
            <IconLoader2 size={14} class="animate-spin" />
          {/if}
          {confirmLabel}
        </button>
      </div>
    </div>
  </div>
{/if}

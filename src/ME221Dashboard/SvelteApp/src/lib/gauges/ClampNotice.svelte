<script lang="ts">
  import { IconInfoCircle, IconAlertTriangle } from '@tabler/icons-svelte';
  import type { Snippet } from 'svelte';

  // Shared inline notice. Variants:
  // - 'clamped-info' (U5): panel-local info, e.g. the ramp clamp result.
  // - 'error' (U6): auto-hide live-reject error (the auto-hide timer lives in
  //   NumberInput via LIVE_REJECT_AUTO_HIDE_MS).
  // - 'clamped-info-persist' (U8): the on-save clamp notice — persists until
  //   any shifter/ramp edit occurs or the producing bound no longer holds
  //   (the parent owns the dismissal policy).
  let { message, variant = 'clamped-info' as const, onDismiss, actions }: {
    message: string;
    variant?: 'clamped-info' | 'error' | 'clamped-info-persist';
    onDismiss?: () => void;
    /** Optional named snippet for action buttons (e.g. "Clear floor", U8). */
    actions?: Snippet;
  } = $props();

  const isError = $derived(variant === 'error');
</script>

<div
  class="flex items-start gap-2 rounded border px-2.5 py-1.5 text-xs"
  style:border-color={isError ? 'rgba(232,17,35,0.45)' : 'rgba(245,159,0,0.35)'}
  style:background-color={isError ? 'rgba(232,17,35,0.10)' : 'rgba(245,159,0,0.08)'}
  style:color={isError ? '#f87171' : '#f5a623'}
  role={isError ? 'alert' : 'status'}
>
  {#if isError}
    <IconAlertTriangle size={14} class="mt-0.5 shrink-0" />
  {:else}
    <IconInfoCircle size={14} class="mt-0.5 shrink-0" />
  {/if}
  <span class="flex-1">{message}</span>
  {#if onDismiss}
    <button
      class="shrink-0 rounded px-1 text-gray-400 transition-colors hover:text-gray-200"
      aria-label="Dismiss notice"
      onclick={onDismiss}
    >✕</button>
  {/if}
  {@render actions?.()}
</div>

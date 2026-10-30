<script lang="ts">
  import {
    ExclamationCircleOutline,
    CheckCircleOutline,
    InfoCircleOutline,
  } from 'flowbite-svelte-icons';
  import MetroModal from './ui/MetroModal.svelte';

  export type NotificationType = 'error' | 'success' | 'warning' | 'info';

  let {
    open = $bindable(false),
    type = 'info' as NotificationType,
    title = '',
    message = '',
  }: {
    open: boolean;
    type?: NotificationType;
    title?: string;
    message?: string;
  } = $props();

  const APPEARANCE: Record<NotificationType, { accent: string; color: string }> = {
    error: { accent: 'var(--metro-red)', color: 'var(--metro-red)' },
    warning: { accent: 'var(--metro-yellow)', color: 'var(--metro-yellow)' },
    success: { accent: 'var(--metro-green)', color: 'var(--metro-green)' },
    info: { accent: 'var(--metro-blue)', color: 'var(--metro-blue)' },
  };

  const appearance = $derived(APPEARANCE[type]);
</script>

<MetroModal bind:open title={title || 'Notice'} size="xs" accent={appearance.accent}>
  <div class="flex flex-col items-center gap-3">
    {#if type === 'success'}
      <CheckCircleOutline class="h-12 w-12" style="color: {appearance.color}" />
    {:else if type === 'info'}
      <InfoCircleOutline class="h-12 w-12" style="color: {appearance.color}" />
    {:else}
      <ExclamationCircleOutline class="h-12 w-12" style="color: {appearance.color}" />
    {/if}

    {#if message}
      <p class="text-center text-[13px] leading-relaxed" style="color: var(--metro-text-secondary)">{message}</p>
    {/if}
  </div>

  {#snippet footer()}
    <button type="button" class="metro-btn-primary" onclick={() => open = false}>Dismiss</button>
  {/snippet}
</MetroModal>

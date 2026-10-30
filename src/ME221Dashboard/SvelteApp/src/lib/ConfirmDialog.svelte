<script lang="ts">
  import MetroModal from './ui/MetroModal.svelte';

  let { open, title, message, confirmLabel = 'Confirm', cancelLabel = 'Cancel', onConfirm, onCancel, variant = 'default' as 'danger' | 'warning' | 'default' }: {
    open: boolean;
    title: string;
    message: string;
    confirmLabel?: string;
    cancelLabel?: string;
    onConfirm: () => void;
    onCancel: () => void;
    variant?: 'danger' | 'warning' | 'default';
  } = $props();

  const ACCENTS = {
    danger: 'var(--metro-red)',
    warning: 'var(--metro-yellow)',
    default: 'var(--metro-blue)',
  } as const;

  const confirmClass = $derived(
    variant === 'danger' ? 'metro-btn-danger'
      : variant === 'warning' ? 'metro-btn-primary'
        : 'metro-btn-primary',
  );
  const accent = $derived(variant === 'danger' ? ACCENTS.danger : ACCENTS[variant]);
</script>

<MetroModal bind:open {title} size="xs" {accent} onclose={onCancel}>
  <p class="mb-5 text-[13px] leading-relaxed" style="color: var(--metro-text-secondary)">{message}</p>

  {#snippet footer()}
    <button type="button" class="metro-btn-secondary" onclick={onCancel}>{cancelLabel}</button>
    <button type="button" class={confirmClass} onclick={onConfirm}>{confirmLabel}</button>
  {/snippet}
</MetroModal>

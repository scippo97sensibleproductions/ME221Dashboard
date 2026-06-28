<script lang="ts">
  import { Modal, Button } from 'flowbite-svelte';

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

  let btnClass = $derived.by(() => {
    return {
      danger: '!bg-red-600 hover:!bg-red-500 !text-white border-red-600',
      warning: '!bg-amber-600 hover:!bg-amber-500 !text-white border-amber-600',
      default: '!bg-cyan-600 hover:!bg-cyan-500 !text-white border-cyan-600',
    }[variant];
  });
</script>

<Modal form bind:open size="xs" placement="center" outsideclose={true} class="backdrop:bg-gray-900/80" ontoggle={(e) => { if (e.newState === 'closed') onCancel(); }}>
  <div class="text-center">
    <h3 class="mb-2 text-base font-semibold text-gray-100">{title}</h3>
    <p class="mb-5 text-sm text-gray-400">{message}</p>
    <div class="flex justify-center gap-3">
      <Button color="alternative" class="!border-gray-600 !bg-gray-700 !text-gray-300 hover:!bg-gray-600" onclick={onCancel}>
        {cancelLabel}
      </Button>
      <Button type="submit" class={btnClass} onclick={onConfirm}>
        {confirmLabel}
      </Button>
    </div>
  </div>
</Modal>

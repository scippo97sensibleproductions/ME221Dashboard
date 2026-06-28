<script lang="ts">
  import { Modal, Button } from 'flowbite-svelte';
  import {
    ExclamationCircleOutline,
    CheckCircleOutline,
    InfoCircleOutline,
  } from 'flowbite-svelte-icons';

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

  const config: Record<NotificationType, {
    icon: typeof ExclamationCircleOutline;
    iconClass: string;
    btnClass: string;
  }> = {
    error: {
      icon: ExclamationCircleOutline,
      iconClass: 'text-red-400',
      btnClass: '!bg-red-600 !text-white hover:!bg-red-500 border-red-600',
    },
    warning: {
      icon: ExclamationCircleOutline,
      iconClass: 'text-amber-400',
      btnClass: '!bg-amber-600 !text-white hover:!bg-amber-500 border-amber-600',
    },
    success: {
      icon: CheckCircleOutline,
      iconClass: 'text-green-400',
      btnClass: '!bg-green-600 !text-white hover:!bg-green-500 border-green-600',
    },
    info: {
      icon: InfoCircleOutline,
      iconClass: 'text-blue-400',
      btnClass: '!bg-blue-600 !text-white hover:!bg-blue-500 border-blue-600',
    },
  };

  let c = $derived(config[type]);
</script>

<Modal form bind:open size="xs" placement="center" outsideclose={true} class="backdrop:bg-gray-900/80">
  <div class="text-center">
    {#if type === 'error' || type === 'warning'}
      <ExclamationCircleOutline class="mx-auto mb-4 h-14 w-14 {c.iconClass}" />
    {:else if type === 'success'}
      <CheckCircleOutline class="mx-auto mb-4 h-14 w-14 {c.iconClass}" />
    {:else}
      <InfoCircleOutline class="mx-auto mb-4 h-14 w-14 {c.iconClass}" />
    {/if}

    {#if title}
      <h3 class="mb-3 text-lg font-semibold text-white">{title}</h3>
    {/if}

    {#if message}
      <p class="mb-6 text-sm leading-relaxed text-gray-300">{message}</p>
    {/if}

    <Button type="submit" value="dismiss" color="alternative" class={c.btnClass}>
      Dismiss
    </Button>
  </div>
</Modal>

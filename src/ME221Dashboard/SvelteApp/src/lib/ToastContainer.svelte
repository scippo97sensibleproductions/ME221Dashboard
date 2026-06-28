<script lang="ts">
  import { Toast } from 'flowbite-svelte';
  import { fly } from 'svelte/transition';
  import { getToasts, type ToastType } from './toasts.svelte';
  import {
    IconCheck,
    IconExclamationMark,
    IconAlertTriangle,
    IconInfoCircle,
  } from '@tabler/icons-svelte';

  const colorMap: Record<ToastType, 'green' | 'red' | 'yellow' | 'cyan'> = {
    success: 'green',
    error: 'red',
    warning: 'yellow',
    info: 'cyan',
  };

  const iconMap: Record<ToastType, typeof IconCheck> = {
    success: IconCheck,
    error: IconExclamationMark,
    warning: IconAlertTriangle,
    info: IconInfoCircle,
  };
</script>

<div class="pointer-events-none fixed inset-x-0 top-4 z-[9999] flex flex-col items-center gap-2 px-4">
  {#each getToasts() as t (t.id)}
    <div class="pointer-events-auto">
      <Toast
        color={colorMap[t.type]}
        transition={fly}
        transitionParams={{ duration: 300, y: -20 }}
        dismissable={false}
        class="!pointer-events-auto"
      >
        {#snippet icon()}
          {@const Icon = iconMap[t.type]}
          <Icon size={20} />
        {/snippet}
        {t.message}
      </Toast>
    </div>
  {/each}
</div>

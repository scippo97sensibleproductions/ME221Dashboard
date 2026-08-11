<script lang="ts">
  import { onDestroy } from 'svelte';
  import { Toast } from 'flowbite-svelte';
  import { fly } from 'svelte/transition';
  import { getToasts, type Toast as ToastItem } from './toasts.svelte';
  import { warningToasts } from './warningToasts';
  import {
    IconCheck,
    IconExclamationMark,
    IconAlertTriangle,
    IconInfoCircle,
  } from '@tabler/icons-svelte';

  let { onNavigate = () => {}, gateBlocked = false }: {
    onNavigate?: (page: string, dataId?: number) => void;
    gateBlocked?: boolean;
  } = $props();
  const colorMap: Record<string, 'green' | 'red' | 'yellow' | 'cyan'> = {
    success: 'green',
    error: 'red',
    warning: 'yellow',
    info: 'cyan',
  };

  const iconMap: Record<string, typeof IconCheck> = {
    success: IconCheck,
    error: IconExclamationMark,
    warning: IconAlertTriangle,
    info: IconInfoCircle,
  };

  let toastEls: Record<number, HTMLElement> = {};
  let lastFocusedId: number | null = null;

  function isGeneric(t: ToastItem): boolean {
    return t.meta === undefined;
  }

  function isHiddenRegion(t: ToastItem): boolean {
    return !!t.meta && !t.meta.displayed && t.meta.announced && !t.meta.firstRunQueued;
  }

  function registerEl(node: HTMLElement, id: number) {
    toastEls[id] = node;
    return {
      destroy() {
        delete toastEls[id];
      },
    };
  }

  function onWarningActivate(id: number) {
    const toast = getToasts().find(t => t.id === id);
    if (warningToasts.handleTap(id) && toast?.meta) {
      onNavigate('dashboard', toast.meta.dataId);
    }
    if (lastFocusedId === id) lastFocusedId = null;
  }

  function onWarningKeydown(e: KeyboardEvent, id: number) {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onWarningActivate(id);
    }
  }

  function focusToast(id: number) {
    const el = toastEls[id];
    if (el && !gateBlocked) {
      el.focus();
      lastFocusedId = id;
    }
  }

  function manageFocus(list: ToastItem[]) {
    const visible = list.filter(t => t.meta?.displayed);
    const focusedStillThere = visible.some(t => t.id === lastFocusedId);
    if (!focusedStillThere && visible.length > 0) {
      focusToast(visible[0].id);
    }
  }

  $effect(() => {
    const list = getToasts();
    manageFocus(list);
  });

  onDestroy(() => {
    // no interval — the per-frame tick in App.svelte drives the controller
  });
</script>

<div
  class="pointer-events-none fixed inset-x-0 top-4 z-[60] flex flex-col items-center gap-2 px-4"
  inert={gateBlocked}
>
  {#each getToasts() as t (t.id)}
    {#if isGeneric(t)}
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
    {:else if t.meta?.displayed}
      <div class="pointer-events-auto">
        <button
          type="button"
          tabindex="0"
          use:registerEl={t.id}
          class="flex items-center gap-2 rounded border px-3 py-2 text-left text-xs font-medium transition-colors"
          class:border-amber-600={t.meta.evictionClass === 'escalation'}
          class:border-gray-700={t.meta.evictionClass !== 'escalation'}
          aria-label={warningToasts.getAnnouncedText(t.id) ?? t.message}
          onclick={() => onWarningActivate(t.id)}
          onkeydown={(e) => onWarningKeydown(e, t.id)}
        >
          <IconAlertTriangle size={16} style="color: {t.meta.evictionClass === 'escalation' ? '#ef4444' : '#f59e0b'};" />
          <span>{t.message}</span>
        </button>
        {#if warningToasts.getAnnouncedText(t.id) !== undefined}
          <span class="sr-only" aria-live="polite">{warningToasts.getAnnouncedText(t.id)}</span>
        {/if}
      </div>
    {:else if isHiddenRegion(t)}
      <span class="sr-only" aria-live="polite">{warningToasts.getAnnouncedText(t.id)}</span>
    {/if}
  {/each}
</div>

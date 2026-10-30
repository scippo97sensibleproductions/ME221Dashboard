<script lang="ts">
  import type { Snippet } from 'svelte';
  import { Modal } from 'flowbite-svelte';
  import type { ModalProps } from 'flowbite-svelte';

  // Shared Metro dialog shell: near-black backdrop, accent-bar uppercase title,
  // and an optional right-aligned footer. Callers pass `footer` as a snippet and
  // keep all content inside the default slot.
  let {
    open = $bindable(false),
    title,
    size = 'md',
    placement = 'center',
    accent = 'var(--metro-blue)',
    form = false,
    onclose,
    children,
    footer,
  }: {
    open?: boolean;
    title: string;
    size?: ModalProps['size'];
    placement?: ModalProps['placement'];
    /** Accent color of the title bar; keep one accent per dialog. */
    accent?: string;
    form?: boolean;
    /** Called when the dialog closes (backdrop, Escape, or close button). */
    onclose?: () => void;
    children: Snippet;
    footer?: Snippet;
  } = $props();

  function handleToggle(event: { newState: string }) {
    if (event.newState === 'closed') onclose?.();
  }
</script>

{#snippet headerSlot()}
  <div class="flex w-full items-center justify-between border-l-4 pl-3" style="border-left-color: {accent};">
    <h2 class="text-[13px] font-extrabold uppercase tracking-wider" style="color: var(--metro-text)">{title}</h2>
  </div>
{/snippet}

{#snippet footerSlot()}
  <div class="flex w-full justify-end gap-2">
    {@render footer?.()}
  </div>
{/snippet}

<Modal
  bind:open
  {size}
  {placement}
  {form}
  outsideclose
  class="backdrop:bg-black/80"
  ontoggle={handleToggle}
  header={headerSlot}
  {...footer ? { footer: footerSlot } : {}}
>
  {@render children()}
</Modal>

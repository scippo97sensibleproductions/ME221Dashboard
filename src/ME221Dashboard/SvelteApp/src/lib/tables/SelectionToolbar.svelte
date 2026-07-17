<script lang="ts">
  import { IconCopy, IconClipboard, IconTransform, IconX } from '@tabler/icons-svelte';

  let { selection, onCopy, onPaste, onTransform, onClose }: {
    selection: { startRow: number; startCol: number; endRow: number; endCol: number };
    onCopy: () => void;
    onPaste: () => void;
    onTransform: () => void;
    onClose: () => void;
  } = $props();

  let cellCount = $derived(
    (Math.abs(selection.endRow - selection.startRow) + 1) * (Math.abs(selection.endCol - selection.startCol) + 1)
  );
</script>

<div
  class="fixed inset-x-0 bottom-0 z-[70] border-t px-3 py-2"
  style="border-color: var(--metro-border); background-color: var(--metro-card);"
>
  <div class="mx-auto flex max-w-lg items-center gap-1">
    <span class="shrink-0 px-2 py-0.5 text-[9px] font-bold uppercase tracking-wider text-[var(--metro-text-muted)]">
      {cellCount} cell{cellCount !== 1 ? 's' : ''}
    </span>
    <div class="mx-1 h-5 w-px shrink-0" style="background-color: var(--metro-border);"></div>
    <button
      class="flex h-8 shrink-0 items-center gap-1 px-2 text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-white"
      onclick={onCopy}
      title="Copy selection"
    >
      <IconCopy size={14} />
      <span>Copy</span>
    </button>
    <button
      class="flex h-8 shrink-0 items-center gap-1 px-2 text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-white"
      onclick={onPaste}
      title="Paste into selection"
    >
      <IconClipboard size={14} />
      <span>Paste</span>
    </button>
    <div class="mx-1 h-5 w-px shrink-0" style="background-color: var(--metro-border);"></div>
    <button
      class="flex h-8 shrink-0 items-center gap-1 px-2 text-[11px] font-bold uppercase tracking-wider text-white transition-colors duration-150"
      style="background-color: var(--metro-orange);"
      onclick={onTransform}
      title="Scale, offset, fill, interpolate, smooth, clamp"
    >
      <IconTransform size={14} />
      <span>Transform</span>
    </button>
    <button
      class="ml-auto flex h-8 items-center gap-1 px-2 text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-muted)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-[var(--metro-text-secondary)]"
      onclick={onClose}
      title="Clear selection"
    >
      <IconX size={12} />
      <span>Clear</span>
    </button>
  </div>
</div>

<script lang="ts">
  import { IconCopy, IconClipboard, IconTransform, IconSelectAll, IconRowInsertTop, IconColumnInsertRight, IconTarget } from '@tabler/icons-svelte';

  let { open, x, y, items, onSelect, onClose }: {
    open: boolean;
    x: number;
    y: number;
    items: { label: string; icon?: typeof IconCopy; action: string; disabled?: boolean }[];
    onSelect: (action: string) => void;
    onClose: () => void;
  } = $props();

  let menuEl = $state<HTMLDivElement>();

  $effect(() => {
    if (open && menuEl) {
      const rect = menuEl.getBoundingClientRect();
      const vw = window.innerWidth;
      const vh = window.innerHeight;
      if (x + rect.width > vw) x = vw - rect.width - 4;
      if (y + rect.height > vh) y = vh - rect.height - 4;
      if (x < 0) x = 4;
      if (y < 0) y = 4;
    }
  });

  function handleClickOutside(e: MouseEvent) {
    if (menuEl && !menuEl.contains(e.target as Node)) {
      onClose();
    }
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Escape') {
      e.preventDefault();
      onClose();
    }
  }

  $effect(() => {
    if (open) {
      document.addEventListener('mousedown', handleClickOutside, true);
      document.addEventListener('keydown', handleKeydown, true);
      return () => {
        document.removeEventListener('mousedown', handleClickOutside, true);
        document.removeEventListener('keydown', handleKeydown, true);
      };
    }
  });
</script>

{#if open}
  <div
    bind:this={menuEl}
    class="fixed z-[90] min-w-[180px] border py-1"
    style="left: {x}px; top: {y}px; background-color: var(--metro-card); border-color: var(--metro-border);"
  >
    {#each items as item}
      <button
        class="flex w-full items-center gap-2.5 px-3 py-1.5 text-left text-[12px] transition-colors duration-100 disabled:opacity-40"
        style="color: var(--metro-text-secondary);"
        disabled={item.disabled}
        onclick={() => { onSelect(item.action); onClose(); }}
        onmouseenter={(e) => { (e.currentTarget as HTMLElement).style.backgroundColor = 'var(--metro-hover)'; (e.currentTarget as HTMLElement).style.color = 'var(--metro-text)'; }}
        onmouseleave={(e) => { (e.currentTarget as HTMLElement).style.backgroundColor = 'transparent'; (e.currentTarget as HTMLElement).style.color = 'var(--metro-text-secondary)'; }}
      >
        {#if item.icon}
          <item.icon size={14} />
        {/if}
        {item.label}
      </button>
    {/each}
  </div>
{/if}

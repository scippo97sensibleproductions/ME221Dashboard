<script lang="ts">
  import type { DataLinkDefinition } from '../HybridBridgeTypes';

  interface Props {
    link: DataLinkDefinition;
  }

  let { link }: Props = $props();

  let open = $state(false);

  function handleTap(e: MouseEvent) {
    e.stopPropagation();
    open = !open;
  }

  function handleClose() {
    open = false;
  }
</script>

<button
  class="flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-left transition-colors hover:bg-gray-700/40 border border-transparent hover:border-gray-600/40"
  onclick={handleTap}
>
  <div class="flex h-8 w-12 shrink-0 items-center justify-center rounded border border-gray-700 bg-gray-800/50">
    <span class="text-[10px] font-bold text-gray-500">{link.measureUnit || '?'}</span>
  </div>
  <div class="min-w-0 flex-1">
    <div class="text-xs font-medium text-gray-100 truncate">{link.name}</div>
    <div class="text-[10px] text-gray-500 mt-0.5">{link.category}</div>
  </div>
</button>

{#if open}
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="fixed inset-0 z-50" onclick={handleClose} onkeydown={(e) => { if (e.key === 'Escape') open = false; }} role="presentation">
    <!-- Popover positioned near-center for now; full positioning deferred -->
    <div class="absolute left-1/2 top-1/4 -translate-x-1/2 w-72 rounded-xl border border-gray-600 bg-gray-800 p-4 shadow-2xl" onclick={(e) => e.stopPropagation()} onkeydown={(e) => e.stopPropagation()} role="dialog" aria-modal="true" tabindex="-1">
      <div class="flex items-center justify-between mb-3">
        <h4 class="text-sm font-semibold text-gray-100 truncate">{link.name}</h4>
        <button
          class="shrink-0 p-1 rounded text-gray-500 hover:text-gray-200 hover:bg-gray-700 ml-2"
          onclick={handleClose}
          aria-label="Close"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 6 6 18"/><path d="m6 6 12 12"/></svg>
        </button>
      </div>
      <div class="space-y-2 text-xs">
        <div class="flex items-center justify-between">
          <span class="text-gray-500">Category</span>
          <span class="text-gray-200">{link.category}</span>
        </div>
        <div class="flex items-center justify-between">
          <span class="text-gray-500">Measure Unit</span>
          <span class="text-gray-200">{link.measureUnit || '—'}</span>
        </div>
        <div class="flex items-center justify-between">
          <span class="text-gray-500">ID</span>
          <span class="text-gray-400 tabular-nums">{link.id}</span>
        </div>
      </div>
    </div>
  </div>
{/if}
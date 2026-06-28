<script lang="ts">
  import { IconX } from '@tabler/icons-svelte';

  let { open, title, value, min, max, step, onClose, onConfirm }: {
    open: boolean;
    title: string;
    value: number;
    min?: number;
    max?: number;
    step?: number;
    onClose: () => void;
    onConfirm: (newValue: number) => void;
  } = $props();

  let editValue = $state('');
  let error = $state('');

  $effect(() => {
    if (open) {
      editValue = value.toFixed(2);
      error = '';
    }
  });

  function handleConfirm() {
    const parsed = parseFloat(editValue);
    if (isNaN(parsed)) {
      error = 'Invalid number';
      return;
    }
    if (min !== undefined && parsed < min) {
      error = `Value must be at least ${min.toFixed(2)}`;
      return;
    }
    if (max !== undefined && parsed > max) {
      error = `Value must be at most ${max.toFixed(2)}`;
      return;
    }
    onConfirm(parsed);
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Enter') handleConfirm();
    else if (e.key === 'Escape') onClose();
  }

  function increment() {
    const s = step ?? 0.01;
    const parsed = parseFloat(editValue);
    if (!isNaN(parsed)) {
      editValue = (parsed + s).toFixed(2);
      error = '';
    }
  }

  function decrement() {
    const s = step ?? 0.01;
    const parsed = parseFloat(editValue);
    if (!isNaN(parsed)) {
      editValue = (parsed - s).toFixed(2);
      error = '';
    }
  }
</script>

{#if open}
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60" role="dialog" aria-modal="true" tabindex="-1" onclick={onClose} onkeydown={handleKeydown}>
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <div class="w-80 rounded-xl border border-gray-700 bg-gray-800 p-4 shadow-2xl" onclick={(e) => e.stopPropagation()} onkeydown={(e) => e.stopPropagation()}>
      <div class="mb-3 flex items-center justify-between">
        <h3 class="text-sm font-semibold text-gray-100">{title}</h3>
        <button class="text-gray-400 hover:text-gray-200" onclick={onClose}>
          <IconX size={16} />
        </button>
      </div>

      <div class="flex items-center gap-2 mb-2">
        <button
          class="flex h-8 w-8 items-center justify-center rounded-lg border border-gray-600 bg-gray-700 text-gray-300 hover:bg-gray-600 active:bg-gray-500"
          onclick={decrement}
        >
          -
        </button>
        <input
          type="text"
          bind:value={editValue}
          onkeydown={handleKeydown}
          class="flex-1 rounded-lg border border-gray-600 bg-gray-900 px-3 py-2 text-center text-lg font-mono text-gray-100 outline-none focus:border-cyan-500"
        />
        <button
          class="flex h-8 w-8 items-center justify-center rounded-lg border border-gray-600 bg-gray-700 text-gray-300 hover:bg-gray-600 active:bg-gray-500"
          onclick={increment}
        >
          +
        </button>
      </div>

      {#if min !== undefined || max !== undefined}
        <div class="mb-2 text-[10px] text-gray-500 text-center">
          {min !== undefined ? `Min: ${min.toFixed(2)}` : ''}{min !== undefined && max !== undefined ? ' | ' : ''}{max !== undefined ? `Max: ${max.toFixed(2)}` : ''}
        </div>
      {/if}

      {#if error}
        <div class="mb-2 text-xs text-red-400 text-center">{error}</div>
      {/if}

      <div class="flex gap-2 mt-3">
        <button
          class="flex-1 rounded-lg border border-gray-600 bg-gray-700 px-3 py-2 text-sm text-gray-300 hover:bg-gray-600"
          onclick={onClose}
        >
          Cancel
        </button>
        <button
          class="flex-1 rounded-lg bg-cyan-600 px-3 py-2 text-sm font-medium text-white hover:bg-cyan-500"
          onclick={handleConfirm}
        >
          Set
        </button>
      </div>
    </div>
  </div>
{/if}

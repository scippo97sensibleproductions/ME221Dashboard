<script lang="ts">
  import { Modal, Button } from 'flowbite-svelte';

  let { open, onApply, onClose }: {
    open: boolean;
    onApply: (operation: string, params: Record<string, number>) => void;
    onClose: () => void;
  } = $props();

  let mode = $state<'multiply' | 'add' | 'clamp'>('multiply');
  let multiplyValue = $state(100);
  let addValue = $state(0);
  let clampMin = $state(0);
  let clampMax = $state(100);

  function handleApply() {
    if (mode === 'multiply') {
      onApply('multiply', { factor: multiplyValue / 100 });
    } else if (mode === 'add') {
      onApply('add', { offset: addValue });
    } else {
      onApply('clamp', { min: Math.min(clampMin, clampMax), max: Math.max(clampMin, clampMax) });
    }
    onClose();
  }
</script>

{#if open}
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div
    class="fixed inset-0 z-[80]"
    style="background-color: rgba(0,0,0,0.8);"
    role="button"
    tabindex="-1"
    onclick={onClose}
    onkeydown={(e) => { if (e.key === 'Escape') { e.preventDefault(); onClose(); } }}
  ></div>

  <div
    class="fixed inset-x-0 bottom-0 z-[81] border-t"
    style="border-color: var(--metro-border); background-color: var(--metro-card);"
  >
    <div class="mx-auto max-w-lg p-4">
      <h3 class="mb-4 text-[13px] font-bold uppercase tracking-wider text-white">Scalar Operation</h3>

      <!-- Mode tabs -->
      <div class="mb-4 flex gap-2">
        <button
          class="flex-1 py-2 text-[11px] font-bold uppercase tracking-wider transition-colors duration-150"
          style={mode === 'multiply'
            ? 'background-color: var(--metro-orange); color: white;'
            : 'background-color: var(--metro-surface); color: var(--metro-text-secondary); border: 1px solid var(--metro-border);'}
          onclick={() => { mode = 'multiply'; }}
        >
          Multiply %
        </button>
        <button
          class="flex-1 py-2 text-[11px] font-bold uppercase tracking-wider transition-colors duration-150"
          style={mode === 'add'
            ? 'background-color: var(--metro-orange); color: white;'
            : 'background-color: var(--metro-surface); color: var(--metro-text-secondary); border: 1px solid var(--metro-border);'}
          onclick={() => { mode = 'add'; }}
        >
          Add Offset
        </button>
        <button
          class="flex-1 py-2 text-[11px] font-bold uppercase tracking-wider transition-colors duration-150"
          style={mode === 'clamp'
            ? 'background-color: var(--metro-orange); color: white;'
            : 'background-color: var(--metro-surface); color: var(--metro-text-secondary); border: 1px solid var(--metro-border);'}
          onclick={() => { mode = 'clamp'; }}
        >
          Clamp
        </button>
      </div>

      <!-- Inputs -->
      {#if mode === 'multiply'}
        <div class="mb-4">
          <label for="multiply-input" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Multiply by (%)</label>
          <input id="multiply-input" bind:value={multiplyValue} type="number" inputmode="decimal" class="metro-input w-full font-mono" />
          <div class="mt-1 text-[10px] text-[var(--metro-text-muted)]">e.g. 105 = +5%, 97 = -3%</div>
        </div>
      {:else if mode === 'add'}
        <div class="mb-4">
          <label for="add-input" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Add offset</label>
          <input id="add-input" bind:value={addValue} type="number" inputmode="decimal" class="metro-input w-full font-mono" />
          <div class="mt-1 text-[10px] text-[var(--metro-text-muted)]">e.g. +2.5 or -1.0</div>
        </div>
      {:else}
        <div class="mb-4 grid grid-cols-2 gap-3">
          <div>
            <label for="clamp-min" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Min</label>
            <input id="clamp-min" bind:value={clampMin} type="number" inputmode="decimal" class="metro-input w-full font-mono" />
          </div>
          <div>
            <label for="clamp-max" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Max</label>
            <input id="clamp-max" bind:value={clampMax} type="number" inputmode="decimal" class="metro-input w-full font-mono" />
          </div>
        </div>
      {/if}

      <button class="metro-btn-primary w-full" onclick={handleApply}>
        Apply
      </button>
    </div>
  </div>
{/if}

<script lang="ts">
  import { onMount, onDestroy } from 'svelte';

  let { open, tableName = '', label, value, originalValue, increment, editMode = 'output', axisName = '', onApply, onRevert, onClose }: {
    open: boolean;
    tableName?: string;
    label: string;
    value: number;
    originalValue: number;
    increment: number;
    editMode?: string;
    axisName?: string;
    onApply: (newValue: number) => void;
    onRevert: () => void;
    onClose: () => void;
  } = $props();

  let isMobile = $state(false);
  let mediaQuery: MediaQueryList | null = null;

  function checkMobile() {
    isMobile = typeof window !== 'undefined' && window.matchMedia('(max-width: 767px)').matches;
  }

  onMount(() => {
    checkMobile();
    mediaQuery = window.matchMedia('(max-width: 767px)');
    mediaQuery.addEventListener('change', checkMobile);
  });

  onDestroy(() => {
    mediaQuery?.removeEventListener('change', checkMobile);
  });

  let inputEl = $state<HTMLInputElement>();
  let inputValue = $state('');
  let snapEnabled = $state(false);

  let isAxisEdit = $derived(editMode === 'input0' || editMode === 'input1');

  function getSnapGrid(): number {
    const name = axisName.toUpperCase();
    if (name.includes('RPM')) return 250;
    if (name.includes('MAP') || name.includes('LOAD') || name.includes('KPA')) return 5;
    return increment;
  }

  function snapValue(val: number): number {
    if (!snapEnabled || !isAxisEdit) return val;
    const grid = getSnapGrid();
    return Math.round(val / grid) * grid;
  }

  $effect(() => {
    if (open) {
      inputValue = value.toFixed(2);
      if (!isMobile && inputEl) {
        inputEl.focus();
        inputEl.select();
      }
    }
  });

  function step(delta: number) {
    const base = parseFloat(inputValue);
    if (isNaN(base)) return;
    const newVal = Math.round((base + delta) * 100) / 100;
    inputValue = newVal.toFixed(2);
  }

  function applyInput() {
    const n = parseFloat(inputValue);
    if (!isNaN(n)) {
      onApply(snapValue(n));
      onClose();
    }
  }

  function handleInputKeydown(e: KeyboardEvent) {
    if (e.key === 'Enter') { e.preventDefault(); applyInput(); }
    if (e.key === 'Escape') { e.preventDefault(); onClose(); }
    if (e.key === 'Tab') { e.preventDefault(); applyInput(); }
  }
</script>

{#if open}
  {#if isMobile}
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <div
      class="fixed inset-0 z-50"
      style="background-color: rgba(0,0,0,0.8);"
      role="button"
      tabindex="-1"
      onclick={onClose}
      onkeydown={(e) => { if (e.key === 'Escape') { e.preventDefault(); onClose(); } }}
    ></div>
    <div
      class="fixed inset-x-0 bottom-0 z-50 border-t p-4"
      style="border-color: var(--metro-border); background-color: var(--metro-card);"
    >
      <div class="mb-3 flex items-center justify-between">
        <div>
          <div class="text-[10px] uppercase tracking-wider text-[var(--metro-text-secondary)]">{tableName}</div>
          <div class="text-[13px] font-bold text-white">{label}</div>
        </div>
        <button
          class="flex h-8 w-8 items-center justify-center text-[var(--metro-text-secondary)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-white"
          onclick={onClose}
        >
          &#x2715;
        </button>
      </div>

      <!-- Value input -->
      <div class="mb-4">
        <div class="mb-1 text-center text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">{label}</div>
        <input
          bind:value={inputValue}
          type="number"
          inputmode="decimal"
          step="any"
          class="metro-input w-full px-4 py-3 text-center text-2xl font-bold font-mono"
          onkeydown={handleInputKeydown}
        />
        {#if Math.abs(parseFloat(inputValue) - originalValue) > 0.01}
          <div class="mt-1 text-center text-[10px] text-[var(--metro-text-muted)]">Original: {originalValue.toFixed(2)}</div>
        {/if}
      </div>

      <!-- Step buttons -->
      <div class="grid grid-cols-3 gap-2 mb-3">
        <button
          class="border border-[var(--metro-red)]/30 bg-[var(--metro-surface)] py-3 text-[13px] font-bold text-[var(--metro-red)] transition-colors duration-150 active:bg-[var(--metro-red)]/20"
          onclick={() => step(-10)}
        >-10</button>
        <button
          class="border border-[var(--metro-red)]/30 bg-[var(--metro-surface)] py-3 text-[13px] font-bold text-[var(--metro-red)] transition-colors duration-150 active:bg-[var(--metro-red)]/20"
          onclick={() => step(-1)}
        >-1</button>
        <button
          class="border border-[var(--metro-red)]/30 bg-[var(--metro-surface)] py-3 text-[13px] font-bold text-[var(--metro-red)] transition-colors duration-150 active:bg-[var(--metro-red)]/20"
          onclick={() => step(-increment)}
        >-{increment}</button>
        <button
          class="border bg-[var(--metro-surface)] py-3 text-[13px] font-bold transition-colors duration-150 active:bg-[var(--metro-orange)]/20"
          style="border-color: var(--metro-orange); color: var(--metro-orange);"
          onclick={() => step(increment)}
        >+{increment}</button>
        <button
          class="border bg-[var(--metro-surface)] py-3 text-[13px] font-bold transition-colors duration-150 active:bg-[var(--metro-orange)]/20"
          style="border-color: var(--metro-orange); color: var(--metro-orange);"
          onclick={() => step(1)}
        >+1</button>
        <button
          class="border bg-[var(--metro-surface)] py-3 text-[13px] font-bold transition-colors duration-150 active:bg-[var(--metro-orange)]/20"
          style="border-color: var(--metro-orange); color: var(--metro-orange);"
          onclick={() => step(10)}
        >+10</button>
      </div>

      {#if isAxisEdit}
        <div class="mb-3 flex items-center gap-2">
          <button
            class="flex items-center gap-1.5 border px-2.5 py-1.5 text-[11px] font-bold uppercase tracking-wider transition-colors duration-150"
            style={snapEnabled
              ? 'border-color: var(--metro-orange); background-color: var(--metro-orange); color: white;'
              : 'border-color: var(--metro-input-border); background-color: var(--metro-surface); color: var(--metro-text-secondary);'}
            onclick={() => { snapEnabled = !snapEnabled; }}
          >
            Snap ({getSnapGrid()})
          </button>
          <span class="text-[10px] text-[var(--metro-text-muted)]">Round to grid</span>
        </div>
      {/if}

      <!-- Actions -->
      <div class="flex gap-2">
        <button class="metro-btn-secondary flex-1" onclick={onRevert}>
          Revert
        </button>
        <button class="metro-btn-primary flex-1" onclick={applyInput}>
          Apply
        </button>
      </div>
    </div>
  {:else}
    <!-- Desktop: inline floating toolbar -->
    <div
      class="fixed z-50 flex items-center gap-1 border px-3 py-2"
      style="top: 100px; left: 50%; transform: translateX(-50%); border-color: var(--metro-orange); background-color: var(--metro-card);"
    >
      <input
        bind:this={inputEl}
        bind:value={inputValue}
        type="number"
        step="any"
        class="metro-input w-20 px-2 py-1 text-center text-[13px] font-bold font-mono"
        onkeydown={handleInputKeydown}
      />
      <div class="mx-1 h-5 w-px" style="background-color: var(--metro-border);"></div>
      <button class="rounded px-1.5 py-0.5 text-[11px] font-bold text-[var(--metro-red)] transition-colors duration-150 hover:bg-[var(--metro-hover)]" onclick={() => step(-10)}>-10</button>
      <button class="rounded px-1.5 py-0.5 text-[11px] font-bold text-[var(--metro-red)] transition-colors duration-150 hover:bg-[var(--metro-hover)]" onclick={() => step(-1)}>-1</button>
      <button class="rounded px-1.5 py-0.5 text-[11px] font-bold text-[var(--metro-red)] transition-colors duration-150 hover:bg-[var(--metro-hover)]" onclick={() => step(-increment)}>-{increment}</button>
      <button class="rounded px-1.5 py-0.5 text-[11px] font-bold transition-colors duration-150 hover:bg-[var(--metro-hover)]" style="color: var(--metro-orange);" onclick={() => step(increment)}>+{increment}</button>
      <button class="rounded px-1.5 py-0.5 text-[11px] font-bold transition-colors duration-150 hover:bg-[var(--metro-hover)]" style="color: var(--metro-orange);" onclick={() => step(1)}>+1</button>
      <button class="rounded px-1.5 py-0.5 text-[11px] font-bold transition-colors duration-150 hover:bg-[var(--metro-hover)]" style="color: var(--metro-orange);" onclick={() => step(10)}>+10</button>
      <div class="mx-1 h-5 w-px" style="background-color: var(--metro-border);"></div>
      <button
        class="flex h-6 w-6 items-center justify-center text-white transition-colors duration-150"
        style="background-color: var(--metro-orange);"
        onclick={applyInput}
        title="Apply"
      >
        &#x2713;
      </button>
      <button
        class="flex h-6 w-6 items-center justify-center text-[var(--metro-text-secondary)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-white"
        onclick={onClose}
        title="Close"
      >
        &#x2715;
      </button>
    </div>
  {/if}
{/if}

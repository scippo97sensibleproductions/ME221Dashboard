<script lang="ts">
  import { IconPercentage, IconMath, IconAdjustments, IconArrowsCross, IconWaveSine, IconMinus, IconPlayerPlay, IconFileImport, IconFileExport, IconArrowLeft } from '@tabler/icons-svelte';

  let { open, cellCount, selectionType = 'output', onApply, onImportYaml, onExportYaml, onClose }: {
    open: boolean;
    cellCount: number;
    selectionType?: 'output' | 'input0' | 'input1';
    onApply: (operation: string, params: Record<string, number>) => void;
    onImportYaml: () => void;
    onExportYaml: () => void;
    onClose: () => void;
  } = $props();

  type Op = 'scale' | 'offset' | 'set' | 'fill' | 'interpolate' | 'smooth' | 'clamp';
  let activeOp = $state<Op | null>(null);

  let scaleValue = $state(100);
  let offsetValue = $state(0);
  let setValue = $state(0);
  let clampMin = $state(0);
  let clampMax = $state(100);

  function selectOp(op: Op) {
    activeOp = activeOp === op ? null : op;
  }

  function handleApply() {
    if (!activeOp) return;
    switch (activeOp) {
      case 'scale': onApply('scale', { factor: scaleValue / 100 }); break;
      case 'offset': onApply('offset', { offset: offsetValue }); break;
      case 'set': onApply('set', { value: setValue }); break;
      case 'fill': onApply('fill', {}); break;
      case 'interpolate': onApply('interpolate', {}); break;
      case 'smooth': onApply('smooth', {}); break;
      case 'clamp': onApply('clamp', { min: Math.min(clampMin, clampMax), max: Math.max(clampMin, clampMax) }); break;
    }
    activeOp = null;
    onClose();
  }

  $effect(() => {
    if (open) activeOp = null;
  });
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
    class="fixed inset-x-0 bottom-0 z-[81] border-t border-[var(--metro-border)]"
    style="background-color: var(--metro-card);"
  >
    <div class="mx-auto max-w-lg p-4">
      <!-- Header -->
      <div class="mb-3 flex items-center justify-between">
        <h3 class="text-[13px] font-bold uppercase tracking-wider text-white">
          {cellCount} {selectionType === 'output' ? 'cell' : 'axis value'}{cellCount !== 1 ? 's' : ''} selected
        </h3>
        <button
          class="flex h-8 w-8 items-center justify-center text-[var(--metro-text-secondary)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-white"
          onclick={onClose}
        >
          &#x2715;
        </button>
      </div>

      {#if !activeOp}
        <!-- Transform operations grid -->
        <div class="grid grid-cols-2 gap-2">
          <button
            class="flex items-center gap-3 border border-[var(--metro-border)] bg-[var(--metro-surface)] px-3 py-2.5 text-left transition-colors duration-150 hover:border-[var(--metro-orange)] hover:bg-[var(--metro-hover)]"
            onclick={() => selectOp('scale')}
          >
            <IconPercentage size={18} class="shrink-0 text-[var(--metro-text-secondary)]" />
            <div>
              <div class="text-[11px] font-bold uppercase tracking-wider text-white">Scale %</div>
              <div class="text-[10px] text-[var(--metro-text-muted)]">Multiply every value by a percentage</div>
            </div>
          </button>
          <button
            class="flex items-center gap-3 border border-[var(--metro-border)] bg-[var(--metro-surface)] px-3 py-2.5 text-left transition-colors duration-150 hover:border-[var(--metro-orange)] hover:bg-[var(--metro-hover)]"
            onclick={() => selectOp('offset')}
          >
            <IconMath size={18} class="shrink-0 text-[var(--metro-text-secondary)]" />
            <div>
              <div class="text-[11px] font-bold uppercase tracking-wider text-white">Offset</div>
              <div class="text-[10px] text-[var(--metro-text-muted)]">Add or subtract from every value</div>
            </div>
          </button>
          <button
            class="flex items-center gap-3 border border-[var(--metro-border)] bg-[var(--metro-surface)] px-3 py-2.5 text-left transition-colors duration-150 hover:border-[var(--metro-orange)] hover:bg-[var(--metro-hover)]"
            onclick={() => selectOp('set')}
          >
            <IconAdjustments size={18} class="shrink-0 text-[var(--metro-text-secondary)]" />
            <div>
              <div class="text-[11px] font-bold uppercase tracking-wider text-white">Set To</div>
              <div class="text-[10px] text-[var(--metro-text-muted)]">Replace every value with one number</div>
            </div>
          </button>
          <button
            class="flex items-center gap-3 border border-[var(--metro-border)] bg-[var(--metro-surface)] px-3 py-2.5 text-left transition-colors duration-150 hover:border-[var(--metro-orange)] hover:bg-[var(--metro-hover)]"
            onclick={() => selectOp('fill')}
          >
            <IconPlayerPlay size={18} class="shrink-0 text-[var(--metro-text-secondary)]" />
            <div>
              <div class="text-[11px] font-bold uppercase tracking-wider text-white">Fill</div>
              <div class="text-[10px] text-[var(--metro-text-muted)]">Blend from edge values inward</div>
            </div>
          </button>
          <button
            class="flex items-center gap-3 border border-[var(--metro-border)] bg-[var(--metro-surface)] px-3 py-2.5 text-left transition-colors duration-150 hover:border-[var(--metro-orange)] hover:bg-[var(--metro-hover)]"
            onclick={() => selectOp('interpolate')}
          >
            <IconArrowsCross size={18} class="shrink-0 text-[var(--metro-text-secondary)]" />
            <div>
              <div class="text-[11px] font-bold uppercase tracking-wider text-white">Interpolate</div>
              <div class="text-[10px] text-[var(--metro-text-muted)]">Straight ramp between corner values</div>
            </div>
          </button>
          <button
            class="flex items-center gap-3 border border-[var(--metro-border)] bg-[var(--metro-surface)] px-3 py-2.5 text-left transition-colors duration-150 hover:border-[var(--metro-orange)] hover:bg-[var(--metro-hover)]"
            onclick={() => selectOp('smooth')}
          >
            <IconWaveSine size={18} class="shrink-0 text-[var(--metro-text-secondary)]" />
            <div>
              <div class="text-[11px] font-bold uppercase tracking-wider text-white">Smooth</div>
              <div class="text-[10px] text-[var(--metro-text-muted)]">Average each cell with neighbors</div>
            </div>
          </button>
          <button
            class="flex items-center gap-3 border border-[var(--metro-border)] bg-[var(--metro-surface)] px-3 py-2.5 text-left transition-colors duration-150 hover:border-[var(--metro-orange)] hover:bg-[var(--metro-hover)] col-span-2 sm:col-span-1"
            onclick={() => selectOp('clamp')}
          >
            <IconMinus size={18} class="shrink-0 text-[var(--metro-text-secondary)]" />
            <div>
              <div class="text-[11px] font-bold uppercase tracking-wider text-white">Clamp</div>
              <div class="text-[10px] text-[var(--metro-text-muted)]">Cap values at min/max limits</div>
            </div>
          </button>
        </div>

        <!-- I/O operations -->
        <div class="mt-3 flex gap-2">
          <button
            class="flex flex-1 items-center justify-center gap-2 border border-[var(--metro-border)] bg-transparent px-3 py-2.5 text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)] transition-colors duration-150 hover:border-[var(--metro-text-secondary)] hover:bg-[var(--metro-hover)] hover:text-white"
            onclick={() => { onImportYaml(); onClose(); }}
          >
            <IconFileImport size={16} />
            Import YAML
          </button>
          <button
            class="flex flex-1 items-center justify-center gap-2 border border-[var(--metro-border)] bg-transparent px-3 py-2.5 text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)] transition-colors duration-150 hover:border-[var(--metro-text-secondary)] hover:bg-[var(--metro-hover)] hover:text-white"
            onclick={() => { onExportYaml(); onClose(); }}
          >
            <IconFileExport size={16} />
            Export YAML
          </button>
        </div>
      {:else}
        <!-- Op parameter inputs -->
        <div class="mb-4">
          {#if activeOp === 'scale'}
            <label for="ops-scale" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Multiply by (%)</label>
            <input id="ops-scale" bind:value={scaleValue} type="number" inputmode="decimal" class="metro-input w-full font-mono" />
            <p class="mt-1 text-[10px] text-[var(--metro-text-muted)]">110 = +10%, 95 = -5%</p>
          {:else if activeOp === 'offset'}
            <label for="ops-offset" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Add value</label>
            <input id="ops-offset" bind:value={offsetValue} type="number" inputmode="decimal" class="metro-input w-full font-mono" />
            <p class="mt-1 text-[10px] text-[var(--metro-text-muted)]">+2.5 or -1.0</p>
          {:else if activeOp === 'set'}
            <label for="ops-set" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Set all cells to</label>
            <input id="ops-set" bind:value={setValue} type="number" inputmode="decimal" class="metro-input w-full font-mono" />
          {:else if activeOp === 'fill'}
            <p class="text-[13px] text-[var(--metro-text-secondary)]">Interpolates from edge values inward. Works on rows, columns, or 2D blocks.</p>
          {:else if activeOp === 'interpolate'}
            <p class="text-[13px] text-[var(--metro-text-secondary)]">Linear ramp between the two diagonal corners of your selection.</p>
          {:else if activeOp === 'smooth'}
            <p class="text-[13px] text-[var(--metro-text-secondary)]">Averages each cell with its immediate neighbors.</p>
          {:else if activeOp === 'clamp'}
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label for="ops-clamp-min" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Min</label>
                <input id="ops-clamp-min" bind:value={clampMin} type="number" inputmode="decimal" class="metro-input w-full font-mono" />
              </div>
              <div>
                <label for="ops-clamp-max" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Max</label>
                <input id="ops-clamp-max" bind:value={clampMax} type="number" inputmode="decimal" class="metro-input w-full font-mono" />
              </div>
            </div>
          {/if}
        </div>

        <div class="flex gap-2">
          <button
            class="metro-btn-secondary flex-1"
            onclick={() => activeOp = null}
          >
            <IconArrowLeft size={14} class="inline mr-1" /> Back
          </button>
          <button
            class="metro-btn-primary flex-1"
            onclick={handleApply}
          >
            Apply
          </button>
        </div>
      {/if}
    </div>
  </div>
{/if}

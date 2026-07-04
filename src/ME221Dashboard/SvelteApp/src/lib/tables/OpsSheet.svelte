<script lang="ts">
  import { IconPercentage, IconMath, IconAdjustments, IconArrowsCross, IconWaveSine, IconMinus, IconPlayerPlay, IconFileImport, IconFileExport, IconArrowLeft, IconArrowsSort, IconCopy, IconFilter, IconChartLine, IconHelp } from '@tabler/icons-svelte';
  import TransformHelpModal from './TransformHelpModal.svelte';

  let { open, cellCount, selectionType = 'output', onApply, onImportYaml, onExportYaml, onClose }: {
    open: boolean;
    cellCount: number;
    selectionType?: 'output' | 'input0' | 'input1';
    onApply: (operation: string, params: Record<string, number>) => void;
    onImportYaml: () => void;
    onExportYaml: () => void;
    onClose: () => void;
  } = $props();

  type Op = 'scale' | 'offset' | 'set' | 'fill' | 'interpolate' | 'smooth' | 'clamp' | 'rowNormalize' | 'colNormalize' | 'gaussianSmooth' | 'mirrorH' | 'mirrorV' | 'copyRow' | 'ramp' | 'conditionalScale';
  let activeOp = $state<Op | null>(null);

  let scaleValue = $state(100);
  let offsetValue = $state(0);
  let setValue = $state(0);
  let clampMin = $state(0);
  let clampMax = $state(100);
  let gaussianSigma = $state(1.0);
  let copyRowSource = $state(0);
  let rampStart = $state(0);
  let rampEnd = $state(100);
  let condScaleFactor = $state(100);
  let condThreshold = $state(0);
  let condOp = $state(0);
  let helpOpen = $state(false);

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
      case 'rowNormalize': onApply('rowNormalize', {}); break;
      case 'colNormalize': onApply('colNormalize', {}); break;
      case 'gaussianSmooth': onApply('gaussianSmooth', { sigma: gaussianSigma }); break;
      case 'mirrorH': onApply('mirrorH', {}); break;
      case 'mirrorV': onApply('mirrorV', {}); break;
      case 'copyRow': onApply('copyRow', { sourceRow: copyRowSource }); break;
      case 'ramp': onApply('ramp', { start: rampStart, end: rampEnd }); break;
      case 'conditionalScale': onApply('conditionalScale', { factor: condScaleFactor / 100, threshold: condThreshold, condOp }); break;
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
              <div class="text-[10px] text-[var(--metro-text-muted)]">Add or subtract a fixed amount</div>
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
          <button
            class="flex items-center gap-3 border border-[var(--metro-border)] bg-[var(--metro-surface)] px-3 py-2.5 text-left transition-colors duration-150 hover:border-[var(--metro-orange)] hover:bg-[var(--metro-hover)]"
            onclick={() => selectOp('rowNormalize')}
          >
            <IconArrowsSort size={18} class="shrink-0 text-[var(--metro-text-secondary)]" />
            <div>
              <div class="text-[11px] font-bold uppercase tracking-wider text-white">Row Normalize</div>
              <div class="text-[10px] text-[var(--metro-text-muted)]">Scale each row to 0–100%</div>
            </div>
          </button>
          <button
            class="flex items-center gap-3 border border-[var(--metro-border)] bg-[var(--metro-surface)] px-3 py-2.5 text-left transition-colors duration-150 hover:border-[var(--metro-orange)] hover:bg-[var(--metro-hover)]"
            onclick={() => selectOp('colNormalize')}
          >
            <IconArrowsSort size={18} class="shrink-0 rotate-90 text-[var(--metro-text-secondary)]" />
            <div>
              <div class="text-[11px] font-bold uppercase tracking-wider text-white">Col Normalize</div>
              <div class="text-[10px] text-[var(--metro-text-muted)]">Scale each column to 0–100%</div>
            </div>
          </button>
          <button
            class="flex items-center gap-3 border border-[var(--metro-border)] bg-[var(--metro-surface)] px-3 py-2.5 text-left transition-colors duration-150 hover:border-[var(--metro-orange)] hover:bg-[var(--metro-hover)]"
            onclick={() => selectOp('gaussianSmooth')}
          >
            <IconWaveSine size={18} class="shrink-0 text-[var(--metro-text-secondary)]" />
            <div>
              <div class="text-[11px] font-bold uppercase tracking-wider text-white">Gaussian Smooth</div>
              <div class="text-[10px] text-[var(--metro-text-muted)]">Bell-curve weighted smoothing</div>
            </div>
          </button>
          <button
            class="flex items-center gap-3 border border-[var(--metro-border)] bg-[var(--metro-surface)] px-3 py-2.5 text-left transition-colors duration-150 hover:border-[var(--metro-orange)] hover:bg-[var(--metro-hover)]"
            onclick={() => selectOp('mirrorH')}
          >
            <IconArrowsSort size={18} class="shrink-0 text-[var(--metro-text-secondary)]" />
            <div>
              <div class="text-[11px] font-bold uppercase tracking-wider text-white">Mirror H</div>
              <div class="text-[10px] text-[var(--metro-text-muted)]">Reverse left-to-right</div>
            </div>
          </button>
          <button
            class="flex items-center gap-3 border border-[var(--metro-border)] bg-[var(--metro-surface)] px-3 py-2.5 text-left transition-colors duration-150 hover:border-[var(--metro-orange)] hover:bg-[var(--metro-hover)]"
            onclick={() => selectOp('mirrorV')}
          >
            <IconArrowsSort size={18} class="shrink-0 rotate-90 text-[var(--metro-text-secondary)]" />
            <div>
              <div class="text-[11px] font-bold uppercase tracking-wider text-white">Mirror V</div>
              <div class="text-[10px] text-[var(--metro-text-muted)]">Reverse top-to-bottom</div>
            </div>
          </button>
          <button
            class="flex items-center gap-3 border border-[var(--metro-border)] bg-[var(--metro-surface)] px-3 py-2.5 text-left transition-colors duration-150 hover:border-[var(--metro-orange)] hover:bg-[var(--metro-hover)]"
            onclick={() => selectOp('copyRow')}
          >
            <IconCopy size={18} class="shrink-0 text-[var(--metro-text-secondary)]" />
            <div>
              <div class="text-[11px] font-bold uppercase tracking-wider text-white">Copy Row</div>
              <div class="text-[10px] text-[var(--metro-text-muted)]">Copy source row to selection</div>
            </div>
          </button>
          <button
            class="flex items-center gap-3 border border-[var(--metro-border)] bg-[var(--metro-surface)] px-3 py-2.5 text-left transition-colors duration-150 hover:border-[var(--metro-orange)] hover:bg-[var(--metro-hover)]"
            onclick={() => selectOp('ramp')}
          >
            <IconChartLine size={18} class="shrink-0 text-[var(--metro-text-secondary)]" />
            <div>
              <div class="text-[11px] font-bold uppercase tracking-wider text-white">Ramp</div>
              <div class="text-[10px] text-[var(--metro-text-muted)]">Sequential fill start to end</div>
            </div>
          </button>
          <button
            class="flex items-center gap-3 border border-[var(--metro-border)] bg-[var(--metro-surface)] px-3 py-2.5 text-left transition-colors duration-150 hover:border-[var(--metro-orange)] hover:bg-[var(--metro-hover)]"
            onclick={() => selectOp('conditionalScale')}
          >
            <IconFilter size={18} class="shrink-0 text-[var(--metro-text-secondary)]" />
            <div>
              <div class="text-[11px] font-bold uppercase tracking-wider text-white">Cond Scale</div>
              <div class="text-[10px] text-[var(--metro-text-muted)]">Scale only if value matches</div>
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
            <p class="mt-1 text-[10px] text-[var(--metro-text-muted)]">110 = +10%, 95 = -5%, 125 = +25%. Scales proportionally, preserving relative differences between cells.</p>
          {:else if activeOp === 'offset'}
            <label for="ops-offset" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Add value</label>
            <input id="ops-offset" bind:value={offsetValue} type="number" inputmode="decimal" class="metro-input w-full font-mono" />
            <p class="mt-1 text-[10px] text-[var(--metro-text-muted)]">Shifts every selected value by the same amount. +2.5 adds 2.5 to all, -1.0 subtracts 1.0 from all.</p>
          {:else if activeOp === 'set'}
            <label for="ops-set" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Set all cells to</label>
            <input id="ops-set" bind:value={setValue} type="number" inputmode="decimal" class="metro-input w-full font-mono" />
            <p class="mt-1 text-[10px] text-[var(--metro-text-muted)]">Replaces every selected value with the same number. Use to reset a region to a known baseline.</p>
          {:else if activeOp === 'fill'}
            <p class="text-[13px] text-[var(--metro-text-secondary)]">Keeps the corner values unchanged and blends everything in between. In 2D, all four corners are preserved and interior cells are bilinearly interpolated. In 1D, the two edge values are preserved and interior cells are linearly blended.</p>
            <p class="mt-2 text-[11px] text-[var(--metro-text-muted)]">Use when you have two good anchor points and want a smooth transition between them without manually editing every cell.</p>
          {:else if activeOp === 'interpolate'}
            <p class="text-[13px] text-[var(--metro-text-secondary)]">Creates a straight linear ramp between the two diagonal corners of your selection. Unlike Fill, this only preserves the two opposite corners, not all four.</p>
            <p class="mt-2 text-[11px] text-[var(--metro-text-muted)]">Use for a simple diagonal blend across a rectangular region, or for a straight ramp in 1D.</p>
          {:else if activeOp === 'smooth'}
            <p class="text-[13px] text-[var(--metro-text-secondary)]">Replaces each cell with the average of itself and its immediate neighbors (3x3 kernel in 2D, 3-cell window in 1D). Reduces spikes and jagged transitions.</p>
            <p class="mt-2 text-[11px] text-[var(--metro-text-muted)]">Good first pass for cleaning up noisy data or harsh edges. Apply multiple times for stronger smoothing.</p>
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
            <p class="mt-1 text-[10px] text-[var(--metro-text-muted)]">Forces all values into the min/max range. Values above max become max, values below min become min. Useful as a safety limit after aggressive scaling.</p>
          {:else if activeOp === 'rowNormalize'}
            <p class="text-[13px] text-[var(--metro-text-secondary)]">Scales each row independently so its values span 0–100%. The shape of each row is preserved, but rows become directly comparable regardless of their original magnitude.</p>
            <p class="mt-2 text-[11px] text-[var(--metro-text-muted)]">Useful for comparing the relative pattern across RPM rows when absolute values differ greatly.</p>
          {:else if activeOp === 'colNormalize'}
            <p class="text-[13px] text-[var(--metro-text-secondary)]">Scales each column independently so its values span 0–100%. The shape of each column is preserved, but columns become directly comparable.</p>
            <p class="mt-2 text-[11px] text-[var(--metro-text-muted)]">Useful for comparing the relative pattern across MAP/load columns when absolute values differ greatly.</p>
          {:else if activeOp === 'gaussianSmooth'}
            <label for="ops-gauss-sigma" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Sigma (smoothness)</label>
            <input id="ops-gauss-sigma" bind:value={gaussianSigma} type="number" inputmode="decimal" step="0.1" min="0.1" max="5" class="metro-input w-full font-mono" />
            <p class="mt-1 text-[10px] text-[var(--metro-text-muted)]">0.5 = subtle, 1.0 = moderate, 2.0 = very smooth. Uses a bell-curve weighted average so nearby cells have more influence than distant ones. Produces smoother results than basic Smooth.</p>
          {:else if activeOp === 'mirrorH'}
            <p class="text-[13px] text-[var(--metro-text-secondary)]">Reverses the order of values left-to-right. The first value becomes the last, the second becomes the second-to-last, and so on.</p>
            <p class="mt-2 text-[11px] text-[var(--metro-text-muted)]">Use for symmetrical tuning or when you want to flip a curve direction (e.g., for a reverse-mapped axis).</p>
          {:else if activeOp === 'mirrorV'}
            <p class="text-[13px] text-[var(--metro-text-secondary)]">Reverses the order of values top-to-bottom. The first row becomes the last, the second becomes the second-to-last, and so on.</p>
            <p class="mt-2 text-[11px] text-[var(--metro-text-muted)]">Use for symmetrical tuning or when you want to flip a table vertically.</p>
          {:else if activeOp === 'copyRow'}
            <label for="ops-copy-src" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Source row index</label>
            <input id="ops-copy-src" bind:value={copyRowSource} type="number" inputmode="numeric" min="0" class="metro-input w-full font-mono" />
            <p class="mt-1 text-[10px] text-[var(--metro-text-muted)]">Copies one row's values to all other selected rows. Useful when you have a well-tuned row and want to use it as a starting point for neighboring rows.</p>
          {:else if activeOp === 'ramp'}
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label for="ops-ramp-start" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Start</label>
                <input id="ops-ramp-start" bind:value={rampStart} type="number" inputmode="decimal" class="metro-input w-full font-mono" />
              </div>
              <div>
                <label for="ops-ramp-end" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">End</label>
                <input id="ops-ramp-end" bind:value={rampEnd} type="number" inputmode="decimal" class="metro-input w-full font-mono" />
              </div>
            </div>
            <p class="mt-1 text-[10px] text-[var(--metro-text-muted)]">Fills the selection with a straight linear ramp from start to end. Unlike Interpolate, you specify the exact start and end values rather than using existing cell values.</p>
          {:else if activeOp === 'conditionalScale'}
            <label for="ops-cond-factor" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Scale by (%)</label>
            <input id="ops-cond-factor" bind:value={condScaleFactor} type="number" inputmode="decimal" class="metro-input w-full font-mono" />
            <div class="mt-3 grid grid-cols-2 gap-3">
              <div>
                <label for="ops-cond-thresh" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Threshold</label>
                <input id="ops-cond-thresh" bind:value={condThreshold} type="number" inputmode="decimal" class="metro-input w-full font-mono" />
              </div>
              <div>
                <label for="ops-cond-op" class="mb-1 block text-[11px] font-bold uppercase tracking-wider text-[var(--metro-text-secondary)]">Condition</label>
                <select id="ops-cond-op" bind:value={condOp} class="metro-input w-full">
                  <option value={0}>Greater than</option>
                  <option value={1}>Less than</option>
                  <option value={2}>Equal to</option>
                </select>
              </div>
            </div>
            <p class="mt-1 text-[10px] text-[var(--metro-text-muted)]">Only scales cells that match the condition. E.g., scale cells above 50 by 110% to boost only the high end of a range.</p>
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
            class="flex items-center justify-center border px-3 transition-colors duration-150"
            style="border-color: var(--metro-border); color: var(--metro-text-secondary);"
            onclick={() => { helpOpen = true; }}
          >
            <IconHelp size={16} />
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

{#if helpOpen && activeOp}
  <TransformHelpModal operation={activeOp} onClose={() => { helpOpen = false; }} />
{/if}

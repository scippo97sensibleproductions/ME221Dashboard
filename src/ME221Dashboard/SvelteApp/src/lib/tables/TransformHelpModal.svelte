<script lang="ts">
  import { heatColor } from './types';

  let { operation, onClose }: {
    operation: string;
    onClose: () => void;
  } = $props();

  interface HelpEntry {
    title: string;
    description: string;
    whenToUse: string;
    before: number[][];
    after: number[][];
  }

  const HELP: Record<string, HelpEntry> = {
    scale: {
      title: 'Scale %',
      description: 'Multiplies every selected value by a percentage. 110% increases all values by 10%, 95% decreases them by 5%. The relative differences between cells are preserved proportionally.',
      whenToUse: 'Your fuel table is 10% too rich across the board after a modification. Scale by 90% to lean out everything proportionally without changing the curve shape. Or boost timing by 5% across a specific RPM range.',
      before: [[45, 50, 55, 60], [50, 55, 60, 65], [55, 60, 65, 70], [60, 65, 70, 75]],
      after: [[49.5, 55, 60.5, 66], [55, 60.5, 66, 71.5], [60.5, 66, 71.5, 77], [66, 71.5, 77, 82.5]],
    },
    offset: {
      title: 'Offset',
      description: 'Adds or subtracts a fixed value from every selected cell. Unlike Scale, this shifts all values by the same absolute amount rather than proportionally.',
      whenToUse: 'You need 2 degrees more timing advance across an entire RPM band. Offset +2 shifts every cell up by 2. Or the idle airflow table is 5 units too high everywhere: Offset -5.',
      before: [[45, 50, 55, 60], [50, 55, 60, 65], [55, 60, 65, 70], [60, 65, 70, 75]],
      after: [[47, 52, 57, 62], [52, 57, 62, 67], [57, 62, 67, 72], [62, 67, 72, 77]],
    },
    set: {
      title: 'Set To',
      description: 'Replaces every selected value with the same number. This is a destructive operation that erases all variation in the selected region.',
      whenToUse: 'Resetting a table region to a known baseline before starting fresh. Zeroing out a disabled enrichment zone. Setting a entire column to a safe conservative value for initial testing.',
      before: [[45, 50, 55, 60], [50, 55, 60, 65], [55, 60, 65, 70], [60, 65, 70, 75]],
      after: [[50, 50, 50, 50], [50, 50, 50, 50], [50, 50, 50, 50], [50, 50, 50, 50]],
    },
    fill: {
      title: 'Fill',
      description: 'Preserves the corner values and smoothly blends everything in between. In 2D, all four corners are kept and interior cells are bilinearly interpolated. In 1D, the two edge values are preserved.',
      whenToUse: 'You have good values at the corners of a table region but the interior was corrupted or needs smoothing. Fill creates a smooth surface anchored to known-good corner values.',
      before: [[20, 30, 70, 80], [25, 99, 99, 75], [35, 99, 99, 65], [40, 50, 60, 70]],
      after: [[20, 30, 70, 80], [25, 40, 60, 75], [35, 43, 57, 65], [40, 50, 60, 70]],
    },
    interpolate: {
      title: 'Interpolate',
      description: 'Creates a straight linear ramp between the two diagonal corners of the selection. Unlike Fill, only two opposite corners are preserved.',
      whenToUse: 'Creating a smooth diagonal gradient across a region. Useful for generating a baseline curve between two known data points, or smoothing a jagged transition in a 1D axis.',
      before: [[20, 99, 99, 80], [99, 99, 99, 99], [99, 99, 99, 99], [40, 99, 99, 70]],
      after: [[20, 40, 60, 80], [30, 45, 55, 65], [30, 40, 50, 60], [40, 50, 60, 70]],
    },
    smooth: {
      title: 'Smooth',
      description: 'Replaces each cell with the average of itself and its immediate neighbors. Uses a 3x3 window in 2D or 3-cell window in 1D. Reduces spikes and jagged transitions.',
      whenToUse: 'Cleaning up noisy data from a rough tune. Softening a harsh spike in a fuel or timing table. Good first-pass before fine-tuning individual cells.',
      before: [[50, 50, 50, 50], [50, 80, 30, 50], [50, 50, 50, 50], [50, 50, 50, 50]],
      after: [[50, 50, 50, 50], [50, 57, 43, 50], [50, 50, 50, 50], [50, 50, 50, 50]],
    },
    clamp: {
      title: 'Clamp',
      description: 'Forces all values into a min/max range. Values above the maximum become the maximum, values below the minimum become the minimum. Intermediate values are unchanged.',
      whenToUse: 'Safety limit after aggressive scaling. Ensuring no cell exceeds a mechanical limit (e.g., max boost, max duty cycle). Preventing negative values after subtraction operations.',
      before: [[-5, 30, 70, 110], [10, 50, 50, 90], [20, 40, 60, 80], [5, 25, 75, 105]],
      after: [[0, 30, 70, 100], [10, 50, 50, 90], [20, 40, 60, 80], [5, 25, 75, 100]],
    },
    rowNormalize: {
      title: 'Row Normalize',
      description: 'Scales each row independently so its values span 0-100%. The shape of each row is preserved, but rows become directly comparable regardless of their original magnitude.',
      whenToUse: 'Comparing the relative fuel enrichment pattern across RPM rows when absolute duty cycle values differ greatly. Seeing the shape of each RPM row on equal footing.',
      before: [[10, 20, 30, 40], [50, 55, 60, 65], [100, 110, 120, 130], [5, 10, 15, 20]],
      after: [[0, 33, 67, 100], [0, 33, 67, 100], [0, 33, 67, 100], [0, 33, 67, 100]],
    },
    colNormalize: {
      title: 'Col Normalize',
      description: 'Scales each column independently so its values span 0-100%. The shape of each column is preserved, but columns become directly comparable.',
      whenToUse: 'Comparing the MAP/load column behavior when each column has a different absolute range. Seeing the shape of each load column on equal footing.',
      before: [[10, 50, 100, 5], [20, 55, 110, 10], [30, 60, 120, 15], [40, 65, 130, 20]],
      after: [[0, 0, 0, 0], [33, 33, 33, 33], [67, 67, 67, 67], [100, 100, 100, 100]],
    },
    gaussianSmooth: {
      title: 'Gaussian Smooth',
      description: 'Like Smooth, but uses a bell-curve weighted average. Nearby cells have more influence than distant ones. Produces smoother, more natural-looking results than basic Smooth.',
      whenToUse: 'When basic Smooth is too aggressive on nearby cells or not strong enough on distant ones. Better for preserving the overall shape of a curve while removing noise.',
      before: [[50, 50, 50, 50], [50, 90, 20, 50], [50, 50, 50, 50], [50, 50, 50, 50]],
      after: [[50, 50, 50, 50], [50, 61, 39, 50], [50, 50, 50, 50], [50, 50, 50, 50]],
    },
    mirrorH: {
      title: 'Mirror H',
      description: 'Reverses the order of values left-to-right. The first value becomes the last, the second becomes the second-to-last, and so on.',
      whenToUse: 'Flipping a fuel or timing curve direction. Creating a symmetrical table from one side. Correcting a table that was entered in reverse axis order.',
      before: [[10, 20, 30, 40], [15, 25, 35, 45], [20, 30, 40, 50], [25, 35, 45, 55]],
      after: [[40, 30, 20, 10], [45, 35, 25, 15], [50, 40, 30, 20], [55, 45, 35, 25]],
    },
    mirrorV: {
      title: 'Mirror V',
      description: 'Reverses the order of values top-to-bottom. The first row becomes the last, the second becomes the second-to-last, and so on.',
      whenToUse: 'Flipping a table vertically. Creating a symmetrical pattern from top to bottom. Correcting a table that was entered with reversed RPM axis.',
      before: [[10, 20, 30, 40], [15, 25, 35, 45], [20, 30, 40, 50], [25, 35, 45, 55]],
      after: [[25, 35, 45, 55], [20, 30, 40, 50], [15, 25, 35, 45], [10, 20, 30, 40]],
    },
    copyRow: {
      title: 'Copy Row',
      description: 'Copies one row\'s values to all other selected rows. The source row is unchanged. All other selected rows receive the source row\'s exact values.',
      whenToUse: 'You have one well-tuned RPM row and want to use it as a starting point for neighboring rows. Copy a known-good fuel row to adjacent rows, then fine-tune each individually.',
      before: [[10, 20, 30, 40], [15, 25, 35, 45], [99, 88, 77, 66], [25, 35, 45, 55]],
      after: [[10, 20, 30, 40], [10, 20, 30, 40], [10, 20, 30, 40], [10, 20, 30, 40]],
    },
    ramp: {
      title: 'Ramp',
      description: 'Fills the selection with a straight linear ramp from a start value to an end value. You specify the exact values rather than using existing cell values.',
      whenToUse: 'Creating a fresh linear gradient as a starting point for tuning. Replacing a corrupted region with a clean baseline. Setting up a progressive enrichment curve from scratch.',
      before: [[99, 99, 99, 99], [99, 99, 99, 99], [99, 99, 99, 99], [99, 99, 99, 99]],
      after: [[20, 40, 60, 80], [20, 40, 60, 80], [20, 40, 60, 80], [20, 40, 60, 80]],
    },
    conditionalScale: {
      title: 'Conditional Scale',
      description: 'Only scales cells that meet a condition (greater than, less than, or equal to a threshold). Cells that don\'t match are left unchanged.',
      whenToUse: 'Boosting only the high-load cells by 10% without affecting idle cells. Reducing values above a safety limit. Targeting a specific operating region for adjustment.',
      before: [[10, 30, 60, 80], [20, 40, 70, 90], [15, 35, 65, 85], [5, 25, 55, 75]],
      after: [[10, 30, 66, 88], [20, 40, 77, 99], [15, 35, 71.5, 93.5], [5, 25, 60.5, 82.5]],
    },
  };

  let help = $derived(HELP[operation] ?? null);

  function miniTable(values: number[][], label: string) {
    const allVals = values.flat();
    const mn = Math.min(...allVals);
    const mx = Math.max(...allVals);
    return { values, mn, mx, label };
  }

  let beforeTable = $derived(help ? miniTable(help.before, 'Before') : null);
  let afterTable = $derived(help ? miniTable(help.after, 'After') : null);
</script>

{#if help}
  <div
    class="fixed inset-0 z-[95] flex items-center justify-center p-4"
    style="background-color: rgba(0,0,0,0.85);"
    role="button"
    tabindex="-1"
    onclick={onClose}
    onkeydown={(e) => { if (e.key === 'Escape') { e.preventDefault(); onClose(); } }}
  >
    <div
      class="max-h-[85vh] w-full max-w-lg overflow-auto border"
      style="background-color: var(--metro-card); border-color: var(--metro-border);"
      onclick={(e) => e.stopPropagation()}
      onkeydown={(e) => e.stopPropagation()}
      role="dialog"
      tabindex="-1"
    >
      <!-- Header -->
      <div class="flex items-center justify-between border-b px-4 py-3" style="border-color: var(--metro-border);">
        <h3 class="text-[14px] font-bold text-white">{help.title}</h3>
        <button
          class="flex h-7 w-7 items-center justify-center text-[var(--metro-text-secondary)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-white"
          onclick={onClose}
        >
          &#x2715;
        </button>
      </div>

      <div class="p-4">
        <!-- Description -->
        <div class="mb-4">
          <div class="mb-1 text-[11px] font-bold uppercase tracking-wider text-[var(--metro-orange)]">What it does</div>
          <p class="text-[12px] leading-relaxed text-[var(--metro-text-secondary)]">{help.description}</p>
        </div>

        <!-- When to use -->
        <div class="mb-4">
          <div class="mb-1 text-[11px] font-bold uppercase tracking-wider text-[var(--metro-orange)]">When to use it</div>
          <p class="text-[12px] leading-relaxed text-[var(--metro-text-secondary)]">{help.whenToUse}</p>
        </div>

        <!-- Before / After -->
        {#if beforeTable && afterTable}
          <div class="mb-2 text-[11px] font-bold uppercase tracking-wider text-[var(--metro-orange)]">Example</div>
          <div class="grid grid-cols-2 gap-4">
            <!-- Before -->
            <div>
              <div class="mb-1 text-center text-[10px] font-bold uppercase tracking-wider text-[var(--metro-text-muted)]">Before</div>
              <div class="overflow-hidden border" style="border-color: var(--metro-border);">
                {#each beforeTable.values as row}
                  <div class="flex">
                    {#each row as val}
                      {@const t = beforeTable.mx === beforeTable.mn ? 0.5 : (val - beforeTable.mn) / (beforeTable.mx - beforeTable.mn)}
                      <div
                        class="flex h-8 flex-1 items-center justify-center border-r border-b text-[9px] font-mono font-medium"
                        style="background-color: {heatColor(val, beforeTable.mn, beforeTable.mx)}; border-color: var(--metro-border); color: {t > 0.6 ? '#fff' : '#ccc'};"
                      >
                        {val.toFixed(0)}
                      </div>
                    {/each}
                  </div>
                {/each}
              </div>
            </div>

            <!-- After -->
            <div>
              <div class="mb-1 text-center text-[10px] font-bold uppercase tracking-wider text-[var(--metro-text-muted)]">After</div>
              <div class="overflow-hidden border" style="border-color: var(--metro-border);">
                {#each afterTable.values as row}
                  <div class="flex">
                    {#each row as val}
                      {@const t = afterTable.mx === afterTable.mn ? 0.5 : (val - afterTable.mn) / (afterTable.mx - afterTable.mn)}
                      <div
                        class="flex h-8 flex-1 items-center justify-center border-r border-b text-[9px] font-mono font-medium"
                        style="background-color: {heatColor(val, afterTable.mn, afterTable.mx)}; border-color: var(--metro-border); color: {t > 0.6 ? '#fff' : '#ccc'};"
                      >
                        {val.toFixed(0)}
                      </div>
                    {/each}
                  </div>
                {/each}
              </div>
            </div>
          </div>
        {/if}
      </div>
    </div>
  </div>
{/if}

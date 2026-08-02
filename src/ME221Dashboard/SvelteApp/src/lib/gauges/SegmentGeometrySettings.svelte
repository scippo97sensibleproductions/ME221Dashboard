<script lang="ts">
  // Shared segment-geometry controls (controlled component; LedRing full, WedgeBar count only).
  let { segmentCount, gap, showGap, onchange }: {
    segmentCount: number;
    gap: number;
    showGap: boolean;
    onchange: (patch: { segmentCount: number; gap?: number }) => void;
  } = $props();

  const presets = [16, 24, 32, 36, 48, 60];
</script>

<div class="space-y-3">
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Segment Count</p>
      <span class="text-xs font-mono text-cyan-400">{segmentCount}</span>
    </div>
    <div class="flex gap-1">
      {#each presets as p (p)}
        <button
          class="flex-1 rounded px-1.5 py-1.5 text-[10px] font-medium transition-colors min-h-[28px]
            {segmentCount === p
              ? 'bg-cyan-600 text-white'
              : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
          onclick={() => onchange({ segmentCount: p, gap })}
        >{p}</button>
      {/each}
    </div>
  </div>
  {#if showGap}
    <div>
      <div class="flex items-center justify-between mb-1.5">
        <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Segment Gap</p>
        <span class="text-xs font-mono text-cyan-400">{gap}</span>
      </div>
      <input
        type="range" step="0.05" min="0" max="1"
        value={gap}
        oninput={(e) => onchange({ segmentCount, gap: parseFloat((e.target as HTMLInputElement).value) })}
        class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
          [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
          [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
      />
      <p class="text-[9px] text-gray-600 mt-0.5">Gap as fraction of each segment slot</p>
    </div>
  {/if}
</div>

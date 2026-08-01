<script lang="ts">
  // Shared redline-band controls for Arc and Bar (controlled component).
  let { start, width, color, onchange }: {
    start: number; // 0 = off
    width?: number; // radial/bar thickness
    color: string;
    onchange: (patch: { start: number; width?: number; color: string }) => void;
  } = $props();
</script>

<div class="space-y-3">
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Redline Band</p>
      <span class="text-xs font-mono text-cyan-400">{start === 0 ? 'OFF' : `${Math.round(start * 100)}%`}</span>
    </div>
    <input
      type="range" step="0.01" min="0" max="1"
      value={start}
      oninput={(e) => onchange({ start: parseFloat((e.target as HTMLInputElement).value), color })}
      class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
        [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
        [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
    />
    <p class="text-[9px] text-gray-600 mt-0.5">0 = band off</p>
  </div>

  {#if start > 0}
    {#if width !== undefined}
      <div>
        <div class="flex items-center justify-between mb-1.5">
          <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Band Width</p>
          <span class="text-xs font-mono text-cyan-400">{width}</span>
        </div>
        <input
          type="range" step="0.5" min="0.5" max="20"
          value={width}
          oninput={(e) => onchange({ start, width: parseFloat((e.target as HTMLInputElement).value), color })}
          class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
            [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
            [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
        />
      </div>
    {/if}
    <div>
      <div class="flex items-center justify-between mb-1.5">
        <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Band Color</p>
      </div>
      <div class="flex items-center gap-2">
        <input type="color" value={color}
          oninput={(e) => onchange({ start, width, color: (e.target as HTMLInputElement).value })}
          class="w-8 h-8 rounded border border-gray-600 cursor-pointer bg-transparent"
        />
        <span class="text-xs font-mono text-gray-400">{color}</span>
      </div>
    </div>
  {/if}
</div>

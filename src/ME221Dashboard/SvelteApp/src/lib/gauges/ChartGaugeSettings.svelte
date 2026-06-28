<script lang="ts">
  import type { GaugeConfigEntry } from '../HybridBridge';

  let { gaugeDef, onchange }: {
    gaugeDef: GaugeConfigEntry;
    onchange: (def: GaugeConfigEntry) => void;
  } = $props();

  function set<K extends keyof GaugeConfigEntry>(key: K, value: GaugeConfigEntry[K]) {
    onchange({ ...gaugeDef, [key]: value });
  }

  const timeWindowOptions = [10, 30, 60, 120, 300, 600];
</script>

<div class="space-y-3">

  <!-- Time Window -->
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Time Window</p>
      <span class="text-xs font-mono text-cyan-400">{gaugeDef.chartTimeWindowSec}s</span>
    </div>
    <div class="flex gap-1">
      {#each timeWindowOptions as tw}
        <button
          class="flex-1 rounded px-1.5 py-1.5 text-[10px] font-medium transition-colors min-h-[28px]
            {gaugeDef.chartTimeWindowSec === tw
              ? 'bg-cyan-600 text-white'
              : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
          onclick={() => set('chartTimeWindowSec', tw)}
        >{tw}s</button>
      {/each}
    </div>
  </div>

  <!-- Precision / Point reduction -->
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Point Precision</p>
      <span class="text-xs font-mono text-cyan-400">{gaugeDef.chartPrecision} dp</span>
    </div>
    <p class="text-[9px] text-gray-600 mb-1.5">Lower = fewer points, faster rendering</p>
    <div class="flex gap-1">
      {#each [0, 1, 2, 3] as dp}
        <button
          class="flex-1 rounded px-1.5 py-1.5 text-[10px] font-medium transition-colors min-h-[28px]
            {gaugeDef.chartPrecision === dp
              ? 'bg-cyan-600 text-white'
              : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
          onclick={() => set('chartPrecision', dp)}
        >{dp} dp</button>
      {/each}
    </div>
  </div>

  <!-- Y-Axis -->
  <div>
    <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-1.5">Y-Axis Range</p>
    <div class="flex gap-1 mb-2">
      <button
        class="flex-1 rounded px-2 py-1.5 text-[10px] font-medium transition-colors min-h-[28px]
          {gaugeDef.chartYMin == null && gaugeDef.chartYMax == null
            ? 'bg-cyan-600 text-white'
            : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
        onclick={() => { set('chartYMin', null); set('chartYMax', null); }}
      >Auto</button>
      <button
        class="flex-1 rounded px-2 py-1.5 text-[10px] font-medium transition-colors min-h-[28px]
          {gaugeDef.chartYMin != null || gaugeDef.chartYMax != null
            ? 'bg-cyan-600 text-white'
            : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
        onclick={() => { set('chartYMin', gaugeDef.chartYMin ?? 0); set('chartYMax', gaugeDef.chartYMax ?? 100); }}
      >Manual</button>
    </div>
    {#if gaugeDef.chartYMin != null || gaugeDef.chartYMax != null}
      <div class="grid grid-cols-2 gap-2">
        <div>
          <label for="ymin-{gaugeDef.entityId}" class="text-[9px] text-gray-500 block mb-0.5">Min</label>
          <input id="ymin-{gaugeDef.entityId}" type="number" step="1"
            value={gaugeDef.chartYMin ?? 0}
            oninput={(e) => set('chartYMin', parseFloat((e.target as HTMLInputElement).value) || 0)}
            class="w-full rounded border border-gray-600 bg-gray-800 px-2 py-1 text-xs font-mono text-gray-200 focus:border-cyan-500 focus:outline-none"
          />
        </div>
        <div>
          <label for="ymax-{gaugeDef.entityId}" class="text-[9px] text-gray-500 block mb-0.5">Max</label>
          <input id="ymax-{gaugeDef.entityId}" type="number" step="1"
            value={gaugeDef.chartYMax ?? 100}
            oninput={(e) => set('chartYMax', parseFloat((e.target as HTMLInputElement).value) || 100)}
            class="w-full rounded border border-gray-600 bg-gray-800 px-2 py-1 text-xs font-mono text-gray-200 focus:border-cyan-500 focus:outline-none"
          />
        </div>
      </div>
    {/if}
  </div>

  <!-- Line Color -->
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Line Color</p>
    </div>
    <div class="flex items-center gap-2">
      <input type="color" value={gaugeDef.chartLineColor}
        oninput={(e) => set('chartLineColor', (e.target as HTMLInputElement).value)}
        class="w-8 h-8 rounded border border-gray-600 cursor-pointer bg-transparent"
      />
      <span class="text-xs font-mono text-gray-400">{gaugeDef.chartLineColor}</span>
    </div>
  </div>

  <!-- Line Width -->
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Line Thickness</p>
      <span class="text-xs font-mono text-cyan-400">{gaugeDef.chartLineWidth}px</span>
    </div>
    <input
      type="range" step="0.5" min="0.5" max="6"
      value={gaugeDef.chartLineWidth}
      oninput={(e) => set('chartLineWidth', parseFloat((e.target as HTMLInputElement).value))}
      class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
        [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
        [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
    />
    <div class="relative mt-0.5 h-3 text-[9px] text-gray-600 overflow-visible select-none">
      <span style="position:absolute;left:0">0.5</span>
      <span style="position:absolute;left:50%;transform:translateX(-50%)">3</span>
      <span style="position:absolute;right:0">6</span>
    </div>
  </div>

  <!-- Toggles -->
  <div class="space-y-1.5">
    <button
      class="flex items-center justify-between w-full rounded px-2 py-1.5 text-[10px] font-medium transition-colors
        {gaugeDef.chartShowGrid ? 'text-cyan-400 hover:text-cyan-300' : 'text-gray-500 hover:text-gray-400'}"
      onclick={() => set('chartShowGrid', !gaugeDef.chartShowGrid)}
    >
      <span>Show Grid Lines</span>
      <span class="text-[10px]">{gaugeDef.chartShowGrid ? 'ON' : 'OFF'}</span>
    </button>
    <button
      class="flex items-center justify-between w-full rounded px-2 py-1.5 text-[10px] font-medium transition-colors
        {gaugeDef.chartFillUnder ? 'text-cyan-400 hover:text-cyan-300' : 'text-gray-500 hover:text-gray-400'}"
      onclick={() => set('chartFillUnder', !gaugeDef.chartFillUnder)}
    >
      <span>Fill Under Line</span>
      <span class="text-[10px]">{gaugeDef.chartFillUnder ? 'ON' : 'OFF'}</span>
    </button>
    <button
      class="flex items-center justify-between w-full rounded px-2 py-1.5 text-[10px] font-medium transition-colors
        {gaugeDef.chartShowLabels ? 'text-cyan-400 hover:text-cyan-300' : 'text-gray-500 hover:text-gray-400'}"
      onclick={() => set('chartShowLabels', !gaugeDef.chartShowLabels)}
    >
      <span>Show Y-Axis Labels</span>
      <span class="text-[10px]">{gaugeDef.chartShowLabels ? 'ON' : 'OFF'}</span>
    </button>
  </div>

</div>

<script lang="ts">
  import type { GaugeConfigEntry } from '../HybridBridge';
  import PositionGrid from './PositionGrid.svelte';
  import RedlineBandSettings from './RedlineBandSettings.svelte';

  let { gaugeDef, onchange }: {
    gaugeDef: GaugeConfigEntry;
    onchange: (def: GaugeConfigEntry) => void;
  } = $props();

  function set<K extends keyof GaugeConfigEntry>(key: K, value: GaugeConfigEntry[K]) {
    onchange({ ...gaugeDef, [key]: value });
  }
</script>

<div class="space-y-4">

  <!-- Text Positions -->
  <div>
    <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Text Positions</p>
    <div class="grid grid-cols-3 gap-3">
      <PositionGrid label="Value" value={gaugeDef.barValuePosition} onchange={(v) => set('barValuePosition', v)} />
      <PositionGrid label="Unit" value={gaugeDef.barUnitPosition} onchange={(v) => set('barUnitPosition', v)} />
      <PositionGrid label="Name" value={gaugeDef.barNamePosition} onchange={(v) => set('barNamePosition', v)} />
    </div>
  </div>

  <!-- Orientation -->
  <div>
    <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Orientation</p>
    <div class="flex gap-1">
      <button
        class="flex-1 rounded px-2 py-1.5 text-[10px] font-medium transition-colors min-h-[28px]
          {(gaugeDef.barOrientation ?? 0) === 0
            ? 'bg-cyan-600 text-white'
            : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
        onclick={() => set('barOrientation', 0)}
      >Auto</button>
      <button
        class="flex-1 rounded px-2 py-1.5 text-[10px] font-medium transition-colors min-h-[28px]
          {gaugeDef.barOrientation === 1
            ? 'bg-cyan-600 text-white'
            : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
        onclick={() => set('barOrientation', 1)}
      >Horizontal</button>
      <button
        class="flex-1 rounded px-2 py-1.5 text-[10px] font-medium transition-colors min-h-[28px]
          {gaugeDef.barOrientation === 2
            ? 'bg-cyan-600 text-white'
            : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
        onclick={() => set('barOrientation', 2)}
      >Vertical</button>
    </div>
    <p class="text-[9px] text-gray-600 mt-0.5">Auto follows gauge shape</p>
  </div>

  <!-- Thickness -->
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Thickness</p>
      <span class="text-xs font-mono text-cyan-400">{gaugeDef.barThickness === 0 || gaugeDef.barThickness === undefined ? 'Auto' : `${gaugeDef.barThickness}%`}</span>
    </div>
    <input
      type="range" step="1" min="0" max="20"
      value={gaugeDef.barThickness ?? 0}
      oninput={(e) => set('barThickness', parseInt((e.target as HTMLInputElement).value, 10))}
      class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
        [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
        [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
    />
    <p class="text-[9px] text-gray-600 mt-0.5">0 = auto thickness</p>
  </div>

  <!-- Toggles -->
  <div class="space-y-1.5">
    <button
      class="flex items-center justify-between w-full rounded px-2 py-1.5 text-[10px] font-medium transition-colors
        {gaugeDef.barTicks ? 'text-cyan-400 hover:text-cyan-300' : 'text-gray-500 hover:text-gray-400'}"
      onclick={() => set('barTicks', !gaugeDef.barTicks)}
    >
      <span>Scale Tick Marks</span>
      <span class="text-[10px]">{gaugeDef.barTicks ? 'ON' : 'OFF'}</span>
    </button>
    <button
      class="flex items-center justify-between w-full rounded px-2 py-1.5 text-[10px] font-medium transition-colors
        {gaugeDef.barMinMaxLabels ? 'text-cyan-400 hover:text-cyan-300' : 'text-gray-500 hover:text-gray-400'}"
      onclick={() => set('barMinMaxLabels', !gaugeDef.barMinMaxLabels)}
    >
      <span>Min/Max Labels</span>
      <span class="text-[10px]">{gaugeDef.barMinMaxLabels ? 'ON' : 'OFF'}</span>
    </button>
  </div>

  <!-- Redline Band -->
  <RedlineBandSettings
    start={gaugeDef.barRedlineStart ?? 0}
    color={gaugeDef.barRedlineColor ?? '#E03131'}
    onchange={(patch) => onchange({ ...gaugeDef, barRedlineStart: patch.start, barRedlineColor: patch.color })}
  />

</div>

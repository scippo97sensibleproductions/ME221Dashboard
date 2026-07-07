<script lang="ts">
  import type { GaugeConfigEntry } from '../HybridBridge';

  let { gaugeDef, onchange }: {
    gaugeDef: GaugeConfigEntry;
    onchange: (def: GaugeConfigEntry) => void;
  } = $props();

  function set<K extends keyof GaugeConfigEntry>(key: K, value: GaugeConfigEntry[K]) {
    onchange({ ...gaugeDef, [key]: value });
  }
</script>

<div>
  <div class="flex items-center justify-between mb-1.5">
    <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Smoothing</p>
    <span class="text-xs font-mono text-gray-400">{gaugeDef.smoothingEnabled ? gaugeDef.smoothingFactor.toFixed(2) : 'Off'}</span>
  </div>
  {#if gaugeDef.smoothingEnabled}
    <input
      type="range" step="0.01" min="0.01" max="1"
      value={gaugeDef.smoothingFactor}
      oninput={(e) => set('smoothingFactor', parseFloat((e.target as HTMLInputElement).value))}
      class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
        [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
        [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
    />
  {/if}
  <button
    class="mt-1.5 text-[10px] font-medium transition-colors
      {gaugeDef?.smoothingEnabled ? 'text-cyan-400 hover:text-cyan-300' : 'text-gray-500 hover:text-gray-400'}"
    onclick={() => gaugeDef && set('smoothingEnabled', !gaugeDef.smoothingEnabled)}
  >
    {gaugeDef?.smoothingEnabled ? 'Disable smoothing' : 'Enable smoothing'}
  </button>
</div>

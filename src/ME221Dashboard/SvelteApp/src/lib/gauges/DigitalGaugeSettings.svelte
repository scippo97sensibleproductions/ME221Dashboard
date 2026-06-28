<script lang="ts">
  import type { GaugeConfigEntry } from '../HybridBridge';
  import SmoothingSettings from './SmoothingSettings.svelte';

  let { gaugeDef, onchange }: {
    gaugeDef: GaugeConfigEntry;
    onchange: (def: GaugeConfigEntry) => void;
  } = $props();

  function set<K extends keyof GaugeConfigEntry>(key: K, value: GaugeConfigEntry[K]) {
    onchange({ ...gaugeDef, [key]: value });
  }

  const styles = [
    { value: 0, label: 'Odometer', desc: 'Rolling digits' },
    { value: 1, label: 'Large', desc: 'Big centered' },
    { value: 2, label: '7-Segment', desc: 'LED display' },
    { value: 3, label: 'Cluster', desc: 'Dense readout' },
    { value: 4, label: 'Label Top', desc: 'Name above value' },
    { value: 5, label: 'Glow Ring', desc: 'Neon circle' },
    { value: 6, label: 'LCD', desc: 'Retro screen' },
  ];
</script>

<div class="space-y-4">

  <!-- Style: visual cards -->
  <div>
    <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Display Style</p>
    <div class="grid grid-cols-3 sm:grid-cols-4 gap-1.5">
      {#each styles as style}
        <button
          class="flex flex-col items-center justify-center rounded-lg border px-2 py-3 text-center transition-all min-h-[52px]
            {gaugeDef.digitalStyle === style.value
              ? 'border-cyan-500/50 bg-cyan-500/10 text-cyan-300'
              : 'border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
          onclick={() => set('digitalStyle', style.value)}
        >
          <span class="text-xs font-medium">{style.label}</span>
          <span class="text-[9px] text-gray-500 mt-0.5">{style.desc}</span>
        </button>
      {/each}
    </div>
  </div>

  <!-- Smoothing -->
  <SmoothingSettings {gaugeDef} {onchange} />
</div>

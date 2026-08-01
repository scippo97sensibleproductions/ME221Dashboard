<script lang="ts">
  import type { GaugeConfigEntry } from '../HybridBridge';
  import SegmentGeometrySettings from './SegmentGeometrySettings.svelte';
  import ZoneThresholdSettings from './ZoneThresholdSettings.svelte';

  let { gaugeDef, onchange }: {
    gaugeDef: GaugeConfigEntry;
    onchange: (def: GaugeConfigEntry) => void;
  } = $props();

  function set<K extends keyof GaugeConfigEntry>(key: K, value: GaugeConfigEntry[K]) {
    onchange({ ...gaugeDef, [key]: value });
  }

  function applySweepPreset(sweep: number) {
    const start = sweep === 360 ? 0 : -sweep / 2;
    onchange({ ...gaugeDef, ringSweepAngle: sweep, ringStartAngle: start });
  }

  const sweepPresets = [
    { angle: 360, label: '360°', icon: 'Full' },
    { angle: 270, label: '270°', icon: '¾' },
  ];
</script>

<div class="space-y-4">

  <!-- Segment Geometry -->
  <SegmentGeometrySettings
    segmentCount={gaugeDef.segmentCount ?? 36}
    gap={gaugeDef.segmentGap ?? 0}
    showGap={true}
    onchange={(patch) => onchange({ ...gaugeDef, ...patch })}
  />

  <!-- Sweep -->
  <div class="border-t border-gray-700/30 pt-4">
    <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Sweep</p>
    <div class="grid grid-cols-2 gap-1.5">
      {#each sweepPresets as opt}
        <button
          class="flex flex-col items-center justify-center rounded-lg border px-2 py-2.5 text-xs font-medium transition-all min-h-[44px]
            {gaugeDef.ringSweepAngle === opt.angle
              ? 'border-cyan-500/50 bg-cyan-500/10 text-cyan-300'
              : 'border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
          onclick={() => applySweepPreset(opt.angle)}
        >
          <span class="text-[10px] text-gray-500">{opt.icon}</span>
          <span>{opt.label}</span>
        </button>
      {/each}
    </div>

    <div class="mt-3">
      <div class="flex items-center justify-between mb-1.5">
        <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Start Angle</p>
        <span class="text-xs font-mono text-cyan-400">{Math.round(gaugeDef.ringStartAngle ?? 0)}°</span>
      </div>
      <input
        type="range" step="1" min="-180" max="180"
        value={gaugeDef.ringStartAngle ?? 0}
        oninput={(e) => set('ringStartAngle', parseInt((e.target as HTMLInputElement).value))}
        class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
          [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
          [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
      />
      <p class="text-[9px] text-gray-600 mt-0.5">Degrees clockwise from 12 o'clock</p>
    </div>
  </div>

  <!-- Zone Thresholds -->
  <div class="border-t border-gray-700/30 pt-4">
    <ZoneThresholdSettings
      amber={gaugeDef.amberThreshold ?? 0.7}
      red={gaugeDef.redThreshold ?? 0.85}
      onchange={(a, r) => onchange({ ...gaugeDef, amberThreshold: a, redThreshold: r })}
    />
  </div>
</div>

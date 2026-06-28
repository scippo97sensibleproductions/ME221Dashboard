<script lang="ts">
  import type { GaugeConfigEntry } from '../HybridBridge';
  import { DEFAULT_COLOR_STOPS } from './types';

  let { gaugeDef, onchange }: {
    gaugeDef: GaugeConfigEntry;
    onchange: (def: GaugeConfigEntry) => void;
  } = $props();

  const stops = $derived(gaugeDef.colorStops?.length ? gaugeDef.colorStops : DEFAULT_COLOR_STOPS);
  const hysteresis = $derived(gaugeDef.colorHysteresis ?? 0.03);

  function setStopColor(idx: number, hex: string) {
    const r = parseInt(hex.slice(1, 3), 16);
    const g = parseInt(hex.slice(3, 5), 16);
    const b = parseInt(hex.slice(5, 7), 16);
    const updated = stops.map((s, i) => i === idx ? { ...s, r, g, b } : s);
    onchange({ ...gaugeDef, colorStops: updated });
  }

  function setStopFraction(idx: number, pct: number) {
    const updated = stops.map((s, i) => i === idx ? { ...s, fraction: Math.max(0, Math.min(1, pct / 100)) } : s);
    onchange({ ...gaugeDef, colorStops: updated });
  }

  function addStop() {
    const last = stops[stops.length - 1];
    const newFraction = Math.min(1, last.fraction + 0.1);
    const updated = [...stops, { fraction: newFraction, r: 128, g: 128, b: 128 }];
    updated.sort((a, b) => a.fraction - b.fraction);
    onchange({ ...gaugeDef, colorStops: updated });
  }

  function removeStop(idx: number) {
    if (stops.length <= 2) return;
    const updated = stops.filter((_, i) => i !== idx);
    onchange({ ...gaugeDef, colorStops: updated });
  }

  function resetToDefault() {
    onchange({ ...gaugeDef, colorStops: DEFAULT_COLOR_STOPS, colorHysteresis: 0.03 });
  }

  function toHex(r: number, g: number, b: number): string {
    return '#' + [r, g, b].map(v => v.toString(16).padStart(2, '0')).join('');
  }

  const previewStops = $derived(() => {
    return stops.map(s => `${toHex(s.r, s.g, s.b)} ${s.fraction * 100}%`).join(', ');
  });
</script>

<div class="space-y-4">

  <!-- Header -->
  <div class="flex items-center justify-between">
    <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Color Stops</p>
    <div class="flex gap-1.5">
      <button
        class="rounded-md px-2 py-1 text-[10px] font-medium transition-colors text-gray-500 hover:text-gray-300"
        onclick={resetToDefault}
      >Reset</button>
      <button
        class="rounded-md bg-cyan-500/10 px-2 py-1 text-[10px] font-medium text-cyan-400 transition-colors hover:bg-cyan-500/20"
        onclick={addStop}
      >+ Add</button>
    </div>
  </div>

  <!-- Gradient preview -->
  <div class="h-3 rounded-full border border-gray-700/50" style="background: linear-gradient(to right, {previewStops()})"></div>

  <!-- Stops -->
  {#each stops as stop, idx}
    <div class="flex items-center gap-2.5">
      <!-- Color picker -->
      <input
        type="color"
        value={toHex(stop.r, stop.g, stop.b)}
        onchange={(e) => setStopColor(idx, (e.target as HTMLInputElement).value)}
        class="h-8 w-8 shrink-0 cursor-pointer rounded-md border border-gray-700 bg-transparent p-0"
      />
      <!-- Fraction slider -->
      <div class="flex-1 min-w-0">
        <div class="flex items-center justify-between mb-0.5">
          <span class="text-[10px] font-mono text-gray-400">{Math.round(stop.fraction * 100)}%</span>
          {#if stops.length > 2}
            <button
              class="text-[10px] text-gray-600 hover:text-red-400 transition-colors"
              onclick={() => removeStop(idx)}
            >remove</button>
          {/if}
        </div>
        <input
          type="range" step="1" min="0" max="100"
          value={Math.round(stop.fraction * 100)}
          oninput={(e) => setStopFraction(idx, parseInt((e.target as HTMLInputElement).value))}
          class="w-full h-1 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
            [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-3 [&::-webkit-slider-thumb]:h-3
            [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500"
        />
      </div>
    </div>
  {/each}

  <!-- Hysteresis -->
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Hysteresis</p>
      <span class="text-xs font-mono text-gray-400">{Math.round(hysteresis * 100)}%</span>
    </div>
    <p class="mb-1.5 text-[10px] text-gray-600">Smooths color at thresholds to prevent flickering</p>
    <input
      type="range" step="1" min="0" max="15"
      value={Math.round(hysteresis * 100)}
      oninput={(e) => onchange({ ...gaugeDef, colorHysteresis: parseInt((e.target as HTMLInputElement).value) / 100 })}
      class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
        [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
        [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
    />
    <div class="relative mt-0.5 h-4 text-[9px] text-gray-600 overflow-visible select-none">
      <span style="position:absolute;left:0;white-space:nowrap">Off</span>
      <span style="position:absolute;left:33.3%;transform:translateX(-50%);white-space:nowrap">5%</span>
      <span style="position:absolute;left:66.7%;transform:translateX(-50%);white-space:nowrap">10%</span>
      <span style="position:absolute;right:0;white-space:nowrap">15%</span>
    </div>
  </div>
</div>

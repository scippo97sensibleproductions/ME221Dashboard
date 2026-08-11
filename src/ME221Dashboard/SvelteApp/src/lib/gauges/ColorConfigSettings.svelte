<script lang="ts">
  import type { GaugeConfigEntry } from '../HybridBridge';
  import { DEFAULT_COLOR_STOPS } from './types';

  let { gaugeDef, onchange, minValue = 0, maxValue = 10000, unit = '' }: {
    gaugeDef: GaugeConfigEntry;
    onchange: (def: GaugeConfigEntry) => void;
    /** Entity value range — the percentage is a position within this range. */
    minValue?: number;
    maxValue?: number;
    unit?: string;
  } = $props();

  const stops = $derived(gaugeDef.colorStops?.length ? gaugeDef.colorStops : DEFAULT_COLOR_STOPS);
  const hysteresis = $derived(gaugeDef.colorHysteresis ?? 0.03);

  const range = $derived(maxValue - minValue > 0 ? maxValue - minValue : 1);

  // A stop's percentage = where it sits between the gauge's min and max value.
  function valueAtFraction(fraction: number): number {
    return minValue + fraction * range;
  }
  function fractionAtValue(value: number): number {
    return Math.max(0, Math.min(1, (value - minValue) / range));
  }

  // Format like the gauge does: whole numbers stay whole, decimals get 1 digit.
  function formatStopValue(v: number): string {
    return Math.abs(v) >= 100 ? v.toFixed(0) : v.toFixed(1).replace(/\.0$/, '');
  }

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

  function setStopValue(idx: number, value: number) {
    const updated = stops.map((s, i) => i === idx ? { ...s, fraction: fractionAtValue(value) } : s);
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
    <div>
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Color Stops</p>
      <p class="text-[9px] text-gray-600">Each stop is a position between the min and max value</p>
    </div>
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
  <div class="flex justify-between -mt-1 text-[9px] font-mono text-gray-600">
    <span>{formatStopValue(minValue)}{unit ? ` ${unit}` : ''}</span>
    <span>{formatStopValue(maxValue)}{unit ? ` ${unit}` : ''}</span>
  </div>

  <!-- Stops -->
  {#each stops as stop, idx (idx)}
    {@const stopValue = valueAtFraction(stop.fraction)}
    {@const stopValueText = formatStopValue(stopValue)}
    <div class="flex items-center gap-2.5">
      <!-- Color picker -->
      <input
        type="color"
        value={toHex(stop.r, stop.g, stop.b)}
        onchange={(e) => setStopColor(idx, (e.target as HTMLInputElement).value)}
        class="h-8 w-8 shrink-0 cursor-pointer rounded-md border border-gray-700 bg-transparent p-0"
      />
      <!-- Value + fraction slider -->
      <div class="flex-1 min-w-0">
        <div class="flex items-center justify-between mb-0.5">
          <input
            type="number"
            value={stopValueText}
            onchange={(e) => {
              const v = parseFloat((e.target as HTMLInputElement).value);
              if (!isNaN(v)) setStopValue(idx, v);
            }}
            title="Value at this color stop"
            class="w-20 rounded border border-gray-700 bg-gray-800/60 px-1.5 py-0.5 text-[11px] font-mono text-gray-300 outline-none focus:border-cyan-500/50"
          />
          <span class="text-[10px] font-mono text-gray-400">{unit ? `${unit} · ` : ''}{Math.round(stop.fraction * 100)}%</span>
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

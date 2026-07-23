<script lang="ts">
  import type { GaugeConfigEntry } from '../HybridBridge';

  let { gaugeDef, onchange }: {
    gaugeDef: GaugeConfigEntry;
    onchange: (def: GaugeConfigEntry) => void;
  } = $props();

  function set<K extends keyof GaugeConfigEntry>(key: K, value: GaugeConfigEntry[K]) {
    onchange({ ...gaugeDef, [key]: value });
  }

  const enabled = $derived(gaugeDef.smoothingResponseMs > 0 || gaugeDef.spikeGatePercent > 0);

  const responseLabel = $derived.by(() => {
    const ms = gaugeDef.smoothingResponseMs;
    if (ms <= 0) return 'Off';
    if (ms < 100) return `${ms.toFixed(0)}ms`;
    if (ms < 500) return `${ms.toFixed(0)}ms`;
    if (ms < 1500) return `${(ms / 1000).toFixed(1)}s`;
    return `${(ms / 1000).toFixed(1)}s`;
  });

  const spikeLabel = $derived.by(() => {
    const pct = gaugeDef.spikeGatePercent;
    if (pct <= 0) return 'Off';
    return `${pct.toFixed(0)}%`;
  });

  function toggleEnabled() {
    if (enabled) {
      set('smoothingResponseMs', 0);
      set('spikeGatePercent', 0);
    } else {
      set('smoothingResponseMs', 300);
      set('spikeGatePercent', 0);
    }
  }
</script>

<div>
  <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Smoothing</p>

  <!-- Toggle row -->
  <div class="flex items-center justify-between rounded bg-gray-800/60 px-3 py-2">
    <span class="text-xs text-gray-300">Enable smoothing</span>
    <button
      class="relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none"
      style="background-color: {enabled ? '#107C10' : '#333'};"
      role="switch"
      aria-checked={enabled}
      aria-label="Toggle smoothing"
      onclick={toggleEnabled}
    >
      <span
        class="pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow-lg ring-0 transition duration-200 ease-in-out"
        style="transform: translateX({enabled ? '18px' : '0'})"
      ></span>
    </button>
  </div>

  {#if enabled}
    <!-- Response Time -->
    <div class="mt-3">
      <div class="flex items-center justify-between mb-1.5">
        <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Response time</p>
        <span class="text-xs font-mono text-cyan-400">{responseLabel}</span>
      </div>
      <div class="rounded bg-gray-800/60 px-3 py-2">
        <input
          type="range"
          step="10"
          min="50"
          max="3000"
          value={gaugeDef.smoothingResponseMs}
          oninput={(e) => set('smoothingResponseMs', parseFloat((e.target as HTMLInputElement).value))}
          class="w-full h-1.5 rounded-full appearance-none bg-gray-700 cursor-pointer
            [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
            [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500
            [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
        />
        <p class="mt-1.5 text-[10px] text-gray-500">How long the display takes to catch up to a sudden change</p>
      </div>
    </div>

    <!-- Spike Rejection -->
    <div class="mt-3">
      <div class="flex items-center justify-between mb-1.5">
        <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Spike rejection</p>
        <span class="text-xs font-mono text-cyan-400">{spikeLabel}</span>
      </div>
      <div class="rounded bg-gray-800/60 px-3 py-2">
        <input
          type="range"
          step="1"
          min="0"
          max="50"
          value={gaugeDef.spikeGatePercent}
          oninput={(e) => set('spikeGatePercent', parseFloat((e.target as HTMLInputElement).value))}
          class="w-full h-1.5 rounded-full appearance-none bg-gray-700 cursor-pointer
            [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
            [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500
            [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
        />
        <p class="mt-1.5 text-[10px] text-gray-500">Ignores jumps larger than this % of the gauge range</p>
      </div>
    </div>
  {/if}
</div>

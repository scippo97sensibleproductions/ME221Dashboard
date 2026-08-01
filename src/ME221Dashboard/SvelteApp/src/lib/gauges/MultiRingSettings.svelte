<script lang="ts">
  import type { GaugeConfigEntry } from '../HybridBridge';

  let { gaugeDef, onchange }: {
    gaugeDef: GaugeConfigEntry;
    onchange: (def: GaugeConfigEntry) => void;
  } = $props();

  function set<K extends keyof GaugeConfigEntry>(key: K, value: GaugeConfigEntry[K]) {
    onchange({ ...gaugeDef, [key]: value });
  }

  const ringCount = $derived(gaugeDef.ringCount ?? 5);
  const ringWidth = $derived(gaugeDef.ringWidth ?? 0);
  const ringGap = $derived(gaugeDef.ringGap ?? 0);
  const sweepAngle = $derived(gaugeDef.ringSweepAngle ?? 270);
  const peakHoldEnabled = $derived(gaugeDef.peakHoldEnabled ?? true);
  const peakResetSec = $derived(gaugeDef.peakHoldAutoResetSec ?? 0);

  const SWEEP_PRESETS = [
    { value: 270, label: '270°', hint: 'Default' },
    { value: 180, label: '180°', hint: 'Half' },
    { value: 360, label: '360°', hint: 'Full' },
  ];

  const SLIDER =
    'w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer ' +
    '[&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4 ' +
    '[&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg';
</script>

<div class="space-y-4">
  <!-- Ring Count (R16) -->
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Ring Count</p>
      <span class="text-xs font-mono text-cyan-400">{ringCount}/5</span>
    </div>
    <div class="flex gap-1">
      {#each [1, 2, 3, 4, 5] as n}
        <button
          class="flex-1 rounded px-2 py-2 text-xs font-medium transition-colors min-h-[36px]
            {ringCount === n
              ? 'bg-cyan-600 text-white'
              : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
          onclick={() => set('ringCount', n)}
        >{n}</button>
      {/each}
    </div>
    <p class="mt-0.5 text-[10px] text-gray-500">One ring per linked channel, outermost first.</p>
  </div>

  <!-- Ring Width (R16) -->
  <div class="border-t border-gray-700/30 pt-4">
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Ring Width</p>
      <span class="text-xs font-mono text-cyan-400">{ringWidth === 0 ? 'Auto' : `${ringWidth}px`}</span>
    </div>
    <input
      type="range" step="1" min="0" max="40"
      value={ringWidth}
      oninput={(e) => set('ringWidth', parseInt((e.target as HTMLInputElement).value))}
      class={SLIDER}
    />
    <div class="relative mt-0.5 h-3 text-[9px] text-gray-600 overflow-visible select-none">
      <span style="position:absolute;left:0">Auto</span>
      <span style="position:absolute;right:0">40px</span>
    </div>
  </div>

  <!-- Ring Gap (R16) -->
  <div class="border-t border-gray-700/30 pt-4">
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Ring Gap</p>
      <span class="text-xs font-mono text-cyan-400">{ringGap === 0 ? 'Auto' : `${ringGap}px`}</span>
    </div>
    <input
      type="range" step="1" min="0" max="40"
      value={ringGap}
      oninput={(e) => set('ringGap', parseInt((e.target as HTMLInputElement).value))}
      class={SLIDER}
    />
    <div class="relative mt-0.5 h-3 text-[9px] text-gray-600 overflow-visible select-none">
      <span style="position:absolute;left:0">Auto</span>
      <span style="position:absolute;right:0">40px</span>
    </div>
  </div>

  <!-- Sweep Angle (R17) -->
  <div class="border-t border-gray-700/30 pt-4">
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Sweep Angle</p>
      <span class="text-xs font-mono text-cyan-400">{sweepAngle}°</span>
    </div>
    <div class="grid grid-cols-3 gap-1">
      {#each SWEEP_PRESETS as opt}
        <button
          class="flex flex-col items-center justify-center rounded-lg border px-2 py-2 text-xs font-medium transition-all min-h-[44px]
            {sweepAngle === opt.value
              ? 'border-cyan-500/50 bg-cyan-500/10 text-cyan-300'
              : 'border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
          onclick={() => set('ringSweepAngle', opt.value)}
        >
          <span>{opt.label}</span>
          <span class="text-[9px] text-gray-500">{opt.hint}</span>
        </button>
      {/each}
    </div>
    <p class="mt-0.5 text-[10px] text-gray-500">Dial stays centered at the top.</p>
  </div>

  <!-- Peak Hold (R18) -->
  <div class="border-t border-gray-700/30 pt-4">
    <div class="flex items-center justify-between rounded bg-gray-800/60 px-3 py-2">
      <div>
        <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Peak Hold</p>
        <p class="text-[10px] text-gray-500">White tick marks the highest value per ring</p>
      </div>
      <button
        class="relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none"
        style="background-color: {peakHoldEnabled ? 'var(--metro-purple)' : 'rgb(55,65,81)'}"
        role="switch"
        aria-checked={peakHoldEnabled}
        aria-label="Toggle peak hold markers"
        onclick={() => set('peakHoldEnabled', !peakHoldEnabled)}
      >
        <span
          class="pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow-lg ring-0 transition duration-200 ease-in-out"
          style="transform: translateX({peakHoldEnabled ? '18px' : '0'})"
        ></span>
      </button>
    </div>

    <div class="mt-3">
      <div class="flex items-center justify-between mb-1.5">
        <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Auto Reset</p>
        <span class="text-xs font-mono text-cyan-400">{peakResetSec === 0 ? 'Never' : `${peakResetSec}s`}</span>
      </div>
      <input
        type="range" step="1" min="0" max="120"
        value={peakResetSec}
        oninput={(e) => set('peakHoldAutoResetSec', parseInt((e.target as HTMLInputElement).value))}
        class={SLIDER}
      />
      <div class="relative mt-0.5 h-3 text-[9px] text-gray-600 overflow-visible select-none">
        <span style="position:absolute;left:0">0 · Never</span>
        <span style="position:absolute;left:50%;transform:translateX(-50%)">60</span>
        <span style="position:absolute;right:0">120</span>
      </div>
      <p class="text-[10px] text-gray-500 mt-1">
        {peakResetSec > 0 ? `Each peak clears ${peakResetSec}s after it is set.` : 'Peaks stay until reset.'}
        Double-clicking the gauge always resets all peaks.
      </p>
    </div>
  </div>

  <!-- Linked entities stay in the Gauge Builder -->
  <div class="border-t border-gray-700/30 pt-4">
    <p class="text-[10px] text-gray-500">Linked channels and their colors are managed in the Gauge Builder.</p>
  </div>
</div>

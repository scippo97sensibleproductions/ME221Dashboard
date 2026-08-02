<script lang="ts">
  import type { GaugeConfigEntry } from '../HybridBridge';
  import { DigitalStyle } from './types';

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

  const currentStyle = $derived(gaugeDef.digitalStyle as DigitalStyle);
  const ledApplicable = $derived(
    currentStyle === DigitalStyle.Odometer ||
    currentStyle === DigitalStyle.LargeDigit ||
    currentStyle === DigitalStyle.SevenSegment
  );
  const bgApplicable = $derived(
    currentStyle === DigitalStyle.Odometer ||
    currentStyle === DigitalStyle.LargeDigit ||
    currentStyle === DigitalStyle.SevenSegment ||
    currentStyle === DigitalStyle.Cluster ||
    currentStyle === DigitalStyle.LabelTop
  );
  const glowApplicable = $derived(
    currentStyle === DigitalStyle.Odometer ||
    currentStyle === DigitalStyle.LargeDigit ||
    currentStyle === DigitalStyle.SevenSegment ||
    currentStyle === DigitalStyle.GlowRing ||
    currentStyle === DigitalStyle.InsetDisplay
  );
  const formatApplicable = $derived(
    currentStyle === DigitalStyle.Odometer ||
    currentStyle === DigitalStyle.LargeDigit ||
    currentStyle === DigitalStyle.SevenSegment ||
    currentStyle === DigitalStyle.Cluster ||
    currentStyle === DigitalStyle.InsetDisplay
  );
  const rollOn = $derived(!!gaugeDef.rollAnimation);
  const decimals = $derived(gaugeDef.digitDecimals ?? -1);
</script>

<div class="space-y-4">

  <!-- Style: visual cards -->
  <div>
    <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Display Style</p>
    <div class="grid grid-cols-3 sm:grid-cols-4 gap-1.5">
      {#each styles as style (style.value)}
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

  <!-- Digit Theme -->
  <div>
    <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Digit Theme</p>
    <div class="space-y-3">
      <div>
        <div class="flex items-center justify-between mb-1.5">
          <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">LED Color</p>
        </div>
        <div class="flex items-center gap-2 {ledApplicable ? '' : 'opacity-40'}" class:pointer-events-none={!ledApplicable}>
          <input type="color" value={gaugeDef.ledColor ?? '#ff3333'}
            oninput={(e) => set('ledColor', (e.target as HTMLInputElement).value)}
            disabled={!ledApplicable}
            class="w-8 h-8 rounded border border-gray-600 cursor-pointer bg-transparent"
          />
          <span class="text-xs font-mono text-gray-400">{gaugeDef.ledColor ?? '#ff3333'}</span>
        </div>
        {#if !ledApplicable}
          <p class="mt-1 text-[9px] italic text-gray-600">applies to seven-segment styles</p>
        {/if}
      </div>

      <div>
        <div class="flex items-center justify-between mb-1.5">
          <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Digit Background</p>
        </div>
        <div class="flex items-center gap-2 {bgApplicable ? '' : 'opacity-40'}" class:pointer-events-none={!bgApplicable}>
          <input type="color" value={gaugeDef.digitBgColor ?? '#1a1a1a'}
            oninput={(e) => set('digitBgColor', (e.target as HTMLInputElement).value)}
            disabled={!bgApplicable}
            class="w-8 h-8 rounded border border-gray-600 cursor-pointer bg-transparent"
          />
          <span class="text-xs font-mono text-gray-400">{gaugeDef.digitBgColor ?? '#1a1a1a'}</span>
        </div>
        {#if !bgApplicable}
          <p class="mt-1 text-[9px] italic text-gray-600">applies to seven-segment styles</p>
        {/if}
      </div>

      <div>
        <div class="flex items-center justify-between mb-1.5">
          <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Glow Strength</p>
          <span class="text-xs font-mono text-cyan-400">{Math.round((gaugeDef.glowStrength ?? 0) * 100)}%</span>
        </div>
        <div class={glowApplicable ? '' : 'opacity-40'} class:pointer-events-none={!glowApplicable}>
          <input
            type="range" min="0" max="100" step="5"
            value={Math.round((gaugeDef.glowStrength ?? 0) * 100)}
            oninput={(e) => set('glowStrength', parseInt((e.target as HTMLInputElement).value) / 100)}
            disabled={!glowApplicable}
            class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
              [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
              [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
          />
        </div>
        {#if !glowApplicable}
          <p class="mt-1 text-[9px] italic text-gray-600">applies to seven-segment styles</p>
        {/if}
      </div>
    </div>
  </div>

  <!-- Format -->
  <div>
    <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Format</p>
    <div class="space-y-3 {formatApplicable ? '' : 'opacity-40'}" class:pointer-events-none={!formatApplicable}>
      <div>
        <div class="flex items-center justify-between mb-1.5">
          <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Decimals</p>
        </div>
        <div class="flex gap-1">
          {#each [{ v: -1, label: 'Auto' }, { v: 0, label: '0' }, { v: 1, label: '1' }, { v: 2, label: '2' }, { v: 3, label: '3' }] as opt (opt.v)}
            <button
              disabled={!formatApplicable}
              class="flex-1 rounded px-1.5 py-1.5 text-[10px] font-medium transition-colors min-h-[28px]
                {decimals === opt.v
                  ? 'bg-cyan-600 text-white'
                  : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
              onclick={() => set('digitDecimals', opt.v)}
            >{opt.label}</button>
          {/each}
        </div>
      </div>
      <button
        disabled={!formatApplicable}
        class="flex items-center justify-between w-full rounded px-2 py-1.5 text-[10px] font-medium transition-colors
          {(gaugeDef.zeroPadding ?? false) ? 'text-cyan-400 hover:text-cyan-300' : 'text-gray-500 hover:text-gray-400'}"
        onclick={() => set('zeroPadding', !gaugeDef.zeroPadding)}
      >
        <span>Zero Padding</span>
        <span class="text-[10px]">{(gaugeDef.zeroPadding ?? false) ? 'ON' : 'OFF'}</span>
      </button>
      <div>
        <div class="flex items-center justify-between mb-1.5">
          <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Min Digit Count</p>
          <span class="text-xs font-mono text-cyan-400">{gaugeDef.minDigitCount ?? 0}</span>
        </div>
        <input
          type="range" min="0" max="12" step="1"
          value={gaugeDef.minDigitCount ?? 0}
          oninput={(e) => set('minDigitCount', parseInt((e.target as HTMLInputElement).value))}
          disabled={!formatApplicable}
          class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
            [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
            [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
        />
      </div>
    </div>
    {#if !formatApplicable}
      <p class="mt-1 text-[9px] italic text-gray-600">applies to seven-segment styles</p>
    {/if}
  </div>

  <!-- Roll Animation -->
  <div>
    <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Roll Animation</p>
    <div class="space-y-3">
      <button
        class="flex items-center justify-between w-full rounded px-2 py-1.5 text-[10px] font-medium transition-colors
          {rollOn ? 'text-cyan-400 hover:text-cyan-300' : 'text-gray-500 hover:text-gray-400'}"
        onclick={() => set('rollAnimation', !rollOn)}
      >
        <span>Animate Digit Rolls</span>
        <span class="text-[10px]">{rollOn ? 'ON' : 'OFF'}</span>
      </button>
      <div class={rollOn ? '' : 'opacity-40'} class:pointer-events-none={!rollOn}>
        <div class="flex items-center justify-between mb-1.5">
          <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Speed</p>
          <span class="text-xs font-mono text-cyan-400">{gaugeDef.rollSpeedMs ?? 300}ms</span>
        </div>
        <input
          type="range" min="50" max="2000" step="50"
          value={gaugeDef.rollSpeedMs ?? 300}
          oninput={(e) => set('rollSpeedMs', parseInt((e.target as HTMLInputElement).value))}
          disabled={!rollOn}
          class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
            [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
            [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
        />
      </div>
    </div>
  </div>

</div>

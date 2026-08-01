<script lang="ts">
  import { onMount } from 'svelte';
  import { HybridBridge, type GaugeConfigEntry, type AvailableSensor } from '../HybridBridge';
  import { IconX, IconSearch } from '@tabler/icons-svelte';

  let { gaugeDef, onchange }: {
    gaugeDef: GaugeConfigEntry;
    onchange: (def: GaugeConfigEntry) => void;
  } = $props();

  function set<K extends keyof GaugeConfigEntry>(key: K, value: GaugeConfigEntry[K]) {
    onchange({ ...gaugeDef, [key]: value });
  }

  const timeWindowOptions = [10, 30, 60, 120, 300, 600];

  // ── Overlay lines (R22) ──
  const MAX_OVERLAYS = 5;
  const OVERLAY_PALETTE = ['#f59e0b', '#a78bfa', '#fb7185', '#4ade80', '#60a5fa'];
  const LINE_STYLE_LABELS = ['Solid', 'Dashed', 'Dotted'];

  let sensors = $state<AvailableSensor[]>([]);
  let pickerOpen = $state(false);
  let searchText = $state('');

  const overlays = $derived(gaugeDef.chartOverlays ?? []);

  const filteredSensors = $derived.by(() => {
    const q = searchText.trim().toLowerCase();
    if (!q) return sensors.slice(0, 40);
    return sensors.filter(s =>
      s.name.toLowerCase().includes(q) ||
      s.category.toLowerCase().includes(q) ||
      s.unit.toLowerCase().includes(q) ||
      String(s.id).includes(q)
    ).slice(0, 40);
  });

  onMount(async () => {
    try {
      const result = await HybridBridge.getAvailableSensors('default');
      sensors = result.sensors;
    } catch {
      sensors = [];
    }
  });

  function sensorName(id: number): string {
    const s = sensors.find(x => x.id === id);
    return s ? (s.customization?.customName || s.name) : `Entity ${id}`;
  }

  function addOverlay(entityId: number) {
    if (overlays.length >= MAX_OVERLAYS) return;
    if (overlays.some(o => o.entityId === entityId)) return;
    const color = OVERLAY_PALETTE[overlays.length % OVERLAY_PALETTE.length];
    set('chartOverlays', [...overlays, { entityId, color, lineWidth: 1.5, lineStyle: 0 }]);
  }

  function updateOverlay(index: number, patch: Partial<{ color: string; lineWidth: number; lineStyle: number }>) {
    const next = overlays.map((o, i) => i === index ? { ...o, ...patch } : o);
    set('chartOverlays', next);
  }

  function removeOverlay(index: number) {
    set('chartOverlays', overlays.filter((_, i) => i !== index));
  }

  const pillPositions = [
    { value: 0, label: '↗', title: 'Top Right' },
    { value: 1, label: '↖', title: 'Top Left' },
    { value: 2, label: '↘', title: 'Bottom Right' },
    { value: 3, label: '↙', title: 'Bottom Left' },
  ];
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

  <!-- Line Style (R24) -->
  <div>
    <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-1.5">Line Style</p>
    <div class="flex gap-1">
      {#each LINE_STYLE_LABELS as label, i}
        <button
          class="flex-1 rounded px-2 py-1.5 text-[10px] font-medium transition-colors min-h-[28px]
            {(gaugeDef.chartLineStyle ?? 0) === i
              ? 'bg-cyan-600 text-white'
              : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
          onclick={() => set('chartLineStyle', i)}
        >{label}</button>
      {/each}
    </div>
  </div>

  <!-- Background Color (R24) -->
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Background Color</p>
      {#if gaugeDef.chartBackgroundColor}
        <button class="text-[9px] text-gray-500 hover:text-gray-300" onclick={() => set('chartBackgroundColor', '')}>Clear</button>
      {/if}
    </div>
    <div class="flex items-center gap-2">
      <input type="color"
        value={gaugeDef.chartBackgroundColor || '#000000'}
        oninput={(e) => set('chartBackgroundColor', (e.target as HTMLInputElement).value)}
        class="w-8 h-8 rounded border border-gray-600 cursor-pointer bg-transparent"
      />
      <span class="text-xs font-mono text-gray-400">{gaugeDef.chartBackgroundColor || 'transparent'}</span>
    </div>
  </div>

  <!-- Readout Pill (R23) -->
  <div>
    <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-1.5">Readout Position</p>
    <div class="grid grid-cols-4 gap-1">
      {#each pillPositions as pos}
        <button
          title={pos.title}
          class="rounded px-2 py-1.5 text-sm font-medium transition-colors min-h-[32px]
            {(gaugeDef.overlayPillPosition ?? 0) === pos.value
              ? 'bg-cyan-600 text-white'
              : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
          onclick={() => set('overlayPillPosition', pos.value)}
        >{pos.label}</button>
      {/each}
    </div>
    <div class="flex items-center justify-between mt-2 mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Readout Font Scale</p>
      <span class="text-xs font-mono text-cyan-400">{(gaugeDef.overlayFontScale ?? 1).toFixed(2)}x</span>
    </div>
    <input
      type="range" step="0.05" min="0.5" max="2"
      value={gaugeDef.overlayFontScale ?? 1}
      oninput={(e) => set('overlayFontScale', parseFloat((e.target as HTMLInputElement).value))}
      class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
        [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
        [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg"
    />
    <div class="relative mt-0.5 h-3 text-[9px] text-gray-600 overflow-visible select-none">
      <span style="position:absolute;left:0">0.5x</span>
      <span style="position:absolute;left:33%;transform:translateX(-50%)">1x</span>
      <span style="position:absolute;right:0">2x</span>
    </div>
  </div>

  <!-- Overlay Lines (R22) -->
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Overlay Lines</p>
      <span class="text-xs font-mono text-cyan-400">{overlays.length}/5</span>
    </div>
    {#if overlays.length > 0}
      <div class="space-y-1.5 mb-2">
        {#each overlays as ov, i (ov.entityId)}
          <div class="rounded border border-gray-700 bg-gray-800/40 p-1.5">
            <div class="flex items-center gap-1.5">
              <span class="w-2.5 h-2.5 rounded-full shrink-0" style="background: {ov.color}"></span>
              <span class="flex-1 min-w-0 truncate text-[10px] font-medium text-gray-200">{sensorName(ov.entityId)}</span>
              <span class="text-[9px] text-gray-500 font-mono shrink-0">#{ov.entityId}</span>
              <button
                class="w-4 h-4 flex items-center justify-center rounded text-gray-500 hover:bg-gray-700 hover:text-red-400 shrink-0"
                onclick={() => removeOverlay(i)}
                title="Remove overlay"
              >
                <IconX size={10} />
              </button>
            </div>
            <div class="flex items-center gap-1.5 mt-1.5">
              <input type="color" value={ov.color}
                oninput={(e) => updateOverlay(i, { color: (e.target as HTMLInputElement).value })}
                class="w-6 h-6 rounded border border-gray-600 cursor-pointer bg-transparent shrink-0"
              />
              <div class="flex gap-0.5">
                {#each LINE_STYLE_LABELS as label, s}
                  <button
                    class="rounded px-1.5 py-0.5 text-[8px] font-medium transition-colors
                      {ov.lineStyle === s
                        ? 'bg-cyan-600 text-white'
                        : 'bg-gray-700 text-gray-400 hover:bg-gray-600 hover:text-gray-200'}"
                    onclick={() => updateOverlay(i, { lineStyle: s })}
                  >{label}</button>
                {/each}
              </div>
              <div class="flex items-center gap-1 ml-auto shrink-0">
                <button
                  class="w-5 h-5 flex items-center justify-center rounded bg-gray-700 text-gray-400 hover:bg-gray-600"
                  onclick={() => updateOverlay(i, { lineWidth: Math.max(0.5, (ov.lineWidth - 0.5)) })}
                >−</button>
                <span class="text-[9px] font-mono text-gray-400 w-7 text-center">{ov.lineWidth.toFixed(1)}</span>
                <button
                  class="w-5 h-5 flex items-center justify-center rounded bg-gray-700 text-gray-400 hover:bg-gray-600"
                  onclick={() => updateOverlay(i, { lineWidth: Math.min(6, (ov.lineWidth + 0.5)) })}
                >+</button>
              </div>
            </div>
          </div>
        {/each}
      </div>
    {/if}
    <button
      class="w-full rounded border border-dashed border-gray-600 py-1.5 text-[10px] font-medium text-gray-400 transition-colors hover:border-cyan-500 hover:text-cyan-300"
      onclick={() => { pickerOpen = true; searchText = ''; }}
    >+ Add Overlay</button>
  </div>

</div>

<!-- Entity picker sheet (R22) -->
{#if pickerOpen}
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="fixed inset-0 z-[70] flex items-end justify-center" style="background: rgba(0,0,0,0.55)" onclick={() => pickerOpen = false}>
    <!-- svelte-ignore a11y_click_events_have_key_events -->
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <div class="w-full max-w-md max-h-[65dvh] border border-gray-700 border-b-0 flex flex-col bg-gray-900"
         style="animation: sheetUp 0.15s ease-out"
         onclick={(e) => e.stopPropagation()}>
      <div class="flex items-center justify-between px-3 py-1.5 border-b border-gray-700">
        <span class="text-[9px] font-bold uppercase tracking-wider text-gray-400">ADD OVERLAY LINE</span>
        <button class="w-5 h-5 flex items-center justify-center text-gray-500 hover:text-white" onclick={() => pickerOpen = false}>
          <IconX size={14} />
        </button>
      </div>
      <div class="flex-1 overflow-y-auto p-2">
        {#if overlays.length >= MAX_OVERLAYS}
          <div class="flex items-center justify-between rounded bg-amber-500/10 border border-amber-500/30 px-2 py-1.5 mb-2">
            <span class="text-[9px] font-semibold uppercase tracking-wider text-amber-400">Max 5 overlay lines</span>
            <span class="text-[9px] text-amber-300">Remove one to add more</span>
          </div>
        {/if}
        <div class="relative mb-2">
          <IconSearch size={13} class="absolute left-2 top-1/2 -translate-y-1/2 text-gray-500" />
          <input
            type="text"
            placeholder="Search sensors..."
            bind:value={searchText}
            class="w-full rounded bg-gray-800 py-1.5 pl-7 pr-2 text-[11px] text-gray-200 placeholder-gray-500 outline-none focus:ring-1 focus:ring-cyan-500/50"
          />
        </div>
        {#if filteredSensors.length === 0}
          <div class="py-6 text-center text-xs text-gray-500">No sensors found</div>
        {:else}
          {#each filteredSensors as s}
            {@const used = overlays.some(o => o.entityId === s.id)}
            {@const atMax = overlays.length >= MAX_OVERLAYS}
            {@const disabled = used || atMax}
            <button
              class="flex items-center gap-2 w-full p-1.5 rounded bg-gray-800/50 hover:bg-gray-800 transition-colors mb-0.5 text-left"
              class:opacity-35={disabled}
              disabled={disabled}
              onclick={() => { addOverlay(s.id); pickerOpen = false; }}
            >
              <span class="w-1.5 h-1.5 rounded-full shrink-0" style="background: {OVERLAY_PALETTE[overlays.length % OVERLAY_PALETTE.length]}"></span>
              <span class="flex-1 text-[10px] font-semibold">{s.customization?.customName || s.name}</span>
              <span class="text-[8px] text-gray-500 font-mono">{s.unit || '—'}</span>
              <span class="text-[8px] text-gray-600 font-mono shrink-0">#{s.id}</span>
            </button>
          {/each}
        {/if}
      </div>
    </div>
  </div>
{/if}

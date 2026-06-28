<script lang="ts">
  import { onMount } from 'svelte';
  import { HybridBridge, type GaugeConfigEntry, type EntityInfo } from './HybridBridge';
  import { GaugeShapeCategory, toGaugeDefinition, formatValue } from './gauges/types';
  import ArcLayoutSettings from './gauges/ArcLayoutSettings.svelte';
  import ArcNeedleSettings from './gauges/ArcNeedleSettings.svelte';
  import BarGaugeSettings from './gauges/BarGaugeSettings.svelte';
  import DigitalGaugeSettings from './gauges/DigitalGaugeSettings.svelte';
  import ChartGaugeSettings from './gauges/ChartGaugeSettings.svelte';
  import ColorConfigSettings from './gauges/ColorConfigSettings.svelte';
  import SmoothingSettings from './gauges/SmoothingSettings.svelte';
  import GpsSpeedDebugSettings from './gauges/GpsSpeedDebugSettings.svelte';
  import GaugePreviewPanel from './GaugePreviewPanel.svelte';
  import { Modal, Button } from 'flowbite-svelte';
  import { IconX, IconChevronDown } from '@tabler/icons-svelte';

  let { open, gaugeDef, gaugeName, entityInfo, onclose, onchange }: {
    open: boolean;
    gaugeDef: GaugeConfigEntry;
    gaugeName: string;
    entityInfo: EntityInfo | null;
    onclose: () => void;
    onchange: (def: GaugeConfigEntry) => void;
  } = $props();

  const categories = [
    { value: GaugeShapeCategory.Arc, label: 'Arc' },
    { value: GaugeShapeCategory.Bar, label: 'Bar' },
    { value: GaugeShapeCategory.Text, label: 'Text' },
    { value: GaugeShapeCategory.Digital, label: 'Digital' },
    { value: GaugeShapeCategory.Chart, label: 'Chart' },
  ];

  let openSections = $state<Record<string, boolean>>({
    shape: false, layout: false, scale: false, labels: false, icon: false, appearance: false, color: false, smoothing: false, odometer: false
  });

  let iconPicking = $state(false);

  async function pickIcon() {
    iconPicking = true;
    try {
      const result = await HybridBridge.pickGaugeTexture(String(gaugeDef.entityId));
      if (result.picked && result.path) {
        onchange({ ...gaugeDef, iconName: result.path });
      }
    } finally {
      iconPicking = false;
    }
  }

  function removeIcon() {
    onchange({ ...gaugeDef, iconName: null });
  }

  function toggleSection(id: string) {
    openSections[id] = !openSections[id];
  }

  let testValue = $state<number | null>(null);

  const minValue = $derived(entityInfo?.minValue ?? 0);
  const maxValue = $derived(entityInfo?.maxValue ?? 10000);
  const liveValue = $derived(entityInfo ? ((entityInfo.minValue ?? 0) + (entityInfo.maxValue ?? 10000)) / 2 : 5000);

  const useTestValue = $derived(testValue !== null);
  const previewValue = $derived(testValue ?? liveValue);

  function setCategory(cat: GaugeShapeCategory) {
    let wf = gaugeDef.widthFraction;
    let hf = gaugeDef.heightFraction;
    switch (cat) {
      case GaugeShapeCategory.Text: wf = 0.15; hf = 0.10; break;
      case GaugeShapeCategory.Arc: wf = 0.22; hf = 0.28; break;
      case GaugeShapeCategory.Bar: wf = 0.30; hf = 0.08; break;
      case GaugeShapeCategory.Digital: wf = 0.22; hf = 0.16; break;
      case GaugeShapeCategory.Chart: wf = 0.35; hf = 0.20; break;
    }
    onchange({ ...gaugeDef, shapeCategory: cat, widthFraction: wf, heightFraction: hf });
  }

  function toggleLabel(key: 'showName' | 'showUnit' | 'showValue') {
    onchange({ ...gaugeDef, [key]: !gaugeDef[key] });
  }

  function onKeydown(e: KeyboardEvent) { if (e.key === 'Escape') onclose(); }

  const isOdometer = $derived(gaugeDef.entityId === -2001);
  let odometerValue = $state<number | null>(null);
  let odometerUnit = $state('km');
  let odometerSetDraft = $state('');
  let odometerSource = $state<'gps' | 'vss'>('gps');
  let vssSpeedInMph = $state(false);
  let odometerSourceLoading = $state(false);
  $effect(() => {
    if (open && isOdometer) {
      HybridBridge.getOdometer().then(r => { odometerValue = r.value; odometerUnit = r.unit; odometerSetDraft = String(Math.round(r.value)); odometerSource = r.speedSource as 'gps' | 'vss'; vssSpeedInMph = r.vssSpeedInMph; });
    }
  });

  const isGpsSpeed = $derived(gaugeDef.entityId === -1001);

  const isArc = $derived(gaugeDef.shapeCategory === GaugeShapeCategory.Arc);
  const isBar = $derived(gaugeDef.shapeCategory === GaugeShapeCategory.Bar);
  const isDigital = $derived(gaugeDef.shapeCategory === GaugeShapeCategory.Digital);
  const isText = $derived(gaugeDef.shapeCategory === GaugeShapeCategory.Text);
  const isChart = $derived(gaugeDef.shapeCategory === GaugeShapeCategory.Chart);
  const hasSmoothing = $derived(isArc || isDigital);
  const hasNeedle = $derived(isArc);
  const hasAppearance = $derived(isArc || isBar);

  const sectionColors: Record<string, { border: string; bg: string; text: string; headerBg: string; slider: string; activeBtn: string; activeBtnText: string }> = {
    shape:     { border: 'border-l-cyan-500',    bg: 'bg-cyan-500/5',    text: 'text-white', headerBg: 'bg-cyan-500/25', slider: 'accent-cyan-500',    activeBtn: 'bg-cyan-600',    activeBtnText: 'text-white' },
    layout:    { border: 'border-l-cyan-500',    bg: 'bg-cyan-500/5',    text: 'text-white', headerBg: 'bg-cyan-500/25', slider: 'accent-cyan-500',    activeBtn: 'bg-cyan-600',    activeBtnText: 'text-white' },
    scale:     { border: 'border-l-violet-500',  bg: 'bg-violet-500/5',  text: 'text-white', headerBg: 'bg-violet-500/25', slider: 'accent-violet-500',  activeBtn: 'bg-violet-600',  activeBtnText: 'text-white' },
    labels:    { border: 'border-l-violet-500',  bg: 'bg-violet-500/5',  text: 'text-white', headerBg: 'bg-violet-500/25', slider: 'accent-violet-500',  activeBtn: 'bg-violet-600',  activeBtnText: 'text-white' },
    icon:      { border: 'border-l-violet-500',  bg: 'bg-violet-500/5',  text: 'text-white', headerBg: 'bg-violet-500/25', slider: 'accent-violet-500',  activeBtn: 'bg-violet-600',  activeBtnText: 'text-white' },
    appearance:{ border: 'border-l-amber-500',   bg: 'bg-amber-500/5',   text: 'text-white', headerBg: 'bg-amber-500/25', slider: 'accent-amber-500',   activeBtn: 'bg-amber-600',   activeBtnText: 'text-white' },
    odometer:  { border: 'border-l-amber-500',   bg: 'bg-amber-500/5',   text: 'text-white', headerBg: 'bg-amber-500/25', slider: 'accent-amber-500',   activeBtn: 'bg-amber-600',   activeBtnText: 'text-white' },
    gpsDebug:  { border: 'border-l-amber-500',   bg: 'bg-amber-500/5',   text: 'text-white', headerBg: 'bg-amber-500/25', slider: 'accent-amber-500',   activeBtn: 'bg-amber-600',   activeBtnText: 'text-white' },
    color:     { border: 'border-l-rose-500',    bg: 'bg-rose-500/5',    text: 'text-white', headerBg: 'bg-rose-500/25', slider: 'accent-rose-500',    activeBtn: 'bg-rose-600',    activeBtnText: 'text-white' },
    smoothing: { border: 'border-l-emerald-500', bg: 'bg-emerald-500/5', text: 'text-white', headerBg: 'bg-emerald-500/25', slider: 'accent-emerald-500', activeBtn: 'bg-emerald-600', activeBtnText: 'text-white' },
  };
</script>

{#if open}
  <Modal {open} size="lg" placement="center" outsideclose={true} dismissable={false} class="backdrop:bg-gray-900/80" ontoggle={(e) => { if (e.newState === 'closed') onclose(); }}>
      <!-- Title bar with close button (inside body scroll context) -->
      <div class="flex items-center justify-between border-b border-gray-700/50 px-4 py-3">
        <div>
          <h3 class="text-sm font-bold text-cyan-400">{gaugeName}</h3>
          <p class="text-[10px] text-gray-500">Entity #{gaugeDef.entityId}</p>
        </div>
        <button class="rounded-lg p-2 text-gray-400 transition-colors hover:bg-gray-700 hover:text-gray-200" onclick={onclose}>
          <IconX size={18} />
        </button>
      </div>

      <!-- Body: preview + settings -->
      <div class="flex flex-col sm:flex-row max-h-[70vh] sm:overflow-y-auto">

        <!-- Preview Panel -->
        <div class="shrink-0 sm:sticky sm:top-0 sm:self-start">
          <GaugePreviewPanel
            {gaugeDef}
            {gaugeName}
            {entityInfo}
            {testValue}
            onTestValueChange={(v) => { testValue = v; }}
          />
        </div>

        <!-- Settings Panel -->
        <div class="flex-1 overflow-y-auto sm:overflow-visible min-h-0 min-w-0 px-4 py-3 space-y-2">

          <!-- 1. Shape -->
          <div class="rounded-lg border border-l-2 {sectionColors.shape.border} border-gray-700/50 overflow-hidden">
            <button class="flex w-full items-center justify-between px-3 py-2.5 text-xs font-semibold uppercase tracking-wider {sectionColors.shape.text} {sectionColors.shape.headerBg} transition-colors"
                    onclick={() => toggleSection('shape')}>
              <span>Shape</span>
              <IconChevronDown size={14} class="transition-transform duration-200 {openSections.shape ? 'rotate-180' : ''}" />
            </button>
            {#if openSections.shape}
              <div class="px-3 pb-3 pt-1 border-t border-gray-700/30 {sectionColors.shape.bg}">
                <div class="flex gap-1">
                  {#each categories as cat}
                    <button
                      class="flex-1 rounded px-2 py-2 text-xs font-medium transition-colors min-h-[36px]
                        {gaugeDef.shapeCategory === cat.value
                          ? sectionColors.shape.activeBtn + ' ' + sectionColors.shape.activeBtnText
                          : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
                      onclick={() => setCategory(cat.value)}
                    >{cat.label}</button>
                  {/each}
                </div>
              </div>
            {/if}
          </div>

          <!-- 2. Layout -->
          <div class="rounded-lg border border-l-2 {sectionColors.layout.border} border-gray-700/50 overflow-hidden">
            <button class="flex w-full items-center justify-between px-3 py-2.5 text-xs font-semibold uppercase tracking-wider {sectionColors.layout.text} {sectionColors.layout.headerBg} transition-colors"
                    onclick={() => toggleSection('layout')}>
              <span>Layout</span>
              <IconChevronDown size={14} class="transition-transform duration-200 {openSections.layout ? 'rotate-180' : ''}" />
            </button>
            {#if openSections.layout}
              <div class="px-3 pb-3 pt-1 border-t border-gray-700/30 {sectionColors.layout.bg}">
                {#if isArc}
                  <ArcLayoutSettings {gaugeDef} {onchange} {minValue} {maxValue} />
                {:else if isBar}
                  <BarGaugeSettings {gaugeDef} {onchange} />
                {:else if isDigital}
                  <DigitalGaugeSettings {gaugeDef} {onchange} />
                {:else if isChart}
                  <ChartGaugeSettings {gaugeDef} {onchange} />
                {:else}
                  <p class="text-xs text-gray-500">No layout settings for this shape.</p>
                {/if}
                <div class="mt-2">
                  <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-1">Layer</p>
                  <input
                    type="number"
                    class="w-full rounded bg-gray-700/80 px-3 py-2 text-sm text-gray-100 placeholder-gray-500 outline-none focus:ring-1 focus:ring-cyan-500/50"
                    value={gaugeDef.zIndex ?? 0}
                    onchange={(e) => {
                      const val = parseInt((e.target as HTMLInputElement).value) || 0;
                      onchange({ ...gaugeDef, zIndex: val });
                    }}
                  />
                  <p class="text-[10px] text-gray-500 mt-0.5">Higher = on top</p>
                </div>
              </div>
            {/if}
          </div>

          <!-- 2b. Scale (all shapes) -->
          <div class="rounded-lg border border-l-2 {sectionColors.scale.border} border-gray-700/50 overflow-hidden">
            <button class="flex w-full items-center justify-between px-3 py-2.5 text-xs font-semibold uppercase tracking-wider {sectionColors.scale.text} {sectionColors.scale.headerBg} transition-colors"
                    onclick={() => toggleSection('scale')}>
              <span>Scale</span>
              <IconChevronDown size={14} class="transition-transform duration-200 {openSections.scale ? 'rotate-180' : ''}" />
            </button>
            {#if openSections.scale}
              <div class="px-3 pb-3 pt-1 border-t border-gray-700/30 {sectionColors.scale.bg}">
                <div>
                  <div class="flex items-center justify-between mb-1.5">
                    <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Size Scale</p>
                    <span class="text-xs font-mono {sectionColors.scale.text}">{(gaugeDef.scale ?? 1.0).toFixed(2)}x</span>
                  </div>
                  <input
                    type="range" step="0.05" min="0.2" max="3"
                    value={gaugeDef.scale ?? 1.0}
                    oninput={(e) => onchange({ ...gaugeDef, scale: parseFloat((e.target as HTMLInputElement).value) })}
                    class="w-full h-1.5 rounded-full appearance-none bg-gray-700 {sectionColors.scale.slider} cursor-pointer
                      [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
                      [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-violet-500 [&::-webkit-slider-thumb]:shadow-lg"
                  />
                  <div class="relative mt-0.5 h-4 text-[9px] text-gray-600 overflow-visible select-none">
                    <span style="position:absolute;left:0;white-space:nowrap">0.2x</span>
                    <span style="position:absolute;left:10.7%;transform:translateX(-50%);white-space:nowrap">0.5x</span>
                    <span style="position:absolute;left:28.6%;transform:translateX(-50%);white-space:nowrap">1.0x</span>
                    <span style="position:absolute;left:64.3%;transform:translateX(-50%);white-space:nowrap">2.0x</span>
                    <span style="position:absolute;right:0;white-space:nowrap">3.0x</span>
                  </div>
                </div>
              </div>
            {/if}
          </div>

          <!-- 3. Labels -->
          <div class="rounded-lg border border-l-2 {sectionColors.labels.border} border-gray-700/50 overflow-hidden">
            <button class="flex w-full items-center justify-between px-3 py-2.5 text-xs font-semibold uppercase tracking-wider {sectionColors.labels.text} {sectionColors.labels.headerBg} transition-colors"
                    onclick={() => toggleSection('labels')}>
              <span>Labels</span>
              <IconChevronDown size={14} class="transition-transform duration-200 {openSections.labels ? 'rotate-180' : ''}" />
            </button>
            {#if openSections.labels}
              <div class="px-3 pb-3 pt-1 border-t border-gray-700/30 {sectionColors.labels.bg} space-y-3">
                <div class="grid grid-cols-3 gap-1">
                  {#each [
                    { key: 'showValue' as const, label: 'Value' },
                    { key: 'showUnit' as const, label: 'Unit' },
                    { key: 'showName' as const, label: 'Name' },
                  ] as lbl}
                    <button
                      class="flex items-center justify-center gap-1.5 rounded px-2 py-2 text-xs font-medium transition-colors min-h-[36px]
                        {gaugeDef[lbl.key]
                          ? 'bg-violet-600/20 text-violet-300'
                          : 'border border-gray-600 text-gray-500 hover:border-gray-500 hover:text-gray-400'}"
                      onclick={() => toggleLabel(lbl.key)}>
                      <span>{lbl.label}</span>
                      <span class="text-[10px]">{gaugeDef[lbl.key] ? 'ON' : 'OFF'}</span>
                    </button>
                  {/each}
                </div>

                <div>
                  <div class="flex items-center justify-between mb-1.5">
                    <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Font Scale</p>
                    <span class="text-xs font-mono {sectionColors.labels.text}">{(gaugeDef.fontSizeScale ?? 1.0).toFixed(2)}x</span>
                  </div>
                  <input
                    type="range" step="0.05" min="0.5" max="2"
                    value={gaugeDef.fontSizeScale ?? 1.0}
                    oninput={(e) => onchange({ ...gaugeDef, fontSizeScale: parseFloat((e.target as HTMLInputElement).value) })}
                    class="w-full h-1.5 rounded-full appearance-none bg-gray-700 {sectionColors.labels.slider} cursor-pointer
                      [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
                      [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-violet-500 [&::-webkit-slider-thumb]:shadow-lg"
                  />
                  <div class="relative mt-0.5 h-4 text-[9px] text-gray-600 overflow-visible select-none">
                    <span style="position:absolute;left:0;white-space:nowrap">0.5x</span>
                    <span style="position:absolute;left:16.7%;transform:translateX(-50%);white-space:nowrap">0.75x</span>
                    <span style="position:absolute;left:33.3%;transform:translateX(-50%);white-space:nowrap">1.0x</span>
                    <span style="position:absolute;left:66.7%;transform:translateX(-50%);white-space:nowrap">1.5x</span>
                    <span style="position:absolute;right:0;white-space:nowrap">2.0x</span>
                  </div>
                </div>

                <!-- Text Color -->
                <div>
                  <div class="flex items-center justify-between mb-1.5">
                    <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Text Color</p>
                    <span class="text-xs font-mono text-gray-400">{gaugeDef.textColor ?? '#ffffff'}</span>
                  </div>
                  <div class="flex items-center gap-2">
                    <input type="color"
                      value={gaugeDef.textColor ?? '#ffffff'}
                      oninput={(e) => onchange({ ...gaugeDef, textColor: (e.target as HTMLInputElement).value })}
                      class="w-10 h-10 rounded-lg border border-gray-600 bg-gray-800 cursor-pointer p-0.5 min-h-[40px]" />
                    <div class="flex gap-1 flex-wrap">
                      {#each ['#ffffff', '#e0e0e0', '#fbbf24', '#22d3ee', '#a78bfa', '#f87171', '#4ade80'] as color}
                        <button
                          class="w-7 h-7 rounded-md border-2 transition-colors min-h-[28px] min-w-[28px]
                            {(gaugeDef.textColor ?? '#ffffff') === color
                              ? 'border-white scale-110'
                              : 'border-gray-600 hover:border-gray-400'}"
                          style="background: {color};"
                          onclick={() => onchange({ ...gaugeDef, textColor: color })}
                          title={color}
                        ></button>
                      {/each}
                    </div>
                  </div>
                </div>
              </div>
            {/if}
          </div>
          <div class="rounded-lg border border-l-2 {sectionColors.icon.border} border-gray-700/50 overflow-hidden">
            <button class="flex w-full items-center justify-between px-3 py-2.5 text-xs font-semibold uppercase tracking-wider {sectionColors.icon.text} {sectionColors.icon.headerBg} transition-colors"
                    onclick={() => toggleSection('icon')}>
              <span>Icon</span>
              <IconChevronDown size={14} class="transition-transform duration-200 {openSections.icon ? 'rotate-180' : ''}" />
            </button>
            {#if openSections.icon}
              <div class="px-3 pb-3 pt-1 border-t border-gray-700/30 {sectionColors.icon.bg} space-y-3">
                <div class="flex items-center gap-2">
                  <button
                    class="rounded-lg border border-gray-600 px-3 py-2 text-xs font-medium text-gray-300 transition-colors hover:border-violet-500 hover:text-violet-300 disabled:opacity-50 min-h-[36px]"
                    onclick={pickIcon}
                    disabled={iconPicking}
                  >
                    {iconPicking ? 'Picking...' : 'Pick Image'}
                  </button>
                  {#if gaugeDef.iconName}
                    <button
                      class="rounded-lg border border-red-600/50 px-3 py-2 text-xs font-medium text-red-400 transition-colors hover:border-red-500 hover:text-red-300 min-h-[36px]"
                      onclick={removeIcon}
                    >Remove</button>
                  {/if}
                </div>
                {#if gaugeDef.iconName}
                  <p class="text-[10px] text-gray-500 truncate max-w-[220px]">{gaugeDef.iconName.split(/[/\\]/).pop()}</p>
                  <!-- Icon Position & Size -->
                  <div class="space-y-2.5">
                    <div>
                      <div class="flex items-center justify-between mb-1">
                        <label for="iox-{gaugeDef.entityId}" class="text-[10px] text-gray-500">Horizontal</label>
                        <span class="text-[10px] font-mono text-gray-400">{Math.round(gaugeDef.iconOffsetX * 100)}%</span>
                      </div>
                      <input id="iox-{gaugeDef.entityId}" type="range" step="5" min="-100" max="100"
                        value={Math.round(gaugeDef.iconOffsetX * 100)}
                        oninput={(e) => onchange({ ...gaugeDef, iconOffsetX: parseInt((e.target as HTMLInputElement).value) / 100 })}
                        class="w-full h-1.5 rounded-full appearance-none bg-gray-700 {sectionColors.icon.slider} cursor-pointer
                          [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-3.5 [&::-webkit-slider-thumb]:h-3.5
                          [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-violet-500"
                      />
                    </div>
                    <div>
                      <div class="flex items-center justify-between mb-1">
                        <label for="ioy-{gaugeDef.entityId}" class="text-[10px] text-gray-500">Vertical</label>
                        <span class="text-[10px] font-mono text-gray-400">{Math.round(gaugeDef.iconOffsetY * 100)}%</span>
                      </div>
                      <input id="ioy-{gaugeDef.entityId}" type="range" step="5" min="-100" max="100"
                        value={Math.round(gaugeDef.iconOffsetY * 100)}
                        oninput={(e) => onchange({ ...gaugeDef, iconOffsetY: parseInt((e.target as HTMLInputElement).value) / 100 })}
                        class="w-full h-1.5 rounded-full appearance-none bg-gray-700 {sectionColors.icon.slider} cursor-pointer
                          [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-3.5 [&::-webkit-slider-thumb]:h-3.5
                          [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-violet-500"
                      />
                    </div>
                    <div>
                      <div class="flex items-center justify-between mb-1">
                        <label for="isz-{gaugeDef.entityId}" class="text-[10px] text-gray-500">Size</label>
                        <span class="text-[10px] font-mono text-gray-400">{Math.round(gaugeDef.iconSize * 100)}%</span>
                      </div>
                      <input id="isz-{gaugeDef.entityId}" type="range" step="5" min="5" max="200"
                        value={Math.round(gaugeDef.iconSize * 100)}
                        oninput={(e) => onchange({ ...gaugeDef, iconSize: parseInt((e.target as HTMLInputElement).value) / 100 })}
                        class="w-full h-1.5 rounded-full appearance-none bg-gray-700 {sectionColors.icon.slider} cursor-pointer
                          [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-3.5 [&::-webkit-slider-thumb]:h-3.5
                          [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-violet-500"
                      />
                    </div>
                  </div>
                {/if}
              </div>
            {/if}
          </div>

          <!-- 4. Appearance (arc: texture, bar: icon, text/digital: nothing) -->
          {#if hasAppearance}
            <div class="rounded-lg border border-l-2 {sectionColors.appearance.border} border-gray-700/50 overflow-hidden">
              <button class="flex w-full items-center justify-between px-3 py-2.5 text-xs font-semibold uppercase tracking-wider {sectionColors.appearance.text} {sectionColors.appearance.headerBg} transition-colors"
                      onclick={() => toggleSection('appearance')}>
                <span>Appearance</span>
                <IconChevronDown size={14} class="transition-transform duration-200 {openSections.appearance ? 'rotate-180' : ''}" />
              </button>
              {#if openSections.appearance}
                <div class="px-3 pb-3 pt-1 border-t border-gray-700/30 {sectionColors.appearance.bg}">
                  {#if isArc}
                    <ArcNeedleSettings {gaugeDef} {onchange}
                      minValue={Math.floor(minValue)} maxValue={Math.ceil(maxValue)} unit={entityInfo?.unit ?? ''}
                      {previewValue} />
                  {:else if isBar}
                    <!-- Bar appearance (icon) is already in BarGaugeSettings via Layout -->
                    <p class="text-xs text-gray-500">Icon settings are in Layout above.</p>
                  {/if}
                </div>
              {/if}
            </div>
          {/if}

          <!-- 4b. Odometer (only for entity -2001) -->
          {#if isOdometer}
            <div class="rounded-lg border border-l-2 {sectionColors.odometer.border} border-gray-700/50 overflow-hidden">
              <button class="flex w-full items-center justify-between px-3 py-2.5 text-xs font-semibold uppercase tracking-wider {sectionColors.odometer.text} {sectionColors.odometer.headerBg} transition-colors"
                      onclick={() => toggleSection('odometer')}>
                <span>Odometer</span>
                <IconChevronDown size={14} class="transition-transform duration-200 {openSections.odometer ? 'rotate-180' : ''}" />
              </button>
              {#if openSections.odometer}
                <div class="px-3 pb-3 pt-1 border-t border-gray-700/30 {sectionColors.odometer.bg} space-y-3">
                  <div class="flex items-center justify-between rounded bg-gray-800/60 px-3 py-2">
                    <span class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Current</span>
                    <span class="font-mono text-sm font-bold {sectionColors.odometer.text} tabular-nums">{odometerValue?.toFixed(1) ?? '—'}</span>
                    <span class="text-xs text-gray-400">{odometerUnit}</span>
                  </div>
                  <div class="flex gap-2">
                    <button
                      class="flex-1 rounded bg-gray-700/80 px-3 py-2 text-xs font-medium text-gray-300 transition-colors hover:bg-red-800/50 hover:text-red-300 active:text-white min-h-[36px]"
                      onclick={async () => { await HybridBridge.resetOdometer(); odometerValue = 0; }}
                    >Reset</button>
                    <button
                      class="flex-1 rounded bg-gray-700/80 px-3 py-2 text-xs font-medium text-gray-300 transition-colors hover:bg-gray-600 active:text-white min-h-[36px]"
                      onclick={async () => {
                        const newVal = !odometerUnit.startsWith('km');
                        await HybridBridge.setOdometerUnit(newVal);
                        odometerUnit = newVal ? 'km' : 'mi';
                      }}
                    >Switch to {odometerUnit.startsWith('km') ? 'mi' : 'km'}</button>
                  </div>
                  <div>
                    <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-1">Set Value</p>
                    <div class="flex gap-2">
                      <input type="number" min="0" step="1"
                        bind:value={odometerSetDraft}
                        class="flex-1 min-w-0 rounded border border-gray-600 bg-gray-800 px-2 py-2 text-sm font-mono text-gray-200 placeholder-gray-500 focus:border-cyan-500 focus:outline-none"
                        placeholder="0"
                      />
                      <button
                        class="rounded {sectionColors.odometer.activeBtn} px-4 py-2 text-xs font-medium {sectionColors.odometer.activeBtnText} transition-colors hover:opacity-90 active:opacity-80 min-h-[36px]"
                        onclick={async () => {
                          const v = parseFloat(odometerSetDraft);
                          if (!isNaN(v) && v >= 0) {
                            await HybridBridge.setOdometerValue(v);
                            odometerValue = v;
                          }
                        }}
                      >Set</button>
                    </div>
                  </div>
                  <div>
                    <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-1">Speed Source</p>
                    <div class="flex gap-2">
                      <button
                        class="flex-1 rounded px-3 py-2 text-xs font-medium transition-colors min-h-[36px]
                          {odometerSource === 'gps' ? 'bg-cyan-600 text-white' : 'bg-gray-700/80 text-gray-300 hover:bg-gray-600'}"
                        onclick={async () => {
                          odometerSourceLoading = true;
                          await HybridBridge.setOdometerSpeedSource('gps');
                          odometerSource = 'gps';
                          odometerSourceLoading = false;
                        }}
                        disabled={odometerSourceLoading}
                      >GPS</button>
                      <button
                        class="flex-1 rounded px-3 py-2 text-xs font-medium transition-colors min-h-[36px]
                          {odometerSource === 'vss' ? 'bg-cyan-600 text-white' : 'bg-gray-700/80 text-gray-300 hover:bg-gray-600'}"
                        onclick={async () => {
                          odometerSourceLoading = true;
                          await HybridBridge.setOdometerSpeedSource('vss');
                          odometerSource = 'vss';
                          odometerSourceLoading = false;
                        }}
                        disabled={odometerSourceLoading}
                      >VSS Speed</button>
                    </div>
                    {#if odometerSourceLoading}
                      <p class="text-[10px] text-gray-500 mt-1">Reading ECU configuration...</p>
                    {:else if odometerSource === 'vss'}
                      <p class="text-[10px] text-gray-500 mt-1">
                        Unit auto-detected from ECU: {vssSpeedInMph ? 'mph' : 'km/h'}
                      </p>
                    {/if}
                  </div>
                </div>
              {/if}
            </div>
          {/if}

          <!-- 4c. GPS Speed debug (only for entity -1001) -->
          {#if isGpsSpeed}
            <div class="rounded-lg border border-l-2 {sectionColors.gpsDebug.border} border-gray-700/50 overflow-hidden">
              <button class="flex w-full items-center justify-between px-3 py-2.5 text-xs font-semibold uppercase tracking-wider {sectionColors.gpsDebug.text} {sectionColors.gpsDebug.headerBg} transition-colors"
                      onclick={() => toggleSection('gpsDebug')}>
                <span>Debug Speed</span>
                <IconChevronDown size={14} class="transition-transform duration-200 {openSections.gpsDebug ? 'rotate-180' : ''}" />
              </button>
              {#if openSections.gpsDebug}
                <div class="px-3 pb-3 pt-1 border-t border-gray-700/30 {sectionColors.gpsDebug.bg}">
                  <GpsSpeedDebugSettings />
                </div>
              {/if}
            </div>
          {/if}

          <!-- 5. Color -->
          <div class="rounded-lg border border-l-2 {sectionColors.color.border} border-gray-700/50 overflow-hidden">
            <button class="flex w-full items-center justify-between px-3 py-2.5 text-xs font-semibold uppercase tracking-wider {sectionColors.color.text} {sectionColors.color.headerBg} transition-colors"
                    onclick={() => toggleSection('color')}>
              <span>Color</span>
              <IconChevronDown size={14} class="transition-transform duration-200 {openSections.color ? 'rotate-180' : ''}" />
            </button>
            {#if openSections.color}
              <div class="px-3 pb-3 pt-1 border-t border-gray-700/30 {sectionColors.color.bg}">
                <ColorConfigSettings {gaugeDef} {onchange} />
              </div>
            {/if}
          </div>

          <!-- 6. Smoothing (arc + digital only) -->
          {#if hasSmoothing}
            <div class="rounded-lg border border-l-2 {sectionColors.smoothing.border} border-gray-700/50 overflow-hidden">
              <button class="flex w-full items-center justify-between px-3 py-2.5 text-xs font-semibold uppercase tracking-wider {sectionColors.smoothing.text} {sectionColors.smoothing.headerBg} transition-colors"
                      onclick={() => toggleSection('smoothing')}>
                <span>Smoothing</span>
                <IconChevronDown size={14} class="transition-transform duration-200 {openSections.smoothing ? 'rotate-180' : ''}" />
              </button>
              {#if openSections.smoothing}
                <div class="px-3 pb-3 pt-1 border-t border-gray-700/30 {sectionColors.smoothing.bg}">
                  <SmoothingSettings {gaugeDef} {onchange} />
                </div>
              {/if}
            </div>
          {/if}
        </div>
      </div>

      <!-- Footer -->
      <svelte:fragment slot="footer">
        <div class="flex w-full justify-end">
          <Button color="alternative" class="!border-gray-600 !bg-gray-700 !text-gray-300 hover:!bg-gray-600 !text-xs" onclick={onclose}>Close</Button>
        </div>
      </svelte:fragment>
    </Modal>
{/if}

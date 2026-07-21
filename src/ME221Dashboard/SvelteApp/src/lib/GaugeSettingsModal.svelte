<script lang="ts">
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
  import TransformSettings from './gauges/TransformSettings.svelte';
  import GaugePreviewPanel from './GaugePreviewPanel.svelte';
  import { Modal, Button, Tabs, TabItem } from 'flowbite-svelte';
  import { IconX } from '@tabler/icons-svelte';

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

  let activeTab = $state('shape');

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
  const hasNeedle = $derived(isArc);

  let customUnitDraft = $state('');
  $effect(() => {
    customUnitDraft = gaugeDef.customUnitLabel ?? '';
  });

  function emitCustomUnit() {
    onchange({ ...gaugeDef, customUnitLabel: customUnitDraft || null });
  }

  function clearCustomUnit() {
    customUnitDraft = '';
    emitCustomUnit();
  }
</script>

{#if open}
  <Modal {open} size="lg" placement="center" outsideclose={true} dismissable={false} class="backdrop:bg-gray-900/80" ontoggle={(e) => { if (e.newState === 'closed') onclose(); }}>
      <!-- Title bar -->
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
        <div class="flex-1 overflow-y-auto sm:overflow-visible min-h-0 min-w-0 px-4 py-3">

          <Tabs bind:selected={activeTab} tabStyle="full" divider={false}
                classes={{ active: 'bg-cyan-600 text-white', inactive: 'text-gray-400 hover:text-gray-200 hover:bg-gray-700/50' }}>
            <!-- Tab 1: Shape -->
            <TabItem key="shape" title="Shape">
              <div class="space-y-4 pt-3">
                <div class="flex gap-1">
                  {#each categories as cat}
                    <button
                      class="flex-1 rounded px-2 py-2 text-xs font-medium transition-colors min-h-[36px]
                        {gaugeDef.shapeCategory === cat.value
                          ? 'bg-cyan-600 text-white'
                          : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
                      onclick={() => setCategory(cat.value)}
                    >{cat.label}</button>
                  {/each}
                </div>

                <!-- Size Scale -->
                <div>
                  <div class="flex items-center justify-between mb-1.5">
                    <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Size Scale</p>
                    <span class="text-xs font-mono text-cyan-400">{(gaugeDef.scale ?? 1.0).toFixed(2)}x</span>
                  </div>
                  <input
                    type="range" step="0.05" min="0.2" max="10"
                    value={gaugeDef.scale ?? 1.0}
                    oninput={(e) => onchange({ ...gaugeDef, scale: parseFloat((e.target as HTMLInputElement).value) })}
                    class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
                      [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
                      [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg"
                  />
                  <div class="relative mt-0.5 h-4 text-[9px] text-gray-600 overflow-visible select-none">
                    <span style="position:absolute;left:0;white-space:nowrap">0.2x</span>
                    <span style="position:absolute;left:8.2%;transform:translateX(-50%);white-space:nowrap">1.0x</span>
                    <span style="position:absolute;left:18.4%;transform:translateX(-50%);white-space:nowrap">2.0x</span>
                    <span style="position:absolute;left:49%;transform:translateX(-50%);white-space:nowrap">5.0x</span>
                    <span style="position:absolute;right:0;white-space:nowrap">10x</span>
                  </div>
                </div>

                <!-- Layer -->
                <div>
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
            </TabItem>

            <!-- Tab 2: Layout -->
            <TabItem key="layout" title="Layout">
              <div class="space-y-4 pt-3">
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
              </div>
            </TabItem>

            <!-- Tab 3: Text -->
            <TabItem key="text" title="Text">
              <div class="space-y-4 pt-3">
                <!-- Visibility toggles -->
                <div class="grid grid-cols-3 gap-1">
                  {#each [
                    { key: 'showValue' as const, label: 'Value' },
                    { key: 'showUnit' as const, label: 'Unit' },
                    { key: 'showName' as const, label: 'Name' },
                  ] as lbl}
                    <button
                      class="flex items-center justify-center gap-1.5 rounded px-2 py-2 text-xs font-medium transition-colors min-h-[36px]
                        {gaugeDef[lbl.key]
                          ? 'bg-cyan-600/20 text-cyan-300'
                          : 'border border-gray-600 text-gray-500 hover:border-gray-500 hover:text-gray-400'}"
                      onclick={() => toggleLabel(lbl.key)}>
                      <span>{lbl.label}</span>
                      <span class="text-[10px]">{gaugeDef[lbl.key] ? 'ON' : 'OFF'}</span>
                    </button>
                  {/each}
                </div>

                <!-- Font Scale -->
                <div>
                  <div class="flex items-center justify-between mb-1.5">
                    <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Font Scale</p>
                    <span class="text-xs font-mono text-cyan-400">{(gaugeDef.fontSizeScale ?? 1.0).toFixed(2)}x</span>
                  </div>
                  <input
                    type="range" step="0.05" min="0.5" max="10"
                    value={gaugeDef.fontSizeScale ?? 1.0}
                    oninput={(e) => onchange({ ...gaugeDef, fontSizeScale: parseFloat((e.target as HTMLInputElement).value) })}
                    class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
                      [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
                      [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg"
                  />
                  <div class="relative mt-0.5 h-4 text-[9px] text-gray-600 overflow-visible select-none">
                    <span style="position:absolute;left:0;white-space:nowrap">0.5x</span>
                    <span style="position:absolute;left:5.3%;transform:translateX(-50%);white-space:nowrap">1.0x</span>
                    <span style="position:absolute;left:15.8%;transform:translateX(-50%);white-space:nowrap">2.0x</span>
                    <span style="position:absolute;left:47.4%;transform:translateX(-50%);white-space:nowrap">5.0x</span>
                    <span style="position:absolute;right:0;white-space:nowrap">10x</span>
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

                <!-- Custom Unit Label -->
                <div>
                  <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-1">Unit Label Override</p>
                  <div class="relative">
                    <input
                      type="text"
                      maxlength="50"
                      value={customUnitDraft}
                      oninput={(e) => { customUnitDraft = (e.target as HTMLInputElement).value; emitCustomUnit(); }}
                      placeholder={entityInfo?.unit ?? 'Default'}
                      class="w-full rounded bg-gray-700/80 px-3 py-2 pr-7 text-sm text-gray-100 placeholder-gray-500 outline-none focus:ring-1 focus:ring-cyan-500/50"
                    />
                    {#if customUnitDraft}
                      <button
                        class="absolute right-1.5 top-1/2 -translate-y-1/2 rounded p-1 text-gray-400 hover:text-gray-200 hover:bg-gray-600"
                        onclick={clearCustomUnit}
                      >
                        <IconX size={12} />
                      </button>
                    {/if}
                  </div>
                </div>
              </div>
            </TabItem>

            <!-- Tab 4: Appearance -->
            <TabItem key="appearance" title="Appearance">
              <div class="space-y-4 pt-3">
                <!-- Icon -->
                <div>
                  <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Icon</p>
                  <div class="flex items-center gap-2">
                    <button
                      class="rounded-lg border border-gray-600 px-3 py-2 text-xs font-medium text-gray-300 transition-colors hover:border-cyan-500 hover:text-cyan-300 disabled:opacity-50 min-h-[36px]"
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
                    <p class="text-[10px] text-gray-500 truncate max-w-[220px] mt-1">{gaugeDef.iconName.split(/[/\\]/).pop()}</p>
                    <div class="space-y-2.5 mt-2">
                      <div>
                        <div class="flex items-center justify-between mb-1">
                          <label for="iox-{gaugeDef.entityId}" class="text-[10px] text-gray-500">Horizontal</label>
                          <span class="text-[10px] font-mono text-gray-400">{Math.round(gaugeDef.iconOffsetX * 100)}%</span>
                        </div>
                        <input id="iox-{gaugeDef.entityId}" type="range" step="5" min="-100" max="100"
                          value={Math.round(gaugeDef.iconOffsetX * 100)}
                          oninput={(e) => onchange({ ...gaugeDef, iconOffsetX: parseInt((e.target as HTMLInputElement).value) / 100 })}
                          class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
                            [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-3.5 [&::-webkit-slider-thumb]:h-3.5
                            [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500"
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
                          class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
                            [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-3.5 [&::-webkit-slider-thumb]:h-3.5
                            [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500"
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
                          class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
                            [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-3.5 [&::-webkit-slider-thumb]:h-3.5
                            [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500"
                        />
                      </div>
                    </div>
                  {/if}
                </div>

                <!-- Arc Needle -->
                {#if hasNeedle}
                  <div class="border-t border-gray-700/30 pt-4">
                    <ArcNeedleSettings {gaugeDef} {onchange}
                      minValue={Math.floor(minValue)} maxValue={Math.ceil(maxValue)} unit={entityInfo?.unit ?? ''}
                      {previewValue} />
                  </div>
                {/if}

                <!-- Color -->
                <div class="border-t border-gray-700/30 pt-4">
                  <ColorConfigSettings {gaugeDef} {onchange} />
                </div>
              </div>
            </TabItem>

            <!-- Tab 5: Behavior -->
            <TabItem key="behavior" title="Behavior">
              <div class="space-y-4 pt-3">
                <!-- Smoothing -->
                <SmoothingSettings {gaugeDef} {onchange} />

                <!-- Histogram -->
                <div class="border-t border-gray-700/30 pt-4">
                  <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Histogram</p>
                  <div class="flex items-center justify-between rounded bg-gray-800/60 px-3 py-2">
                    <span class="text-xs text-gray-300">Show value history</span>
                    <button
                      class="relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none"
                      style="background-color: {gaugeDef.showHistogram ? 'var(--metro-purple)' : 'rgb(55,65,81)'}"
                      role="switch"
                      aria-checked={gaugeDef.showHistogram ?? false}
                      onclick={() => onchange({ ...gaugeDef, showHistogram: !(gaugeDef.showHistogram ?? false) })}
                    >
                      <span
                        class="pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow-lg ring-0 transition duration-200 ease-in-out"
                        style="transform: translateX({gaugeDef.showHistogram ? '18px' : '0'})"
                      ></span>
                    </button>
                  </div>
                </div>

                <!-- Transform -->
                <div class="border-t border-gray-700/30 pt-4">
                  <TransformSettings {gaugeDef} {entityInfo} customUnitLabel={gaugeDef.customUnitLabel ?? null} {onchange} />
                </div>
              </div>
            </TabItem>

            <!-- Tab 6: Special (conditional) -->
            {#if isOdometer || isGpsSpeed}
              <TabItem key="special" title="Special">
                <div class="space-y-4 pt-3">
                  {#if isOdometer}
                    <div>
                      <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Odometer</p>
                      <div class="flex items-center justify-between rounded bg-gray-800/60 px-3 py-2">
                        <span class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Current</span>
                        <span class="font-mono text-sm font-bold text-amber-400 tabular-nums">{odometerValue?.toFixed(1) ?? '—'}</span>
                        <span class="text-xs text-gray-400">{odometerUnit}</span>
                      </div>
                      <div class="flex gap-2 mt-2">
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
                      <div class="mt-2">
                        <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-1">Set Value</p>
                        <div class="flex gap-2">
                          <input type="number" min="0" step="1"
                            bind:value={odometerSetDraft}
                            class="flex-1 min-w-0 rounded border border-gray-600 bg-gray-800 px-2 py-2 text-sm font-mono text-gray-200 placeholder-gray-500 focus:border-cyan-500 focus:outline-none"
                            placeholder="0"
                          />
                          <button
                            class="rounded bg-amber-600 px-4 py-2 text-xs font-medium text-white transition-colors hover:opacity-90 active:opacity-80 min-h-[36px]"
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
                      <div class="mt-2">
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

                  {#if isGpsSpeed}
                    <div>
                      <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Debug Speed</p>
                      <GpsSpeedDebugSettings />
                    </div>
                  {/if}
                </div>
              </TabItem>
            {/if}
          </Tabs>
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

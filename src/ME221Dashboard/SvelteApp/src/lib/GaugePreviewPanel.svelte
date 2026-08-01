<script lang="ts">
  import { GaugeShapeCategory, toGaugeDefinition, formatValue } from './gauges/types';
  import type { GaugeConfigEntry, EntityInfo } from './HybridBridge';
  import GaugeCard from './gauges/GaugeCard.svelte';
  import NumberInput from './NumberInput.svelte';
  import { deviceMode } from './stores/deviceMode.svelte';
  import { IconZoomIn, IconZoomOut, IconRotate, IconChevronDown } from '@tabler/icons-svelte';

  let { gaugeDef, gaugeName, entityInfo, testValue, onTestValueChange }: {
    gaugeDef: GaugeConfigEntry;
    gaugeName: string;
    entityInfo: EntityInfo | null;
    testValue: number | null;
    onTestValueChange: (v: number | null) => void;
  } = $props();

  const minValue = $derived(entityInfo?.minValue ?? 0);
  const maxValue = $derived(entityInfo?.maxValue ?? 10000);
  const liveValue = $derived(entityInfo ? ((entityInfo.minValue ?? 0) + (entityInfo.maxValue ?? 10000)) / 2 : 5000);
  const useTestValue = $derived(testValue !== null);
  const previewValue = $derived(testValue ?? liveValue);

  const PREVIEW_PX = 180;
  const MOBILE_PREVIEW_PX = 80;
  let isMobile = $derived(deviceMode.uiMode === 'mobile');
  let zoomLevel = $state(1.0);
  let mobileTestExpanded = $state(false);
  const ZOOM_STEPS = [0.5, 0.75, 1.0, 1.5, 2.0];

  const previewArcPx = $derived(isMobile ? MOBILE_PREVIEW_PX : PREVIEW_PX);
  const previewBarH = $derived(isMobile ? 40 : 60);
  const previewDigitalH = $derived(isMobile ? 56 : 80);

  const isBar = $derived(gaugeDef.shapeCategory === GaugeShapeCategory.Bar);
  const isDigital = $derived(gaugeDef.shapeCategory === GaugeShapeCategory.Digital);

  // Pass the FULL config through toGaugeDefinition (not a hand-picked subset)
  // so the preview matches the dashboard: textures, linkedEntities, needle shape,
  // scale marks, redline, bar/ring/wedge options, chart overlays, etc.
  const previewGauge = $derived(toGaugeDefinition(
    {
      ...gaugeDef,
      needleCurve: gaugeDef.needleCurve,
      linkedEntities: gaugeDef.linkedEntities,
      fractionX: 0,
      fractionY: 0,
    },
    {
      name: gaugeName,
      unit: entityInfo?.unit ?? '',
      value: previewValue,
      formattedValue: formatValue(previewValue, gaugeName, entityInfo?.unit ?? ''),
      minValue,
      maxValue,
    }
  ));

  function zoomIn() {
    const idx = ZOOM_STEPS.indexOf(zoomLevel);
    zoomLevel = idx < ZOOM_STEPS.length - 1 ? ZOOM_STEPS[idx + 1] : zoomLevel;
  }
  function zoomOut() {
    const idx = ZOOM_STEPS.indexOf(zoomLevel);
    zoomLevel = idx > 0 ? ZOOM_STEPS[idx - 1] : zoomLevel;
  }
</script>

<div class="shrink-0 border-b sm:border-b-0 sm:border-r border-gray-700/50 bg-gray-950/40 p-2 sm:p-4">
  <div class="flex flex-col items-center gap-2 sm:gap-3">
    <!-- Gauge preview -->
    <div class="relative shrink-0 flex items-center justify-center rounded-lg bg-gray-800/30 overflow-hidden"
         style="width: {previewArcPx}px; height: {previewArcPx}px;">
      <div style="transform: scale({zoomLevel}); transform-origin: center center;">
        {#if isBar}
          <div style="width: {previewArcPx}px; height: {previewBarH}px;">
            <GaugeCard gauge={previewGauge} pixelWidth={previewArcPx} pixelHeight={previewBarH} />
          </div>
        {:else if isDigital}
          <div style="width: {previewArcPx}px; height: {previewDigitalH}px;">
            <GaugeCard gauge={previewGauge} pixelWidth={previewArcPx} pixelHeight={previewDigitalH} />
          </div>
        {:else}
          <div style="width: {previewArcPx}px; height: {previewArcPx}px; position: relative;">
            <GaugeCard gauge={previewGauge} pixelWidth={previewArcPx} pixelHeight={previewArcPx} />
          </div>
        {/if}
      </div>
    </div>

    <!-- Zoom controls (desktop only) -->
    <div class="hidden sm:flex items-center justify-center gap-2 w-full shrink-0">
      <button class="flex items-center justify-center rounded-md border border-gray-600 p-1.5 text-gray-300 hover:bg-gray-700 hover:text-white transition-colors disabled:opacity-30 min-w-[32px] min-h-[32px]"
              disabled={zoomLevel <= ZOOM_STEPS[0]} onclick={zoomOut} title="Zoom out">
        <IconZoomOut size={16} />
      </button>
      <span class="text-[11px] text-gray-400 tabular-nums w-10 text-center font-mono">{Math.round(zoomLevel * 100)}%</span>
      <button class="flex items-center justify-center rounded-md border border-gray-600 p-1.5 text-gray-300 hover:bg-gray-700 hover:text-white transition-colors disabled:opacity-30 min-w-[32px] min-h-[32px]"
              disabled={zoomLevel >= ZOOM_STEPS[ZOOM_STEPS.length - 1]} onclick={zoomIn} title="Zoom in">
        <IconZoomIn size={16} />
      </button>
    </div>

    <!-- Test value -->
    {#if isMobile}
      <!-- Mobile: compact collapsed test value -->
      <div class="w-full">
        <button class="flex w-full items-center justify-between rounded border border-gray-700/50 px-2 py-1.5 text-[10px] font-semibold uppercase tracking-wider text-gray-500 hover:bg-gray-800/50 transition-colors"
                onclick={() => mobileTestExpanded = !mobileTestExpanded}>
          <span>{useTestValue ? `Test: ${testValue}` : 'Test Value'}</span>
          <IconChevronDown size={12} class="transition-transform duration-200 {mobileTestExpanded ? 'rotate-180' : ''}" />
        </button>
        {#if mobileTestExpanded}
          <div class="mt-2 space-y-2">
            <input type="range" step="1"
              min={Math.floor(minValue)} max={Math.ceil(maxValue)}
              value={useTestValue ? testValue : Math.round(liveValue)}
              oninput={(e) => onTestValueChange(parseInt((e.target as HTMLInputElement).value, 10))}
              class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
                [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
                [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500" />
            <NumberInput
              value={useTestValue ? (testValue ?? 0) : (liveValue ?? 0)}
              min={Math.floor(minValue)}
              max={Math.ceil(maxValue)}
              unit={entityInfo?.unit ?? ''}
              onchange={(v) => onTestValueChange(Math.floor(v))}
            />
            {#if useTestValue}
              <button class="flex items-center gap-0.5 text-[10px] text-cyan-400 hover:text-cyan-300 transition-colors"
                      onclick={() => onTestValueChange(null)}>
                <IconRotate size={10} /> Live
              </button>
            {/if}
          </div>
        {/if}
      </div>
    {:else}
      <!-- Desktop: full test value controls -->
      <div class="w-full">
        <div class="flex items-center justify-between mb-1">
          <label for="test-value" class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Test Value</label>
          {#if useTestValue}
            <button class="flex items-center gap-0.5 text-[10px] text-cyan-400 hover:text-cyan-300 transition-colors"
                    onclick={() => onTestValueChange(null)}>
              <IconRotate size={10} /> Live
            </button>
          {/if}
        </div>
        <input id="test-value" type="range" step="1"
          min={Math.floor(minValue)} max={Math.ceil(maxValue)}
          value={useTestValue ? testValue : Math.round(liveValue)}
          oninput={(e) => onTestValueChange(parseInt((e.target as HTMLInputElement).value, 10))}
          class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
            [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
            [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30" />
        <div class="mt-2">
          <NumberInput
            value={useTestValue ? (testValue ?? 0) : (liveValue ?? 0)}
            min={Math.floor(minValue)}
            max={Math.ceil(maxValue)}
            unit={entityInfo?.unit ?? ''}
            onchange={(v) => onTestValueChange(Math.floor(v))}
          />
        </div>
        <div class="mt-1 flex justify-between text-[10px] text-gray-600">
          <span>{Math.floor(minValue)}</span>
          <span>{Math.ceil(maxValue)}</span>
        </div>
      </div>
    {/if}
  </div>
</div>

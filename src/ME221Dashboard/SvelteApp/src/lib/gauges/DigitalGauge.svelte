<script lang="ts">
  import type { GaugeDefinition } from './types';
  import { DigitalStyle } from './types';
  import { HybridBridge } from '../HybridBridge';

  let { gauge, pixelWidth, pixelHeight }: {
    gauge: GaugeDefinition;
    pixelWidth: number;
    pixelHeight: number;
  } = $props();

  let iconDataUrl = $state<string | null>(null);
  $effect(() => {
    const p = gauge.iconName;
    if (p) {
      HybridBridge.getImageBase64(p).then(r => { iconDataUrl = r.success && r.dataUrl ? r.dataUrl : null; }).catch(() => { iconDataUrl = null; });
    } else {
      iconDataUrl = null;
    }
  });

  const dim = $derived(Math.min(pixelWidth, pixelHeight));
  const fs = $derived(Math.max(0.5, Math.min(2.0, gauge.fontSizeScale ?? 1.0)));

  // Scale font sizes to fit within the container
  const _digitBg = '#1a1a1a';
  const _ledColor = '#ff3333';
  const _ledColorDim = '#ff6666';
  const _ledBg = '#0d0d0d';

  const unitSize = $derived(Math.max(8, dim * 0.08 * fs));
  const nameSize = $derived(Math.max(6, dim * 0.04 * fs));
  const largeDigitValueSize = $derived(Math.max(12, dim * 0.16 * fs));
  const segmentDigitSize = $derived(Math.max(8, dim * 0.10 * fs));
  const segmentCellW = $derived(Math.max(12, dim * 0.10 * fs));
  const segmentCellH = $derived(Math.max(18, dim * 0.16 * fs));
  const clusterValueSize = $derived(Math.max(14, dim * 0.18 * fs));
  const clusterUnitSize = $derived(Math.max(8, dim * 0.07 * fs));
  const clusterNameSize = $derived(Math.max(6, dim * 0.05 * fs));
  const separatorWidth = $derived(Math.max(16, dim * 0.22 * fs));
  const iconSz = $derived(Math.max(4, Math.min(80, dim * gauge.iconSize)));

  // LabelTop sizes
  const labelTopLabelSize = $derived(Math.max(6, dim * 0.04 * fs));
  const labelTopValueSize = $derived(Math.max(12, dim * 0.16 * fs));
  const labelTopUnitSize = $derived(Math.max(7, dim * 0.06 * fs));

  // GlowRing sizes
  const glowValueSize = $derived(Math.max(10, dim * 0.12 * fs));
  const glowUnitSize = $derived(Math.max(7, dim * 0.05 * fs));
  const glowNameSize = $derived(Math.max(6, dim * 0.035 * fs));

  // InsetDisplay sizes
  const insetValueSize = $derived(Math.max(10, dim * 0.13 * fs));
  const insetUnitSize = $derived(Math.max(7, dim * 0.06 * fs));
  const insetNameSize = $derived(Math.max(6, dim * 0.04 * fs));

  function buildDigits(value: string): string[] {
    const display = value || '';
    const padded = display.length < 3 ? display.padStart(3) : display;
    return padded.split('');
  }

  const digits = $derived(buildDigits(gauge.formattedValue));
</script>

<div class="relative flex h-full w-full flex-col items-center justify-center overflow-hidden">
  {#if iconDataUrl}
    <img src={iconDataUrl} alt="" class="absolute pointer-events-none"
         style="width: {iconSz}px; height: {iconSz}px; left: calc(50% + {gauge.iconOffsetX * 100}% - {iconSz / 2}px); top: calc(50% + {gauge.iconOffsetY * 100}% - {iconSz / 2}px);" />
  {/if}

  {#if gauge.digitalStyle === DigitalStyle.LargeDigit}
    <!-- LargeDigit: value in dark pill, unit + name below, constrained to container -->
    <div class="flex flex-col items-center justify-center gap-0.5 max-h-full">
      {#if gauge.showValue}
        <div class="rounded-lg px-3 py-0.5 max-w-full overflow-hidden" style="background: {_digitBg};">
          <span class="block truncate" style="color: {gauge.textColor}; font-size: {largeDigitValueSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8); line-height: 1.2;">
            {gauge.formattedValue}
          </span>
        </div>
      {/if}
      {#if gauge.showUnit}
        <span class="block truncate max-w-full" style="color: {gauge.textColor}; font-size: {unitSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8); line-height: 1.2;">
          {gauge.unit}
        </span>
      {/if}
      {#if gauge.showName}
        <span class="block truncate max-w-full" style="color: {gauge.textColor}; font-size: {nameSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8); line-height: 1.2;">
          {gauge.name}
        </span>
      {/if}
    </div>

  {:else if gauge.digitalStyle === DigitalStyle.SevenSegment}
    <!-- SevenSegment: red LED digits in dark cells -->
    <div class="flex flex-col items-center justify-center gap-0.5 max-h-full">
      <div class="flex items-center justify-center gap-0.5">
        {#if gauge.showValue}
          {#each digits as ch}
            <div class="flex items-center justify-center rounded-sm"
                 style="background: {_ledBg}; width: {segmentCellW}px; height: {segmentCellH}px;">
              <span class="font-bold" style="font-size: {segmentDigitSize}px; color: {_ledColor};">
                {ch}
              </span>
            </div>
          {/each}
        {/if}
      </div>
      {#if gauge.showUnit}
        <span style="font-size: {unitSize}px; color: {_ledColorDim}; text-shadow: 1px 1px 3px rgba(0,0,0,0.8);">
          {gauge.unit}
        </span>
      {/if}
      {#if gauge.showName}
        <span class="truncate max-w-full" style="color: {gauge.textColor}; font-size: {nameSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8);">
          {gauge.name}
        </span>
      {/if}
    </div>

  {:else if gauge.digitalStyle === DigitalStyle.Cluster}
    <!-- Cluster: dense value+unit with separator line and name -->
    <div class="flex flex-col items-center justify-center gap-0 max-h-full">
      <div class="flex items-center gap-1">
        {#if gauge.showValue}
          <span class="truncate" style="color: {gauge.textColor}; font-size: {clusterValueSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8); line-height: 1.1;">
            {gauge.formattedValue}
          </span>
        {/if}
        {#if gauge.showUnit}
          <span style="color: {gauge.textColor}; font-size: {clusterUnitSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8); line-height: 1.1; margin-bottom: 2px;">
            {gauge.unit}
          </span>
        {/if}
      </div>
      <div style="background: {_digitBg}; border-radius: 2px; height: 2px; width: {separatorWidth}px; margin: 3px 0;"></div>
      {#if gauge.showName}
        <span class="truncate max-w-full font-medium" style="color: {gauge.textColor}; font-size: {clusterNameSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8);">
          {gauge.name}
        </span>
      {/if}
    </div>

  {:else if gauge.digitalStyle === DigitalStyle.LabelTop}
    <!-- LabelTop: name as label above value, unit below — inverted hierarchy -->
    <div class="flex flex-col items-center justify-center gap-0 max-h-full">
      {#if gauge.showName}
        <span class="truncate max-w-full uppercase tracking-widest" style="color: {gauge.textColor}; opacity: 0.5; font-size: {labelTopLabelSize}px; line-height: 1.2;">
          {gauge.name}
        </span>
      {/if}
      {#if gauge.showValue}
        <span class="font-bold" style="color: {gauge.textColor}; font-size: {labelTopValueSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8); line-height: 1.1;">
          {gauge.formattedValue}
        </span>
      {/if}
      {#if gauge.showUnit}
        <span style="color: {gauge.textColor}; opacity: 0.6; font-size: {labelTopUnitSize}px; line-height: 1.2;">
          {gauge.unit}
        </span>
      {/if}
    </div>

  {:else if gauge.digitalStyle === DigitalStyle.GlowRing}
    <!-- GlowRing: neon ring around value, pulsing glow -->
    <div class="flex flex-col items-center justify-center gap-0.5 max-h-full">
      {#if gauge.showValue}
        <div class="relative flex items-center justify-center rounded-full"
             style="width: {dim * 0.55}px; height: {dim * 0.55}px; min-width: 50px; min-height: 50px;">
          <!-- Glow ring -->
          <div class="absolute inset-0 rounded-full"
               style="border: 2px solid {gauge.textColor}; box-shadow: 0 0 8px {gauge.textColor}40, inset 0 0 6px {gauge.textColor}20;"></div>
          <!-- Value -->
          <span class="relative z-10 font-bold" style="color: {gauge.textColor}; font-size: {glowValueSize}px; text-shadow: 0 0 6px {gauge.textColor}60; line-height: 1.1;">
            {gauge.formattedValue}
          </span>
        </div>
      {/if}
      <div class="flex items-baseline gap-1">
        {#if gauge.showUnit}
          <span style="color: {gauge.textColor}; opacity: 0.7; font-size: {glowUnitSize}px; line-height: 1.2;">
            {gauge.unit}
          </span>
        {/if}
      </div>
      {#if gauge.showName}
        <span class="truncate max-w-full" style="color: {gauge.textColor}; opacity: 0.6; font-size: {glowNameSize}px; line-height: 1.2;">
          {gauge.name}
        </span>
      {/if}
    </div>

  {:else if gauge.digitalStyle === DigitalStyle.InsetDisplay}
    <!-- InsetDisplay: recessed LCD-like screen with beveled border -->
    <div class="flex flex-col items-center justify-center gap-0.5 max-h-full">
      <div class="rounded-md px-3 py-1.5 max-w-full overflow-hidden"
           style="background: #0a0a0a; border: 1px solid #333; box-shadow: inset 0 1px 3px rgba(0,0,0,0.8), inset 0 -1px 1px rgba(255,255,255,0.05);">
        {#if gauge.showValue}
          <div class="text-center">
            <span class="block font-mono font-bold" style="color: #00ff88; font-size: {insetValueSize}px; text-shadow: 0 0 4px #00ff8840; line-height: 1.2;">
              {gauge.formattedValue}
            </span>
          </div>
        {/if}
        {#if gauge.showUnit}
          <div class="text-center">
            <span class="block font-mono" style="color: #00ff88; opacity: 0.5; font-size: {insetUnitSize}px; line-height: 1.2;">
              {gauge.unit}
            </span>
          </div>
        {/if}
      </div>
      {#if gauge.showName}
        <span class="truncate max-w-full" style="color: {gauge.textColor}; opacity: 0.7; font-size: {insetNameSize}px; line-height: 1.2;">
          {gauge.name}
        </span>
      {/if}
    </div>

  {:else}
    <!-- Odometer (default): dark cells with light text -->
    <div class="flex flex-col items-center justify-center gap-0.5 max-h-full">
      <div class="flex items-center justify-center gap-0.5">
        {#if gauge.showValue}
          {#each digits as ch}
            <div class="flex items-center justify-center rounded-sm"
                 style="background: {_digitBg}; width: {segmentCellW}px; height: {segmentCellH}px;">
              <span style="color: {gauge.textColor}; font-size: {segmentDigitSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8);">
                {ch}
              </span>
            </div>
          {/each}
        {/if}
      </div>
      {#if gauge.showUnit}
        <span style="color: {gauge.textColor}; font-size: {unitSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8);">
          {gauge.unit}
        </span>
      {/if}
      {#if gauge.showName}
        <span class="truncate max-w-full" style="color: {gauge.textColor}; font-size: {nameSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8);">
          {gauge.name}
        </span>
      {/if}
    </div>
  {/if}
</div>

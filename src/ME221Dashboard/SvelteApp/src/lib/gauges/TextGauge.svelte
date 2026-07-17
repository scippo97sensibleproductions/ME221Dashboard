<script lang="ts">
  import type { GaugeDefinition } from './types';
  import { HybridBridge } from '../HybridBridge';

  let { gauge, pixelWidth, pixelHeight, valueTextColor }: {
    gauge: GaugeDefinition;
    pixelWidth: number;
    pixelHeight: number;
    valueTextColor?: string;
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
  const fontSizeScale = $derived(Math.max(0.5, Math.min(2.0, gauge.fontSizeScale ?? 1.0)));
  const valueSize = $derived(Math.max(14, dim * 0.22 * fontSizeScale));
  const unitSize = $derived(Math.max(10, dim * 0.08 * fontSizeScale));
  const nameSize = $derived(Math.max(6, 9 * fontSizeScale));
  const iconSz = $derived(Math.max(4, Math.min(80, dim * gauge.iconSize)));
  const displayTextColor = $derived(valueTextColor ?? '#dee2e6');
</script>

<div class="relative flex h-full w-full flex-col items-center justify-center gap-0.5">
  {#if iconDataUrl}
    <img src={iconDataUrl} alt="" class="absolute pointer-events-none"
         style="width: {iconSz}px; height: {iconSz}px; left: calc(50% + {gauge.iconOffsetX * 100}% - {iconSz / 2}px); top: calc(50% + {gauge.iconOffsetY * 100}% - {iconSz / 2}px);" />
  {/if}
  {#if gauge.showValue}
    <span class="font-bold leading-tight" style="color: {displayTextColor}; font-size: {valueSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8);">
      {gauge.formattedValue}
    </span>
  {/if}
  {#if gauge.showUnit}
    <span class="leading-tight" style="color: {displayTextColor}; font-size: {unitSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8);">
      {gauge.unit}
    </span>
  {/if}
  {#if gauge.showName}
    <span class="max-w-full truncate leading-tight" style="color: {displayTextColor}; font-size: {nameSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8);">
      {gauge.name}
    </span>
  {/if}
</div>

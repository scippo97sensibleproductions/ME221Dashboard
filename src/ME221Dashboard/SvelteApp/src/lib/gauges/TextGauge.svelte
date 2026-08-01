<script lang="ts">
  import type { GaugeDefinition } from './types';
  import { HybridBridge } from '../HybridBridge';
  import { DEFAULT_COLOR_STOPS, buildColorLuts, computeValueFraction, gaugeValueColor } from './types';

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

  // R7 — value-driven color: reuse the Arc hysteresis LUT engine (AE6); when
  // disabled the LUT is never built and the color stays on the AE1 fallback.
  const valueFraction = $derived(computeValueFraction(gauge.value, gauge.minValue, gauge.maxValue));
  const colorLuts = $derived(buildColorLuts(
    gauge.colorStops?.length ? gauge.colorStops : DEFAULT_COLOR_STOPS,
    gauge.colorHysteresis ?? 0.03
  ));
  let _prevFraction = 0;
  const colorStopColor = $derived.by(() => {
    if (!gauge.colorStopColoring) return null;
    const frac = valueFraction;
    const color = gaugeValueColor(frac, _prevFraction, colorLuts);
    _prevFraction = frac;
    return color;
  });
  const valueColor = $derived(colorStopColor ?? displayTextColor);

  // R9 — change flash: jump bigger than flashThreshold × range since the last
  // displayed value triggers a one-shot CSS transition class (AE1: 0 = off).
  let prevDisplayedValue: number | null = null;
  let flashActive = $state(false);
  const valueJump = $derived.by(() => {
    const v = gauge.value;
    const th = gauge.flashThreshold ?? 0;
    const range = gauge.maxValue - gauge.minValue;
    let jumped = false;
    if (th > 0 && range > 0 && prevDisplayedValue != null) {
      jumped = Math.abs(v - prevDisplayedValue) > th * range;
    }
    prevDisplayedValue = v;
    return jumped;
  });
  $effect(() => {
    if (valueJump) flashActive = true;
  });

  // R8 — panel backgrounds behind the text block (icon stays outside, never clipped)
  const panelClasses = $derived(
    gauge.panelStyle === 1
      ? 'rounded-full bg-black/40 px-5 py-2'
      : gauge.panelStyle === 2
        ? 'rounded-2xl border border-white/10 bg-gradient-to-b from-white/10 to-white/5 px-4 py-2 shadow-lg backdrop-blur-md'
        : gauge.panelStyle === 3
          ? 'rounded-xl border border-gray-600 bg-gray-900/90 px-4 py-2 shadow-lg'
          : ''
  );
</script>

<div class="relative flex h-full w-full flex-col items-center justify-center gap-0.5 select-none">
  {#if iconDataUrl}
    <img src={iconDataUrl} alt="" class="absolute pointer-events-none"
         style="width: {iconSz}px; height: {iconSz}px; left: calc(50% + {gauge.iconOffsetX * 100}% - {iconSz / 2}px); top: calc(50% + {gauge.iconOffsetY * 100}% - {iconSz / 2}px);" />
  {/if}
  <div class="flex max-w-full flex-col items-center gap-0.5 {panelClasses}">
    {#if gauge.showValue}
      <span class="font-bold leading-tight"
            class:text-gauge-flash={flashActive}
            ontransitionend={() => { flashActive = false; }}
            style="color: {valueColor}; font-size: {valueSize}px; font-family: var(--font-display); text-shadow: 1px 1px 3px rgba(0,0,0,0.8);">
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
</div>

<style>
  .text-gauge-flash {
    color: #f59e0b !important;
    text-shadow: 0 0 8px rgba(245, 158, 11, 0.7) !important;
    transition: color 0.4s ease, text-shadow 0.4s ease;
  }
</style>

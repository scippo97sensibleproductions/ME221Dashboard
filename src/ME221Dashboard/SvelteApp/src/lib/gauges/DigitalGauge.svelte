<script lang="ts">
  import type { GaugeDefinition } from './types';
  import { DigitalStyle } from './types';
  import { HybridBridge } from '../HybridBridge';
  import { formatDigits } from './digitUtils';

  let { gauge, pixelWidth, pixelHeight, valueTextColor, valueHistory = [] }: {
    gauge: GaugeDefinition;
    pixelWidth: number;
    pixelHeight: number;
    valueTextColor?: string;
    valueHistory?: number[];
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

  // Digit theming (R10): digitBgColor covers both the digit pill/cell and the
  // LED cell roles; the dim unit variant derives from ledColor; glowStrength
  // scales the existing glow blurs and opacities (1 + strength * 2).
  const digitBg = $derived(gauge.digitBgColor ?? '#1a1a1a');
  const ledColor = $derived(gauge.ledColor ?? '#ff3333');
  const ledColorDim = $derived(mixTowardWhite(ledColor, 0.35));
  const glowStrength = $derived(Math.max(0, Math.min(1, gauge.glowStrength ?? 0)));
  const glowFactor = $derived(1 + glowStrength * 2);
  const digitGlow = $derived(glowStrength > 0 ? glowText(ledColor, 6 * glowFactor, 0.25 * glowFactor) : '');
  const valueShadow = $derived('1px 1px 3px rgba(0,0,0,0.8)' + (digitGlow ? ', ' + digitGlow : ''));

  // Roll animation (R12): CSS animation restarted via keyed blocks when the
  // formatted digit string changes; snap (no animation) when disabled.
  const rollOn = $derived(!!gauge.rollAnimation);
  const rollSpeed = $derived(Math.max(50, Math.min(2000, gauge.rollSpeedMs ?? 300)));

  // Scale font sizes to fit within the container
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

  // Sparkline path for histogram background
  const sparklinePath = $derived.by(() => {
    if (valueHistory.length < 2) return '';
    const w = pixelWidth;
    const h = pixelHeight;
    const pad = 4;
    const plotW = w - pad * 2;
    const plotH = h * 0.28;
    const plotY = h - pad - plotH;
    let mn = Infinity, mx = -Infinity;
    for (const v of valueHistory) {
      if (v < mn) mn = v;
      if (v > mx) mx = v;
    }
    if (mn === mx) { mx = mn + 1; }
    const range = mx - mn;
    const step = plotW / (valueHistory.length - 1);
    let d = '';
    for (let i = 0; i < valueHistory.length; i++) {
      const x = pad + i * step;
      const y = plotY + plotH - ((valueHistory[i] - mn) / range) * plotH;
      d += (i === 0 ? 'M' : 'L') + `${x.toFixed(1)},${y.toFixed(1)}`;
    }
    return d;
  });

  const sparklineFillPath = $derived.by(() => {
    if (!sparklinePath) return '';
    const w = pixelWidth;
    const h = pixelHeight;
    const pad = 4;
    const plotH = h * 0.28;
    const plotY = h - pad - plotH;
    return `${sparklinePath}L${(pad + (valueHistory.length - 1) * ((w - pad * 2) / (valueHistory.length - 1))).toFixed(1)},${(plotY + plotH).toFixed(1)}L${pad},${(plotY + plotH).toFixed(1)}Z`;
  });

  function buildDigits(value: string): string[] {
    const display = value || '';
    const padded = display.length < 3 ? display.padStart(3) : display;
    return padded.split('');
  }

  const displayValue = $derived.by(() => {
    const dec = gauge.digitDecimals ?? -1;
    const pad = gauge.zeroPadding ?? false;
    const minD = gauge.minDigitCount ?? 0;
    if (dec === -1 && !pad && minD === 0) return gauge.formattedValue;
    return formatDigits(gauge.value, dec, pad, minD);
  });

  const digits = $derived(buildDigits(displayValue));
  const displayTextColor = $derived(valueTextColor ?? '#dee2e6');

  const insetGlow = $derived(glowText('#00ff88', 4 * glowFactor, (64 / 255) * glowFactor));
  const ringGlow = $derived(glowText(displayTextColor, 8 * glowFactor, (64 / 255) * glowFactor));
  const ringGlowInset = $derived(glowText(displayTextColor, 6 * glowFactor, (32 / 255) * glowFactor));
  const ringTextGlow = $derived(glowText(displayTextColor, 6 * glowFactor, (96 / 255) * glowFactor));

  function mixTowardWhite(hex: string, t: number): string {
    const h = hex.replace('#', '');
    const r = parseInt(h.slice(0, 2), 16);
    const g = parseInt(h.slice(2, 4), 16);
    const b = parseInt(h.slice(4, 6), 16);
    const mix = (c: number) => Math.round(c + (255 - c) * t);
    const toHex = (c: number) => c.toString(16).padStart(2, '0');
    return `#${toHex(mix(r))}${toHex(mix(g))}${toHex(mix(b))}`;
  }

  function rgba(hex: string, alpha: number): string {
    const h = hex.replace('#', '');
    const r = parseInt(h.slice(0, 2), 16);
    const g = parseInt(h.slice(2, 4), 16);
    const b = parseInt(h.slice(4, 6), 16);
    return `rgba(${r},${g},${b},${Math.max(0, Math.min(1, alpha)).toFixed(3)})`;
  }

  function glowText(color: string, blur: number, alpha: number): string {
    return `0 0 ${blur.toFixed(1)}px ${rgba(color, alpha)}`;
  }
</script>

<div class="relative flex h-full w-full flex-col items-center justify-center overflow-hidden select-none">
  {#if iconDataUrl}
    <img src={iconDataUrl} alt="" class="absolute pointer-events-none"
         style="width: {iconSz}px; height: {iconSz}px; left: calc(50% + {gauge.iconOffsetX * 100}% - {iconSz / 2}px); top: calc(50% + {gauge.iconOffsetY * 100}% - {iconSz / 2}px);" />
  {/if}

  {#if gauge.digitalStyle === DigitalStyle.LargeDigit}
    <!-- LargeDigit: value in dark pill, unit + name below, constrained to container -->
    <div class="flex flex-col items-center justify-center gap-0.5 max-h-full">
      {#if gauge.showValue}
        <div class="rounded-lg px-3 py-0.5 max-w-full overflow-hidden" style="background: {digitBg};">
          {#key rollOn ? displayValue : ''}
            <span class="block truncate {rollOn ? 'roll-digit' : ''}" style="animation-duration: {rollSpeed}ms; color: {displayTextColor}; font-size: {largeDigitValueSize}px; font-family: var(--font-display); text-shadow: {valueShadow}; line-height: 1.2;">
              {displayValue}
            </span>
          {/key}
        </div>
      {/if}
      {#if gauge.showUnit}
        <span class="block truncate max-w-full" style="color: {displayTextColor}; font-size: {unitSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8); line-height: 1.2;">
          {gauge.unit}
        </span>
      {/if}
      {#if gauge.showName}
        <span class="block truncate max-w-full" style="color: {displayTextColor}; font-size: {nameSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8); line-height: 1.2;">
          {gauge.name}
        </span>
      {/if}
    </div>

  {:else if gauge.digitalStyle === DigitalStyle.SevenSegment}
    <!-- SevenSegment: red LED digits in dark cells -->
    <div class="flex flex-col items-center justify-center gap-0.5 max-h-full">
      <div class="flex items-center justify-center gap-0.5">
        {#if gauge.showValue}
          {#each digits as ch, i (rollOn ? `${i}-${ch}` : i)}
            <div class="flex items-center justify-center rounded-sm"
                 style="background: {digitBg}; width: {segmentCellW}px; height: {segmentCellH}px;">
              <span class={rollOn ? 'roll-digit' : ''} style="animation-duration: {rollSpeed}ms; font-family: var(--font-7seg); font-size: {segmentDigitSize}px; color: {valueTextColor || ledColor}; text-shadow: {digitGlow || 'none'};">
                {ch}
              </span>
            </div>
          {/each}
        {/if}
      </div>
      {#if gauge.showUnit}
        <span style="font-size: {unitSize}px; color: {ledColorDim}; text-shadow: 1px 1px 3px rgba(0,0,0,0.8);">
          {gauge.unit}
        </span>
      {/if}
      {#if gauge.showName}
        <span class="truncate max-w-full" style="color: {displayTextColor}; font-size: {nameSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8);">
          {gauge.name}
        </span>
      {/if}
    </div>

  {:else if gauge.digitalStyle === DigitalStyle.Cluster}
    <!-- Cluster: dense value+unit with separator line and name -->
    <div class="flex flex-col items-center justify-center gap-0 max-h-full">
      <div class="flex items-center gap-1">
        {#if gauge.showValue}
          {#key rollOn ? displayValue : ''}
            <span class="truncate {rollOn ? 'roll-digit' : ''}" style="animation-duration: {rollSpeed}ms; color: {displayTextColor}; font-size: {clusterValueSize}px; font-family: var(--font-display); text-shadow: 1px 1px 3px rgba(0,0,0,0.8); line-height: 1.1;">
              {displayValue}
            </span>
          {/key}
        {/if}
        {#if gauge.showUnit}
          <span style="color: {displayTextColor}; font-size: {clusterUnitSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8); line-height: 1.1; margin-bottom: 2px;">
            {gauge.unit}
          </span>
        {/if}
      </div>
      <div style="background: {digitBg}; border-radius: 2px; height: 2px; width: {separatorWidth}px; margin: 3px 0;"></div>
      {#if gauge.showName}
        <span class="truncate max-w-full font-medium" style="color: {displayTextColor}; font-size: {clusterNameSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8);">
          {gauge.name}
        </span>
      {/if}
    </div>

  {:else if gauge.digitalStyle === DigitalStyle.LabelTop}
    <!-- LabelTop: name as label above value, unit below — inverted hierarchy -->
    <div class="flex flex-col items-center justify-center gap-0 max-h-full">
      {#if gauge.showName}
        <span class="truncate max-w-full uppercase tracking-widest" style="color: {displayTextColor}; opacity: 0.5; font-size: {labelTopLabelSize}px; line-height: 1.2;">
          {gauge.name}
        </span>
      {/if}
      {#if gauge.showValue}
        {#key rollOn ? gauge.formattedValue : ''}
          <span class="font-bold {rollOn ? 'roll-digit' : ''}" style="animation-duration: {rollSpeed}ms; color: {displayTextColor}; font-size: {labelTopValueSize}px; font-family: var(--font-display); text-shadow: 1px 1px 3px rgba(0,0,0,0.8); line-height: 1.1;">
            {gauge.formattedValue}
          </span>
        {/key}
      {/if}
      {#if gauge.showUnit}
        <span style="color: {displayTextColor}; opacity: 0.6; font-size: {labelTopUnitSize}px; line-height: 1.2;">
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
               style="border: 2px solid {displayTextColor}; box-shadow: {ringGlow}, inset {ringGlowInset};"></div>
          <!-- Value -->
          {#key rollOn ? gauge.formattedValue : ''}
            <span class="relative z-10 font-bold {rollOn ? 'roll-digit' : ''}" style="animation-duration: {rollSpeed}ms; color: {displayTextColor}; font-size: {glowValueSize}px; font-family: var(--font-display); text-shadow: {ringTextGlow}; line-height: 1.1;">
              {gauge.formattedValue}
            </span>
          {/key}
        </div>
      {/if}
      <div class="flex items-baseline gap-1">
        {#if gauge.showUnit}
          <span style="color: {displayTextColor}; opacity: 0.7; font-size: {glowUnitSize}px; line-height: 1.2;">
            {gauge.unit}
          </span>
        {/if}
      </div>
      {#if gauge.showName}
        <span class="truncate max-w-full" style="color: {displayTextColor}; opacity: 0.6; font-size: {glowNameSize}px; line-height: 1.2;">
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
            {#key rollOn ? displayValue : ''}
              <span class="block font-bold {rollOn ? 'roll-digit' : ''}" style="animation-duration: {rollSpeed}ms; color: {valueTextColor || '#00ff88'}; font-size: {insetValueSize}px; font-family: var(--font-7seg); text-shadow: {insetGlow}; line-height: 1.2;">
                {displayValue}
              </span>
            {/key}
          </div>
        {/if}
        {#if gauge.showUnit}
          <div class="text-center">
            <span class="block" style="color: #00ff88; opacity: 0.5; font-size: {insetUnitSize}px; font-family: var(--font-7seg); line-height: 1.2;">
              {gauge.unit}
            </span>
          </div>
        {/if}
      </div>
      {#if gauge.showName}
        <span class="truncate max-w-full" style="color: {displayTextColor}; opacity: 0.7; font-size: {insetNameSize}px; line-height: 1.2;">
          {gauge.name}
        </span>
      {/if}
    </div>

  {:else}
    <!-- Odometer (default): dark cells with light text -->
    <div class="flex flex-col items-center justify-center gap-0.5 max-h-full">
      <div class="flex items-center justify-center gap-0.5">
        {#if gauge.showValue}
          {#each digits as ch, i (rollOn ? `${i}-${ch}` : i)}
            <div class="flex items-center justify-center rounded-sm"
                 style="background: {digitBg}; width: {segmentCellW}px; height: {segmentCellH}px;">
              <span class={rollOn ? 'roll-digit' : ''} style="animation-duration: {rollSpeed}ms; color: {displayTextColor}; font-size: {segmentDigitSize}px; font-family: var(--font-7seg); text-shadow: {valueShadow};">
                {ch}
              </span>
            </div>
          {/each}
        {/if}
      </div>
      {#if gauge.showUnit}
        <span style="color: {displayTextColor}; font-size: {unitSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8);">
          {gauge.unit}
        </span>
      {/if}
      {#if gauge.showName}
        <span class="truncate max-w-full" style="color: {displayTextColor}; font-size: {nameSize}px; text-shadow: 1px 1px 3px rgba(0,0,0,0.8);">
          {gauge.name}
        </span>
      {/if}
    </div>
  {/if}

  {#if sparklinePath && valueHistory.length > 1}
    <svg class="pointer-events-none absolute inset-0" width={pixelWidth} height={pixelHeight}>
      <path d={sparklineFillPath} fill={displayTextColor ?? gauge.textColor} opacity="0.08" />
      <path d={sparklinePath} fill="none" stroke={displayTextColor ?? gauge.textColor} stroke-width="1.5" opacity="0.35" />
    </svg>
  {/if}
</div>

<style>
  .roll-digit {
    display: inline-block;
    animation-name: digit-roll;
    animation-timing-function: ease-out;
    animation-fill-mode: backwards;
  }

  @keyframes digit-roll {
    from {
      transform: translateY(50%);
      opacity: 0;
    }
    to {
      transform: translateY(0);
      opacity: 1;
    }
  }
</style>

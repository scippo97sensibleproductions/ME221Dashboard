<script lang="ts">
  import type { GaugeDefinition } from './types';
  import { computeValueFraction, gaugeValueColor, buildColorLuts, DEFAULT_COLOR_STOPS } from './types';
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

  const isHorizontal = $derived(pixelWidth >= pixelHeight);
  const valueFraction = $derived(computeValueFraction(gauge.value, gauge.minValue, gauge.maxValue));
  const colorLuts = $derived(buildColorLuts(
    gauge.colorStops?.length ? gauge.colorStops : DEFAULT_COLOR_STOPS,
    gauge.colorHysteresis ?? 0.03
  ));

  let _prevFraction = 0;
  const barColor = $derived.by(() => {
    const frac = valueFraction;
    const color = gaugeValueColor(frac, _prevFraction, colorLuts);
    _prevFraction = frac;
    return color;
  });

  const fontSizeScale = $derived(Math.max(0.5, Math.min(2.0, gauge.fontSizeScale ?? 1.0)));
  const valueSize = $derived(Math.max(12, (isHorizontal ? pixelHeight : pixelWidth) * 0.22 * fontSizeScale));
  const unitSize = $derived(Math.max(9, valueSize * 0.55));
  const nameSize = $derived(Math.max(7, valueSize * 0.35));
  const iconSz = $derived(Math.max(4, Math.min(80, (isHorizontal ? pixelHeight : pixelWidth) * gauge.iconSize)));

  const barGap = $derived(Math.min(6, (isHorizontal ? pixelHeight : pixelWidth) * 0.04));

  const hBarW = $derived(pixelWidth * 0.9);
  const hBarH = $derived(pixelHeight * 0.3);
  const hBarX = $derived((pixelWidth - hBarW) / 2);
  const hBarY = $derived((pixelHeight - hBarH) / 2);

  const vBarW = $derived(pixelWidth * 0.3);
  const vBarH = $derived(pixelHeight * 0.9);
  const vBarX = $derived((pixelWidth - vBarW) / 2);
  const vBarY = $derived((pixelHeight - vBarH) / 2);

  const iconX = $derived(isHorizontal ? pixelWidth / 2 + gauge.iconOffsetX * pixelWidth : vBarX + vBarW / 2 + gauge.iconOffsetX * pixelWidth);
  const iconY = $derived(isHorizontal ? hBarY + hBarH / 2 + gauge.iconOffsetY * pixelHeight : pixelHeight / 2 + gauge.iconOffsetY * pixelHeight);

  function posToXY(idx: number, textW: number, textH: number): { x: number; y: number } {
    const col = idx % 3;
    const row = Math.floor(idx / 3);
    let x: number;
    let y: number;
    if (isHorizontal) {
      const textAreaTop = row === 0 ? 0 : row === 1 ? hBarY : hBarY + hBarH + barGap;
      const textAreaH = row === 1 ? hBarH : row === 0 ? hBarY - barGap : pixelHeight - hBarY - hBarH - barGap;
      const textAreaLeft = 0;
      const textAreaW = pixelWidth;
      x = col === 0 ? textAreaLeft + textW / 2 + 2 : col === 2 ? textAreaLeft + textAreaW - textW / 2 - 2 : textAreaLeft + textAreaW / 2;
      y = row === 1
        ? hBarY + hBarH / 2 - textH / 2 + textH * 0.8
        : textAreaTop + textAreaH / 2 + textH * 0.3;
    } else {
      const textAreaLeft = col === 0 ? 0 : col === 1 ? vBarX : vBarX + vBarW + barGap;
      const textAreaW = col === 1 ? vBarW : col === 0 ? vBarX - barGap : pixelWidth - vBarX - vBarW - barGap;
      const textAreaTop = 0;
      const textAreaH = pixelHeight;
      x = textAreaLeft + textAreaW / 2;
      y = row === 0 ? textAreaTop + textH * 0.8 + 2 : row === 2 ? textAreaTop + textAreaH - textH * 0.2 - 2 : textAreaTop + textAreaH / 2 - textH / 2 + textH * 0.8;
    }
    return { x, y };
  }

  const valuePos = $derived(posToXY(gauge.barValuePosition, valueSize * gauge.formattedValue.length * 0.6, valueSize));
  const unitPos = $derived(posToXY(gauge.barUnitPosition, unitSize * gauge.unit.length * 0.6, unitSize));
  const namePos = $derived(posToXY(gauge.barNamePosition, nameSize * gauge.name.length * 0.5, nameSize));

  const displayTextColor = $derived(valueTextColor ?? '#dee2e6');
</script>

<svg width={pixelWidth} height={pixelHeight} viewBox="0 0 {pixelWidth} {pixelHeight}" overflow="visible" class="select-none" xmlns="http://www.w3.org/2000/svg">
  {#if isHorizontal}
    <rect x={hBarX} y={hBarY} width={hBarW} height={hBarH} rx="4" fill="#ced4da" />
    {#if valueFraction > 0}
      <rect x={hBarX} y={hBarY} width={Math.max(4, valueFraction * hBarW)} height={hBarH} rx="4" fill={barColor} />
    {/if}
  {:else}
    <rect x={vBarX} y={vBarY} width={vBarW} height={vBarH} rx="4" fill="#ced4da" />
    {#if valueFraction > 0}
      <rect x={vBarX} y={vBarY + vBarH * (1 - valueFraction)} width={vBarW} height={Math.max(4, valueFraction * vBarH)} rx="4" fill={barColor} />
    {/if}
  {/if}

  {#if iconDataUrl}
    <image href={iconDataUrl} x={iconX - iconSz / 2} y={iconY - iconSz / 2} width={iconSz} height={iconSz} />
  {/if}

  {#if gauge.showValue}
    <text x={valuePos.x} y={valuePos.y} text-anchor="middle" fill={displayTextColor} font-size={valueSize} font-weight="bold">
      {gauge.formattedValue}
    </text>
  {/if}
  {#if gauge.showUnit && gauge.unit}
    <text x={unitPos.x} y={unitPos.y} text-anchor="middle" fill={displayTextColor} font-size={unitSize}>
      {gauge.unit}
    </text>
  {/if}
  {#if gauge.showName}
    <text x={namePos.x} y={namePos.y} text-anchor="middle" fill={displayTextColor} font-size={nameSize}>
      {gauge.name}
    </text>
  {/if}
</svg>

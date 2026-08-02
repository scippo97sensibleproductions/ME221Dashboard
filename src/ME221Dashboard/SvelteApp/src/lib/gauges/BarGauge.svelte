<script lang="ts">
  import type { GaugeDefinition } from './types';
  import { computeValueFraction, gaugeValueColor, buildColorLuts, DEFAULT_COLOR_STOPS } from './types';
  import { buildScaleTicks } from './scaleUtils';
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

  // 0=auto (shape-based, current behavior) 1=horizontal 2=vertical
  const isHorizontal = $derived(
    gauge.barOrientation === 1 ? true : gauge.barOrientation === 2 ? false : pixelWidth >= pixelHeight
  );
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

  // barThickness: 0=auto (pixelHeight*0.3 / pixelWidth*0.3), else % of the perpendicular dimension
  const barThicknessFrac = $derived(gauge.barThickness > 0 ? Math.min(1, gauge.barThickness / 100) : 0);

  const hBarW = $derived(pixelWidth * 0.9);
  const hBarH = $derived(barThicknessFrac > 0 ? Math.max(2, pixelHeight * barThicknessFrac) : pixelHeight * 0.3);
  const hBarX = $derived((pixelWidth - hBarW) / 2);
  const hBarY = $derived((pixelHeight - hBarH) / 2);

  const vBarW = $derived(barThicknessFrac > 0 ? Math.max(2, pixelWidth * barThicknessFrac) : pixelWidth * 0.3);
  const vBarH = $derived(pixelHeight * 0.9);
  const vBarX = $derived((pixelWidth - vBarW) / 2);
  const vBarY = $derived((pixelHeight - vBarH) / 2);

  const barTicks = $derived(
    gauge.barTicks ? buildScaleTicks(gauge.minValue, gauge.maxValue, gauge.tickCount, false, 1) : []
  );
  const endLabelSize = $derived(Math.max(8, Math.min(12, (isHorizontal ? hBarH : vBarW) * 0.5)));

  function formatEndLabel(v: number): string {
    if (!Number.isFinite(v)) return '';
    if (Math.abs(v) >= 100 || Number.isInteger(v)) {
      return Math.round(v).toLocaleString('en-US');
    }
    return String(Math.round(v * 10) / 10);
  }

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
    {#if gauge.barRedlineStart > 0}
      <rect x={hBarX + gauge.barRedlineStart * hBarW} y={hBarY} width={Math.max(4, (1 - gauge.barRedlineStart) * hBarW)} height={hBarH} rx="4" fill={gauge.barRedlineColor} />
    {/if}
    {#if barTicks.length}
      {#each barTicks as t (t.fraction)}
        <line x1={hBarX + t.fraction * hBarW} y1={hBarY} x2={hBarX + t.fraction * hBarW} y2={hBarY + hBarH} stroke="#868e96" stroke-width="1.5" stroke-linecap="round" />
      {/each}
    {/if}
    {#if gauge.barMinMaxLabels}
      <text x={hBarX + 3} y={hBarY + hBarH / 2 + endLabelSize * 0.35} text-anchor="start" fill="#868e96" font-size={endLabelSize}>
        {formatEndLabel(gauge.minValue)}
      </text>
      <text x={hBarX + hBarW - 3} y={hBarY + hBarH / 2 + endLabelSize * 0.35} text-anchor="end" fill="#868e96" font-size={endLabelSize}>
        {formatEndLabel(gauge.maxValue)}
      </text>
    {/if}
  {:else}
    <rect x={vBarX} y={vBarY} width={vBarW} height={vBarH} rx="4" fill="#ced4da" />
    {#if valueFraction > 0}
      <rect x={vBarX} y={vBarY + vBarH * (1 - valueFraction)} width={vBarW} height={Math.max(4, valueFraction * vBarH)} rx="4" fill={barColor} />
    {/if}
    {#if gauge.barRedlineStart > 0}
      <rect x={vBarX} y={vBarY + vBarH * (1 - gauge.barRedlineStart)} width={vBarW} height={Math.max(4, gauge.barRedlineStart * vBarH)} rx="4" fill={gauge.barRedlineColor} />
    {/if}
    {#if barTicks.length}
      {#each barTicks as t (t.fraction)}
        <line x1={vBarX} y1={vBarY + vBarH * (1 - t.fraction)} x2={vBarX + vBarW} y2={vBarY + vBarH * (1 - t.fraction)} stroke="#868e96" stroke-width="1.5" stroke-linecap="round" />
      {/each}
    {/if}
    {#if gauge.barMinMaxLabels}
      <text x={vBarX + vBarW / 2} y={vBarY + vBarH - 3} text-anchor="middle" fill="#868e96" font-size={endLabelSize}>
        {formatEndLabel(gauge.minValue)}
      </text>
      <text x={vBarX + vBarW / 2} y={vBarY + endLabelSize} text-anchor="middle" fill="#868e96" font-size={endLabelSize}>
        {formatEndLabel(gauge.maxValue)}
      </text>
    {/if}
  {/if}

  {#if iconDataUrl}
    <image href={iconDataUrl} x={iconX - iconSz / 2} y={iconY - iconSz / 2} width={iconSz} height={iconSz} />
  {/if}

  {#if gauge.showValue}
    <text x={valuePos.x} y={valuePos.y} text-anchor="middle" fill={displayTextColor} font-size={valueSize} font-weight="bold" font-family="var(--font-display)">
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

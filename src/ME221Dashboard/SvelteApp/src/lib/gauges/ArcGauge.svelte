<script lang="ts">
  import type { GaugeDefinition } from './types';
  import { DEFAULT_COLOR_STOPS, computeValueFraction, gaugeValueColor, positionToCenterAngle, describeArc, buildColorLuts, interpolateNeedleAngle } from './types';
  import { HybridBridge } from '../HybridBridge';

  let { gauge, pixelWidth, pixelHeight }: {
    gauge: GaugeDefinition;
    pixelWidth: number;
    pixelHeight: number;
  } = $props();

  const containerBox = $derived(Math.min(pixelWidth, pixelHeight));
  const arcBox = $derived(containerBox);
  const arcThickness = $derived(Math.max(4, Math.min(16, arcBox * 0.06)));
  const cx = $derived(arcBox / 2);
  const cy = $derived(arcBox / 2);

  const valueFraction = $derived(computeValueFraction(gauge.value, gauge.minValue, gauge.maxValue));
  const colorLuts = $derived(buildColorLuts(
    gauge.colorStops?.length ? gauge.colorStops : DEFAULT_COLOR_STOPS,
    gauge.colorHysteresis ?? 0.03
  ));

  let _prevFraction = 0;
  const arcColor = $derived.by(() => {
    const frac = valueFraction;
    const color = gaugeValueColor(frac, _prevFraction, colorLuts);
    _prevFraction = frac;
    return color;
  });

  // Load texture image as data URL — Svelte tracks gauge.texturePath and only re-runs on change
  let textureDataUrl = $state<string | null>(null);
  $effect(() => {
    const p = gauge.texturePath;
    if (p) {
      HybridBridge.getImageBase64(p).then(result => {
        textureDataUrl = result.success && result.dataUrl ? result.dataUrl : null;
      }).catch(() => { textureDataUrl = null; });
    } else {
      textureDataUrl = null;
    }
  });

  // Load icon image — Svelte tracks gauge.iconName and only re-runs on change
  let iconDataUrl = $state<string | null>(null);
  $effect(() => {
    const p = gauge.iconName;
    if (p) {
      HybridBridge.getImageBase64(p).then(r => { iconDataUrl = r.success && r.dataUrl ? r.dataUrl : null; }).catch(() => { iconDataUrl = null; });
    } else {
      iconDataUrl = null;
    }
  });
  const iconSz = $derived(Math.max(4, Math.min(80, arcBox * gauge.iconSize)));
  const iconX = $derived(cx + gauge.iconOffsetX * arcBox);
  const iconY = $derived(cy + gauge.iconOffsetY * arcBox);

  const centerAngle = $derived(positionToCenterAngle(gauge.arcPosition));
  const arcStartAngle = $derived.by(() => {
    let start = centerAngle - gauge.sweepAngle / 2;
    if (start < 0) start += 360;
    return start;
  });
  const arcEndAngle = $derived(arcStartAngle + gauge.sweepAngle);
  const valueArcEndAngle = $derived(arcStartAngle + valueFraction * gauge.sweepAngle);

  const needleAngle = $derived(
    gauge.needleCurve?.length
      ? interpolateNeedleAngle(gauge.value, gauge.needleCurve)
      : gauge.needleStartAngle + valueFraction * (gauge.needleEndAngle - gauge.needleStartAngle)
  );
  const needleThickness = $derived(Math.max(1, Math.min(10, gauge.needleWidth)));
  const needleRadius = $derived((arcBox / 2 - arcThickness * 1.5) * Math.max(0.1, Math.min(2.0, gauge.needleLength)));
  const needleCenterX = $derived(cx + gauge.needleOffsetX);
  const needleCenterY = $derived(cy + gauge.needleOffsetY);
  const needleRad = $derived(needleAngle * Math.PI / 180);
  const needleEndX = $derived(needleCenterX + needleRadius * Math.cos(needleRad));
  const needleEndY = $derived(needleCenterY + needleRadius * Math.sin(needleRad));

  const tickPcts = [0, 0.5, 1.0];
  const r = $derived(arcBox / 2);
  const innerR = $derived(r - arcThickness - 6);
  const outerR = $derived(r - 4);

  const fontSizeScale = $derived(Math.max(0.5, Math.min(2.0, gauge.fontSizeScale ?? 1.0)));
  const valueSize = $derived(Math.max(14, arcBox * 0.22 * fontSizeScale));
  const unitSize = $derived(Math.max(10, arcBox * 0.08 * fontSizeScale));
  const nameSize = $derived(Math.max(6, 9 * fontSizeScale));
  const textY = $derived(Math.max(0, Math.min(Math.max(0, arcBox - 40), arcBox * 0.65 + gauge.labelVerticalOffset)));


  // Cached background arc path — only recomputes on dimension/position change, not value
  const bgArcPath = $derived(describeArc(cx, cy, r - arcThickness / 2, arcStartAngle, arcEndAngle));
</script>

<svg width="{arcBox}" height="{arcBox}" viewBox="0 0 {arcBox} {arcBox}" overflow="visible"
     style="position:absolute; left:50%; top:50%; transform:translate(-50%,-50%);"
     xmlns="http://www.w3.org/2000/svg">
  {#if textureDataUrl}
    <image href={textureDataUrl} width="100%" height="100%"
           preserveAspectRatio="xMidYMid slice" />
  {:else}
    <path d={bgArcPath}
          fill="none" stroke="#ced4da" stroke-width={arcThickness} stroke-linecap="round" />
    <path d={describeArc(cx, cy, r - arcThickness / 2, arcStartAngle, valueArcEndAngle)}
          fill="none" stroke={arcColor} stroke-width={arcThickness} stroke-linecap="round" />
    {#each tickPcts as pct}
      {@const tickAngleRad = (arcStartAngle + pct * gauge.sweepAngle) * Math.PI / 180}
      <line x1={cx + outerR * Math.cos(tickAngleRad)}
            y1={cy + outerR * Math.sin(tickAngleRad)}
            x2={cx + innerR * Math.cos(tickAngleRad)}
            y2={cy + innerR * Math.sin(tickAngleRad)}
            stroke="#868e96" stroke-width="1.5" stroke-linecap="round" />
    {/each}
  {/if}

  <line x1={needleCenterX} y1={needleCenterY}
        x2={needleEndX} y2={needleEndY}
        stroke={arcColor} stroke-width={needleThickness} stroke-linecap="round" />

  <circle cx={needleCenterX} cy={needleCenterY} r="4"
          fill="none" stroke={arcColor} stroke-width="2" />
  <circle cx={needleCenterX} cy={needleCenterY} r="2"
          fill={arcColor} />

  {#if iconDataUrl}
    <image href={iconDataUrl} x={iconX - iconSz / 2} y={iconY - iconSz / 2} width={iconSz} height={iconSz} />
  {/if}

  {#if gauge.showValue || gauge.showUnit || gauge.showName}
    <g transform="translate(0, {textY})">
      {#if gauge.showValue}
        <text x={cx} y="0" text-anchor="middle" fill={gauge.textColor}
              font-size={valueSize} font-weight="bold"
              >
          {gauge.formattedValue}
        </text>
        {@const valueHeight = valueSize}
        {#if gauge.showUnit}
          <text x={cx} y={valueHeight + 2} text-anchor="middle" fill={gauge.textColor}
                font-size={unitSize}>
            {gauge.unit}
          </text>
          {@const unitPos = valueHeight + 2 + unitSize}
          {#if gauge.showName}
            <text x={cx} y={unitPos + 2} text-anchor="middle" fill={gauge.textColor}
                  font-size={nameSize}>
              {gauge.name}
            </text>
          {/if}
        {:else if gauge.showName}
          <text x={cx} y={valueHeight + 2} text-anchor="middle" fill={gauge.textColor}
                font-size={nameSize}>
            {gauge.name}
          </text>
        {/if}
      {:else if gauge.showUnit}
        <text x={cx} y="0" text-anchor="middle" fill={gauge.textColor}
              font-size={unitSize}>
          {gauge.unit}
        </text>
        {#if gauge.showName}
          <text x={cx} y={unitSize + 2} text-anchor="middle" fill={gauge.textColor}
                font-size={nameSize}>
            {gauge.name}
          </text>
        {/if}
      {:else if gauge.showName}
        <text x={cx} y="0" text-anchor="middle" fill={gauge.textColor}
              font-size={nameSize}>
          {gauge.name}
        </text>
      {/if}
    </g>
  {/if}
</svg>

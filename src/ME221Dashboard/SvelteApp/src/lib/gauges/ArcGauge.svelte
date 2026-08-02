<script lang="ts">
  import type { GaugeDefinition } from './types';
  import { DEFAULT_COLOR_STOPS, computeValueFraction, gaugeValueColor, positionToCenterAngle, describeArc, buildColorLuts, interpolateNeedleAngle } from './types';
  import { buildScaleTicks } from './scaleUtils';
  import { HybridBridge } from '../HybridBridge';

  let { gauge, pixelWidth, pixelHeight, valueTextColor }: {
    gauge: GaugeDefinition;
    pixelWidth: number;
    pixelHeight: number;
    valueTextColor?: string;
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

  const ticks = $derived(buildScaleTicks(
    gauge.minValue, gauge.maxValue, gauge.tickCount, gauge.tickLabels, gauge.tickLabelEvery
  ));
  const r = $derived(arcBox / 2);
  const innerR = $derived(r - arcThickness - 6);
  const outerR = $derived(r - 4);
  // tickSide 0=inside (default, matches legacy 3-tick geometry), 1=outside (mirrored outward)
  const tickOuterR = $derived(gauge.tickSide === 1 ? r + 4 : outerR);
  const tickInnerR = $derived(gauge.tickSide === 1 ? r + 4 - (outerR - innerR) : innerR);
  const tickLabelR = $derived(gauge.tickSide === 1 ? tickOuterR + 8 : tickInnerR - 8);
  const tickLabelSize = $derived(Math.max(8, arcBox * 0.05));
  const redlineStrokeWidth = $derived(Math.max(0.5, Math.min(20, gauge.redlineWidth)));

  // ── Needle geometry (shape 0 = legacy line + dot; 1-3 are polygons) ──
  const halfW = $derived(needleThickness / 2);
  const unitX = $derived(Math.cos(needleRad));
  const unitY = $derived(Math.sin(needleRad));
  const perpX = $derived(-unitY);
  const perpY = $derived(unitX);
  const taperedPoints = $derived([
    needleEndX, needleEndY,
    needleCenterX + perpX * halfW, needleCenterY + perpY * halfW,
    needleCenterX - perpX * halfW, needleCenterY - perpY * halfW,
  ].join(' '));
  const paddleTipX = $derived(needleCenterX + unitX * needleRadius * 1.08);
  const paddleTipY = $derived(needleCenterY + unitY * needleRadius * 1.08);
  const paddleW = $derived(Math.max(halfW * 2, halfW + 1.5));
  const paddlePoints = $derived([
    paddleTipX + perpX * paddleW, paddleTipY + perpY * paddleW,
    paddleTipX - perpX * paddleW, paddleTipY - perpY * paddleW,
    needleCenterX - perpX * halfW, needleCenterY - perpY * halfW,
    needleCenterX + perpX * halfW, needleCenterY + perpY * halfW,
  ].join(' '));
  const cwTailLen = $derived(needleRadius * 0.35);
  const cwTailW = $derived(halfW * 0.7);
  const cwTailX = $derived(needleCenterX - unitX * cwTailLen);
  const cwTailY = $derived(needleCenterY - unitY * cwTailLen);
  const cwPoints = $derived([
    needleEndX, needleEndY,
    needleCenterX + perpX * halfW, needleCenterY + perpY * halfW,
    cwTailX + perpX * cwTailW, cwTailY + perpY * cwTailW,
    cwTailX - perpX * cwTailW, cwTailY - perpY * cwTailW,
    needleCenterX - perpX * halfW, needleCenterY - perpY * halfW,
  ].join(' '));

  const fontSizeScale = $derived(Math.max(0.5, Math.min(2.0, gauge.fontSizeScale ?? 1.0)));
  const valueSize = $derived(Math.max(14, arcBox * 0.22 * fontSizeScale));
  const unitSize = $derived(Math.max(10, arcBox * 0.08 * fontSizeScale));
  const nameSize = $derived(Math.max(6, 9 * fontSizeScale));
  const displayTextColor = $derived(valueTextColor ?? '#dee2e6');
  const textY = $derived(Math.max(0, Math.min(Math.max(0, arcBox - 40), arcBox * 0.65 + gauge.labelVerticalOffset)));


  // Cached background arc path — only recomputes on dimension/position change, not value
  const bgArcPath = $derived(describeArc(cx, cy, r - arcThickness / 2, arcStartAngle, arcEndAngle));
</script>

<svg width="{arcBox}" height="{arcBox}" viewBox="0 0 {arcBox} {arcBox}" overflow="visible" class="select-none"
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
    {#if gauge.redlineStart > 0}
      <path d={describeArc(cx, cy, r - arcThickness / 2, arcStartAngle + gauge.redlineStart * gauge.sweepAngle, arcEndAngle)}
            fill="none" stroke={gauge.redlineColor} stroke-width={redlineStrokeWidth} stroke-linecap="round" />
    {/if}
    {#each ticks as tick (tick.fraction)}
      {@const tickAngleRad = (arcStartAngle + tick.fraction * gauge.sweepAngle) * Math.PI / 180}
      {@const tickX1 = cx + tickOuterR * Math.cos(tickAngleRad)}
      {@const tickY1 = cy + tickOuterR * Math.sin(tickAngleRad)}
      {@const tickX2 = cx + tickInnerR * Math.cos(tickAngleRad)}
      {@const tickY2 = cy + tickInnerR * Math.sin(tickAngleRad)}
      <line x1={tickX1} y1={tickY1} x2={tickX2} y2={tickY2}
            stroke="#868e96" stroke-width="1.5" stroke-linecap="round" />
      {#if tick.label !== null}
        {@const tickLx = cx + tickLabelR * Math.cos(tickAngleRad)}
        {@const tickLy = cy + tickLabelR * Math.sin(tickAngleRad)}
        {@const tickRot = arcStartAngle + tick.fraction * gauge.sweepAngle + 90}
        <text x={tickLx} y={tickLy} text-anchor="middle" dominant-baseline="middle"
              font-size={tickLabelSize} fill="#868e96"
              transform="rotate({tickRot} {tickLx} {tickLy})"
              font-family="var(--font-display)">
          {tick.label}
        </text>
      {/if}
    {/each}
  {/if}

  {#if gauge.needleShape === 0}
    <line x1={needleCenterX} y1={needleCenterY}
          x2={needleEndX} y2={needleEndY}
          stroke={arcColor} stroke-width={needleThickness} stroke-linecap="round" />

    <circle cx={needleCenterX} cy={needleCenterY} r="4"
            fill="none" stroke={arcColor} stroke-width="2" />
    <circle cx={needleCenterX} cy={needleCenterY} r="2"
            fill={arcColor} />
  {:else if gauge.needleShape === 1}
    <polygon points={taperedPoints} fill={arcColor} />
  {:else if gauge.needleShape === 2}
    <polygon points={paddlePoints} fill={arcColor} />
  {:else}
    <polygon points={cwPoints} fill={arcColor} />
  {/if}

  {#if iconDataUrl}
    <image href={iconDataUrl} x={iconX - iconSz / 2} y={iconY - iconSz / 2} width={iconSz} height={iconSz} />
  {/if}

  {#if gauge.showValue || gauge.showUnit || gauge.showName}
    <g transform="translate(0, {textY})">
      {#if gauge.showValue}
        <text x={cx} y="0" text-anchor="middle" fill={displayTextColor}
              font-size={valueSize} font-weight="bold"
              font-family="var(--font-display)">
          {gauge.formattedValue}
        </text>
        {@const valueHeight = valueSize}
        {#if gauge.showUnit}
          <text x={cx} y={valueHeight + 2} text-anchor="middle" fill={displayTextColor}
                font-size={unitSize}>
            {gauge.unit}
          </text>
          {@const unitPos = valueHeight + 2 + unitSize}
          {#if gauge.showName}
            <text x={cx} y={unitPos + 2} text-anchor="middle" fill={displayTextColor}
                  font-size={nameSize}>
              {gauge.name}
            </text>
          {/if}
        {:else if gauge.showName}
          <text x={cx} y={valueHeight + 2} text-anchor="middle" fill={displayTextColor}
                font-size={nameSize}>
            {gauge.name}
          </text>
        {/if}
      {:else if gauge.showUnit}
        <text x={cx} y="0" text-anchor="middle" fill={displayTextColor}
              font-size={unitSize}>
          {gauge.unit}
        </text>
        {#if gauge.showName}
          <text x={cx} y={unitSize + 2} text-anchor="middle" fill={displayTextColor}
                font-size={nameSize}>
            {gauge.name}
          </text>
        {/if}
      {:else if gauge.showName}
        <text x={cx} y="0" text-anchor="middle" fill={displayTextColor}
              font-size={nameSize}>
          {gauge.name}
        </text>
      {/if}
    </g>
  {/if}
</svg>

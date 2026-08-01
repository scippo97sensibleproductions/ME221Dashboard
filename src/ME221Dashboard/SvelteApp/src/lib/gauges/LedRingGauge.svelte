<script lang="ts">
  import type { GaugeDefinition } from './types';
  import { computeValueFraction, gaugeValueColor, buildColorLuts, DEFAULT_COLOR_STOPS } from './types';
  import { clampZoneThresholds, zoneColorAt } from './zoneUtils';

  let { gauge, pixelWidth, pixelHeight, valueTextColor }: {
    gauge: GaugeDefinition;
    pixelWidth: number;
    pixelHeight: number;
    valueTextColor?: string;
  } = $props();

  const valueFraction = $derived(computeValueFraction(gauge.value, gauge.minValue, gauge.maxValue));
  const colorLuts = $derived(buildColorLuts(
    gauge.colorStops?.length ? gauge.colorStops : DEFAULT_COLOR_STOPS,
    gauge.colorHysteresis ?? 0.03
  ));

  let _prevFraction = 0;
  const ringColor = $derived.by(() => {
    const frac = valueFraction;
    const color = gaugeValueColor(frac, _prevFraction, colorLuts);
    _prevFraction = frac;
    return color;
  });

  const fontSizeScale = $derived(Math.max(0.5, Math.min(2.0, gauge.fontSizeScale ?? 1.0)));
  const size = $derived(Math.min(pixelWidth, pixelHeight));
  const cx = $derived(pixelWidth / 2);
  const cy = $derived(pixelHeight / 2);
  const outerR = $derived(size * 0.44);
  const ringWidth = $derived(size * 0.09);
  const segmentCount = $derived(Math.max(4, Math.min(120, Math.round(gauge.segmentCount ?? 36))));
  const segmentGap = $derived(Math.max(0, Math.min(1, gauge.segmentGap ?? 0)));
  const startAngle = $derived(-90 + (gauge.ringStartAngle ?? 0));
  const sweep = $derived(gauge.ringSweepAngle ?? 360);
  const zone = $derived(clampZoneThresholds(gauge.amberThreshold ?? 0.7, gauge.redThreshold ?? 0.85));

  function polarToCart(cx: number, cy: number, r: number, angleDeg: number) {
    const rad = angleDeg * Math.PI / 180;
    return { x: cx + r * Math.cos(rad), y: cy + r * Math.sin(rad) };
  }

  function segColor(i: number): string {
    const f = i / segmentCount;
    return zoneColorAt(f, zone.amber, zone.red, ringColor);
  }

  const activeCount = $derived(Math.round(valueFraction * segmentCount));
  const valueSize = $derived(Math.max(10, size * 0.18 * fontSizeScale));
  const unitSize = $derived(Math.max(7, size * 0.07 * fontSizeScale));
  const nameSize = $derived(Math.max(6, size * 0.05 * fontSizeScale));
</script>

<svg viewBox="0 0 {pixelWidth} {pixelHeight}" xmlns="http://www.w3.org/2000/svg" class="size-full">
  <defs>
    <filter id="lr-{gauge.entityId}"><feGaussianBlur stdDeviation="2" result="b"/>
      <feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
  </defs>

  <!-- Segments -->
  {#each Array(segmentCount) as _, i}
    {@const f = i / segmentCount}
    {@const angle = startAngle + f * sweep}
    {@const active = i < activeCount}
    {@const col = active ? segColor(i) : '#14141e'}
    {@const p1 = polarToCart(cx, cy, outerR - ringWidth / 2, angle)}
    {@const p2 = polarToCart(cx, cy, outerR + ringWidth / 2, angle)}
    {@const segLen = (sweep / segmentCount) * outerR * 0.45 * (1 - segmentGap)}
    <line x1={p1.x} y1={p1.y} x2={p2.x} y2={p2.y}
      stroke={col} stroke-width={segLen} stroke-linecap="round"
      opacity={active ? 0.9 : 0.25}
      filter={active ? `url(#lr-${gauge.entityId})` : ''}/>
  {/each}

  <!-- Center background -->
  <circle {cx} {cy} r={outerR - ringWidth - 2} fill="#0a0a0a" stroke="#1a1a1a" stroke-width="0.5"/>

  <!-- Value -->
  {#if gauge.showValue}
    <text x={cx} y={cy + 1} text-anchor="middle" dominant-baseline="central"
      fill={valueTextColor || ringColor} font-family="'Orbitron Variable','Segoe UI',sans-serif"
      font-size={valueSize} font-weight="800"
      filter={`url(#lr-${gauge.entityId})`}>
      {gauge.formattedValue}
    </text>
  {/if}

  <!-- Unit -->
  {#if gauge.showUnit && gauge.unit}
    <text x={cx} y={cy + valueSize * 0.7} text-anchor="middle"
      fill="#666666" font-family="'Segoe UI',sans-serif"
      font-size={unitSize} letter-spacing="0.1em">
      {gauge.unit}
    </text>
  {/if}

  <!-- Name -->
  {#if gauge.showName}
    <text x={cx} y={cy + outerR + ringWidth + nameSize + 4} text-anchor="middle"
      fill="#666666" font-family="'Segoe UI',sans-serif"
      font-size={nameSize} letter-spacing="0.08em" text-transform="uppercase">
      {gauge.name}
    </text>
  {/if}
</svg>

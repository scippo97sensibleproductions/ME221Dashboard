<script lang="ts">
  import type { GaugeDefinition } from './types';
  import { GaugeShapeCategory } from './types';
  import { levelVisualStyle } from './gaugeUtils';
  import { IconAlertTriangle } from '@tabler/icons-svelte';
  import ArcGauge from './ArcGauge.svelte';
  import BarGauge from './BarGauge.svelte';
  import TextGauge from './TextGauge.svelte';
  import DigitalGauge from './DigitalGauge.svelte';
  import ChartGauge from './ChartGauge.svelte';
  import WedgeBarGauge from './WedgeBarGauge.svelte';
  import LedRingGauge from './LedRingGauge.svelte';
  import MultiRingGauge from './MultiRingGauge.svelte';
  import ShiftLightGauge from './ShiftLightGauge.svelte';
  import { pulseCounter } from './pulseCounter';

  let { gauge, pixelWidth, pixelHeight, valueHistory = [], overlayHistories = {}, preview = false }: {
    gauge: GaugeDefinition;
    pixelWidth: number;
    pixelHeight: number;
    valueHistory?: number[];
    overlayHistories?: Record<string, { t: number; v: number }[]>;
    /** Settings-modal preview (shift-light only: steady test-value rendering). */
    preview?: boolean;
  } = $props();

  // R11: level color border/bg; the pulse counter feeds one-shot pulses per
  // toast-firing activation into a flash-enabled level.
  let pulseBaseline = $state(pulseCounter.mount());
  let pulseCount = $state(0);
  let pulsing = $state(false);

  const warningStyle = $derived(
    gauge.warningState === 'none'
      ? levelVisualStyle(null, null, gauge.name)
      : levelVisualStyle(gauge.warningLevelColor ?? null, gauge.warningLevelName ?? null, gauge.name)
  );

  // Consume pulses (delta read, non-destructive); each pulse re-keys the
  // one-shot animation. A drop to "none" ends the animation immediately.
  $effect(() => {
    const delta = pulseCounter.delta(pulseBaseline);
    if (delta > 0) {
      pulseBaseline += delta;
      pulseCount++;
      pulsing = true;
    }
    if (gauge.warningState === 'none') {
      pulsing = false;
    }
  });

  // R11: level-name indicator hidden below the gauge-area threshold; reduced
  // scale when a CVD-confusable pair exists on the datalink.
  const showLevelName = $derived(
    gauge.warningState !== 'none' && gauge.warningLevelName != null
  );
  const cvdReduced = $derived(gauge.warningCvdFlag === true);
  const belowAreaThreshold = $derived(pixelHeight < 40 || pixelWidth < 80);
</script>

<div
  class="size-full relative gauge-card-wrap"
  style:border={warningStyle.border}
  style:border-radius="0.375rem"
  style:background={warningStyle.bg}
  style:box-shadow={warningStyle.shadow}
  style:--pulse-color={warningStyle.color ?? '#f59e0b'}
  style:animation={pulsing && gauge.warningState !== 'none' ? `pulse-level 0.8s ease ${pulseCount}` : 'none'}
  role={warningStyle.ariaLabel ? 'alert' : undefined}
  aria-label={warningStyle.ariaLabel ?? undefined}
>
  {#if gauge.category === GaugeShapeCategory.Text}
    <TextGauge {gauge} {pixelWidth} {pixelHeight} valueTextColor={warningStyle.color ?? gauge.textColor} />
  {:else if gauge.category === GaugeShapeCategory.Bar}
    <BarGauge {gauge} {pixelWidth} {pixelHeight} valueTextColor={warningStyle.color ?? gauge.textColor} />
  {:else if gauge.category === GaugeShapeCategory.Digital}
    <DigitalGauge {gauge} {pixelWidth} {pixelHeight} valueTextColor={warningStyle.color ?? gauge.textColor} valueHistory={gauge.showHistogram ? valueHistory : []} />
  {:else if gauge.category === GaugeShapeCategory.Chart}
    <ChartGauge {gauge} {pixelWidth} {pixelHeight} {overlayHistories} />
  {:else if gauge.category === GaugeShapeCategory.WedgeBar}
    <WedgeBarGauge {gauge} {pixelWidth} {pixelHeight} valueTextColor={warningStyle.color ?? gauge.textColor} />
  {:else if gauge.category === GaugeShapeCategory.LedRing}
    <LedRingGauge {gauge} {pixelWidth} {pixelHeight} valueTextColor={warningStyle.color ?? gauge.textColor} />
  {:else if gauge.category === GaugeShapeCategory.MultiRing}
    <MultiRingGauge {gauge} {pixelWidth} {pixelHeight} valueTextColor={warningStyle.color ?? gauge.textColor} />
  {:else if gauge.category === GaugeShapeCategory.ShiftLight}
    <ShiftLightGauge {gauge} {pixelWidth} {pixelHeight} {preview} />
  {:else}
    <ArcGauge {gauge} {pixelWidth} {pixelHeight} valueTextColor={warningStyle.color ?? gauge.textColor} />
  {/if}

  {#if showLevelName && (!belowAreaThreshold || cvdReduced)}
    <div
      class="absolute top-1 left-1 z-10 max-w-[60%] truncate rounded px-1.5 py-0.5 text-[9px] font-bold"
      class:scale-90={cvdReduced}
      style="background: {warningStyle.color}22; color: {warningStyle.color}; border: 1px solid {warningStyle.color}55;"
      title={gauge.warningLevelName ?? ''}
    >
      {gauge.warningLevelName}
    </div>
  {/if}

  {#if gauge.warningState !== 'none'}
    <div class="absolute top-1 right-1 z-10 flex items-center justify-center rounded-full"
         style="background: {warningStyle.color}; width: 20px; height: 20px; box-shadow: 0 0 6px {warningStyle.color}80;">
      <IconAlertTriangle size={14} color="#fff" />
    </div>
  {/if}
</div>

<style>
  @keyframes pulse-level {
    0%, 100% {
      box-shadow: 0 0 12px var(--pulse-color, rgba(245,158,11,0.3)), inset 0 0 20px rgba(245,158,11,0.05);
    }
    50% {
      box-shadow: 0 0 4px rgba(245,158,11,0.15), inset 0 0 10px rgba(245,158,11,0.02);
    }
  }
  .gauge-card-wrap {
    transition: border-color 0.2s ease, background 0.2s ease, box-shadow 0.2s ease;
  }
</style>

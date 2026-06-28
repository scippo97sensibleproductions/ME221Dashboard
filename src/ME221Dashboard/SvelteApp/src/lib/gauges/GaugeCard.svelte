<script lang="ts">
  import type { GaugeDefinition } from './types';
  import { GaugeShapeCategory } from './types';
  import ArcGauge from './ArcGauge.svelte';
  import BarGauge from './BarGauge.svelte';
  import TextGauge from './TextGauge.svelte';
  import DigitalGauge from './DigitalGauge.svelte';
  import ChartGauge from './ChartGauge.svelte';

  let { gauge, pixelWidth, pixelHeight }: {
    gauge: GaugeDefinition;
    pixelWidth: number;
    pixelHeight: number;
  } = $props();
</script>

<div class="size-full relative">
  {#if gauge.category === GaugeShapeCategory.Text}
    <TextGauge {gauge} {pixelWidth} {pixelHeight} />
  {:else if gauge.category === GaugeShapeCategory.Bar}
    <BarGauge {gauge} {pixelWidth} {pixelHeight} />
  {:else if gauge.category === GaugeShapeCategory.Digital}
    <DigitalGauge {gauge} {pixelWidth} {pixelHeight} />
  {:else if gauge.category === GaugeShapeCategory.Chart}
    <ChartGauge {gauge} {pixelWidth} {pixelHeight} />
  {:else}
    <ArcGauge {gauge} {pixelWidth} {pixelHeight} />
  {/if}
</div>

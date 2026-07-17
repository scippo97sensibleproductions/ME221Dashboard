<script lang="ts">
  import type { GaugeDefinition } from './types';
  import { GaugeShapeCategory } from './types';
  import { IconAlertTriangle, IconAlertOctagon } from '@tabler/icons-svelte';
  import ArcGauge from './ArcGauge.svelte';
  import BarGauge from './BarGauge.svelte';
  import TextGauge from './TextGauge.svelte';
  import DigitalGauge from './DigitalGauge.svelte';
  import ChartGauge from './ChartGauge.svelte';

  let { gauge, pixelWidth, pixelHeight, valueHistory = [] }: {
    gauge: GaugeDefinition;
    pixelWidth: number;
    pixelHeight: number;
    valueHistory?: number[];
  } = $props();

  const warningStyle = $derived.by(() => {
    const ws = gauge.warningState;
    if (ws === 'critical') {
      return {
        color: '#ef4444',
        border: '2px solid #ef4444',
        bg: 'rgba(239,68,68,0.08)',
        shadow: '0 0 12px rgba(239,68,68,0.3), inset 0 0 20px rgba(239,68,68,0.05)',
        ariaLabel: `Critical: ${gauge.name} exceeded threshold`,
        valueTextColor: '#ef4444',
      };
    }
    if (ws === 'warning') {
      return {
        color: '#f59e0b',
        border: '2px solid #f59e0b',
        bg: 'rgba(245,158,11,0.08)',
        shadow: '0 0 12px rgba(245,158,11,0.3), inset 0 0 20px rgba(245,158,11,0.05)',
        ariaLabel: `Warning: ${gauge.name} outside range`,
        valueTextColor: '#f59e0b',
      };
    }
    return {
      color: null,
      border: 'none',
      bg: 'transparent',
      shadow: 'none',
      ariaLabel: null,
      valueTextColor: gauge.textColor,
    };
  });
</script>

<div
  class="size-full relative gauge-card-wrap"
  class:pulse-warning={gauge.warningState === 'warning'}
  class:pulse-critical={gauge.warningState === 'critical'}
  style:border={warningStyle.border}
  style:border-radius="0.375rem"
  style:background={warningStyle.bg}
  style:box-shadow={warningStyle.shadow}
  role={warningStyle.ariaLabel ? 'alert' : undefined}
  aria-label={warningStyle.ariaLabel ?? undefined}
>
  {#if gauge.category === GaugeShapeCategory.Text}
    <TextGauge {gauge} {pixelWidth} {pixelHeight} valueTextColor={warningStyle.valueTextColor} />
  {:else if gauge.category === GaugeShapeCategory.Bar}
    <BarGauge {gauge} {pixelWidth} {pixelHeight} valueTextColor={warningStyle.valueTextColor} />
  {:else if gauge.category === GaugeShapeCategory.Digital}
    <DigitalGauge {gauge} {pixelWidth} {pixelHeight} valueTextColor={warningStyle.valueTextColor} valueHistory={gauge.showHistogram ? valueHistory : []} />
  {:else if gauge.category === GaugeShapeCategory.Chart}
    <ChartGauge {gauge} {pixelWidth} {pixelHeight} />
  {:else}
    <ArcGauge {gauge} {pixelWidth} {pixelHeight} valueTextColor={warningStyle.valueTextColor} />
  {/if}

  {#if gauge.warningState !== 'none'}
    <div class="absolute top-1 right-1 z-10 flex items-center justify-center rounded-full"
         style="background: {warningStyle.color}; width: 20px; height: 20px; box-shadow: 0 0 6px {warningStyle.color}80;">
      {#if gauge.warningState === 'critical'}
        <IconAlertOctagon size={14} color="#fff" />
      {:else}
        <IconAlertTriangle size={14} color="#fff" />
      {/if}
    </div>
  {/if}
</div>

<style>
  @keyframes pulse-warn {
    0%, 100% {
      border-color: #f59e0b;
      box-shadow: 0 0 12px rgba(245,158,11,0.3), inset 0 0 20px rgba(245,158,11,0.05);
    }
    50% {
      border-color: #f59e0b88;
      box-shadow: 0 0 4px rgba(245,158,11,0.15), inset 0 0 10px rgba(245,158,11,0.02);
    }
  }
  @keyframes pulse-crit {
    0%, 100% {
      border-color: #ef4444;
      box-shadow: 0 0 16px rgba(239,68,68,0.4), inset 0 0 24px rgba(239,68,68,0.08);
    }
    50% {
      border-color: #ef444488;
      box-shadow: 0 0 6px rgba(239,68,68,0.2), inset 0 0 12px rgba(239,68,68,0.03);
    }
  }
  .pulse-warning {
    animation: pulse-warn 1.2s ease-in-out infinite;
  }
  .pulse-critical {
    animation: pulse-crit 0.8s ease-in-out infinite;
  }
  .gauge-card-wrap {
    transition: border-color 0.2s ease, background 0.2s ease, box-shadow 0.2s ease;
  }
</style>

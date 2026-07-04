<script lang="ts">
  import type { ColorScheme } from './types';
  import { heatColor } from './types';

  let { data, rows, cols, minVal, maxVal, colorScheme = 'thermal' }: {
    data: number[];
    rows: number;
    cols: number;
    minVal: number;
    maxVal: number;
    colorScheme?: ColorScheme;
  } = $props();

  let canvasW = 80;
  let canvasH = 50;
  let cellW = $derived(canvasW / cols);
  let cellH = $derived(canvasH / rows);
  let is1D = $derived(rows === 1);

  let sparklinePath = $derived.by(() => {
    if (!is1D || data.length === 0) return '';
    const range = maxVal - minVal || 1;
    const points = data.map((v, i) => {
      const x = (i / (data.length - 1)) * canvasW;
      const y = canvasH - ((v - minVal) / range) * (canvasH - 4) - 2;
      return `${x},${y}`;
    });
    return `M${points.join('L')}`;
  });

  let sparklineArea = $derived.by(() => {
    if (!is1D || data.length === 0) return '';
    const range = maxVal - minVal || 1;
    const points = data.map((v, i) => {
      const x = (i / (data.length - 1)) * canvasW;
      const y = canvasH - ((v - minVal) / range) * (canvasH - 4) - 2;
      return `${x},${y}`;
    });
    return `M0,${canvasH}L${points.join('L')}L${canvasW},${canvasH}Z`;
  });
</script>

<div
  class="overflow-hidden"
  style="width: {canvasW}px; height: {canvasH}px; border: 1px solid var(--metro-border); background-color: var(--metro-card);"
>
  {#if is1D}
    <svg width={canvasW} height={canvasH} viewBox="0 0 {canvasW} {canvasH}">
      <defs>
        <linearGradient id="spark-fill" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stop-color="rgb(251,191,36)" stop-opacity="0.3" />
          <stop offset="100%" stop-color="rgb(251,191,36)" stop-opacity="0.05" />
        </linearGradient>
      </defs>
      <path d={sparklineArea} fill="url(#spark-fill)" />
      <path d={sparklinePath} fill="none" stroke="rgb(251,191,36)" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" />
    </svg>
  {:else}
    <svg width={canvasW} height={canvasH} viewBox="0 0 {canvasW} {canvasH}">
      {#each data as val, i (i)}
        {@const r = Math.floor(i / cols)}
        {@const c = i % cols}
        <rect
          x={c * cellW}
          y={r * cellH}
          width={cellW + 0.5}
          height={cellH + 0.5}
          fill={heatColor(val, minVal, maxVal, colorScheme)}
        />
      {/each}
    </svg>
  {/if}
</div>

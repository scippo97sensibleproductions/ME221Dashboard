<script lang="ts">
  let { name, value, unit, highlight = false }: {
    name: string;
    value: number | null;
    unit: string;
    highlight?: boolean;
  } = $props();

  let displayValue = $derived(value !== null ? value.toFixed(1) : '---');
</script>

<div
  class="flex items-center justify-between border-b px-3 py-1.5 transition-colors duration-150"
  style="border-color: var(--metro-border); {highlight ? 'background-color: rgba(216,59,1,0.08);' : ''}"
>
  <span class="truncate text-[11px] font-medium" style="color: var(--metro-text-secondary);">{name}</span>
  <div class="flex items-center gap-1.5">
    <span class="font-mono text-[13px] font-bold tabular-nums" style="color: {highlight ? 'var(--metro-orange)' : 'var(--metro-text-primary)'};">
      {displayValue}
    </span>
    <span class="text-[10px]" style="color: var(--metro-text-muted);">{unit}</span>
  </div>
</div>

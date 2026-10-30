<script lang="ts">
  // Metro checkbox row: sharp square, solid accent when checked.
  let { label, checked = $bindable(), hint }: {
    label: string;
    checked?: boolean;
    hint?: string;
  } = $props();
</script>

<label class="flex items-start gap-2">
  <input type="checkbox" bind:checked class="mt-0.5 h-4 w-4 shrink-0" style="accent-color: var(--metro-blue);" />
  <span class="text-[12px] leading-snug" style="color: var(--metro-text)">
    {label}
    {#if hint}
      <span class="block text-[10px]" style="color: var(--metro-text-muted)">{hint}</span>
    {/if}
  </span>
</label>

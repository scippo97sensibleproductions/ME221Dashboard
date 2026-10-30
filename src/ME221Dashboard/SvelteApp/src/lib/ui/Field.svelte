<script lang="ts">
  import type { Snippet } from 'svelte';

  let { label, hint, children }: {
    label: string;
    /** Optional helper line under the control. */
    hint?: string;
    children: Snippet;
  } = $props();
</script>

<label class="block space-y-1">
  <span class="text-[10px] font-bold uppercase tracking-wider" style="color: var(--metro-text-secondary)">{label}</span>
  {@render children()}
  {#if hint}
    <span class="block text-[10px] leading-relaxed" style="color: var(--metro-text-muted)">{hint}</span>
  {/if}
</label>

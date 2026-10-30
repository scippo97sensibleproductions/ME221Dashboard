<script lang="ts">
  import Field from './Field.svelte';

  // Bound as text so a half-typed value ("4.") survives the way a raw input
  // does; the owner parses it when saving.
  let { label, value = $bindable(''), step, min, max, hint }: {
    label: string;
    value?: string;
    step?: number | string;
    min?: number | string;
    max?: number | string;
    hint?: string;
  } = $props();
</script>

<Field {label} {hint}>
  <input type="number" class="metro-input w-full font-mono" bind:value {step} {min} {max} />
</Field>

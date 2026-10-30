<script lang="ts" generics="T extends string">
  import Field from './Field.svelte';

  let { label, value = $bindable(), options, hint }: {
    label: string;
    value?: T;
    options: readonly { value: T; label: string }[];
    hint?: string;
  } = $props();
</script>

<Field {label} {hint}>
  <select
    class="metro-input w-full"
    value={value}
    onchange={(e) => { value = (e.currentTarget as HTMLSelectElement).value as T; }}
  >
    {#each options as option (option.value)}
      <option value={option.value}>{option.label}</option>
    {/each}
  </select>
</Field>

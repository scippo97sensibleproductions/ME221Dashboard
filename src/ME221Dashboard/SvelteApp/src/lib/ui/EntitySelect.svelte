<script lang="ts">
  // One entity picker for every source-mapping row: the label on the left, the
  // sensor list on the right, and a "disabled" entry for the null mapping.
  let { label, value, sensors, onchange, emptyLabel = 'Auto-detect / Disabled' }: {
    label: string;
    value: number | null;
    sensors: readonly { id: number; name: string }[];
    onchange: (id: number | null) => void;
    emptyLabel?: string;
  } = $props();
</script>

<label class="flex items-center justify-between gap-3">
  <span class="shrink-0 text-[12px]" style="color: var(--metro-text)">{label}</span>
  <select
    class="metro-input w-64"
    value={value ?? ''}
    onchange={(e) => {
      const raw = (e.currentTarget as HTMLSelectElement).value;
      onchange(raw ? Number(raw) : null);
    }}
  >
    <option value="">{emptyLabel}</option>
    {#each sensors as sensor (sensor.id)}
      <option value={sensor.id}>{sensor.name} (ID {sensor.id})</option>
    {/each}
  </select>
</label>

<script lang="ts">
  import type { GaugeConfigEntry } from '../HybridBridge';
  import PositionGrid from './PositionGrid.svelte';

  let { gaugeDef, onchange }: {
    gaugeDef: GaugeConfigEntry;
    onchange: (def: GaugeConfigEntry) => void;
  } = $props();

  function set<K extends keyof GaugeConfigEntry>(key: K, value: GaugeConfigEntry[K]) {
    onchange({ ...gaugeDef, [key]: value });
  }
</script>

<div class="space-y-4">

  <!-- Text Positions -->
  <div>
    <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Text Positions</p>
    <div class="grid grid-cols-3 gap-3">
      <PositionGrid label="Value" value={gaugeDef.barValuePosition} onchange={(v) => set('barValuePosition', v)} />
      <PositionGrid label="Unit" value={gaugeDef.barUnitPosition} onchange={(v) => set('barUnitPosition', v)} />
      <PositionGrid label="Name" value={gaugeDef.barNamePosition} onchange={(v) => set('barNamePosition', v)} />
    </div>
  </div>

</div>

<script lang="ts">
  import { onMount } from 'svelte';
  import { Modal, Button } from 'flowbite-svelte';
  import { defaultDerivedConfig } from './derived/types';
  import type { VehicleConfig } from './derived/types';
  import { loadDerivedConfig, saveDerivedConfig } from './derived/vehicleConfig';
  import { autoDetectMapping } from './derived/autoDetect';
  import { DERIVED_ENTITIES, DerivedEntityId } from './derived/types';

  let { open = $bindable(true), sensors, onclose }: {
    open: boolean;
    sensors: { id: number; name: string }[];
    onclose: () => void;
  } = $props();

  let config = $state<VehicleConfig>(defaultDerivedConfig());

  let tab: 'vehicle' | 'mapping' | 'gauges' = $state('vehicle');

  let tireStr = $state('');
  let fdStr = $state('');
  let gearStrs = $state<string[]>([]);
  let slipStr = $state('');
  let enabled = $state(true);

  onMount(async () => {
    console.log('[VEHCFG] Modal onMount: loading global config');
    const loaded = await loadDerivedConfig();
    console.log('[VEHCFG] Modal loaded config:', loaded);
    config = loaded;
    enabled = loaded.enabled;
    tireStr = String(loaded.tireDiameterInches);
    fdStr = String(loaded.finalDriveRatio);
    gearStrs = loaded.gearRatios.map(String);
    slipStr = String(loaded.wheelSlipPercent);
    console.log(`[VEHCFG] Modal form fields: tire=${tireStr}, fd=${fdStr}, gears=${gearStrs}, slip=${slipStr}, enabled=${enabled}`);

    // Auto-detect if no mapping saved
    if (loaded.rpmEntityId === null && sensors.length > 0) {
      const detected = autoDetectMapping(sensors);
      if (detected.rpmEntityId !== null) config.rpmEntityId = detected.rpmEntityId;
      if (detected.vssSpeedEntityId !== null) config.vssSpeedEntityId = detected.vssSpeedEntityId;
      if (detected.mapEntityId !== null) config.mapEntityId = detected.mapEntityId;
      if (detected.baroEntityId !== null) config.baroEntityId = detected.baroEntityId;
    }
  });

  function sensorLabel(id: number | null): string {
    if (id === null) return 'None';
    const s = sensors.find(s => s.id === id);
    return s ? `${s.name} (ID ${s.id})` : `Entity ${id}`;
  }

  async function handleSave() {
    const tire = parseFloat(tireStr);
    const fd = parseFloat(fdStr);
    const ratios = gearStrs.map(s => parseFloat(s)).filter(n => !isNaN(n) && n > 0);
    const slip = parseFloat(slipStr);
    const def = defaultDerivedConfig();

    config.enabled = enabled;
    config.tireDiameterInches = isNaN(tire) || tire <= 0 ? def.tireDiameterInches : tire;
    config.finalDriveRatio = isNaN(fd) || fd <= 0 ? def.finalDriveRatio : fd;
    config.gearRatios = ratios.length > 0 ? ratios : def.gearRatios;
    config.wheelSlipPercent = isNaN(slip) ? def.wheelSlipPercent : slip;

    console.log('[VEHCFG] handleSave: about to call saveDerivedConfig with:', JSON.parse(JSON.stringify(config)));
    await saveDerivedConfig(config);
    onclose();
  }
</script>

<Modal bind:open size="lg" placement="center" outsideclose={true} class="backdrop:bg-gray-900/80" ontoggle={(e) => { if (e.newState === 'closed') onclose(); }}>
  <svelte:fragment slot="header">
    <div class="flex w-full items-center justify-between">
      <h2 class="text-base font-semibold text-gray-100">Derived Values</h2>
    </div>
  </svelte:fragment>

  <!-- Tabs -->
  <div class="-mx-5 -mt-5 mb-4 flex border-b border-gray-700 px-4">
    <button class={"px-3 py-2 text-sm border-b-2 transition-colors " + (tab === 'vehicle' ? 'border-cyan-400 text-cyan-300' : 'border-transparent text-gray-400 hover:text-gray-200')} onclick={() => tab = 'vehicle'}>Vehicle</button>
    <button class={"px-3 py-2 text-sm border-b-2 transition-colors " + (tab === 'mapping' ? 'border-cyan-400 text-cyan-300' : 'border-transparent text-gray-400 hover:text-gray-200')} onclick={() => tab = 'mapping'}>Sensor Mapping</button>
    <button class={"px-3 py-2 text-sm border-b-2 transition-colors " + (tab === 'gauges' ? 'border-cyan-400 text-cyan-300' : 'border-transparent text-gray-400 hover:text-gray-200')} onclick={() => tab = 'gauges'}>Available</button>
  </div>

  <div class="space-y-4">
    {#if tab === 'vehicle'}
      <label class="flex items-center gap-2">
        <input type="checkbox" bind:checked={enabled} class="rounded border-gray-600 bg-gray-800 text-cyan-500 focus:ring-cyan-500" />
        <span class="text-sm text-gray-300">Enable derived values</span>
      </label>
      <div class="grid grid-cols-2 gap-4">
        <label class="space-y-1">
          <span class="text-xs text-gray-400">Tire Diameter (in)</span>
          <input type="number" step="0.1" bind:value={tireStr} class="w-full rounded-lg border border-gray-600 bg-gray-800 px-3 py-2 text-sm text-gray-100 placeholder-gray-500 focus:border-cyan-500 focus:outline-none" />
        </label>
        <label class="space-y-1">
          <span class="text-xs text-gray-400">Final Drive Ratio</span>
          <input type="number" step="0.01" bind:value={fdStr} class="w-full rounded-lg border border-gray-600 bg-gray-800 px-3 py-2 text-sm text-gray-100 placeholder-gray-500 focus:border-cyan-500 focus:outline-none" />
        </label>
      </div>
      <div class="space-y-1">
        <span class="text-xs text-gray-400">Gear Ratios</span>
        <div class="grid grid-cols-3 gap-2 sm:grid-cols-6">
          {#each gearStrs as _, i}
            <label class="space-y-0.5">
              <span class="text-xs text-gray-500">{i + 1}</span>
              <input type="number" step="0.01" bind:value={gearStrs[i]} class="w-full rounded-lg border border-gray-600 bg-gray-800 px-2 py-1.5 text-sm text-gray-100 placeholder-gray-500 focus:border-cyan-500 focus:outline-none" />
            </label>
          {/each}
        </div>
      </div>
      <label class="space-y-1">
        <span class="text-xs text-gray-400">Wheel Slip (%)</span>
        <input type="number" step="0.5" bind:value={slipStr} class="w-full rounded-lg border border-gray-600 bg-gray-800 px-3 py-2 text-sm text-gray-100 placeholder-gray-500 focus:border-cyan-500 focus:outline-none" />
      </label>
      <p class="text-xs text-gray-500">Tire diameter, final drive, and gear ratios are used to predict gear and calculate true speed from RPM.</p>

    {:else if tab === 'mapping'}
      <div class="space-y-3">
        {#each [
          { label: 'RPM', key: 'rpmEntityId' as const },
          { label: 'VSS Speed', key: 'vssSpeedEntityId' as const },
          { label: 'MAP', key: 'mapEntityId' as const },
          { label: 'Barometric Pressure', key: 'baroEntityId' as const },
          { label: 'ECU Gear', key: 'gearEntityId' as const },
        ] as item}
          <div class="flex items-center justify-between gap-3">
            <span class="text-sm text-gray-300">{item.label}</span>
            <select class="w-64 rounded-lg border border-gray-600 bg-gray-800 px-3 py-1.5 text-sm text-gray-100 focus:border-cyan-500 focus:outline-none" value={config[item.key] ?? ''} onchange={(e) => { const v = (e.target as HTMLSelectElement).value; config[item.key] = v ? Number(v) : null; }}>
              <option value="">Auto-detect / Disabled</option>
              {#each sensors as s}
                <option value={s.id}>{s.name} (ID {s.id})</option>
              {/each}
            </select>
          </div>
        {/each}
        <p class="text-xs text-gray-500">
          Map each sensor to the correct entity. Entities with "Auto-detect / Disabled" use name-based matching or are skipped.
        </p>
      </div>

    {:else if tab === 'gauges'}
      <div class="space-y-2">
        <p class="text-sm text-gray-300">The following derived values are computed and available as gauges (add them from Configure):</p>
        <div class="space-y-1.5">
          {#each Object.entries(DERIVED_ENTITIES) as [idStr, info]}
            <div class="flex items-center justify-between rounded-lg bg-gray-800/50 px-3 py-2">
              <span class="text-sm text-gray-200">{info.name}</span>
              <span class="text-xs text-gray-500">{info.unit || '—'}</span>
            </div>
          {/each}
        </div>
        <p class="text-xs text-gray-500">Gear prediction requires RPM + VSS or GPS Speed. True speed requires RPM + gear. Boost requires MAP.</p>
      </div>
    {/if}
  </div>

  <svelte:fragment slot="footer">
    {#if tab !== 'gauges'}
      <div class="flex w-full justify-end gap-2">
        <Button color="alternative" class="!border-gray-600 !bg-gray-700 !text-gray-300 hover:!bg-gray-600" onclick={onclose}>Cancel</Button>
        <Button class="!bg-cyan-600 hover:!bg-cyan-500 !text-white border-cyan-600" onclick={handleSave}>Save</Button>
      </div>
    {/if}
  </svelte:fragment>
</Modal>

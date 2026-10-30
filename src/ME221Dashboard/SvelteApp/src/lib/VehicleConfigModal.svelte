<script lang="ts">
  import { onMount } from 'svelte';
  import {
    DERIVED_ENTITIES,
    FUEL_EFFICIENCY_UNITS,
    FUEL_SPEED_SOURCE_LABELS,
    defaultDerivedConfig,
    DerivedEntityId,
  } from './derived/types';
  import type { FuelEfficiencyUnit, FuelSpeedSource, RtcConfig, VehicleConfig } from './derived/types';
  import { loadDerivedConfig, saveDerivedConfig } from './derived/vehicleConfig';
  import { autoDetectMapping } from './derived/autoDetect';
  import { fuelTripStore } from './derived/fuelTripStore.svelte';
  import NumberField from './ui/NumberField.svelte';
  import SelectField from './ui/SelectField.svelte';
  import CheckField from './ui/CheckField.svelte';
  import EntitySelect from './ui/EntitySelect.svelte';
  import MetroModal from './ui/MetroModal.svelte';

  let { open = $bindable(true), sensors, onclose }: {
    open: boolean;
    sensors: { id: number; name: string }[];
    onclose: () => void;
  } = $props();

  const TABS = [
    { id: 'vehicle', label: 'Vehicle' },
    { id: 'fuel', label: 'Fuel' },
    { id: 'clock', label: 'Clock' },
    { id: 'mapping', label: 'Mapping' },
    { id: 'available', label: 'Available' },
  ] as const;

  type TabId = (typeof TABS)[number]['id'];
  type MappingKey =
    | 'rpmEntityId'
    | 'vssSpeedEntityId'
    | 'mapEntityId'
    | 'baroEntityId'
    | 'gearEntityId'
    | 'fuelFlowEntityId'
    | 'injectorDutyEntityId';
  type RtcKey = Exclude<keyof RtcConfig, 'hour12' | 'showSeconds'>;

  const MAPPING_ROWS: readonly { key: MappingKey; label: string }[] = [
    { key: 'rpmEntityId', label: 'RPM' },
    { key: 'vssSpeedEntityId', label: 'VSS Speed' },
    { key: 'mapEntityId', label: 'MAP' },
    { key: 'baroEntityId', label: 'Barometric Pressure' },
    { key: 'gearEntityId', label: 'ECU Gear' },
  ];

  const FUEL_SOURCE_ROWS: readonly { key: MappingKey; label: string }[] = [
    { key: 'fuelFlowEntityId', label: 'Fuel Flow (L/hr)' },
    { key: 'injectorDutyEntityId', label: 'Injector Duty (%)' },
  ];

  const RTC_ROWS: readonly { key: RtcKey; label: string }[] = [
    { key: 'statusEntityId', label: 'Status' },
    { key: 'hourEntityId', label: 'Hour' },
    { key: 'minuteEntityId', label: 'Minute' },
    { key: 'secondEntityId', label: 'Second' },
    { key: 'dayEntityId', label: 'Day' },
    { key: 'monthEntityId', label: 'Month' },
    { key: 'yearEntityId', label: 'Year' },
  ];

  const UNIT_OPTIONS = (Object.entries(FUEL_EFFICIENCY_UNITS) as [FuelEfficiencyUnit, string][])
    .map(([value, label]) => ({ value, label }));
  const SPEED_SOURCE_OPTIONS = (Object.entries(FUEL_SPEED_SOURCE_LABELS) as [FuelSpeedSource, string][])
    .map(([value, label]) => ({ value, label }));

  let config = $state<VehicleConfig>(defaultDerivedConfig());
  let tab = $state<TabId>('vehicle');

  let tireStr = $state('');
  let fdStr = $state('');
  let gearStrs = $state<string[]>([]);
  let slipStr = $state('');
  let enabled = $state(true);
  let injectorFlowStr = $state('');
  let cylinderStr = $state('');
  let fuelUnit = $state<FuelEfficiencyUnit>('kmL');
  let fuelSpeedSource = $state<FuelSpeedSource>('auto');

  // Auto-detect tracking (R8): an auto-detect-applied save carries an explicit
  // payload flag so the seed template gate excludes it; a manual mapping edit
  // marks the save as deliberate (seed refresh allowed).
  let autoDetectApplied = $state(false);
  let mappingTouched = $state(false);

  const tripAverage = $derived(fuelTripStore.average(fuelUnit));
  const tripDistance = $derived(fuelTripStore.distanceKm);
  const tripFuel = $derived(fuelTripStore.fuelLiters);

  function mappingValue(key: MappingKey): number | null {
    return config[key];
  }

  function setMapping(key: MappingKey, id: number | null) {
    config[key] = id;
    mappingTouched = true;
  }

  function rtcValue(key: RtcKey): number | null {
    return config.rtc[key];
  }

  function setRtc(key: RtcKey, id: number | null) {
    config.rtc[key] = id;
  }

  /** Unit shown for a derived entity in the "Available" list. */
  function derivedUnit(id: number, fallback: string): string {
    return id === DerivedEntityId.FuelEfficiency || id === DerivedEntityId.AverageFuelEfficiency
      ? FUEL_EFFICIENCY_UNITS[fuelUnit]
      : fallback;
  }

  async function resetTrip() {
    await fuelTripStore.reset();
  }

  onMount(async () => {
    const loaded = await loadDerivedConfig();
    config = loaded;
    enabled = loaded.enabled;
    tireStr = String(loaded.tireDiameterInches);
    fdStr = String(loaded.finalDriveRatio);
    gearStrs = loaded.gearRatios.map(String);
    slipStr = String(loaded.wheelSlipPercent);
    injectorFlowStr = String(loaded.injectorFlowCcPerMin);
    cylinderStr = String(loaded.cylinderCount);
    fuelUnit = loaded.fuelEfficiencyUnit;
    fuelSpeedSource = loaded.fuelSpeedSource;

    // Auto-detect if no mapping saved
    if (loaded.rpmEntityId === null && sensors.length > 0) {
      const detected = autoDetectMapping(sensors);
      if (detected.rpmEntityId !== null) config.rpmEntityId = detected.rpmEntityId;
      if (detected.vssSpeedEntityId !== null) config.vssSpeedEntityId = detected.vssSpeedEntityId;
      if (detected.mapEntityId !== null) config.mapEntityId = detected.mapEntityId;
      if (detected.baroEntityId !== null) config.baroEntityId = detected.baroEntityId;
      if (detected.fuelFlowEntityId !== null) config.fuelFlowEntityId = detected.fuelFlowEntityId;
      if (detected.injectorDutyEntityId !== null) config.injectorDutyEntityId = detected.injectorDutyEntityId;
      config.rtc = { ...config.rtc, ...detected.rtc };
      autoDetectApplied = true;
    }
  });

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

    const injectorFlow = parseFloat(injectorFlowStr);
    const cylinders = parseInt(cylinderStr, 10);
    config.injectorFlowCcPerMin = isNaN(injectorFlow) || injectorFlow < 0 ? 0 : injectorFlow;
    config.cylinderCount = isNaN(cylinders) || cylinders < 1 ? def.cylinderCount : cylinders;
    config.fuelEfficiencyUnit = fuelUnit;
    config.fuelSpeedSource = fuelSpeedSource;

    // Vehicle-only save: strip the shifter block (this modal never edits shifter
    // settings) so it cannot zero or overwrite the shifter block on either the
    // C# side or the TS cache (U1 partial-payload contract).
    const { shifter: _shifter, ...vehicleOnly } = config;
    void _shifter;

    // Auto-detect writes carry the flag so the seed gate excludes them (R8);
    // manual mapping edits make the save deliberate (seed refresh allowed).
    const payload = {
      ...vehicleOnly,
      autoDetect: autoDetectApplied && !mappingTouched,
    };

    await saveDerivedConfig(payload);
    onclose();
  }
</script>

<MetroModal bind:open title="Derived Values" size="lg" onclose={onclose}>
  <!-- Tabs -->
  <div class="-mx-5 -mt-5 mb-4 flex px-4" style="border-bottom: 1px solid var(--metro-border);">
    {#each TABS as item (item.id)}
      <button
        class="px-3 py-2 text-[10px] font-bold uppercase tracking-wider transition-colors"
        style="border-bottom: 2px solid {tab === item.id ? 'var(--metro-blue)' : 'transparent'}; color: {tab === item.id ? 'var(--metro-text)' : 'var(--metro-text-secondary)'};"
        onclick={() => tab = item.id}
      >{item.label}</button>
    {/each}
  </div>

  <div class="space-y-4">
    {#if tab === 'vehicle'}
      <CheckField label="Enable derived values" bind:checked={enabled} />
      <div class="grid grid-cols-2 gap-4">
        <NumberField label="Tire Diameter (in)" step="0.1" bind:value={tireStr} />
        <NumberField label="Final Drive Ratio" step="0.01" bind:value={fdStr} />
      </div>
      <div class="space-y-1">
        <div class="flex items-center justify-between">
          <span class="text-[10px] font-bold uppercase tracking-wider" style="color: var(--metro-text-secondary)">Gear Ratios</span>
          <div class="flex gap-1">
            <button type="button" class="px-2 py-1 text-[10px] font-bold uppercase tracking-wider" style="border: 1px solid var(--metro-input-border); color: var(--metro-text-secondary);" onclick={() => gearStrs = [...gearStrs, '1.0']}>+ Add Gear</button>
            {#if gearStrs.length > 1}
              <button type="button" class="px-2 py-1 text-[10px] font-bold uppercase tracking-wider" style="border: 1px solid var(--metro-input-border); color: var(--metro-text-secondary);" onclick={() => gearStrs = gearStrs.slice(0, -1)}>− Remove</button>
            {/if}
          </div>
        </div>
        <div class="grid grid-cols-3 gap-2 sm:grid-cols-6">
          {#each gearStrs as _, i (i)}
            <label class="space-y-0.5">
              <span class="text-[10px] font-bold uppercase tracking-wider" style="color: var(--metro-text-muted)">{i + 1}</span>
              <input type="number" step="0.01" bind:value={gearStrs[i]} class="metro-input w-full font-mono" />
            </label>
          {/each}
        </div>
      </div>
      <NumberField label="Wheel Slip (%)" step="0.5" bind:value={slipStr} />
      <p class="text-[11px] leading-relaxed" style="color: var(--metro-text-muted)">Tire diameter, final drive, and gear ratios are used to predict gear and calculate true speed from RPM.</p>

    {:else if tab === 'fuel'}
      <div class="grid grid-cols-2 gap-4">
        <SelectField label="Output Unit" bind:value={fuelUnit} options={UNIT_OPTIONS} />
        <SelectField label="Speed Source" bind:value={fuelSpeedSource} options={SPEED_SOURCE_OPTIONS} />
      </div>
      <div class="space-y-2">
        {#each FUEL_SOURCE_ROWS as row (row.key)}
          <EntitySelect label={row.label} value={mappingValue(row.key)} {sensors} onchange={(id) => setMapping(row.key, id)} />
        {/each}
      </div>
      <div class="grid grid-cols-2 gap-4">
        <NumberField label="Injector Flow (cc/min)" step="5" bind:value={injectorFlowStr} />
        <NumberField label="Cylinders" step="1" min="1" bind:value={cylinderStr} />
      </div>
      <div class="flex items-center justify-between gap-3 px-3 py-2" style="background-color: var(--metro-surface);">
        <div class="flex flex-col gap-0.5">
          <span class="text-[10px] font-bold uppercase tracking-wider" style="color: var(--metro-text-muted)">Trip Average</span>
          <span class="font-mono text-[16px] font-bold" style="color: var(--metro-text)">
            {tripAverage === null ? '---' : tripAverage.toFixed(1)} {FUEL_EFFICIENCY_UNITS[fuelUnit]}
          </span>
          <span class="text-[10px]" style="color: var(--metro-text-muted)">{tripDistance.toFixed(1)} km · {tripFuel.toFixed(2)} L</span>
        </div>
        <button type="button" class="metro-btn-secondary" onclick={resetTrip}>Reset</button>
      </div>
      <p class="text-[11px] leading-relaxed" style="color: var(--metro-text-muted)">
        Fuel flow is used directly when mapped; otherwise injector duty × injector flow × cylinders gives the flow. The trip average integrates fuel and distance, so idling and stops count against it. True Speed keeps the figure working without a speed sensor.
      </p>

    {:else if tab === 'clock'}
      <div class="flex gap-6">
        <CheckField label="12-hour clock" bind:checked={config.rtc.hour12} />
        <CheckField label="Show seconds" bind:checked={config.rtc.showSeconds} />
      </div>
      <div class="space-y-2">
        {#each RTC_ROWS as row (row.key)}
          <EntitySelect label={row.label} value={rtcValue(row.key)} {sensors} onchange={(id) => setRtc(row.key, id)} />
        {/each}
      </div>
      <p class="text-[11px] leading-relaxed" style="color: var(--metro-text-muted)">
        The firmware reports the clock as separate channels. Map them once and the ECU Clock and ECU Date gauges become available. Status gates the clock: while the firmware reports “Err - Not Init” the gauge shows “---” instead of a plausible-looking 00:00.
      </p>

    {:else if tab === 'mapping'}
      <div class="space-y-2">
        {#each MAPPING_ROWS as row (row.key)}
          <EntitySelect label={row.label} value={mappingValue(row.key)} {sensors} onchange={(id) => setMapping(row.key, id)} />
        {/each}
      </div>
      <p class="text-[11px] leading-relaxed" style="color: var(--metro-text-muted)">
        Map each sensor to the correct entity. “Auto-detect / Disabled” uses name-based matching or skips the source.
      </p>

    {:else if tab === 'available'}
      <div class="space-y-2">
        <p class="text-[12px]" style="color: var(--metro-text)">Computed values available as gauges (add them from Configure):</p>
        {#each Object.entries(DERIVED_ENTITIES) as [idStr, info] (idStr)}
          <div class="flex items-center justify-between px-3 py-2" style="background-color: var(--metro-surface); border-left: 2px solid var(--metro-blue);">
            <span class="text-[12px]" style="color: var(--metro-text)">{info.name}</span>
            <span class="text-[11px]" style="color: var(--metro-text-muted)">{derivedUnit(Number(idStr), info.unit)}</span>
          </div>
        {/each}
        <p class="text-[11px] leading-relaxed" style="color: var(--metro-text-muted)">Gear prediction requires RPM + VSS or GPS Speed. True speed requires RPM + gear. Boost requires MAP. Fuel efficiency requires a fuel source plus speed; the average also needs a distance run. Clock and date require the RTC channels.</p>
      </div>
    {/if}
  </div>

  {#snippet footer()}
    {#if tab !== 'available'}
      <button type="button" class="metro-btn-secondary" onclick={onclose}>Cancel</button>
      <button type="button" class="metro-btn-primary" onclick={handleSave}>Save</button>
    {/if}
  {/snippet}
</MetroModal>

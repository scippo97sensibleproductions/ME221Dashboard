<script lang="ts">
  import { onMount, untrack } from 'svelte';
  import type { GaugeConfigEntry } from '../HybridBridgeTypes';
  import { HybridBridge } from '../HybridBridge';
  import { SHIFT_ZONE_COLORS, DEFAULT_SEGMENT_COUNT, clampSegmentCount, clampZoneCount } from './shiftLightRender';
  import SegmentGeometrySettings from './SegmentGeometrySettings.svelte';
  import ClampNotice from './ClampNotice.svelte';
  import { SHIFTER_COPY, formatShifterCopy } from '../shift/shifterConfig';

  let { gaugeDef, onchange }: {
    gaugeDef: GaugeConfigEntry;
    onchange: (def: GaugeConfigEntry) => void;
  } = $props();

  function set<K extends keyof GaugeConfigEntry>(key: K, value: GaugeConfigEntry[K]) {
    onchange({ ...gaugeDef, [key]: value });
  }

  // Per-dashboard shift-point bound for the ramp clamp (U5): loaded on mount.
  let shiftPointBound = $state<number | null>(null);
  let boundLoading = $state(true);

  onMount(() => {
    HybridBridge.getVehicleConfig().then(vc => {
      shiftPointBound = vc.shifter?.shiftPointRpm ?? 0;
      boundLoading = false;
    }).catch(() => {
      boundLoading = false;
    });
  });

  // Session-frozen snapshot of the ramp at open (the clamp baseline captures
  // the initial gaugeDef — never re-read reactively).
  const rampAtMount = untrack(() => gaugeDef.rampWidthRpm ?? 1500);
  let rampStr = $state(String(rampAtMount));
  let clampedNotice = $state<string | null>(null);

  function clampRamp(ramp: number, shiftPoint: number): number {
    return Math.max(0, Math.min(ramp, shiftPoint));
  }

  // Ramp clamp on the field's commit (AE16): the shift point bound must be set
  // and loaded; otherwise the clamp is skipped and the value passes through.
  function commitRamp() {
    const parsed = parseFloat(rampStr);
    if (isNaN(parsed) || parsed < 0) {
      rampStr = String(gaugeDef.rampWidthRpm ?? 1500);
      return;
    }
    if (shiftPointBound != null && shiftPointBound > 0 && !boundLoading) {
      const clamped = clampRamp(parsed, shiftPointBound);
      if (clamped !== parsed) {
        set('rampWidthRpm', clamped);
        rampStr = String(clamped);
        // U5-owned ramp-clamp copy, canonical in SHIFTER_COPY (copy-parity test).
        clampedNotice = formatShifterCopy(SHIFTER_COPY.rampClamped, clamped);
        return;
      }
    }
    set('rampWidthRpm', parsed);
    clampedNotice = null;
  }

  function clearNotice() {
    clampedNotice = null;
  }

  const zoneCount = $derived(clampZoneCount(gaugeDef.zoneCount));
  const zonePreviewColors = $derived(
    zoneCount === 1 ? [SHIFT_ZONE_COLORS.red]
      : zoneCount === 2 ? [SHIFT_ZONE_COLORS.green, SHIFT_ZONE_COLORS.red]
        : [SHIFT_ZONE_COLORS.green, SHIFT_ZONE_COLORS.amber, SHIFT_ZONE_COLORS.red]
  );
</script>

<div class="space-y-4">
  <!-- Ramp width -->
  <div class="border-b border-gray-700/30 pb-4">
    <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Ramp Width</p>
    <div class="flex items-center gap-2">
      <input
        type="number"
        step="50"
        min="0"
        bind:value={rampStr}
        onchange={commitRamp}
        onblur={commitRamp}
        class="w-28 rounded-lg border border-gray-600 bg-gray-800 px-2 py-1.5 text-sm text-gray-100 focus:border-cyan-500 focus:outline-none"
        aria-label="Ramp width in rpm"
      />
      <span class="text-xs text-gray-500">rpm before the shift point</span>
    </div>
    <p class="mt-1.5 text-[10px] text-gray-600">
      How far before the shift point the first segment lights. Clamped so the band never starts below zero.
    </p>
    {#if clampedNotice}
      <div class="mt-2">
        <ClampNotice message={clampedNotice} onDismiss={clearNotice} />
      </div>
    {/if}
  </div>

  <!-- Segments -->
  <div class="border-b border-gray-700/30 pb-4">
    <SegmentGeometrySettings
      segmentCount={clampSegmentCount(gaugeDef.segmentCount ?? DEFAULT_SEGMENT_COUNT)}
      gap={0}
      showGap={false}
      presets={[3, 4, 6, 8, 12, 16, 24, 32, 48]}
      onchange={(p) => set('segmentCount', p.segmentCount)}
    />
  </div>

  <!-- Color zones -->
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Color Zones</p>
      <span class="text-xs font-mono text-cyan-400">{zoneCount}</span>
    </div>
    <div class="flex gap-1">
      {#each [1, 2, 3] as z (z)}
        <button
          class="flex-1 rounded px-1.5 py-1.5 text-[10px] font-medium transition-colors min-h-[28px]
            {zoneCount === z
              ? 'bg-cyan-600 text-white'
              : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
          onclick={() => set('zoneCount', z)}
        >{z}</button>
      {/each}
    </div>
    <div class="mt-2 flex h-2.5 gap-[2px] rounded-full overflow-hidden" role="img" aria-label="Zone color preview">
      {#each zonePreviewColors as c (c)}
        <div class="flex-1" style="background: {c};"></div>
      {/each}
    </div>
    <p class="mt-1.5 text-[10px] text-gray-600">
      Zones split the bar left to right; 3 = green/amber/red, 2 = green/red, 1 = red.
    </p>
  </div>
</div>

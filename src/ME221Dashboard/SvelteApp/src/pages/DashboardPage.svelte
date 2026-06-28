<script lang="ts">
  import { onMount, onDestroy, untrack } from 'svelte';
  import GaugeCard from '../lib/gauges/GaugeCard.svelte';
  import GaugeSettingsModal from '../lib/GaugeSettingsModal.svelte';
  import { HybridBridge, type BridgeEvent, type GaugeConfigEntry, type EntityInfo, type GpsLocation } from '../lib/HybridBridge';
  import { GaugeShapeCategory, toGaugeDefinition, formatValue, toSavePayload, estimateVisualSize } from '../lib/gauges/types';
  import type { GaugeDefinition } from '../lib/gauges/types';
  import { loadDerivedConfig } from '../lib/derived/vehicleConfig';
  import { computeDerived, type ComputationInputs } from '../lib/derived/compute';
  import { DERIVED_ENTITIES } from '../lib/derived/types';

  let { dashboardName, onNavigate, gpsLocation }: {
    dashboardName: string;
    onNavigate: (page: string) => void;
    gpsLocation: GpsLocation | null;
  } = $props();

  let containerEl = $state<HTMLDivElement | null>(null);
  let containerWidth = $state(800);
  let containerHeight = $state(600);
  let gaugeDefs = $state<GaugeConfigEntry[]>([]);
  let entityLookup = $state<Record<string, EntityInfo>>({});
  let entityValues = $state<Record<string, number | null>>({});
  let dragging = $state<{
    entityId: number;
    startFracX: number;
    startFracY: number;
    deltaFracX: number;
    deltaFracY: number;
    startClientX: number;
    startClientY: number;
  } | null>(null);
  let loaded = $state(false);
  let loadError = $state<string | null>(null);
  let gridRows = $state(4);
  let gridColumns = $state(7);
  let backgroundImageDataUrl = $state<string | null>(null);

  // GPS entity IDs (must match C# HybridBridgeService constants)
  const GPS_SPEED = -1001;
  const GPS_LAT = -1002;
  const GPS_LON = -1003;
  const GPS_ALT = -1004;
  const GPS_COURSE = -1005;
  const GPS_ACC = -1006;
  const ODOMETER = -2001;

  // Inject GPS values into entityValues when GPS updates.
  // Also update gaugeStates in place for zero-GC rendering.
  $effect(() => {
    if (gpsLocation) {
      const gpsValues: Record<number, number | null> = {
        [GPS_SPEED]: gpsLocation.speed != null ? gpsLocation.speed * 3.6 : null,
        [GPS_LAT]: gpsLocation.latitude,
        [GPS_LON]: gpsLocation.longitude,
        [GPS_ALT]: gpsLocation.altitude ?? null,
        [GPS_COURSE]: gpsLocation.course ?? null,
        [GPS_ACC]: gpsLocation.accuracy ?? null,
      };
      for (const [idStr, val] of Object.entries(gpsValues)) {
        entityValues[idStr] = val;
        updateGaugeValue(Number(idStr), val);
      }
      computeAndInjectDerived();
    }
  });

  // Inject GPS entity names into entityLookup on first GPS update
  $effect(() => {
    if (gpsLocation && !entityLookup[String(GPS_SPEED)]) {
      entityLookup = {
        ...entityLookup,
        [GPS_SPEED]: { name: 'GPS Speed', unit: 'km/h', minValue: 0, maxValue: 240 },
        [GPS_LAT]: { name: 'GPS Latitude', unit: '°', minValue: -90, maxValue: 90 },
        [GPS_LON]: { name: 'GPS Longitude', unit: '°', minValue: -180, maxValue: 180 },
        [GPS_ALT]: { name: 'GPS Altitude', unit: 'm', minValue: 0, maxValue: 1000 },
        [GPS_COURSE]: { name: 'GPS Course', unit: '°', minValue: 0, maxValue: 360 },
        [GPS_ACC]: { name: 'GPS Accuracy', unit: 'm', minValue: 0, maxValue: 50 },
      };
    }
  });

  // Initialize odometer entity from persisted state on mount
  $effect(() => {
    HybridBridge.getOdometer().then(r => {
      entityValues[String(ODOMETER)] = r.value;
      updateGaugeValue(ODOMETER, r.value);
      if (!entityLookup[String(ODOMETER)]) {
        entityLookup[String(ODOMETER)] = { name: 'Odometer', unit: r.unit, minValue: 0, maxValue: 999999 };
      }
    });
  });

  // ── Derived value computation ──────────────────────────────────────────
  let derivedLookupInjected = false;

  function injectDerived(result: Record<string, number | null>) {
    if (!derivedLookupInjected) {
      const entries: Record<string, EntityInfo> = {};
      for (const [idStr, info] of Object.entries(DERIVED_ENTITIES)) {
        const id = Number(idStr);
        entries[String(id)] = { name: info.name, unit: info.unit, minValue: info.minValue, maxValue: info.maxValue };
      }
      entityLookup = { ...entityLookup, ...entries };
      derivedLookupInjected = true;
    }
    for (const [idStr, value] of Object.entries(result)) {
      entityValues[idStr] = value;
      updateGaugeValue(Number(idStr), value);
    }
  }

  let _derivedTimer: ReturnType<typeof setTimeout> | null = null;
  let _lastDerivedFingerprint = '';

  async function computeAndInjectDerived() {
    if (_derivedTimer) return;
    _derivedTimer = setTimeout(async () => {
      _derivedTimer = null;
      const config = await loadDerivedConfig();
      if (!config.enabled) return;

      // Fingerprint: only recompute if inputs actually changed
      const gpsSpeed = gpsLocation?.speed != null ? gpsLocation.speed * 3.6 : null;
      const fp = `${config.rpmEntityId}:${entityValues[String(config.rpmEntityId)] ?? ''}|${config.vssSpeedEntityId}:${entityValues[String(config.vssSpeedEntityId)] ?? ''}|${config.mapEntityId}:${entityValues[String(config.mapEntityId)] ?? ''}|${config.baroEntityId}:${entityValues[String(config.baroEntityId)] ?? ''}|${config.gearEntityId}:${entityValues[String(config.gearEntityId)] ?? ''}|${gpsSpeed ?? ''}`;
      if (fp === _lastDerivedFingerprint) return;
      _lastDerivedFingerprint = fp;

      const inputs: ComputationInputs = {
        entityValues,
        config,
        gpsSpeedKmh: gpsSpeed,
        gpsValid: gpsLocation != null && gpsLocation.speed != null,
      };
      const result = computeDerived(inputs);
      injectDerived(result);
    }, 20);
  }

  // Settings modal state
  let settingsOpen = $state(false);
  let settingsTargetId = $state(-1);
  let settingsDef = $state<GaugeConfigEntry | null>(null);

  // Debounced save timer
  let saveTimer: ReturnType<typeof setTimeout> | null = null;

  const defaultGaugeName = (id: number) => `Entity ${id}`;
  const defaultGaugeUnit = (id: number) => '';

  // ── Static config — only rebuilds when gaugeDefs or entityLookup change ──
  // This is the expensive part (35-field mapping per gauge). By splitting it
  // from the value-only path, we avoid rebuilding ALL gauge objects at data rate.
  // Pre-computes entityIdStr to avoid String() allocation in the 30fps hot path.
  const staticGaugeConfigs = $derived(gaugeDefs.map(def => {
    const entInfo = entityLookup[String(def.entityId)];
    const name = entInfo?.name ?? defaultGaugeName(def.entityId);
    const unit = entInfo?.unit ?? defaultGaugeUnit(def.entityId);
    const minValue = (entInfo?.minValue ?? 0) <= (entInfo?.maxValue ?? 10000)
      ? (entInfo?.minValue ?? 0) : 0;
    const maxValue = (entInfo?.maxValue ?? 10000) > minValue
      ? (entInfo?.maxValue ?? 10000) : 10000;

    return {
      config: def,
      entityIdStr: String(def.entityId),
      name,
      unit,
      minValue,
      maxValue,
    };
  }));

  // ── GaugeDefinition cache — rebuilt only when static config changes ──
  // Avoids calling toGaugeDefinition() (which does needleCurve.slice().sort())
  // on every data frame. Only value/formattedValue are updated per frame.
  // Uses $derived.by so Svelte tracks all reactive dependencies automatically.
  const _gaugeDefCache = $derived.by(() =>
    staticGaugeConfigs.map(sc =>
      toGaugeDefinition(sc.config, {
        name: sc.name,
        unit: sc.unit,
        value: 0,
        formattedValue: '',
        minValue: sc.minValue,
        maxValue: sc.maxValue,
      })
    )
  );

  // ── Gauge state array — $state with in-place mutations for zero-GC live updates ──
  // Rebuilt only when config changes (rare). Values mutated in place at data rate (10Hz).
  // $state proxies trigger granular Svelte updates without object creation.
  let gaugeStates = $state<GaugeDefinition[]>([]);

  // Lookup: entityId → index in gaugeStates (rebuilt with gaugeStates)
  let _gaugeIndexByEntityId = new Map<number, number>();

  // Per-gauge smoothed values for EMA (exponential moving average)
  let _smoothedValues = new Map<number, number>();

  function rebuildGaugeStates() {
    const next: GaugeDefinition[] = [];
    const idxMap = new Map<number, number>();
    // Reset smoothed values on config change
    _smoothedValues = new Map<number, number>();
    for (let i = 0; i < staticGaugeConfigs.length; i++) {
      const sc = staticGaugeConfigs[i];
      const rawValue = entityValues[sc.entityIdStr];
      const value = rawValue ?? 0;
      next.push({
        ..._gaugeDefCache[i],
        value,
        formattedValue: formatValue(value, sc.name, sc.unit),
        name: sc.name,
        unit: sc.unit,
        minValue: sc.minValue,
        maxValue: sc.maxValue,
        fractionX: sc.config.fractionX,
        fractionY: sc.config.fractionY,
      });
      idxMap.set(sc.config.entityId, i);
      // Seed smoothed value with current raw value
      _smoothedValues.set(sc.config.entityId, value);
    }
    gaugeStates = next;
    _gaugeIndexByEntityId = idxMap;
  }

  // Update a single gauge's value in place — O(1), no allocation
  function updateGaugeValue(entityId: number, value: number | null) {
    const idx = _gaugeIndexByEntityId.get(entityId);
    if (idx === undefined) return;
    const g = gaugeStates[idx];
    const raw = value ?? 0;
    let v = raw;
    if (g.smoothingEnabled && g.smoothingFactor > 0 && g.smoothingFactor < 1) {
      const prev = _smoothedValues.get(entityId) ?? raw;
      v = prev + g.smoothingFactor * (raw - prev);
      _smoothedValues.set(entityId, v);
    }
    g.value = v;
    g.formattedValue = formatValue(v, g.name, g.unit);
  }

  // Rebuild gaugeStates when config or def cache changes (rare — on load, settings change).
  // untrack(rebuildGaugeStates) prevents Svelte from tracking entityValues reads inside
  // rebuildGaugeStates as dependencies — value updates are handled by updateGaugeValue() instead.
  $effect(() => {
    staticGaugeConfigs;
    _gaugeDefCache;
    untrack(rebuildGaugeStates);
  });

  function loadRealConfig() {
    HybridBridge.getDashboardConfig(dashboardName).then(async result => {
      if (result.error) {
        loadError = result.error;
        return;
      }
      gaugeDefs = result.gauges;
      gridRows = result.gridRows;
      gridColumns = result.gridColumns;
      if (result.entities) {
        entityLookup = result.entities;
      }

      // Inject GPS entity names and default ranges if GPS gauges are configured
      if (gaugeDefs.some(g => g.entityId <= -1001 && g.entityId >= -1006)) {
        entityLookup = {
          ...entityLookup,
          [GPS_SPEED]: entityLookup[String(GPS_SPEED)] ?? { name: 'GPS Speed', unit: 'km/h', minValue: 0, maxValue: 240 },
          [GPS_LAT]: entityLookup[String(GPS_LAT)] ?? { name: 'GPS Latitude', unit: '°', minValue: -90, maxValue: 90 },
          [GPS_LON]: entityLookup[String(GPS_LON)] ?? { name: 'GPS Longitude', unit: '°', minValue: -180, maxValue: 180 },
          [GPS_ALT]: entityLookup[String(GPS_ALT)] ?? { name: 'GPS Altitude', unit: 'm', minValue: 0, maxValue: 1000 },
          [GPS_COURSE]: entityLookup[String(GPS_COURSE)] ?? { name: 'GPS Course', unit: '°', minValue: 0, maxValue: 360 },
          [GPS_ACC]: entityLookup[String(GPS_ACC)] ?? { name: 'GPS Accuracy', unit: 'm', minValue: 0, maxValue: 50 },
        };
      }
      loaded = true;

      // Load background image if present
      const bgPath = result.backgroundImagePath;
      if (bgPath) {
        try {
          const img = await HybridBridge.getImageBase64(bgPath);
          if (img.success && img.dataUrl) {
            backgroundImageDataUrl = img.dataUrl;
          }
        } catch {
          // Background image not critical — ignore load failure
        }
      }
    }).catch(err => {
      loadError = String(err);
    });
  }

  let unsubBridge: () => void;
  let lastLiveDataTime = 0;

  function handleBridgeEvent(event: BridgeEvent) {
    if (event.event === 'liveDataUpdate' && event.values) {
      const now = performance.now();
      if (now - lastLiveDataTime < 33) return; // throttle to ~10Hz (ECU rate)
      lastLiveDataTime = now;

      // Per-key mutations: O(changed_keys) through existing proxy.
      for (const key in event.values) {
        entityValues[key] = event.values[key]!;
        updateGaugeValue(Number(key), event.values[key]);
      }
      computeAndInjectDerived();
    } else if (event.event === 'odometerUpdate') {
      entityValues[String(ODOMETER)] = event.odometer;
      updateGaugeValue(ODOMETER, event.odometer);
      if (entityLookup[String(ODOMETER)]) {
        entityLookup[String(ODOMETER)].unit = event.odometerUnit;
      }
    }
  }

  function handlePointerDown(e: PointerEvent, entityId: number) {
    if (!e.target || (e.target as HTMLElement).closest('input, button, a')) return;
    const def = gaugeDefs.find(d => d.entityId === entityId);
    if (!def) return;
    dragging = {
      entityId,
      startFracX: def.fractionX,
      startFracY: def.fractionY,
      deltaFracX: 0,
      deltaFracY: 0,
      startClientX: e.clientX,
      startClientY: e.clientY,
    };
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
  }

  let pendingDragDx = 0;
  let pendingDragDy = 0;
  let dragRaf: number | null = null;

  function handlePointerMove(e: PointerEvent) {
    if (!dragging) return;
    pendingDragDx = (e.clientX - dragging.startClientX) / containerWidth;
    pendingDragDy = (e.clientY - dragging.startClientY) / containerHeight;
    if (dragRaf === null) {
      dragRaf = requestAnimationFrame(() => {
        dragRaf = null;
        dragging.deltaFracX = pendingDragDx;
        dragging.deltaFracY = pendingDragDy;
      });
    }
  }

  function persistLayout() {
    const gauges = gaugeDefs.map(def => toSavePayload(def));
    HybridBridge.saveDashboardLayout(dashboardName, gauges);
  }

  function handlePointerUp() {
    if (dragging) {
      if (dragRaf !== null) {
        cancelAnimationFrame(dragRaf);
        dragRaf = null;
        dragging.deltaFracX = pendingDragDx;
        dragging.deltaFracY = pendingDragDy;
      }
      // Commit final clamped position to gaugeDefs
      const def = gaugeDefs.find(d => d.entityId === dragging.entityId);
      if (def) {
        const s = def.scale ?? 1.0;
        const pxW = containerWidth * def.widthFraction * s;
        const pxH = containerHeight * def.heightFraction * s;
        const visual = estimateVisualSize(def.shapeCategory as GaugeShapeCategory, pxW, pxH, {
          fontSizeScale: def.fontSizeScale,
          digitalStyle: def.digitalStyle,
          showValue: def.showValue,
          showUnit: def.showUnit,
          showName: def.showName,
        });
        def.fractionX = Math.max(0, Math.min(1 - visual.w / containerWidth, dragging.startFracX + dragging.deltaFracX));
        def.fractionY = Math.max(0, Math.min(1 - visual.h / containerHeight, dragging.startFracY + dragging.deltaFracY));
        gaugeDefs = gaugeDefs;
        persistLayout();
      }
    }
    dragging = null;
  }

  function handleContextMenu(e: MouseEvent, entityId: number) {
    e.preventDefault();
    const def = gaugeDefs.find(d => d.entityId === entityId);
    if (!def) return;
    settingsTargetId = entityId;
    settingsDef = { ...def };
    settingsOpen = true;
  }

  function handleSettingsChange(updated: GaugeConfigEntry) {
    settingsDef = updated;
    gaugeDefs = gaugeDefs.map(d => d.entityId === updated.entityId ? updated : d);
    if (saveTimer) clearTimeout(saveTimer);
    saveTimer = setTimeout(() => {
      persistLayout();
    }, 1500);
  }

  function handleSettingsClose() {
    settingsOpen = false;
    settingsTargetId = -1;
    settingsDef = null;
  }

  let resizeObserver: ResizeObserver | null = null;

  function observeContainer() {
    if (!containerEl) return;
    resizeObserver = new ResizeObserver(entries => {
      for (const entry of entries) {
        containerWidth = entry.contentRect.width;
        containerHeight = entry.contentRect.height;
      }
    });
    resizeObserver.observe(containerEl);
  }

  $effect(() => {
    if (containerEl && loaded && gaugeStates.length > 0) {
      observeContainer();
      return () => {
        if (resizeObserver) resizeObserver.disconnect();
      };
    }
  });

  onMount(() => {
    loadRealConfig();
    unsubBridge = HybridBridge.onMessage(handleBridgeEvent);
    HybridBridge.enableReporting();
  });

  onDestroy(async () => {
    if (saveTimer) clearTimeout(saveTimer);
    persistLayout();
    if (unsubBridge) unsubBridge();
    if (resizeObserver) resizeObserver.disconnect();
    await HybridBridge.disableReporting();
  });
</script>

<div class="relative h-full w-full" role="application" aria-label="Dashboard">
  {#if loadError}
    <div class="flex h-full w-full flex-col items-center justify-center gap-2">
      <p class="text-sm text-red-400">Failed to load dashboard</p>
      <p class="text-xs text-gray-500">{loadError}</p>
      <button class="mt-1 rounded bg-cyan-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-cyan-500" onclick={loadRealConfig}>Retry</button>
    </div>
  {:else if !loaded}
    <div class="flex h-full w-full items-center justify-center">
      <span class="inline-block h-6 w-6 animate-spin rounded-full border-2 border-gray-500 border-t-cyan-400"></span>
    </div>
  {:else if gaugeStates.length === 0}
    <div class="flex h-full w-full flex-col items-center justify-center gap-2">
      <p class="text-sm text-gray-400">No gauges configured</p>
      <p class="text-xs text-gray-500">Open the sidebar and go to Configure to add gauges</p>
    </div>
  {:else}
    <div
      bind:this={containerEl}
      class="relative h-full w-full"
      role="application"
      onpointermove={handlePointerMove}
      onpointerup={handlePointerUp}
      onpointerleave={handlePointerUp}
    >
      {#if backgroundImageDataUrl}
        <img
          src={backgroundImageDataUrl}
          alt=""
          class="pointer-events-none absolute inset-0 h-full w-full object-cover"
          draggable="false"
        />
      {/if}
      {#each gaugeStates as gauge (gauge.entityId)}
        {const pxW = $derived(Math.round(containerWidth * gauge.widthFraction * gauge.scale))}
        {const pxH = $derived(Math.round(containerHeight * gauge.heightFraction * gauge.scale))}
        {const visual = $derived(estimateVisualSize(gauge.category, pxW, pxH, {
          fontSizeScale: gauge.fontSizeScale,
          digitalStyle: gauge.digitalStyle,
          formattedValue: gauge.formattedValue,
          showValue: gauge.showValue,
          showUnit: gauge.showUnit,
          showName: gauge.showName,
          unitText: gauge.unit,
          nameText: gauge.name,
        }))}
        {const cw = $derived(visual.w)}
        {const ch = $derived(visual.h)}
        {const offsetX = $derived(dragging?.entityId === gauge.entityId ? dragging.deltaFracX : 0)}
        {const offsetY = $derived(dragging?.entityId === gauge.entityId ? dragging.deltaFracY : 0)}
        {const pxL = $derived(Math.round(containerWidth * (gauge.fractionX + offsetX)))}
        {const pxT = $derived(Math.round(containerHeight * (gauge.fractionY + offsetY)))}
        {const maxL = $derived(containerWidth - cw)}
        {const maxT = $derived(containerHeight - ch)}
        {const clampedL = $derived(Math.max(0, Math.min(pxL, maxL)))}
        {const clampedT = $derived(Math.max(0, Math.min(pxT, maxT)))}
        <div
          class="absolute cursor-grab"
          style:transform="translate({clampedL}px, {clampedT}px)"
          style:width="{cw}px"
          style:height="{ch}px"
          style:touch-action="none"
          style:will-change="transform"
          style:z-index={gauge.zIndex}
          onpointerdown={(e) => handlePointerDown(e, gauge.entityId)}
          oncontextmenu={(e) => handleContextMenu(e, gauge.entityId)}
          role="button" tabindex="0"
        >
          <GaugeCard {gauge} pixelWidth={pxW} pixelHeight={pxH} />
        </div>
      {/each}
    </div>
  {/if}
</div>

{#if settingsOpen && settingsDef}
  <GaugeSettingsModal
    open={true}
    gaugeDef={settingsDef}
    gaugeName={entityLookup[String(settingsDef.entityId)]?.name ?? `Entity ${settingsDef.entityId}`}
    entityInfo={entityLookup[String(settingsDef.entityId)] ?? null}
    onclose={handleSettingsClose}
    onchange={handleSettingsChange}
  />
{/if}

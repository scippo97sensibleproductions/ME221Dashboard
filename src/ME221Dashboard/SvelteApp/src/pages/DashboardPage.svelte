<script lang="ts">
  import { onMount, onDestroy, untrack } from 'svelte';
  import GaugeCard from '../lib/gauges/GaugeCard.svelte';
  import GaugeSettingsModal from '../lib/GaugeSettingsModal.svelte';
  import { HybridBridge, type GaugeConfigEntry, type EntityInfo, type GpsLocation } from '../lib/HybridBridge';
  import type { DataLinkWarningSetting } from '../lib/HybridBridgeTypes';
  import { liveDataStore } from '../lib/stores/LiveDataStore.svelte';
  import { GaugeShapeCategory, toGaugeDefinition, formatValue, toSavePayload, estimateVisualSize, applyTransform, isTransformable, computeWarningState, buildWarningMap, type ValueTransformStep } from '../lib/gauges/types';
  import type { GaugeDefinition } from '../lib/gauges/types';
  import { loadDerivedConfig } from '../lib/derived/vehicleConfig';
  import { computeDerived, type ComputationInputs } from '../lib/derived/compute';
  import { DERIVED_ENTITIES } from '../lib/derived/types';
  import type { DashboardTableEntry } from '../lib/HybridBridgeTypes';
  import TableWidget from '../lib/tables/TableWidget.svelte';
  import TableSettingsModal from '../lib/tables/TableSettingsModal.svelte';
  import AddGaugePopup from '../lib/AddGaugePopup.svelte';

  let { dashboardName, onNavigate, gpsLocation }: {
    dashboardName: string;
    onNavigate: (page: string) => void;
    gpsLocation: GpsLocation | null;
  } = $props();

  let containerEl = $state<HTMLDivElement | null>(null);
  let containerWidth = $derived(canvasWidth);
  let containerHeight = $derived(canvasHeight);
  let gaugeDefs = $state<GaugeConfigEntry[]>([]);
  let entityLookup = $state<Record<string, EntityInfo>>({});
  let entityValues = liveDataStore.values;
  let dragging = $state<{
    entityId: number;
    startFracX: number;
    startFracY: number;
    deltaFracX: number;
    deltaFracY: number;
    startClientX: number;
    startClientY: number;
  } | null>(null);
  let tableDragging = $state<{
    tableId: number;
    startFracX: number;
    startFracY: number;
    deltaFracX: number;
    deltaFracY: number;
    startClientX: number;
    startClientY: number;
  } | null>(null);
  // Table settings modal state
  let tableSettingsOpen = $state(false);
  let tableSettingsEntry = $state<DashboardTableEntry | null>(null);
  let tableSettingsName = $state('');
  let addGaugePopupOpen = $state(false);
  let addGaugePopupX = $state(0);
  let addGaugePopupY = $state(0);
  let loaded = $state(false);
  let loadError = $state<string | null>(null);
  let gridRows = $state(4);
  let gridColumns = $state(7);
  let backgroundImageDataUrl = $state<string | null>(null);
  let warningSettings = $state<DataLinkWarningSetting[] | null>(null);
  let warningMap = $state<Map<number, DataLinkWarningSetting> | null>(null);
  let useLambdaMode = $state(false);
  let stoichAfr = $state(14.7);

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
      untrack(() => {
        for (const [idStr, val] of Object.entries(gpsValues)) {
          entityValues[idStr] = val;
          updateGaugeValue(Number(idStr), val);
        }
        computeAndInjectDerived();
      });
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

  // Push live odometer updates from C# into entityValues so the gauge updates
  $effect(() => {
    const od = liveDataStore.odometer;
    if (od != null) {
      entityValues[String(ODOMETER)] = od;
      updateGaugeValue(ODOMETER, od);
    }
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
  let layoutDirty = false;

  const defaultGaugeName = (id: number) => `Entity ${id}`;
  const defaultGaugeUnit = (id: number) => '';

  // ── Static config — only rebuilds when gaugeDefs or entityLookup change ──
  // This is the expensive part (35-field mapping per gauge). By splitting it
  // from the value-only path, we avoid rebuilding ALL gauge objects at data rate.
  // Pre-computes entityIdStr to avoid String() allocation in the 30fps hot path.
  const staticGaugeConfigs = $derived(gaugeDefs.map(def => {
    const entInfo = entityLookup[String(def.entityId)];
    const name = entInfo?.name ?? defaultGaugeName(def.entityId);
    const defaultUnit = entInfo?.unit ?? defaultGaugeUnit(def.entityId);
    const unit = def.customUnitLabel || defaultUnit;
    const minValue = (entInfo?.minValue ?? 0) <= (entInfo?.maxValue ?? 10000)
      ? (entInfo?.minValue ?? 0) : 0;
    const maxValue = (entInfo?.maxValue ?? 10000) > minValue
      ? (entInfo?.maxValue ?? 10000) : 10000;
    const transformSteps = def.transformSteps;

    // Regenerate needleCurve if entity min/max changed from when the curve was saved
    let needleCurve = def.needleCurve;
    if (needleCurve && needleCurve.length >= 2) {
      const curveMin = needleCurve[0].rawValue;
      const curveMax = needleCurve[needleCurve.length - 1].rawValue;
      if (curveMin !== minValue || curveMax !== maxValue) {
        const oldRange = curveMax - curveMin;
        const newRange = maxValue - minValue;
        needleCurve = needleCurve.map(p => ({
          rawValue: oldRange === 0 ? minValue : minValue + ((p.rawValue - curveMin) / oldRange) * newRange,
          angle: p.angle,
        }));
      }
    }

    return {
      config: { ...def, needleCurve },
      entityIdStr: String(def.entityId),
      name,
      unit,
      lowerName: name.toLowerCase(),
      lowerUnit: unit.toLowerCase(),
      minValue,
      maxValue,
      transformSteps,
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
  // Per-gauge previous raw values for spike gate
  let _prevRawValues = new Map<number, number>();
  // Last frame timestamp for frame-rate independent smoothing
  let _lastFrameTime = 0;

  // Per-gauge value history for histograms (circular buffer, last 60 values)
  const HIST_BUFFER_SIZE = 60;
  let gaugeValueHistory = $state<Map<number, number[]>>(new Map());

  function rebuildGaugeStates() {
    const next: GaugeDefinition[] = [];
    const idxMap = new Map<number, number>();
    // Reset smoothed values on config change
    _smoothedValues = new Map<number, number>();
    _prevRawValues = new Map<number, number>();
    _lastFrameTime = 0;
    // Reset history buffers
    const newHistory = new Map<number, number[]>();
    for (let i = 0; i < staticGaugeConfigs.length; i++) {
      const sc = staticGaugeConfigs[i];
      const rawValue = entityValues[sc.entityIdStr];
      const value = rawValue ?? 0;
      next.push({
        ..._gaugeDefCache[i],
        value,
        formattedValue: formatValue(value, sc.name, sc.unit, useLambdaMode, stoichAfr, sc.lowerName, sc.lowerUnit),
        name: sc.name,
        unit: sc.unit,
        lowerName: sc.lowerName,
        lowerUnit: sc.lowerUnit,
        minValue: sc.minValue,
        maxValue: sc.maxValue,
        fractionX: sc.config.fractionX,
        fractionY: sc.config.fractionY,
        transformSteps: sc.transformSteps,
        warningState: 'none',
      });
      idxMap.set(sc.config.entityId, i);
      // Seed smoothed value with current raw value
      _smoothedValues.set(sc.config.entityId, value);
      _prevRawValues.set(sc.config.entityId, value);
      // Initialize history buffer
      newHistory.set(sc.config.entityId, [value]);
    }
    gaugeStates = next;
    _gaugeIndexByEntityId = idxMap;
    gaugeValueHistory = newHistory;
  }

  // ── Shared smoothing + transform pipeline ──
  function applyPipeline(raw: number, entityId: number, g: {
    smoothingEnabled: boolean;
    smoothingFactor: number;
    smoothingResponseMs: number;
    spikeGatePercent: number;
    transformSteps?: ValueTransformStep[];
  }): number {
    let v = raw;
    const now = performance.now();

    // Spike gate: reject jumps beyond threshold (percentage of gauge range)
    if (g.spikeGatePercent > 0 && g.minValue != null && g.maxValue != null) {
      const prevRaw = _prevRawValues.get(entityId) ?? raw;
      const range = Math.abs(g.maxValue - g.minValue);
      const maxDelta = range * (g.spikeGatePercent / 100);
      const jump = Math.abs(raw - prevRaw);
      if (jump > maxDelta) {
        // Clamp to max delta from previous raw
        v = prevRaw + Math.sign(raw - prevRaw) * maxDelta;
      }
    }
    _prevRawValues.set(entityId, v);

    // Smoothing: response-time-based EMA (frame-rate independent)
    if (g.smoothingEnabled) {
      let alpha: number;
      if (g.smoothingResponseMs > 0) {
        // Derive alpha from response time: alpha = 1 - exp(-dt / tau)
        // dt = frame interval, tau = response time in ms
        const dt = _lastFrameTime > 0 ? Math.min(now - _lastFrameTime, 200) : 33;
        const tau = g.smoothingResponseMs;
        alpha = 1 - Math.exp(-dt / tau);
      } else {
        // Fallback to old factor-based smoothing
        alpha = g.smoothingFactor > 0 && g.smoothingFactor < 1 ? g.smoothingFactor : 0.3;
      }
      const prev = _smoothedValues.get(entityId) ?? v;
      v = prev + alpha * (v - prev);
      _smoothedValues.set(entityId, v);
    }
    _lastFrameTime = now;

    if (g.transformSteps && isTransformable(entityId)) {
      const transformed = applyTransform(v, g.transformSteps);
      if (Number.isFinite(transformed)) {
        v = transformed;
      }
    }
    return v;
  }

  // Update a single gauge's value in place — O(1), no allocation
  function updateGaugeValue(entityId: number, value: number | null) {
    const idx = _gaugeIndexByEntityId.get(entityId);
    if (idx === undefined) return;
    const g = gaugeStates[idx];
    const v = applyPipeline(value ?? 0, entityId, g);
    g.value = v;
    g.formattedValue = formatValue(v, g.name, g.unit, useLambdaMode, stoichAfr, g.lowerName, g.lowerUnit);
  }

  // Rebuild gaugeStates when config or def cache changes (rare — on load, settings change).
  // untrack(rebuildGaugeStates) prevents Svelte from tracking entityValues reads inside
  // rebuildGaugeStates as dependencies — value updates are handled by updateGaugeValue() instead.
  $effect(() => {
    staticGaugeConfigs;
    _gaugeDefCache;
    untrack(rebuildGaugeStates);
  });

  // ─── Live data hot path ───
  // Drives gauge updates whenever the store has new data. Iterates ONLY gauges on this dashboard
  // (small N), not all entities (large K). O(N_gauges) per frame at ~10Hz = no measurable cost.
  $effect(() => {
    // Only track the frame tick. The loop below mutates gaugeStates in place, so tracking
    // gauge values here would make the effect read and write the same state.
    void liveDataStore.frameCount;

    untrack(() => {
      if (gaugeStates.length === 0) return;
      const states = gaugeStates;
      const v = entityValues;
      const ws = warningMap;
      for (let i = 0; i < states.length; i++) {
        const g = states[i];
        const rawKv = v[g.entityId];
        const raw = rawKv == null ? 0 : rawKv;
        const val = applyPipeline(raw, g.entityId, g);
        if (g.value !== val) {
          g.value = val;
          g.formattedValue = formatValue(val, g.name, g.unit, useLambdaMode, stoichAfr, g.lowerName, g.lowerUnit);
        }
        g.warningState = computeWarningState(val, ws, g.entityId);
        // Push to history buffer for histograms
        if (g.showHistogram) {
          let buf = gaugeValueHistory.get(g.entityId);
          if (!buf) {
            buf = [];
            gaugeValueHistory.set(g.entityId, buf);
          }
          buf.push(val);
          if (buf.length > HIST_BUFFER_SIZE) buf.shift();
        }
      }
      computeAndInjectDerived();
    });
  });

  function loadRealConfig() {
    HybridBridge.getDashboardConfig(dashboardName).then(async result => {
      if (result.error) {
        loadError = result.error;
        return;
      }
      gaugeDefs = result.gauges;
      tableDefs = result.tables ?? [];
      if (tableDefs.length > 0) {
        HybridBridge.getTableDefinitions().then(defs => {
          const names: Record<number, string> = {};
          for (const t of defs.tables ?? []) {
            names[t.id] = t.name;
          }
          tableNames = names;
        }).catch(() => {});
      }
      gridRows = result.gridRows;
      gridColumns = result.gridColumns;
      if (result.entities) {
        entityLookup = result.entities;
      }

      // Inject GPS entity names and default ranges if GPS gauges are configured
      // Only add if not already present to avoid unnecessary staticGaugeConfigs rebuild
      if (gaugeDefs.some(g => g.entityId <= -1001 && g.entityId >= -1006)) {
        let changed = false;
        for (const [id, info] of [
          [GPS_SPEED, { name: 'GPS Speed', unit: 'km/h', minValue: 0, maxValue: 240 }],
          [GPS_LAT, { name: 'GPS Latitude', unit: '°', minValue: -90, maxValue: 90 }],
          [GPS_LON, { name: 'GPS Longitude', unit: '°', minValue: -180, maxValue: 180 }],
          [GPS_ALT, { name: 'GPS Altitude', unit: 'm', minValue: 0, maxValue: 1000 }],
          [GPS_COURSE, { name: 'GPS Course', unit: '°', minValue: 0, maxValue: 360 }],
          [GPS_ACC, { name: 'GPS Accuracy', unit: 'm', minValue: 0, maxValue: 50 }],
        ] as const) {
          if (!entityLookup[String(id)]) {
            entityLookup[String(id)] = info;
            changed = true;
          }
        }
        if (changed) entityLookup = entityLookup;
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

  // ── Tables ──────────────────────────────────────────────────────
  let tableDefs = $state<DashboardTableEntry[]>([]);
  let tableNames = $state<Record<number, string>>({});

  // ── Drag refcount: report ref retained while this component is mounted ──
  // (no longer needed — reporting is owned by App shell)

  let pendingDrag = $state<{
    type: 'gauge' | 'table';
    id: number;
    startFracX: number;
    startFracY: number;
    startClientX: number;
    startClientY: number;
    thresholdMet: boolean;
    target: HTMLElement;
    pointerId: number;
  } | null>(null);

  const DRAG_THRESHOLD = 4;
  let layoutLocked = $state(false);

  function tryActivateDrag(dx: number, dy: number) {
    if (!pendingDrag) return;
    if (pendingDrag.thresholdMet) return;
    if (dx < DRAG_THRESHOLD && dy < DRAG_THRESHOLD) return;
    pendingDrag.thresholdMet = true;
    if (pendingDrag.type === 'gauge') {
      const def = gaugeDefs.find(d => d.entityId === pendingDrag!.id);
      if (!def) return;
      dragging = {
        entityId: pendingDrag.id,
        startFracX: def.fractionX,
        startFracY: def.fractionY,
        deltaFracX: 0,
        deltaFracY: 0,
        startClientX: pendingDrag.startClientX,
        startClientY: pendingDrag.startClientY,
      };
    } else {
      const def = tableDefs.find(d => d.tableId === pendingDrag!.id);
      if (!def) return;
      tableDragging = {
        tableId: pendingDrag.id,
        startFracX: def.fractionX,
        startFracY: def.fractionY,
        deltaFracX: 0,
        deltaFracY: 0,
        startClientX: pendingDrag.startClientX,
        startClientY: pendingDrag.startClientY,
      };
    }
    try {
      pendingDrag.target.setPointerCapture(pendingDrag.pointerId);
    } catch {
      // ignore
    }
  }

  function handlePointerDown(e: PointerEvent, entityId: number) {
    if (layoutLocked) return;
    if (!e.target || (e.target as HTMLElement).closest('input, button, a')) return;
    const def = gaugeDefs.find(d => d.entityId === entityId);
    if (!def) return;
    pendingDrag = {
      type: 'gauge',
      id: entityId,
      startFracX: def.fractionX,
      startFracY: def.fractionY,
      startClientX: e.clientX,
      startClientY: e.clientY,
      thresholdMet: false,
      target: e.currentTarget as HTMLElement,
      pointerId: e.pointerId,
    };
  }

  function handleTablePointerDown(e: PointerEvent, tableId: number) {
    if (layoutLocked) return;
    if (!e.target || (e.target as HTMLElement).closest('input, button, a, td, th, table')) return;
    const def = tableDefs.find(d => d.tableId === tableId);
    if (!def) return;
    pendingDrag = {
      type: 'table',
      id: tableId,
      startFracX: def.fractionX,
      startFracY: def.fractionY,
      startClientX: e.clientX,
      startClientY: e.clientY,
      thresholdMet: false,
      target: e.currentTarget as HTMLElement,
      pointerId: e.pointerId,
    };
  }

  let pendingDragDx = 0;
  let pendingDragDy = 0;
  let dragRaf: number | null = null;

  function handlePointerMove(e: PointerEvent) {
    if (pendingDrag && !pendingDrag.thresholdMet) {
      const dx = Math.abs(e.clientX - pendingDrag.startClientX);
      const dy = Math.abs(e.clientY - pendingDrag.startClientY);
      tryActivateDrag(dx, dy);
      if (!pendingDrag.thresholdMet) return;
    }
    if (dragging) {
      pendingDragDx = (e.clientX - dragging.startClientX) / containerWidth;
      pendingDragDy = (e.clientY - dragging.startClientY) / containerHeight;
      if (dragRaf === null) {
        dragRaf = requestAnimationFrame(() => {
          dragRaf = null;
          dragging.deltaFracX = pendingDragDx;
          dragging.deltaFracY = pendingDragDy;
        });
      }
    } else if (tableDragging) {
      pendingDragDx = (e.clientX - tableDragging.startClientX) / containerWidth;
      pendingDragDy = (e.clientY - tableDragging.startClientY) / containerHeight;
      if (dragRaf === null) {
        dragRaf = requestAnimationFrame(() => {
          dragRaf = null;
          tableDragging.deltaFracX = pendingDragDx;
          tableDragging.deltaFracY = pendingDragDy;
        });
      }
    }
  }

  function persistLayout() {
    if (!layoutDirty) return;
    layoutDirty = false;
    const gauges = gaugeDefs.map(def => toSavePayload(def));
    const tables = tableDefs.map(t => ({
      tableId: t.tableId,
      fractionX: t.fractionX,
      fractionY: t.fractionY,
      widthFraction: t.widthFraction,
      heightFraction: t.heightFraction,
      zIndex: t.zIndex,
      colorScheme: t.colorScheme,
      showLabels: t.showLabels,
      showDimensionBadge: t.showDimensionBadge,
    }));
    HybridBridge.saveDashboardLayout(dashboardName, gauges, tables);
  }

  function schedulePersist() {
    layoutDirty = true;
    if (saveTimer) clearTimeout(saveTimer);
    saveTimer = setTimeout(() => {
      saveTimer = null;
      persistLayout();
    }, 1500);
  }

  function handlePointerUp() {
    pendingDrag = null;
    if (dragging) {
      if (dragRaf !== null) {
        cancelAnimationFrame(dragRaf);
        dragRaf = null;
        dragging.deltaFracX = pendingDragDx;
        dragging.deltaFracY = pendingDragDy;
      }
      const def = gaugeDefs.find(d => d.entityId === dragging!.entityId);
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
        layoutDirty = true;
        persistLayout();
      }
    } else if (tableDragging) {
      if (dragRaf !== null) {
        cancelAnimationFrame(dragRaf);
        dragRaf = null;
        tableDragging.deltaFracX = pendingDragDx;
        tableDragging.deltaFracY = pendingDragDy;
      }
      const def = tableDefs.find(d => d.tableId === tableDragging!.tableId);
      if (def) {
        const pxW = containerWidth * def.widthFraction;
        const pxH = containerHeight * def.heightFraction;
        def.fractionX = Math.max(0, Math.min(1 - pxW / containerWidth, tableDragging.startFracX + tableDragging.deltaFracX));
        def.fractionY = Math.max(0, Math.min(1 - pxH / containerHeight, tableDragging.startFracY + tableDragging.deltaFracY));
        tableDefs = tableDefs;
        layoutDirty = true;
        persistLayout();
      }
    }
    dragging = null;
    tableDragging = null;
  }

  function handleContextMenu(e: MouseEvent, entityId: number) {
    e.preventDefault();
    const def = gaugeDefs.find(d => d.entityId === entityId);
    if (!def) return;
    settingsTargetId = entityId;
    settingsDef = { ...def };
    settingsOpen = true;
  }

  function handleTableContextMenu(e: MouseEvent, tableId: number) {
    e.preventDefault();
    const def = tableDefs.find(d => d.tableId === tableId);
    if (!def) return;
    tableSettingsEntry = { ...def };
    tableSettingsName = tableNames[tableId] ?? `Table ${tableId}`;
    tableSettingsOpen = true;
  }

  function handleTableSettingsChange(updated: DashboardTableEntry) {
    tableSettingsEntry = updated;
    tableDefs = tableDefs.map(t => t.tableId === updated.tableId ? updated : t);
    schedulePersist();
  }

  function handleTableSettingsClose() {
    tableSettingsOpen = false;
    tableSettingsEntry = null;
  }

  function handleContainerContextMenu(e: MouseEvent) {
    if ((e.target as HTMLElement).closest('.gauge-wrap, .table-wrap')) return;
    e.preventDefault();
    addGaugePopupX = e.clientX;
    addGaugePopupY = e.clientY;
    addGaugePopupOpen = true;
  }

  function handleAddGauge(sensor: { id: number; name: string; category: string; unit: string }, clickX: number, clickY: number) {
    if (gaugeDefs.some(d => d.entityId === sensor.id)) return;
    const containerRect = containerEl?.getBoundingClientRect();
    const fracX = containerRect ? (clickX - containerRect.left) / containerRect.width : 0.5;
    const fracY = containerRect ? (clickY - containerRect.top) / containerRect.height : 0.5;
    const newEntry: GaugeConfigEntry = {
      entityId: sensor.id,
      shapeCategory: 3,
      sweepAngle: 220,
      arcPosition: 0,
      digitalStyle: 0,
      texturePath: null,
      needleStartAngle: 135,
      needleEndAngle: 405,
      needleOffsetX: 0,
      needleOffsetY: 0,
      needleWidth: 2.5,
      needleLength: 1.0,
      scale: 1.0,
      fontSizeScale: 1.0,
      labelVerticalOffset: 0,
      showName: true,
      showUnit: true,
      showValue: true,
      iconName: null,
      iconOffsetX: 0,
      iconOffsetY: 0,
      iconSize: 0.5,
      barValuePosition: 4,
      barUnitPosition: 7,
      barNamePosition: 8,
      colorStops: [],
      colorHysteresis: 0.03,
      smoothingEnabled: false,
      smoothingFactor: 0.3,
      smoothingResponseMs: 0,
      spikeGatePercent: 0,
      fractionX: Math.max(0, Math.min(0.78, fracX)),
      fractionY: Math.max(0, Math.min(0.72, fracY)),
      widthFraction: 0.22,
      heightFraction: 0.28,
      chartTimeWindowSec: 30,
      chartYMin: null,
      chartYMax: null,
      chartLineColor: '#22c8e6',
      chartLineWidth: 2,
      chartShowGrid: true,
      chartFillUnder: false,
      chartShowLabels: true,
      chartPrecision: 1,
      textColor: '#ffffff',
      zIndex: gaugeDefs.length,
    };
    gaugeDefs = [...gaugeDefs, newEntry];
    layoutDirty = true;
    persistLayout();
  }

  function handleSettingsChange(updated: GaugeConfigEntry) {
    settingsDef = updated;
    gaugeDefs = gaugeDefs.map(d => d.entityId === updated.entityId ? updated : d);
    schedulePersist();
  }

  function handleSettingsClose() {
    settingsOpen = false;
    settingsTargetId = -1;
    settingsDef = null;
  }

  let resizeObserver: ResizeObserver | null = null;
  let parentWidth = $state(800);
  let parentHeight = $state(600);
  let isAndroid = $state(false);
  const ASPECT = 16 / 9;

  function observeContainer() {
    if (!containerEl) return;
    const parent = containerEl.parentElement;
    if (!parent) return;
    resizeObserver = new ResizeObserver(entries => {
      for (const entry of entries) {
        parentWidth = entry.contentRect.width;
        parentHeight = entry.contentRect.height;
      }
    });
    resizeObserver.observe(parent);
  }

  let canvasWidth = $derived.by(() => {
    if (isAndroid) return Math.round(parentWidth);
    const byW = parentWidth;
    const byH = parentHeight * ASPECT;
    return Math.round(Math.min(byW, byH));
  });
  let canvasHeight = $derived.by(() => {
    if (isAndroid) return Math.round(parentHeight);
    const byH = parentHeight;
    const byW = parentWidth / ASPECT;
    return Math.round(Math.min(byH, byW));
  });

  $effect(() => {
    if (containerEl && loaded && gaugeStates.length > 0) {
      observeContainer();
      return () => {
        if (resizeObserver) resizeObserver.disconnect();
      };
    }
  });

  onMount(() => {
    HybridBridge.getPlatform().then(p => { isAndroid = p === 'Android'; }).catch(() => {});
    loadRealConfig();
    const od = liveDataStore.odometer;
    if (od != null) entityValues[String(ODOMETER)] = od;
    HybridBridge.getWarningSettings().then(s => {
      warningSettings = s;
      warningMap = s ? buildWarningMap(s) : null;
    }).catch(() => {});
    HybridBridge.getLambdaSettings().then(s => {
      useLambdaMode = s.useLambdaMode;
      stoichAfr = s.stoichAfr;
    }).catch(() => {});
  });

  onDestroy(() => {
    if (layoutDirty) persistLayout();
    if (saveTimer) clearTimeout(saveTimer);
    if (resizeObserver) resizeObserver.disconnect();
  });
</script>

<div class="relative flex h-full w-full items-center justify-center" role="application" aria-label="Dashboard">
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
  {:else if gaugeStates.length === 0 && tableDefs.length === 0}
    <div class="flex h-full w-full flex-col items-center justify-center gap-2">
      <p class="text-sm text-gray-400">No gauges configured</p>
      <p class="text-xs text-gray-500">Open the sidebar and go to Configure to add gauges</p>
    </div>
  {:else}
    <!-- Lock/unlock toggle — floating top-right -->
    <button
      class="absolute top-2 right-2 z-50 flex h-8 w-8 items-center justify-center rounded-lg border transition-colors
        {layoutLocked
          ? 'border-amber-500/40 bg-amber-500/10 text-amber-400 hover:bg-amber-500/20'
          : 'border-gray-600 bg-gray-800 text-gray-400 hover:bg-gray-700 hover:text-gray-200'}"
      title={layoutLocked ? 'Unlock layout (allow dragging)' : 'Lock layout (prevent dragging)'}
      onclick={() => { layoutLocked = !layoutLocked; }}
    >
      {#if layoutLocked}
        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="11" x="3" y="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>
      {:else}
        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="18" height="11" x="3" y="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0 1 9.9-1"/></svg>
      {/if}
    </button>
    <div
      bind:this={containerEl}
      class="dashboard-canvas relative overflow-hidden"
      style:width="{canvasWidth}px"
      style:height="{canvasHeight}px"
      role="application"
      onpointermove={handlePointerMove}
      onpointerup={handlePointerUp}
      onpointerleave={handlePointerUp}
      oncontextmenu={handleContainerContextMenu}
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
          class="absolute gauge-wrap"
          style:transform="translate({clampedL}px, {clampedT}px)"
          style:width="{cw}px"
          style:height="{ch}px"
          style:touch-action="none"
          style:will-change="transform"
          style:z-index={gauge.zIndex}
          style:cursor={layoutLocked ? 'default' : 'grab'}
          onpointerdown={(e) => handlePointerDown(e, gauge.entityId)}
          oncontextmenu={(e) => handleContextMenu(e, gauge.entityId)}
          role="button" tabindex="0"
        >
          <GaugeCard {gauge} pixelWidth={pxW} pixelHeight={pxH} valueHistory={gaugeValueHistory.get(gauge.entityId) ?? []} />
        </div>
      {/each}
      {#each tableDefs as tw (tw.tableId)}
        {const w = $derived(Math.round(containerWidth * tw.widthFraction))}
        {const h = $derived(Math.round(containerHeight * tw.heightFraction))}
        {const offsetX = $derived(tableDragging?.tableId === tw.tableId ? tableDragging.deltaFracX : 0)}
        {const offsetY = $derived(tableDragging?.tableId === tw.tableId ? tableDragging.deltaFracY : 0)}
        {const pxL = $derived(Math.round(containerWidth * (tw.fractionX + offsetX)))}
        {const pxT = $derived(Math.round(containerHeight * (tw.fractionY + offsetY)))}
        {const maxL = $derived(containerWidth - w)}
        {const maxT = $derived(containerHeight - h)}
        {const clampedL = $derived(Math.max(0, Math.min(pxL, maxL)))}
        {const clampedT = $derived(Math.max(0, Math.min(pxT, maxT)))}
        <div
          class="absolute table-wrap"
          style:transform="translate({clampedL}px, {clampedT}px)"
          style:width="{w}px"
          style:height="{h}px"
          style:touch-action="none"
          style:will-change="transform"
          style:z-index={tw.zIndex}
          style:cursor={layoutLocked ? 'default' : 'grab'}
          onpointerdown={(e) => handleTablePointerDown(e, tw.tableId)}
          oncontextmenu={(e) => handleTableContextMenu(e, tw.tableId)}
          role="button" tabindex="0"
        >
          <TableWidget
            tableId={tw.tableId}
            tableName={tableNames[tw.tableId] ?? `Table ${tw.tableId}`}
            colorScheme={tw.colorScheme}
            showLabels={tw.showLabels}
            showDimensionBadge={tw.showDimensionBadge}
            onTap={(id) => onNavigate('tableEditor', { tableId: id })}
            onSettings={(id) => {
              tableSettingsEntry = { ...tw };
              tableSettingsName = tableNames[id] ?? `Table ${id}`;
              tableSettingsOpen = true;
            }}
          />
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
    entityLookup={entityLookup}
    onclose={handleSettingsClose}
    onchange={handleSettingsChange}
  />
{/if}

{#if tableSettingsOpen && tableSettingsEntry}
  <TableSettingsModal
    open={true}
    {tableSettingsName}
    entry={tableSettingsEntry}
    onclose={handleTableSettingsClose}
    onchange={handleTableSettingsChange}
  />
{/if}

{#if addGaugePopupOpen}
  <AddGaugePopup
    {dashboardName}
    existingEntityIds={new Set(gaugeDefs.map(d => d.entityId))}
    anchorX={addGaugePopupX}
    anchorY={addGaugePopupY}
    onAdd={handleAddGauge}
    onclose={() => { addGaugePopupOpen = false; }}
  />
{/if}

<script lang="ts">
  import { onMount } from 'svelte';
  import { HybridBridge } from '../lib/HybridBridge';
  import type { AvailableSensor, GaugeConfigEntry, DashboardConfigResult } from '../lib/HybridBridgeTypes';
  import { GaugeShapeCategory } from '../lib/gauges/gaugeTypes';
  import { IconPlus, IconX, IconChevronRight, IconChevronLeft, IconTrash } from '@tabler/icons-svelte';

  let { onNavigate, dashboardName = 'default' }: {
    onNavigate: (page: string, params?: Record<string, unknown>) => void;
    dashboardName?: string;
  } = $props();

  // ─── State ───
  let step = $state<'build' | 'review'>('build');
  let sensors = $state<AvailableSensor[]>([]);
  let loading = $state(true);
  let searchFilter = $state('');

  // Gauge configs being built
  interface BuilderGauge {
    uid: number;
    shapeCategory: GaugeShapeCategory;
    primaryEntityId: number | null;
    linkedEntities: { entityId: number; color: string }[];
  }
  let gauges = $state<BuilderGauge[]>([]);
  let nextUid = $state(1);
  let editingUid = $state<number | null>(null);

  // Bottom sheet state
  let sheetOpen = $state<'type' | 'entity' | 'props' | null>(null);

  const TYPE_DEFS: { type: GaugeShapeCategory; name: string; icon: string; color: string; desc: string; maxEnt: number }[] = [
    { type: GaugeShapeCategory.Arc, name: 'Arc', icon: '◎', color: '#0078D7', desc: 'Sweep gauge with needle', maxEnt: 1 },
    { type: GaugeShapeCategory.Bar, name: 'Bar', icon: '▬', color: '#00A5A5', desc: 'Segmented bar graph', maxEnt: 1 },
    { type: GaugeShapeCategory.Text, name: 'Text', icon: 'Aa', color: '#A0A0A0', desc: 'Plain numeric value', maxEnt: 1 },
    { type: GaugeShapeCategory.Digital, name: 'Digital', icon: '▯', color: '#107C10', desc: '7-segment / odometer', maxEnt: 1 },
    { type: GaugeShapeCategory.Chart, name: 'Chart', icon: '〜', color: '#D83B01', desc: 'Live line graph', maxEnt: 1 },
    { type: GaugeShapeCategory.WedgeBar, name: 'Wedge', icon: '◧', color: '#F59F00', desc: 'Fiat Uno wedge bar', maxEnt: 1 },
    { type: GaugeShapeCategory.LedRing, name: 'LED Ring', icon: '◉', color: '#6B2C91', desc: 'Circular LED segments', maxEnt: 1 },
    { type: GaugeShapeCategory.MultiRing, name: 'Multi-Ring', icon: '◎+', color: '#FFB900', desc: 'Concentric arcs', maxEnt: 5 },
  ];

  const RING_COLORS = ['#0078D7', '#107C10', '#F59F00', '#E81123', '#6B2C91'];

  const filteredSensors = $derived(
    sensors.filter(s => s.isSelected).filter(s =>
      !searchFilter || s.name.toLowerCase().includes(searchFilter.toLowerCase()) || s.unit.toLowerCase().includes(searchFilter.toLowerCase())
    )
  );

  const editingGauge = $derived(gauges.find(g => g.uid === editingUid));
  const editingType = $derived(editingGauge ? TYPE_DEFS.find(t => t.type === editingGauge.shapeCategory) : null);

  // Visible gauges: hide individual entries absorbed into Multi-Ring
  const visibleGauges = $derived.by(() => {
    const absorbed = new Set<number>();
    for (const g of gauges) {
      if (g.shapeCategory === GaugeShapeCategory.MultiRing) {
        for (const le of g.linkedEntities) absorbed.add(le.entityId);
      }
    }
    return gauges.filter(g => {
      if (g.shapeCategory === GaugeShapeCategory.MultiRing) return true;
      return !absorbed.has(g.primaryEntityId ?? -1);
    });
  });

  // ─── Load ───
  onMount(async () => {
    try {
      const [sensorResult, dashConfig] = await Promise.all([
        HybridBridge.getAvailableSensors(dashboardName),
        HybridBridge.getDashboardConfig(dashboardName),
      ]);
      sensors = sensorResult.sensors ?? [];

      // Import existing gauges from dashboard config
      if (dashConfig.gauges && dashConfig.gauges.length > 0) {
        // Collect entity IDs absorbed into Multi-Ring gauges
        const absorbedIds = new Set<number>();
        for (const g of dashConfig.gauges) {
          if (g.shapeCategory === GaugeShapeCategory.MultiRing && g.linkedEntities) {
            for (const le of g.linkedEntities) absorbedIds.add(le.entityId);
          }
        }
        // Filter out individual entries absorbed into Multi-Ring (but keep the Multi-Ring itself)
        const relevant = dashConfig.gauges.filter(g => g.shapeCategory === GaugeShapeCategory.MultiRing || !absorbedIds.has(g.entityId));
        const existing: BuilderGauge[] = relevant.map((g, i) => ({
          uid: nextUid++,
          shapeCategory: g.shapeCategory as GaugeShapeCategory,
          primaryEntityId: g.entityId,
          linkedEntities: g.linkedEntities ?? [],
        }));
        gauges = existing;
      }
    } catch {
      sensors = [];
    } finally {
      loading = false;
    }
  });

  // ─── Gauge CRUD ───
  function addGauge(type: GaugeShapeCategory) {
    const g: BuilderGauge = { uid: nextUid++, shapeCategory: type, primaryEntityId: null, linkedEntities: [] };
    gauges = [...gauges, g];
    editingUid = g.uid;
    sheetOpen = 'entity';
    if (type === GaugeShapeCategory.MultiRing) {
      // Multi-ring stays open to add more entities
    } else {
      // Single-entity: close after selection handled by pickEntity
    }
  }

  function removeGauge(uid: number) {
    gauges = gauges.filter(g => g.uid !== uid);
    if (editingUid === uid) { editingUid = null; sheetOpen = null; }
  }

  function openProps(uid: number) {
    editingUid = uid;
    sheetOpen = 'props';
  }

  // ─── Entity assignment ───
  function pickEntity(entityId: number) {
    const g = gauges.find(x => x.uid === editingUid);
    if (!g) return;
    const typeDef = TYPE_DEFS.find(t => t.type === g.shapeCategory);
    if (!typeDef) return;

    if (g.shapeCategory === GaugeShapeCategory.MultiRing) {
      const idx = g.linkedEntities.findIndex(l => l.entityId === entityId);
      if (idx >= 0) {
        g.linkedEntities = g.linkedEntities.filter(l => l.entityId !== entityId);
      } else if (g.linkedEntities.length < typeDef.maxEnt) {
        g.linkedEntities = [...g.linkedEntities, { entityId, color: RING_COLORS[g.linkedEntities.length % 5] }];
      }
    } else {
      g.primaryEntityId = g.primaryEntityId === entityId ? null : entityId;
      if (g.primaryEntityId !== null) {
        sheetOpen = null;
      }
    }
    gauges = [...gauges];
  }

  function removeLinked(entityId: number) {
    const g = gauges.find(x => x.uid === editingUid);
    if (!g) return;
    g.linkedEntities = g.linkedEntities.filter(l => l.entityId !== entityId);
    gauges = [...gauges];
  }

  function setRingColor(entityId: number, color: string) {
    const g = gauges.find(x => x.uid === editingUid);
    if (!g) return;
    g.linkedEntities = g.linkedEntities.map(l => l.entityId === entityId ? { ...l, color } : l);
    gauges = [...gauges];
  }

  function getEntityName(id: number): string {
    return sensors.find(s => s.id === id)?.name ?? `Entity ${id}`;
  }
  function getEntityUnit(id: number): string {
    return sensors.find(s => s.id === id)?.unit ?? '';
  }

  // ─── Save ───
  async function saveAndGoToDashboard() {
    try {
      // Read existing config to preserve positions/sizes
      const existingConfig = await HybridBridge.getDashboardConfig(dashboardName);
      const existingMap = new Map<number, GaugeConfigEntry>();
      if (existingConfig.gauges) {
        for (const g of existingConfig.gauges) existingMap.set(g.entityId, g);
      }

      // Collect all entity IDs absorbed into Multi-Ring gauges
      const absorbedIds = new Set<number>();
      for (const g of gauges) {
        if (g.shapeCategory === GaugeShapeCategory.MultiRing) {
          for (const le of g.linkedEntities) absorbedIds.add(le.entityId);
        }
      }

      // Only save gauges that are NOT absorbed into a Multi-Ring
      // (absorbed individual entries stay in config for isSelected tracking,
      //  dashboard filters them from rendering)
      const gaugesToSave = gauges.filter(g => {
        if (g.shapeCategory === GaugeShapeCategory.MultiRing) return true;
        const id = g.primaryEntityId;
        return id == null || !absorbedIds.has(id);
      });

      let idx = 0;
      const gaugePayloads = gaugesToSave.map((g) => {
        const primaryId = g.primaryEntityId ?? (g.linkedEntities.length > 0 ? g.linkedEntities[0].entityId : 0);
        const existing = existingMap.get(primaryId);
        const i = idx++;

        return {
          entityId: primaryId,
          shapeCategory: g.shapeCategory,
          fractionX: existing?.fractionX ?? 0.1,
          fractionY: existing?.fractionY ?? Math.min(0.05 + i * 0.15, 0.85),
          widthFraction: existing?.widthFraction ?? (g.shapeCategory === GaugeShapeCategory.MultiRing ? 0.35 : 0.25),
          heightFraction: existing?.heightFraction ?? (g.shapeCategory === GaugeShapeCategory.MultiRing ? 0.35 : 0.20),
          linkedEntities: g.linkedEntities.length > 0 ? g.linkedEntities : undefined,
          sweepAngle: existing?.sweepAngle ?? 220,
          arcPosition: existing?.arcPosition ?? 0,
          digitalStyle: existing?.digitalStyle ?? 0,
          wedgeStyle: existing?.wedgeStyle ?? 0,
          needleStartAngle: existing?.needleStartAngle ?? 135,
          needleEndAngle: existing?.needleEndAngle ?? 405,
          needleOffsetX: existing?.needleOffsetX ?? 0,
          needleOffsetY: existing?.needleOffsetY ?? 0,
          needleWidth: existing?.needleWidth ?? 2.5,
          needleLength: existing?.needleLength ?? 1.0,
          scale: existing?.scale ?? 1,
          fontSizeScale: existing?.fontSizeScale ?? 1,
          labelVerticalOffset: existing?.labelVerticalOffset ?? 0,
          showName: existing?.showName ?? true,
          showUnit: existing?.showUnit ?? true,
          showValue: existing?.showValue ?? true,
          iconName: existing?.iconName ?? null,
          iconOffsetX: existing?.iconOffsetX ?? 0,
          iconOffsetY: existing?.iconOffsetY ?? 0,
          iconSize: existing?.iconSize ?? 0.5,
          barValuePosition: existing?.barValuePosition ?? 4,
          barUnitPosition: existing?.barUnitPosition ?? 7,
          barNamePosition: existing?.barNamePosition ?? 8,
          smoothingEnabled: existing?.smoothingEnabled ?? false,
          smoothingFactor: existing?.smoothingFactor ?? 0.3,
          smoothingResponseMs: existing?.smoothingResponseMs ?? 0,
          spikeGatePercent: existing?.spikeGatePercent ?? 0,
          textColor: existing?.textColor ?? '#ffffff',
          zIndex: existing?.zIndex ?? i,
        };
      });

      await HybridBridge.saveDashboardLayout(dashboardName, gaugePayloads);
      onNavigate('dashboard');
    } catch (e) {
      console.error('Failed to save gauge config:', e);
    }
  }

  function resetAll() {
    gauges = [];
    nextUid = 1;
    editingUid = null;
    sheetOpen = null;
  }
</script>

<div class="flex flex-col h-full bg-gray-900">
  <!-- Header -->
  <div class="flex items-center justify-between px-3 py-1.5 border-b border-gray-700" style="background: var(--metro-sidebar)">
    <div class="flex items-center gap-2">
      <span class="text-[11px] font-bold uppercase tracking-wider" style="color: var(--metro-blue); font-family: 'Orbitron Variable', sans-serif">GAUGE BUILDER</span>
      <div class="flex items-center gap-1">
        <span class="text-[9px] font-bold uppercase px-1.5 py-0.5 border"
              style={step === 'build' ? 'color: var(--metro-blue); border-color: var(--metro-blue); background: rgba(0,120,215,0.08)' : 'color: var(--metro-text-muted); border-color: transparent'}>
          1 BUILD
        </span>
        <span class="text-gray-600 text-[10px]">▸</span>
        <span class="text-[9px] font-bold uppercase px-1.5 py-0.5 border"
              style={step === 'review' ? 'color: var(--metro-green); border-color: var(--metro-green); background: rgba(16,124,16,0.08)' : 'color: var(--metro-text-muted); border-color: transparent'}>
          2 REVIEW
        </span>
      </div>
    </div>
    <div class="flex items-center gap-1.5">
      {#if step === 'build'}
        <button class="metro-btn-secondary flex items-center gap-1 text-[10px] px-2 py-1" onclick={() => sheetOpen = 'type'}>
          <IconPlus size={12} /> ADD
        </button>
      {/if}
      <button class="metro-btn-danger text-[10px] px-2 py-1" onclick={resetAll}>RESET</button>
      {#if step === 'build'}
        <button class="metro-btn-primary text-[10px] px-2 py-1" onclick={() => step = 'review'} disabled={gauges.length === 0}>REVIEW ▸</button>
      {:else}
        <button class="metro-btn-primary text-[10px] px-2 py-1" onclick={saveAndGoToDashboard}>SAVE</button>
      {/if}
    </div>
  </div>

  <!-- Step 1: Build -->
  {#if step === 'build'}
    <div class="flex-1 overflow-y-auto">
      {#if loading}
        <div class="flex items-center justify-center h-full text-gray-500 text-xs">Loading sensors...</div>
      {:else if gauges.length === 0}
        <div class="flex flex-col items-center justify-center h-full px-4 text-center">
          <div class="text-gray-600 text-3xl mb-2">+</div>
          <div class="text-gray-500 text-[11px] font-bold uppercase">No gauges yet</div>
          <div class="text-gray-600 text-[9px] mt-1">Tap ADD to create your first gauge</div>
        </div>
      {:else}
        {#each visibleGauges as g (g.uid)}
          {@const typeDef = TYPE_DEFS.find(t => t.type === g.shapeCategory)}
          <div class="flex items-center gap-1.5 px-3 py-1.5 border-b border-gray-800 cursor-pointer hover:bg-gray-800/50 transition-colors"
               role="button" tabindex="0"
               onclick={() => openProps(g.uid)}
               onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); openProps(g.uid); } }}>
            <!-- Accent dot -->
            <div class="w-0.5 self-stretch rounded-sm" style="background: {typeDef?.color}"></div>
            <!-- Type icon -->
            <div class="w-5 h-5 flex items-center justify-center text-[11px] border border-gray-700 shrink-0"
                 style="color: {typeDef?.color}">{typeDef?.icon}</div>
            <!-- Info -->
            <div class="flex-1 min-w-0 flex items-center gap-1.5">
              <span class="text-[10px] font-bold uppercase tracking-wide" style="color: {typeDef?.color}">{typeDef?.name}</span>
              <span class="text-gray-700 text-[8px]">·</span>
              {#if g.shapeCategory === GaugeShapeCategory.MultiRing}
                <span class="text-gray-400 text-[10px] truncate">
                  {g.linkedEntities.length > 0 ? `${g.linkedEntities.length} entit${g.linkedEntities.length === 1 ? 'y' : 'ies'}` : '—'}
                </span>
              {:else}
                <span class="text-gray-400 text-[10px] truncate">{g.primaryEntityId != null ? getEntityName(g.primaryEntityId) : '—'}</span>
                {#if g.primaryEntityId != null}
                  <span class="text-gray-600 text-[8px] font-mono shrink-0">{getEntityUnit(g.primaryEntityId)}</span>
                {/if}
              {/if}
            </div>
            <!-- Delete -->
            <button class="w-5 h-5 flex items-center justify-center border border-gray-700 text-gray-600 hover:border-red-500 hover:text-red-500 shrink-0"
                    onclick={(e) => { e.stopPropagation(); removeGauge(g.uid); }}>
              <IconX size={10} />
            </button>
          </div>
          <!-- Multi-ring sub-rows -->
          {#if g.shapeCategory === GaugeShapeCategory.MultiRing && g.linkedEntities.length > 0}
            {#each g.linkedEntities as le}
              <div class="flex items-center gap-1.5 pl-9 pr-3 py-0.5 border-b border-gray-800/50 text-[9px]">
                <div class="w-1 h-1 rounded-full shrink-0" style="background: {le.color}"></div>
                <span class="flex-1 text-gray-400 truncate">{getEntityName(le.entityId)}</span>
                <span class="text-gray-600 font-mono text-[8px]">{getEntityUnit(le.entityId)}</span>
              </div>
            {/each}
            {#if g.linkedEntities.length < 5}
              <div class="flex items-center justify-center pl-9 pr-3 py-0.5 border-b border-gray-800/50 text-[8px] text-gray-600 cursor-pointer hover:text-blue-400"
                   role="button" tabindex="0"
                   onclick={(e) => { e.stopPropagation(); editingUid = g.uid; sheetOpen = 'entity'; }}
                   onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); e.stopPropagation(); editingUid = g.uid; sheetOpen = 'entity'; } }}>
                + add entity
              </div>
            {/if}
          {/if}
        {/each}
      {/if}
    </div>
  {/if}

  <!-- Step 2: Review -->
  {#if step === 'review'}
    <div class="flex-1 overflow-y-auto p-3">
      <div class="text-center mb-3">
        <div class="text-[12px] font-bold uppercase tracking-wider" style="color: var(--metro-blue); font-family: 'Orbitron Variable', sans-serif">CONFIG SUMMARY</div>
        <div class="text-[9px] text-gray-500 mt-0.5">Tap ← Back to edit. On the dashboard, drag to position.</div>
      </div>
      <div class="max-w-sm mx-auto">
        {#if gauges.length === 0}
          <div class="text-center text-gray-600 text-[9px] italic py-4">No gauges configured.</div>
        {:else}
        {#each visibleGauges as g (g.uid)}
            {@const typeDef = TYPE_DEFS.find(t => t.type === g.shapeCategory)}
            <div class="flex items-center gap-1.5 px-2 py-1 border-b border-gray-800 text-[10px]">
              <div class="w-1 h-1 rounded-full shrink-0" style="background: {typeDef?.color}"></div>
              <span class="font-bold uppercase tracking-wide w-14 shrink-0" style="color: {typeDef?.color}">{typeDef?.name}</span>
              <span class="flex-1 text-gray-400 truncate">
                {#if g.shapeCategory === GaugeShapeCategory.MultiRing}
                  {#if g.linkedEntities.length > 0}
                    {#each g.linkedEntities as le, i}
                      <span style="color: {le.color}">●</span> {getEntityName(le.entityId)}
                      {#if i < g.linkedEntities.length - 1} · {/if}
                    {/each}
                  {:else}
                    <span class="italic">—</span>
                  {/if}
                {:else if g.primaryEntityId != null}
                  {getEntityName(g.primaryEntityId)}
                  <span class="text-gray-600">{getEntityUnit(g.primaryEntityId)}</span>
                {:else}
                  <span class="italic">—</span>
                {/if}
              </span>
            </div>
          {/each}
        {/if}
      </div>
      <div class="flex items-center justify-center gap-2 mt-4">
        <button class="metro-btn-secondary text-[10px] px-3 py-1.5" onclick={() => step = 'build'}>◂ BACK</button>
        <button class="metro-btn-primary text-[10px] px-3 py-1.5" onclick={saveAndGoToDashboard} disabled={gauges.length === 0}>SAVE</button>
      </div>
    </div>
  {/if}
</div>

<!-- Type picker sheet -->
{#if sheetOpen === 'type'}
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="fixed inset-0 z-50 flex items-end justify-center" style="background: rgba(0,0,0,0.55)" onclick={() => sheetOpen = null}>
    <!-- svelte-ignore a11y_click_events_have_key_events -->
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <div class="w-full max-w-md max-h-[65dvh] border border-gray-700 border-b-0 flex flex-col"
         style="background: var(--metro-sidebar); animation: sheetUp 0.15s ease-out"
         onclick={(e) => e.stopPropagation()}>
      <div class="flex items-center justify-between px-3 py-1.5 border-b border-gray-700">
        <span class="text-[9px] font-bold uppercase tracking-wider text-gray-400">NEW GAUGE</span>
        <button class="w-5 h-5 flex items-center justify-center text-gray-500 hover:text-white" onclick={() => sheetOpen = null}>
          <IconX size={14} />
        </button>
      </div>
      <div class="flex-1 overflow-y-auto p-2">
        {#each TYPE_DEFS as t}
          <button class="flex items-center gap-2 w-full p-2 border border-gray-700 bg-gray-800/50 hover:bg-gray-800 transition-colors mb-1 text-left"
                  onclick={() => addGauge(t.type)}>
            <div class="w-7 h-7 flex items-center justify-center text-sm border border-gray-700 shrink-0"
                 style="color: {t.color}; border-color: {t.color}30">{t.icon}</div>
            <div class="flex-1 min-w-0">
              <div class="text-[10px] font-bold uppercase tracking-wide" style="color: {t.color}">{t.name}</div>
              <div class="text-[8px] text-gray-500">{t.desc}</div>
            </div>
            <span class="text-[7px] font-bold uppercase px-1 border border-gray-700 text-gray-500 shrink-0">{t.maxEnt} ENT</span>
          </button>
        {/each}
      </div>
    </div>
  </div>
{/if}

<!-- Entity picker sheet -->
{#if sheetOpen === 'entity' && editingGauge}
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="fixed inset-0 z-50 flex items-end justify-center" style="background: rgba(0,0,0,0.55)" onclick={() => sheetOpen = null}>
    <!-- svelte-ignore a11y_click_events_have_key_events -->
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <div class="w-full max-w-md max-h-[65dvh] border border-gray-700 border-b-0 flex flex-col"
         style="background: var(--metro-sidebar); animation: sheetUp 0.15s ease-out"
         onclick={(e) => e.stopPropagation()}>
      <div class="flex items-center justify-between px-3 py-1.5 border-b border-gray-700">
        <span class="text-[9px] font-bold uppercase tracking-wider flex items-center gap-1.5">
          <span class="w-1.5 h-1.5 rounded-full" style="background: {editingType?.color}"></span>
          {editingType?.name} — ASSIGN
        </span>
        <button class="w-5 h-5 flex items-center justify-center text-gray-500 hover:text-white" onclick={() => sheetOpen = null}>
          <IconX size={14} />
        </button>
      </div>
      <div class="flex-1 overflow-y-auto p-2">
        <input class="w-full px-2 py-1 border border-gray-600 bg-gray-900 text-white text-[11px] mb-2 outline-none focus:border-blue-500"
               placeholder="Search..." bind:value={searchFilter}>
        {#each filteredSensors as s}
          {@const isUsed = editingGauge.primaryEntityId === s.id || editingGauge.linkedEntities.some(l => l.entityId === s.id)}
          <button class="flex items-center gap-2 w-full p-1.5 border border-gray-700 bg-gray-800/50 hover:bg-gray-800 transition-colors mb-0.5 text-left"
                  class:opacity-30={isUsed}
                  onclick={() => pickEntity(s.id)}>
            <div class="w-1.5 h-1.5 rounded-full shrink-0" style="background: {editingType?.color}"></div>
            <span class="flex-1 text-[10px] font-semibold">{s.name}</span>
            <span class="text-[8px] text-gray-500 font-mono">{s.unit || '—'}</span>
            {#if isUsed}
              <span class="w-3 h-3 flex items-center justify-center text-[8px] bg-blue-600 text-white shrink-0">✓</span>
            {/if}
          </button>
        {/each}
      </div>
    </div>
  </div>
{/if}

<!-- Properties sheet -->
{#if sheetOpen === 'props' && editingGauge}
  {@const typeDef = TYPE_DEFS.find(t => t.type === editingGauge.shapeCategory)}
  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <!-- svelte-ignore a11y_no_static_element_interactions -->
  <div class="fixed inset-0 z-50 flex items-end justify-center" style="background: rgba(0,0,0,0.55)" onclick={() => sheetOpen = null}>
    <!-- svelte-ignore a11y_click_events_have_key_events -->
    <!-- svelte-ignore a11y_no_static_element_interactions -->
    <div class="w-full max-w-md max-h-[65dvh] border border-gray-700 border-b-0 flex flex-col"
         style="background: var(--metro-sidebar); animation: sheetUp 0.15s ease-out"
         onclick={(e) => e.stopPropagation()}>
      <div class="flex items-center justify-between px-3 py-1.5 border-b border-gray-700">
        <span class="text-[9px] font-bold uppercase tracking-wider flex items-center gap-1.5">
          <span class="w-1.5 h-1.5 rounded-full" style="background: {typeDef?.color}"></span>
          {typeDef?.name} — EDIT
        </span>
        <button class="w-5 h-5 flex items-center justify-center text-gray-500 hover:text-white" onclick={() => sheetOpen = null}>
          <IconX size={14} />
        </button>
      </div>
      <div class="flex-1 overflow-y-auto p-3">
        <!-- Assigned entities -->
        <div class="text-[9px] font-bold uppercase tracking-wider text-gray-500 mb-1.5">ASSIGNED ENTITIES</div>
        {#if editingGauge.shapeCategory === GaugeShapeCategory.MultiRing}
          {#if editingGauge.linkedEntities.length > 0}
            {#each editingGauge.linkedEntities as le}
              <div class="flex items-center gap-1.5 py-1 border-b border-gray-800">
                <div class="w-1.5 h-1.5 rounded-full shrink-0" style="background: {le.color}"></div>
                <span class="flex-1 text-[10px] font-semibold">{getEntityName(le.entityId)}</span>
                <span class="text-[8px] text-gray-500 font-mono">{getEntityUnit(le.entityId)}</span>
                <button class="w-4 h-4 flex items-center justify-center border border-gray-700 text-gray-600 hover:border-red-500 hover:text-red-500"
                        onclick={() => removeLinked(le.entityId)}>
                  <IconX size={8} />
                </button>
              </div>
            {/each}
            <!-- Ring colors -->
            <div class="text-[9px] font-bold uppercase tracking-wider text-gray-500 mt-3 mb-1.5">RING COLORS</div>
            {#each editingGauge.linkedEntities as le}
              <div class="flex items-center gap-1.5 py-1 border-b border-gray-800">
                <span class="flex-1 text-[9px] text-gray-400">{getEntityName(le.entityId)}</span>
                <div class="flex gap-0.5">
                  {#each RING_COLORS as c}
                    <button class="w-3.5 h-3.5 rounded-full border-2 cursor-pointer transition-all"
                            aria-label={`Select color ${c}`}
                            title={c}
                            style="background: {c}; border-color: {le.color === c ? 'white' : 'transparent'}"
                            onclick={() => setRingColor(le.entityId, c)}></button>
                  {/each}
                </div>
              </div>
            {/each}
          {:else}
            <div class="text-[9px] text-gray-600 italic py-1">No entities assigned</div>
          {/if}
          {#if editingGauge.linkedEntities.length < 5}
            <button class="metro-btn-secondary w-full text-[9px] py-1 mt-2"
                    onclick={() => sheetOpen = 'entity'}>+ ADD ENTITY</button>
          {/if}
        {:else}
          {#if editingGauge.primaryEntityId != null}
            <div class="flex items-center gap-1.5 py-1 border-b border-gray-800">
              <div class="w-1.5 h-1.5 rounded-full shrink-0" style="background: {typeDef?.color}"></div>
              <span class="flex-1 text-[10px] font-semibold">{getEntityName(editingGauge.primaryEntityId)}</span>
              <span class="text-[8px] text-gray-500 font-mono">{getEntityUnit(editingGauge.primaryEntityId)}</span>
              <button class="w-4 h-4 flex items-center justify-center border border-gray-700 text-gray-600 hover:border-red-500 hover:text-red-500"
                      onclick={() => { editingGauge.primaryEntityId = null; gauges = [...gauges]; }}>
                <IconX size={8} />
              </button>
            </div>
          {:else}
            <div class="text-[9px] text-gray-600 italic py-1">No entity assigned</div>
          {/if}
          <button class="metro-btn-secondary w-full text-[9px] py-1 mt-2"
                  onclick={() => sheetOpen = 'entity'}>
            {editingGauge.primaryEntityId != null ? 'CHANGE ENTITY' : 'ASSIGN ENTITY'}
          </button>
        {/if}

        <button class="metro-btn-danger w-full text-[10px] py-1.5 mt-4"
                onclick={() => { removeGauge(editingUid!); sheetOpen = null; }}>
          DELETE GAUGE
        </button>
      </div>
    </div>
  </div>
{/if}

<style>
  @keyframes sheetUp {
    from { transform: translateY(100%); opacity: 0.6; }
    to { transform: translateY(0); opacity: 1; }
  }
</style>

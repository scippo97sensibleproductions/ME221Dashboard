<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { IconArrowUp, IconArrowDown } from '@tabler/icons-svelte';
  import type { GaugeDefinition } from './types';
  import { shiftLightRenderer, flashPhase, SHIFT_ZONE_COLORS } from './shiftLightRender';
  import { isUrgentState, shiftLightAnnouncer } from './shiftLightAnnouncer';
  import { liveDataStore, STALE_MS } from '../stores/LiveDataStore.svelte';
  import { DerivedEntityId } from '../derived/types';

  let { gauge, pixelWidth, pixelHeight, preview = false }: {
    gauge: GaugeDefinition;
    pixelWidth: number;
    pixelHeight: number;
    /** Settings-modal preview: the test value drives the ramp; steady, no flash. */
    preview?: boolean;
  } = $props();

  const barHeight = $derived(Math.max(16, Math.min(28, pixelHeight * 0.35, Math.max(8, pixelWidth * 0.5))));
  // The ▲/▼ sit OUTSIDE the bar (right of it), never overlapping the last (red)
  // segments; sized from the bar height so they scale with the gauge.
  const arrowSize = $derived(Math.max(20, Math.min(36, barHeight * 1.5)));

  // The renderer binds the countdown (primary) and shift-state (linked) entities
  // directly from the live store, null-preserving (R14) — bypassing the gauge's
  // null-coalescing value path so the inert state is distinguishable.
  const primaryId = $derived(
    gauge.entityId === DerivedEntityId.ShiftState ? DerivedEntityId.RpmToShift : gauge.entityId
  );
  const linkedId = $derived(gauge.linkedEntities?.[0]?.entityId ?? DerivedEntityId.ShiftState);

  // Enriched by DashboardPage (U2): the per-dashboard shift point + RPM datalink.
  const shiftPoint = $derived(gauge.shiftPoint ?? null);
  const rpmEntityId = $derived(gauge.rpmEntityId ?? null);

  // Zone colors are CVD-safe by construction (KTD6); the render result carries
  // the segment intensities/colors, the component only paints.
  const pairKey = $derived(`${primaryId}:${linkedId}`);

  let flashOn = $state(false);
  let rafId: number | null = null;
  let announcedText = $state('');

  function tickFlash(nowMs: number): void {
    const rate = renderResult.flash?.rateHz ?? 0;
    const on = rate > 0 ? flashPhase(nowMs, rate) : false;
    if (on !== flashOn) flashOn = on;
    rafId = requestAnimationFrame(tickFlash);
  }

  // Flash-loop lifecycle: the rAF chain runs ONLY while a flash-capable mode
  // is active (the renderer signals it via the flash payload); entering any
  // other mode cancels the chain so an inert gauge costs zero frames.
  $effect(() => {
    if (renderResult.flash && !preview) {
      if (rafId === null) rafId = requestAnimationFrame(tickFlash);
    } else if (rafId !== null) {
      cancelAnimationFrame(rafId);
      rafId = null;
      if (flashOn) flashOn = false;
    }
  });

  const renderResult = $derived.by(() => {
    const values = liveDataStore.values;
    const now = performance.now();
    if (preview) {
      // Shared preview panel: the single test value is treated as RPM, and the
      // ramp/countdown/state are derived from the enriched shift point — the
      // same mapping the old gauge-local preview used.
      const sp = shiftPoint != null && shiftPoint > 0 ? shiftPoint : null;
      const pv = gauge.value;
      return shiftLightRenderer.render({
        countdown: sp != null ? Math.max(0, sp - pv) : null,
        shiftState: sp != null && pv >= sp ? 1 : 0,
        rpm: sp != null ? pv : null,
        stale: false,
        shiftPoint: sp,
        rampWidthRpm: gauge.rampWidthRpm,
        flashOn: false,
        steady: true,
        segmentCount: gauge.segmentCount,
        zoneCount: gauge.zoneCount,
      });
    }
    return shiftLightRenderer.render({
      countdown: values[String(DerivedEntityId.RpmToShift)] ?? null,
      shiftState: values[String(DerivedEntityId.ShiftState)] ?? null,
      rpm: rpmEntityId !== null ? values[String(rpmEntityId)] ?? null : null,
      stale: now - liveDataStore.lastUpdateAt > STALE_MS,
      shiftPoint,
      rampWidthRpm: gauge.rampWidthRpm,
      flashOn,
      segmentCount: gauge.segmentCount,
      zoneCount: gauge.zoneCount,
    });
  });

  // Announcement coordinator: dedupe by (entity pair, state), debounce/deferral,
  // assertive/polite split, Warning-Centre suppression — all inside the pure
  // coordinator. The sr-only live region below emits the returned text.
  $effect(() => {
    if (!renderResult.announcement) return;
    const text = shiftLightAnnouncer.push(pairKey, renderResult.announcement, isUrgentState(renderResult.announcement));
    if (text !== null) announcedText = text;
  });

  // Static aria status from the render mode (accessible state exposure).
  const ariaStatus = $derived.by(() => {
    const m = renderResult.mode;
    if (m === 'shiftNow') return 'Shift up due';
    if (m === 'downshift') return 'Downshift suggested';
    if (m === 'inert') return renderResult.announcement ?? 'Shift light inactive';
    if (m === 'cold') return 'Below shift range';
    return 'Approaching shift point';
  });

  onMount(() => {
    // The lifecycle effect above arms the flash chain when a flash-capable
    // mode first appears; nothing is scheduled on mount (an inert gauge idles).
  });

  onDestroy(() => {
    if (rafId !== null) cancelAnimationFrame(rafId);
  });
</script>

<div
  class="relative flex h-full w-full items-center justify-center"
  role="img"
  aria-label={gauge.name}
  aria-live="polite"
>
  <!-- Segment bar + right-of-bar shift arrows: ▲/▼ sit side by side at the
       SAME level, just right of the bar's shift-point end — never stacked,
       never always-on, never overlapping the red segments. The arrow slot is
       ALWAYS reserved (fixed width) so the bar never reflows when an arrow
       appears — the arrow only fades via opacity. -->
  <div class="relative flex w-full items-center justify-center gap-1" style:height="{barHeight}px">
    <div class="flex min-w-0 flex-1 gap-[2px]" style:height="{barHeight}px">
      {#each renderResult.segments as seg, i (i)}
        <div
          class="flex-1 rounded-[1px]"
          style:background={seg.color}
          style:opacity="{seg.intensity}"
          style:transition="opacity 75ms ease"
        ></div>
      {/each}
    </div>

    <div class="flex shrink-0 items-center justify-center" style:width="{arrowSize}px">
      {#if renderResult.showUp}
        <div style:opacity={renderResult.flash?.on ?? true ? 1 : 0.15}>
          <IconArrowUp
            size={arrowSize}
            color={SHIFT_ZONE_COLORS.red}
          />
        </div>
      {:else if renderResult.showDown}
        <div style:opacity={0.9}>
          <IconArrowDown
            size={arrowSize}
            color="#E81123"
          />
        </div>
      {/if}
    </div>
  </div>

  <span class="sr-only" aria-live="assertive">{ariaStatus}</span>
  <span class="sr-only" aria-live="polite">{announcedText}</span>
</div>

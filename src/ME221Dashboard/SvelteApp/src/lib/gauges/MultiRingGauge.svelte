<script lang="ts">
	import type { GaugeDefinition } from './types';
	import { computeValueFraction } from './types';
	import { HybridBridge } from '../HybridBridge';

	let { gauge, pixelWidth, pixelHeight, valueTextColor }: {
		gauge: GaugeDefinition;
		pixelWidth: number;
		pixelHeight: number;
		valueTextColor?: string;
	} = $props();

	const allEntities = $derived(gauge.linkedEntities ?? []);

	let entityValues = $state<Record<string, number>>({});
	let peaks = $state<Record<string, number>>({});
	let peakTimes = $state<Record<string, number>>({});
	let hoveredId = $state<number | null>(null);

	// ── Peak hold (R18) ─────────────────────────────────────────────────
	const peakHoldOn = $derived(gauge.peakHoldEnabled !== false);
	const peakResetMs = $derived((gauge.peakHoldAutoResetSec ?? 0) * 1000);

	$effect(() => {
		if (!peakHoldOn) {
			peaks = {};
			peakTimes = {};
		}
	});

	// Timestamp-based auto-reset: no timers, one pass over the ≤5 markers in
	// the existing peak-update path (no allocation unless something expires).
	function pruneExpired(now: number) {
		if (peakResetMs <= 0) return;
		let nextPeaks = peaks;
		let nextTimes = peakTimes;
		let changed = false;
		for (const idStr of Object.keys(nextPeaks)) {
			if (now - (nextTimes[idStr] ?? 0) > peakResetMs) {
				if (!changed) { nextPeaks = { ...peaks }; nextTimes = { ...peakTimes }; changed = true; }
				delete nextPeaks[idStr];
				delete nextTimes[idStr];
			}
		}
		if (changed) { peaks = nextPeaks; peakTimes = nextTimes; }
	}

	$effect(() => {
		const ids = allEntities.map(e => e.entityId);
		if (ids.length === 0) return;
		const unsubscribe = HybridBridge.onMessage((msg) => {
			if (msg.event === 'liveDataUpdate') {
				const now = performance.now();
				pruneExpired(now);
				const updates: Record<number, number> = {};
				for (const id of ids) {
					const val = msg.values[String(id)];
					if (val != null) updates[id] = val;
				}
				if (Object.keys(updates).length > 0) {
					entityValues = { ...entityValues, ...updates };
					if (peakHoldOn) {
						let nextPeaks = peaks;
						let nextTimes = peakTimes;
						let changed = false;
						for (const [idStr, v] of Object.entries(updates)) {
							const id = Number(idStr);
							if (Number.isFinite(v) && (nextPeaks[id] === undefined || v > nextPeaks[id])) {
								if (!changed) { nextPeaks = { ...peaks }; nextTimes = { ...peakTimes }; changed = true; }
								nextPeaks[id] = v;
								nextTimes[id] = now;
							}
						}
						if (changed) { peaks = nextPeaks; peakTimes = nextTimes; }
					}
				}
			}
		});
		return () => unsubscribe();
	});

	// Primary peak tracks the pipeline value (smoothed/transformed)
	$effect(() => {
		const v = gauge.value;
		const id = gauge.entityId;
		if (!Number.isFinite(v)) return;
		const now = performance.now();
		pruneExpired(now);
		if (!peakHoldOn) return;
		const cur = peaks[id];
		if (cur === undefined || v > cur) {
			peaks = { ...peaks, [id]: v };
			peakTimes = { ...peakTimes, [id]: now };
		}
	});

	function resetPeaks() {
		peaks = {};
		peakTimes = {};
	}

	// ── Geometry (R16/R17) ──────────────────────────────────────────────
	const V = 360;
	const cx = V / 2;
	const cy = V / 2;
	const DISC_R = 61;

	const RING_DEFS = [
		{ r: 148, w: 12, stub: 6.0, label: 11.5 },
		{ r: 127, w: 10.5, stub: 5.5, label: 10.5 },
		{ r: 108, w: 9.5, stub: 5.0, label: 10.0 },
		{ r: 91,  w: 8.5, stub: 4.5, label: 9.5 },
		{ r: 76,  w: 7.5, stub: 3.5, label: 8.5 },
	];

	// R17: configurable sweep — 270° default (AE1, category-aware in the
	// mapper), clamped 45–360. Dial stays centered at the top:
	// 90 + (360 - 270)/2 = 135 (today's START).
	const sweep = $derived(Math.min(360, Math.max(45, gauge.ringSweepAngle || 270)));
	const start = $derived(90 + (360 - sweep) / 2);

	// R16: ring count + geometry — 5 rings at the current layout stay the
	// default (AE1). Auto width keeps the tapered strokes; auto gap keeps the
	// current radii (21/19/17/15 spacing). Explicit values are minimums that
	// never let ring bands overlap; rings always stay outside the center disc.
	const ringCount = $derived(Math.min(5, Math.max(1, Math.round(gauge.ringCount || 5))));
	const ringWidthPx = $derived(gauge.ringWidth > 0 ? gauge.ringWidth : 0); // 0 = auto
	const ringGapPx = $derived(gauge.ringGap > 0 ? gauge.ringGap : 0);       // 0 = auto

	const TICKS = [
		{ f: 0,     major: true },
		{ f: 0.125, major: false },
		{ f: 0.25,  major: true },
		{ f: 0.375, major: false },
		{ f: 0.5,   major: true },
		{ f: 0.625, major: false },
		{ f: 0.75,  major: true },
		{ f: 0.875, major: false },
		{ f: 1,     major: true },
	];
	const SCALE_FRACS = [0, 0.5, 1];

	const BEZEL_TICKS: number[] = [];
	for (let a = 0; a < 360; a += 6) {
		if (a > 40 && a < 140) continue; // keep the bottom gap clean for readouts
		BEZEL_TICKS.push(a);
	}

	function polar(r: number, angleDeg: number) {
		const rad = angleDeg * Math.PI / 180;
		return { x: cx + r * Math.cos(rad), y: cy + r * Math.sin(rad) };
	}

	function arcPath(r: number, a0: number, a1: number): string {
		const p0 = polar(r, a0);
		const p1 = polar(r, a1);
		const large = (a1 - a0) > 180 ? 1 : 0;
		return `M ${p0.x.toFixed(2)} ${p0.y.toFixed(2)} A ${r} ${r} 0 ${large} 1 ${p1.x.toFixed(2)} ${p1.y.toFixed(2)}`;
	}

	// ── Ring model ───────────────────────────────────────────────────────
	const rings = $derived.by(() => {
		const result: {
			entityId: number; r: number; w: number; stub: number; labelR: number;
			color: string; label: string; unit: string; value: number;
			frac: number; minV: number; maxV: number;
			peakFrac: number | null; isPrimary: boolean;
		}[] = [];
		const n = Math.min(allEntities.length, ringCount);
		const outerR = RING_DEFS[0].r;
		let prevR = outerR;
		let prevSpacing = 0;
		for (let i = 0; i < n; i++) {
			const le = allEntities[i];
			const isPrimary = i === 0;
			const val = isPrimary ? gauge.value : (entityValues[le.entityId] ?? 0);
			const minV = isPrimary ? gauge.minValue : (le.minValue ?? 0);
			const maxV = isPrimary ? gauge.maxValue : (le.maxValue ?? 100);
			const frac = computeValueFraction(val, minV, maxV);
			const peak = peaks[le.entityId];
			const peakFrac = peak !== undefined ? computeValueFraction(peak, minV, maxV) : null;
			const w = ringWidthPx > 0 ? ringWidthPx : RING_DEFS[i].w;
			const r = i === 0
				? outerR
				: Math.max(prevR - prevSpacing, DISC_R + w / 2 + 1);
			const nextW = ringWidthPx > 0 ? ringWidthPx : (RING_DEFS[i + 1]?.w ?? w);
			const minSpacing = (w + nextW) / 2 + 2;
			const autoSpacing = 21 - 2 * i; // today's radii: 148, 127, 108, 91, 76
			prevSpacing = ringGapPx > 0 ? Math.max(ringGapPx, minSpacing) : Math.max(autoSpacing, minSpacing);
			prevR = r;
			result.push({
				entityId: le.entityId,
				r, w, stub: RING_DEFS[i].stub,
				labelR: r + w / 2 + RING_DEFS[i].label,
				color: le.color || '#0078D7',
				label: isPrimary ? (gauge.name || 'PRIMARY') : (le.name || `CH ${le.entityId}`),
				unit: isPrimary ? gauge.unit : (le.unit || ''),
				value: val, frac, minV, maxV, peakFrac, isPrimary,
			});
		}
		return result;
	});

	const secondaries = $derived(rings.slice(1));
	const primary = $derived(rings[0] ?? null);

	const alarmColor = $derived(
		gauge.warningState === 'critical' ? '#ef4444'
		: gauge.warningState === 'warning' ? '#f59e0b'
		: null
	);
	const primaryStroke = $derived(alarmColor ?? primary?.color ?? '#0078D7');
	const valueFill = $derived(alarmColor ?? (valueTextColor || primary?.color || '#ffffff'));

	const valueLen = $derived(gauge.formattedValue.length);
	const valueFs = $derived(valueLen > 6 ? 20 : valueLen > 4 ? 26 : 33);

	const filterId = $derived(`mr-${gauge.entityId}`);

	// ── Formatting ───────────────────────────────────────────────────────
	function fmtVal(v: number): string {
		const a = Math.abs(v);
		if (a >= 100) return v.toFixed(0);
		return v.toFixed(1);
	}
	function fmtScale(v: number): string {
		const a = Math.abs(v);
		if (a >= 10000) return (v / 1000).toFixed(0) + 'k';
		if (a >= 1000) return (v / 1000).toFixed(1).replace(/\.0$/, '') + 'k';
		if (Number.isInteger(v)) return String(v);
		return v.toFixed(1);
	}
	function trunc(s: string, n: number): string {
		return s.length > n ? s.slice(0, n - 1) + '…' : s;
	}
</script>

<svg
	viewBox="0 0 {V} {V}"
	width={pixelWidth}
	height={pixelHeight}
	preserveAspectRatio="xMidYMid meet"
	role="img"
	aria-label="{gauge.name} multi-channel gauge"
	ondblclick={resetPeaks}
>
	<title>{gauge.name} — double-click to reset peak markers</title>
	<defs>
		<filter id={filterId} x="-40%" y="-40%" width="180%" height="180%">
			<feGaussianBlur stdDeviation="2.5" result="b" />
			<feMerge><feMergeNode in="b" /><feMergeNode in="SourceGraphic" /></feMerge>
		</filter>
		<radialGradient id="halo-{filterId}" cx="50%" cy="50%" r="50%">
			<stop offset="55%" stop-color="rgba(0,0,0,0)" />
			<stop offset="100%" stop-color="rgba(0,0,0,0.45)" />
		</radialGradient>
		<radialGradient id="disc-{filterId}" cx="50%" cy="42%" r="68%">
			<stop offset="0%" stop-color="#161622" />
			<stop offset="100%" stop-color="#0a0a12" />
		</radialGradient>
	</defs>

	<!-- Ambient halo for readability over dashboard backgrounds -->
	<circle {cx} {cy} r="176" fill="url(#halo-{filterId})" />

	<!-- Machined bezel -->
	<circle {cx} {cy} r="166" fill="none" stroke="#171722" stroke-width="0.75" />
	{#each BEZEL_TICKS as a (a)}
		{@const p1 = polar(168, a)}
		{@const p2 = polar(171.5, a)}
		<line x1={p1.x} y1={p1.y} x2={p2.x} y2={p2.y} stroke="#23232f" stroke-width="1" />
	{/each}

	{#if rings.length === 0}
		<circle {cx} {cy} r={DISC_R} fill="url(#disc-{filterId})" stroke="#262636" />
		<text x={cx} y={cy - 4} text-anchor="middle" fill="#565669"
			font-family="'Segoe UI',sans-serif" font-size="8" letter-spacing="0.2em">NO CHANNELS</text>
		<text x={cx} y={cy + 10} text-anchor="middle" fill="#3f3f50"
			font-family="'Segoe UI',sans-serif" font-size="6" letter-spacing="0.12em">LINK IN GAUGE BUILDER</text>
	{:else}
		<!-- ── Rings ── -->
		{#each rings as ring (ring.entityId)}
			{@const dimmed = hoveredId !== null && hoveredId !== ring.entityId}
			{@const stroke = ring.isPrimary ? primaryStroke : ring.color}
			<g
				class="mr-ring"
				class:dimmed
				role="presentation"
				onmouseenter={() => { hoveredId = ring.entityId; }}
				onmouseleave={() => { hoveredId = null; }}
			>
				<!-- Track -->
				<path d={arcPath(ring.r, start, start + sweep)} fill="none"
					stroke="#141420" stroke-width={ring.w + 2} />
				<path d={arcPath(ring.r, start, start + sweep)} fill="none"
					stroke="#0d0d15" stroke-width={ring.w} />

				<!-- Value arc -->
				{#if ring.frac > 0.004}
					<path
						d={arcPath(ring.r, start, start + ring.frac * sweep)}
						fill="none" stroke={stroke} stroke-width={ring.w} stroke-linecap="round"
						opacity="0.92" filter="url(#{filterId})"
						class={ring.isPrimary && gauge.warningState === 'critical' ? 'mr-crit'
							: ring.isPrimary && gauge.warningState === 'warning' ? 'mr-warn' : ''}
					/>
				{/if}

				<!-- Quadrant separators across the band -->
				{#each TICKS.filter(t => t.major) as t (t.f)}
					{@const ang = start + t.f * sweep}
					{@const p1 = polar(ring.r - ring.w / 2 - 2, ang)}
					{@const p2 = polar(ring.r + ring.w / 2 + 2, ang)}
					<line x1={p1.x} y1={p1.y} x2={p2.x} y2={p2.y} stroke="#0a0a12" stroke-width="1.6" />
				{/each}

				<!-- Outer tick stubs — light up as the arc passes them -->
				{#each TICKS as t (t.f)}
					{@const ang = start + t.f * sweep}
					{@const p1 = polar(ring.r + ring.w / 2 + 2.5, ang)}
					{@const p2 = polar(ring.r + ring.w / 2 + 2.5 + (t.major ? ring.stub : ring.stub - 2), ang)}
					{@const lit = t.f <= ring.frac + 0.001}
					<line x1={p1.x} y1={p1.y} x2={p2.x} y2={p2.y}
						stroke={lit ? ring.color : '#2c2c3a'}
						stroke-width={t.major ? 1.4 : 0.8}
						opacity={lit ? 0.9 : 0.7} />
				{/each}

				<!-- Per-channel scale numerals: min / mid / max -->
				{#each SCALE_FRACS as f (f)}
					{@const ang = start + f * sweep}
					{@const p = polar(ring.labelR, ang)}
					{@const lit = f <= ring.frac + 0.001}
					<text x={p.x} y={p.y} text-anchor="middle" dominant-baseline="central"
						font-family="'JetBrains Mono',monospace" font-size="6.5" font-weight="600"
						fill={lit ? ring.color : '#3f3f50'} opacity={lit ? 0.95 : 0.8}>
						{fmtScale(f === 0 ? ring.minV : f === 1 ? ring.maxV : (ring.minV + ring.maxV) / 2)}
					</text>
				{/each}

				<!-- Peak hold marker -->
				{#if peakHoldOn && ring.peakFrac !== null && ring.peakFrac > 0.004}
					{@const ang = start + ring.peakFrac * sweep}
					{@const p1 = polar(ring.r - ring.w / 2 - 1, ang)}
					{@const p2 = polar(ring.r + ring.w / 2 + 1.5, ang)}
					<line x1={p1.x} y1={p1.y} x2={p2.x} y2={p2.y}
						stroke="#ffffff" stroke-width="1.3" opacity="0.85" />
				{/if}
			</g>
		{/each}

		<!-- ── Center disc: primary channel ── -->
		<circle {cx} {cy} r={DISC_R} fill="url(#disc-{filterId})" stroke="#262636" stroke-width="1" />
		<circle {cx} {cy} r={DISC_R - 4.5} fill="none" stroke="#1a1a26" stroke-width="0.5" />
		{#each [0, 90, 180, 270] as a (a)}
			{@const p1 = polar(DISC_R - 5, a)}
			{@const p2 = polar(DISC_R - 1, a)}
			<line x1={p1.x} y1={p1.y} x2={p2.x} y2={p2.y} stroke="#23232f" stroke-width="1" />
		{/each}

		{#if primary}
			<text x={cx} y={cy - 27} text-anchor="middle" fill="#565669"
				font-family="'Segoe UI',sans-serif" font-size="6.5" font-weight="600"
				letter-spacing="0.18em">
				{trunc(primary.label, 16).toUpperCase()}
			</text>
			<text x={cx} y={cy + 8} text-anchor="middle" fill={valueFill}
				font-family="'Orbitron Variable','Segoe UI',sans-serif" font-size={valueFs} font-weight="900"
				filter="url(#{filterId})"
				class={gauge.warningState === 'critical' ? 'mr-crit' : gauge.warningState === 'warning' ? 'mr-warn' : ''}>
				{gauge.formattedValue}
			</text>
			{#if primary.unit}
				<text x={cx} y={cy + 22} text-anchor="middle" fill="#66667a"
					font-family="'Segoe UI',sans-serif" font-size="7.5" letter-spacing="0.12em">
					{primary.unit}
				</text>
			{/if}
			<rect x={cx - 12} y={cy + 28} width="24" height="1.5" fill={primary.color} opacity="0.6" />
		{/if}

		<!-- ── Secondary channel readout chips (in the arc gap) ── -->
		{#if secondaries.length > 0}
			{@const n = secondaries.length}
			{@const chipW = Math.min(74, (330 - (n - 1) * 6) / n)}
			{@const totalW = n * chipW + (n - 1) * 6}
			{@const x0 = cx - totalW / 2}
			{#each secondaries as ring, i (ring.entityId)}
				{@const x = x0 + i * (chipW + 6)}
				{@const hovered = hoveredId === ring.entityId}
				{@const dimmed = hoveredId !== null && !hovered}
				<g
					class="mr-chip"
					class:dimmed
					role="presentation"
					onmouseenter={() => { hoveredId = ring.entityId; }}
					onmouseleave={() => { hoveredId = null; }}
				>
					<rect x={x} y="306" width={chipW} height="27"
						fill={hovered ? '#161624' : '#0f0f18'}
						stroke={hovered ? ring.color : '#1d1d2b'} stroke-width="0.75" />
					<rect x={x} y="306" width="2.5" height="27" fill={ring.color} />
					<text x={x + chipW / 2 + 1} y="314.5" text-anchor="middle"
						font-family="'Segoe UI',sans-serif" font-size="5" font-weight="600"
						letter-spacing="0.08em" fill="#565669">
						{trunc(ring.label, 13).toUpperCase()}
					</text>
					<text x={x + chipW / 2 + 1} y="327.5" text-anchor="middle"
						font-family="'JetBrains Mono',monospace" font-size="10.5" font-weight="700"
						fill={ring.color}>
						{fmtVal(ring.value)}<tspan dx="2" font-size="6" fill="#565669">{ring.unit}</tspan>
					</text>
				</g>
			{/each}
		{/if}
	{/if}
</svg>

<style>
	.mr-ring, .mr-chip {
		transition: opacity 0.2s ease;
		cursor: crosshair;
	}
	.dimmed {
		opacity: 0.28;
	}
	@keyframes mr-pulse-crit {
		0%, 100% { opacity: 1; }
		50% { opacity: 0.45; }
	}
	@keyframes mr-pulse-warn {
		0%, 100% { opacity: 1; }
		50% { opacity: 0.62; }
	}
	:global(.mr-crit) { animation: mr-pulse-crit 0.8s ease-in-out infinite; }
	:global(.mr-warn) { animation: mr-pulse-warn 1.4s ease-in-out infinite; }
</style>

<script lang="ts">
	import type { GaugeDefinition } from './types';
	import { WedgeStyle, computeValueFraction, gaugeValueColor, buildColorLuts, DEFAULT_COLOR_STOPS } from './types';

	let { gauge, pixelWidth, pixelHeight, valueTextColor }: {
		gauge: GaugeDefinition;
		pixelWidth: number;
		pixelHeight: number;
		valueTextColor?: string;
	} = $props();

	const valueFraction = $derived(computeValueFraction(gauge.value, gauge.minValue, gauge.maxValue));
	const colorLuts = $derived(buildColorLuts(
		gauge.colorStops?.length ? gauge.colorStops : DEFAULT_COLOR_STOPS,
		gauge.colorHysteresis ?? 0.03
	));
	let _prevFraction = 0;
	const barColor = $derived.by(() => {
		const frac = valueFraction;
		const color = gaugeValueColor(frac, _prevFraction, colorLuts);
		_prevFraction = frac;
		return color;
	});

	const style = $derived(gauge.wedgeStyle ?? WedgeStyle.Classic);
	const fontSizeScale = $derived(Math.max(0.5, Math.min(2.0, gauge.fontSizeScale ?? 1.0)));

	// ── Geometry ─────────────────────────────────────────────────────────
	const W = $derived(pixelWidth);
	const H = $derived(pixelHeight);
	const pad = $derived({ l: W * 0.06, r: W * 0.04, t: H * 0.10, b: H * 0.18 });
	const barAreaW = $derived(W - pad.l - pad.r);
	const barAreaH = $derived(H - pad.t - pad.b);
	const baselineY = $derived(pad.t + barAreaH);

	const TOTAL = 32;
	const redlineStart = 0.8;
	const minH = $derived(barAreaH * 0.22);
	const maxH = $derived(barAreaH * 0.92);

	// Classic / Thermal segments
	const segGap = $derived(Math.max(1, W * 0.008));
	const segW = $derived((barAreaW - (TOTAL - 1) * segGap) / TOTAL);
	// Stacked segments (chunkier gaps)
	const stGap = $derived(Math.max(2, W * 0.016));
	const stW = $derived((barAreaW - (TOTAL - 1) * stGap) / TOTAL);
	// Chevrons
	const chevStep = $derived(barAreaW / TOTAL);
	const chevBody = $derived(chevStep * 0.66);
	const chevPoint = $derived(chevStep * 0.34);

	const activeCount = $derived(Math.round(valueFraction * TOTAL));

	function segX(i: number): number {
		return pad.l + i * (segW + segGap);
	}
	function segHeight(i: number): number {
		return minH + (maxH - minH) * ((i + 0.5) / TOTAL);
	}
	function zoneColor(f: number): string {
		if (f >= redlineStart) return '#E03131';
		if (f >= redlineStart - 0.04) return '#F59F00';
		return barColor;
	}
	function lutAt(f: number): string {
		const c = Math.max(0, Math.min(1, f));
		return gaugeValueColor(c, c, colorLuts);
	}
	function chevronPath(i: number): string {
		const h = segHeight(i);
		const x = pad.l + i * chevStep;
		const y = baselineY - h;
		const bx = x + chevBody;
		const px = bx + chevPoint;
		const mid = y + h / 2;
		return `M ${x} ${y + h} L ${x} ${y} L ${bx} ${y} L ${px} ${mid} L ${bx} ${y + h} Z`;
	}

	// ── Scale ticks ──────────────────────────────────────────────────────
	const scaleRange = $derived(gauge.maxValue - gauge.minValue);
	const scaleTicks = $derived.by(() => {
		const count = 5;
		const ticks: { val: number; frac: number }[] = [];
		for (let i = 0; i < count; i++) {
			const frac = i / (count - 1);
			ticks.push({ val: gauge.minValue + frac * scaleRange, frac });
		}
		return ticks;
	});
	const scaleDecimals = $derived(scaleRange <= 200 ? (scaleRange <= 50 ? 1 : 0) : 0);

	// ── Needle / Wire geometry ───────────────────────────────────────────
	const slopeY = (f: number) => baselineY - (minH + (maxH - minH) * f);
	const needle = $derived.by(() => {
		const f = Math.max(0, Math.min(1, valueFraction));
		return { x: pad.l + f * barAreaW, y: slopeY(f), f };
	});
	const wedgeSilhouette = $derived(
		`M ${pad.l} ${baselineY} L ${pad.l} ${baselineY - minH} L ${pad.l + barAreaW} ${baselineY - maxH} L ${pad.l + barAreaW} ${baselineY} Z`
	);
	const slopeNormal = $derived.by(() => {
		const dx = barAreaW;
		const dy = minH - maxH;
		const len = Math.hypot(dx, dy) || 1;
		return { x: dy / len, y: -dx / len };
	});

	const valueSize = $derived(Math.max(10, W * 0.09 * fontSizeScale));
	const labelSize = $derived(Math.max(7, W * 0.04 * fontSizeScale));
	const scaleSize = $derived(Math.max(7, W * 0.04 * fontSizeScale));
</script>

<svg viewBox="0 0 {W} {H}" xmlns="http://www.w3.org/2000/svg" class="size-full">
	<defs>
		<filter id="wg-{gauge.entityId}"><feGaussianBlur stdDeviation="1.5" result="b"/>
			<feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
		<filter id="wg2-{gauge.entityId}"><feGaussianBlur stdDeviation="2.5" result="b"/>
			<feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
		<linearGradient id="wgf-{gauge.entityId}" x1="0" y1="1" x2="0" y2="0">
			<stop offset="0%" stop-color={barColor} stop-opacity="0.04"/>
			<stop offset="100%" stop-color={barColor} stop-opacity="0.22"/>
		</linearGradient>
		<clipPath id="wedgeClip-{gauge.entityId}">
			<path d={wedgeSilhouette}/>
		</clipPath>
	</defs>

	<!-- ═══ CLASSIC — rising bars, redline tip ═══ -->
	{#if style === WedgeStyle.Classic}
		<rect x={pad.l - 1} y={pad.t + barAreaH * 0.06} width={barAreaW + 2} height={maxH + 2} rx="1"
			fill="#0d0d0d" stroke="#1a1a1a" stroke-width="0.5"/>
		{#each Array(TOTAL) as _, i}
			{@const f = i / TOTAL}
			{@const h = segHeight(i)}
			{@const active = i < activeCount}
			<rect x={segX(i)} y={baselineY - h} width={segW} height={h} rx="1"
				fill={active ? zoneColor(f) : '#14141e'}
				opacity={active ? 1 : 0.4}
				filter={active && f >= redlineStart ? `url(#wg2-${gauge.entityId})` : active ? `url(#wg-${gauge.entityId})` : ''}/>
		{/each}

	<!-- ═══ STACKED — Uno cluster LED blocks ═══ -->
	{:else if style === WedgeStyle.Stacked}
		<rect x={pad.l - 2} y={baselineY - maxH - 3} width={barAreaW + 4} height={maxH + 6} rx="2"
			fill="#0b0b12" stroke="#191924" stroke-width="0.5"/>
		{#each Array(TOTAL) as _, i}
			{@const f = (i + 0.5) / TOTAL}
			{@const h = segHeight(i)}
			{@const x = pad.l + i * (stW + stGap)}
			{@const y = baselineY - h}
			{@const active = i < activeCount}
			{@const zc = f >= redlineStart ? '#e03131' : f >= 0.55 ? '#f59f00' : '#37b24d'}
			<rect {x} {y} width={stW} height={h} rx="1.5"
				fill={active ? zc : '#10101a'}
				stroke={active ? 'none' : '#1d1d2b'} stroke-width="0.5"
				opacity={active ? 0.95 : 0.5}
				filter={active ? `url(#wg-${gauge.entityId})` : ''}/>
			{#if active}
				<line x1={x + 1} y1={y + 1} x2={x + stW - 1} y2={y + 1}
					stroke="#ffffff" stroke-width="0.6" opacity="0.35"/>
			{/if}
		{/each}

	<!-- ═══ NEEDLE — analog pointer on the slope ═══ -->
	{:else if style === WedgeStyle.Needle}
		<path d={wedgeSilhouette} fill="url(#wgf-{gauge.entityId})" stroke="#2c2c3a" stroke-width="0.75"/>
		{#if needle.f > 0.004}
			<path d={`M ${pad.l} ${baselineY} L ${pad.l} ${baselineY - minH} L ${needle.x} ${needle.y} L ${needle.x} ${baselineY} Z`}
				fill={barColor} opacity="0.14"/>
		{/if}
		{#each Array(17) as _, j}
			{@const f = j / 16}
			{@const major = j % 4 === 0}
			{@const px = pad.l + f * barAreaW}
			{@const py = slopeY(f)}
			{@const len = major ? 5 : 2.5}
			<line x1={px} y1={py} x2={px + slopeNormal.x * len} y2={py + slopeNormal.y * len}
				stroke={f >= redlineStart ? '#E0313190' : '#4a4a58'} stroke-width={major ? 0.9 : 0.5}/>
		{/each}
		<line x1={needle.x} y1={needle.y - 7} x2={needle.x} y2={baselineY + 3}
			stroke={barColor} stroke-width="1.5" filter={`url(#wg2-${gauge.entityId})`}/>
		<polygon points={`${needle.x - 3.5},${needle.y - 7} ${needle.x + 3.5},${needle.y - 7} ${needle.x},${needle.y - 1}`}
			fill={barColor}/>
		<circle cx={needle.x} cy={baselineY} r="1.5" fill={barColor}/>

	<!-- ═══ THERMAL — colorStop LUT per segment ═══ -->
	{:else if style === WedgeStyle.Thermal}
		<rect x={pad.l - 1} y={pad.t + barAreaH * 0.06} width={barAreaW + 2} height={maxH + 2} rx="1"
			fill="#0d0d0d" stroke="#1a1a1a" stroke-width="0.5"/>
		{#each Array(TOTAL) as _, i}
			{@const h = segHeight(i)}
			{@const active = i < activeCount}
			<rect x={segX(i)} y={baselineY - h} width={segW} height={h} rx="1"
				fill={active ? lutAt((i + 0.5) / TOTAL) : '#12121c'}
				opacity={active ? 1 : 0.35}
				filter={active ? `url(#wg-${gauge.entityId})` : ''}/>
		{/each}
		{#if activeCount > 0}
			{@const ti = activeCount - 1}
			{@const th = segHeight(ti)}
			<rect x={segX(ti)} y={baselineY - th} width={segW} height={th} rx="1"
				fill="none" stroke="#ffffff" stroke-width="0.6" opacity="0.55"
				filter={`url(#wg2-${gauge.entityId})`}/>
		{/if}

	<!-- ═══ WIRE — blueprint outline ═══ -->
	{:else if style === WedgeStyle.Wire}
		{#each [0.25, 0.5, 0.75] as gf}
			<line x1={pad.l} y1={baselineY - maxH * gf} x2={pad.l + barAreaW} y2={baselineY - maxH * gf}
				stroke="#ffffff" stroke-width="0.4" opacity="0.05"/>
		{/each}
		<path d={wedgeSilhouette} fill="rgba(255,255,255,0.02)" stroke="#3a3a48" stroke-width="1"/>
		<g clip-path={`url(#wedgeClip-${gauge.entityId})`}>
			{#each Array(24) as _, i}
				{@const x = pad.l + ((i + 0.5) / 24) * barAreaW}
				<line x1={x} y1={baselineY - maxH} x2={x} y2={baselineY}
					stroke="#ffffff" stroke-width="0.5" opacity="0.04"/>
			{/each}
			{#if needle.f > 0.004}
				<path d={`M ${pad.l} ${baselineY - minH} L ${needle.x} ${needle.y}`}
					fill="none" stroke={barColor} stroke-width="2" filter={`url(#wg2-${gauge.entityId})`}/>
				<line x1={needle.x} y1={needle.y} x2={needle.x} y2={baselineY}
					stroke={barColor} stroke-width="0.75" stroke-dasharray="2 3" opacity="0.6"/>
			{/if}
		</g>
		<circle cx={needle.x} cy={needle.y} r="2.5" fill={barColor} stroke="#0a0a0f" stroke-width="1"
			filter={`url(#wg-${gauge.entityId})`}/>
		<line x1={pad.l} y1={baselineY} x2={pad.l + barAreaW} y2={baselineY}
			stroke="#3a3a48" stroke-width="1" stroke-dasharray="1 2"/>

	<!-- ═══ CHEVRON — arrow cascade ═══ -->
	{:else if style === WedgeStyle.Chevron}
		{#each Array(TOTAL) as _, i}
			{@const f = (i + 0.5) / TOTAL}
			{@const active = i < activeCount}
			<path d={chevronPath(i)}
				fill={active ? zoneColor(f) : 'none'}
				stroke={active ? 'none' : '#23232f'} stroke-width="0.75"
				opacity={active ? 0.95 : 0.5}
				filter={active && f >= redlineStart ? `url(#wg2-${gauge.entityId})` : active ? `url(#wg-${gauge.entityId})` : ''}/>
		{/each}
	{/if}

	<!-- ── Shared: baseline scale ticks ── -->
	{#each scaleTicks as tick}
		{@const x = pad.l + tick.frac * barAreaW}
		{@const displayVal = scaleDecimals > 0 ? tick.val.toFixed(scaleDecimals) : Math.round(tick.val)}
		{@const isRed = tick.frac >= redlineStart}
		<line x1={x} y1={baselineY + 1} x2={x} y2={baselineY + 4}
			stroke={isRed ? '#E0313140' : '#333333'} stroke-width="0.5"/>
		<text x={x} y={baselineY + scaleSize + 3} text-anchor="middle"
			fill={isRed ? '#E03131' : '#666666'}
			font-family="'JetBrains Mono',monospace" font-size={scaleSize} font-weight="600">{displayVal}</text>
	{/each}

	<!-- ── Shared: value / name / unit ── -->
	<text x={W - pad.r} y={pad.t + valueSize} text-anchor="end"
		fill={valueTextColor || gauge.textColor}
		font-family={style === WedgeStyle.Wire ? "'JetBrains Mono',monospace" : "'Orbitron Variable','Segoe UI',sans-serif"}
		font-size={valueSize} font-weight="700"
		filter={valueFraction >= redlineStart ? `url(#wg2-${gauge.entityId})` : ''}>
		{gauge.formattedValue}
	</text>
	{#if gauge.showName}
		<text x={pad.l} y={pad.t + labelSize} fill="#666666"
			font-family="'Segoe UI',sans-serif" font-size={labelSize}
			letter-spacing="0.08em">
			{gauge.name}
		</text>
	{/if}
	{#if gauge.showUnit && gauge.unit}
		<text x={pad.l + barAreaW / 2} y={H - pad.b + scaleSize + 16} text-anchor="middle"
			fill="#444444" font-family="'Segoe UI',sans-serif" font-size={labelSize * 0.9}
			letter-spacing="0.05em">
			{gauge.unit}
		</text>
	{/if}
</svg>

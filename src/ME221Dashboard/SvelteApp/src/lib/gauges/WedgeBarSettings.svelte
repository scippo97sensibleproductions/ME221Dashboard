<script lang="ts">
	import type { GaugeConfigEntry } from '../HybridBridge';
	import { WedgeStyle } from './types';
	import SegmentGeometrySettings from './SegmentGeometrySettings.svelte';

	let { gaugeDef, onchange }: {
		gaugeDef: GaugeConfigEntry;
		onchange: (def: GaugeConfigEntry) => void;
	} = $props();

	function set<K extends keyof GaugeConfigEntry>(key: K, value: GaugeConfigEntry[K]) {
		onchange({ ...gaugeDef, [key]: value });
	}

	const styles = [
		{ value: WedgeStyle.Classic, label: 'Classic', desc: 'Rising bars' },
		{ value: WedgeStyle.Stacked, label: 'Stacked', desc: 'LED blocks' },
		{ value: WedgeStyle.Needle, label: 'Needle', desc: 'Analog sweep' },
		{ value: WedgeStyle.Thermal, label: 'Thermal', desc: 'Heat colors' },
		{ value: WedgeStyle.Wire, label: 'Wire', desc: 'Blueprint' },
		{ value: WedgeStyle.Chevron, label: 'Chevron', desc: 'Arrow cascade' },
	];
</script>

<div class="space-y-4">
	<div>
		<p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Wedge Style</p>
		<div class="grid grid-cols-3 gap-1.5">
			{#each styles as s}
				<button
					class="flex flex-col items-center justify-center rounded-lg border px-2 py-3 text-center transition-all min-h-[52px]
						{(gaugeDef.wedgeStyle ?? 0) === s.value
							? 'border-cyan-500/50 bg-cyan-500/10 text-cyan-300'
							: 'border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
					onclick={() => set('wedgeStyle', s.value)}
				>
					<span class="text-xs font-medium">{s.label}</span>
					<span class="text-[9px] text-gray-500 mt-0.5">{s.desc}</span>
				</button>
			{/each}
		</div>
	</div>

	<!-- R19 — segment count (gap is fixed for wedge; slider hidden) -->
	<div class="border-t border-gray-700/30 pt-4">
		<SegmentGeometrySettings
			segmentCount={gaugeDef.wedgeSegmentCount ?? 32}
			gap={0}
			showGap={false}
			onchange={(patch) => set('wedgeSegmentCount', patch.segmentCount)}
		/>
	</div>

	<!-- R20 — redline position -->
	<div class="border-t border-gray-700/30 pt-4">
		<div class="flex items-center justify-between mb-1.5">
			<p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Redline Position</p>
			<span class="text-xs font-mono text-cyan-400">{Math.round((gaugeDef.wedgeRedlineStart ?? 0.8) * 100)}%</span>
		</div>
		<input
			type="range" step="0.01" min="0.1" max="1"
			value={gaugeDef.wedgeRedlineStart ?? 0.8}
			oninput={(e) => set('wedgeRedlineStart', parseFloat((e.target as HTMLInputElement).value))}
			class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
				[&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
				[&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
		/>
		<div class="relative mt-0.5 h-3 text-[9px] text-gray-600 overflow-visible select-none">
			<span style="position:absolute;left:0">10%</span>
			<span style="position:absolute;left:80%;transform:translateX(-50%)">80%</span>
			<span style="position:absolute;right:0">100%</span>
		</div>
	</div>
</div>

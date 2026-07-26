<script lang="ts">
	import type { GaugeConfigEntry } from '../HybridBridge';
	import { WedgeStyle } from './types';

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
</div>

<script lang="ts">
  // Shared scale-marks controls for Arc and Bar.
  let { tickCount, tickLabels, tickLabelEvery, tickSide, onchange }: {
    tickCount: number;
    tickLabels: boolean;
    tickLabelEvery: number;
    tickSide: number; // 0=inside 1=outside
    onchange: (patch: Partial<{ tickCount: number; tickLabels: boolean; tickLabelEvery: number; tickSide: number }>) => void;
  } = $props();

  function set<K extends 'tickCount' | 'tickLabels' | 'tickLabelEvery' | 'tickSide'>(key: K, value: number | boolean) {
    onchange({ [key]: value } as never);
  }
</script>

<div class="space-y-3">
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Tick Marks</p>
      <span class="text-xs font-mono text-cyan-400">{tickCount}</span>
    </div>
    <input
      type="range" step="1" min="0" max="20"
      value={tickCount}
      oninput={(e) => set('tickCount', parseInt((e.target as HTMLInputElement).value, 10))}
      class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
        [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
        [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
    />
    <p class="text-[9px] text-gray-600 mt-0.5">0 = no ticks</p>
  </div>

  {#if tickCount > 0}
    <div class="space-y-1.5">
      <button
        class="flex items-center justify-between w-full rounded px-2 py-1.5 text-[10px] font-medium transition-colors
          {tickLabels ? 'text-cyan-400 hover:text-cyan-300' : 'text-gray-500 hover:text-gray-400'}"
        onclick={() => set('tickLabels', !tickLabels)}
      >
        <span>Numeric Labels</span>
        <span class="text-[10px]">{tickLabels ? 'ON' : 'OFF'}</span>
      </button>
      {#if tickLabels}
        <div>
          <div class="flex items-center justify-between mb-1.5">
            <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Label Every</p>
            <span class="text-xs font-mono text-cyan-400">{tickLabelEvery}</span>
          </div>
          <div class="flex gap-1">
            {#each [1, 2, 3, 4, 5] as n}
              <button
                class="flex-1 rounded px-1.5 py-1.5 text-[10px] font-medium transition-colors min-h-[28px]
                  {tickLabelEvery === n
                    ? 'bg-cyan-600 text-white'
                    : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
                onclick={() => set('tickLabelEvery', n)}
              >{n}</button>
            {/each}
          </div>
        </div>
      {/if}
      {#if tickSide !== undefined}
        <div class="flex gap-1">
          <button
            class="flex-1 rounded px-2 py-1.5 text-[10px] font-medium transition-colors min-h-[28px]
              {tickSide === 0
                ? 'bg-cyan-600 text-white'
                : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
            onclick={() => set('tickSide', 0)}
          >Inside</button>
          <button
            class="flex-1 rounded px-2 py-1.5 text-[10px] font-medium transition-colors min-h-[28px]
              {tickSide === 1
                ? 'bg-cyan-600 text-white'
                : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
            onclick={() => set('tickSide', 1)}
          >Outside</button>
        </div>
      {/if}
    </div>
  {/if}
</div>

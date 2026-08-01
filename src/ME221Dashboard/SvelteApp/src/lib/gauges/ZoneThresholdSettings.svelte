<script lang="ts">
  // Shared amber/red zone-threshold controls (controlled component; used by LedRing).
  let { amber, red, onchange }: {
    amber: number;
    red: number;
    onchange: (amber: number, red: number) => void;
  } = $props();

  function setAmber(v: number) {
    onchange(Math.min(v, red), red);
  }

  function setRed(v: number) {
    onchange(amber, Math.max(v, amber));
  }
</script>

<div class="space-y-3">
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Amber Zone</p>
      <span class="text-xs font-mono text-amber-400">{Math.round(amber * 100)}%</span>
    </div>
    <input
      type="range" step="0.01" min="0" max="1"
      value={amber}
      oninput={(e) => setAmber(parseFloat((e.target as HTMLInputElement).value))}
      class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-amber-500 cursor-pointer
        [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
        [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-amber-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-amber-500/30"
    />
  </div>
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Red Zone</p>
      <span class="text-xs font-mono text-red-400">{Math.round(red * 100)}%</span>
    </div>
    <input
      type="range" step="0.01" min="0" max="1"
      value={red}
      oninput={(e) => setRed(parseFloat((e.target as HTMLInputElement).value))}
      class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-red-500 cursor-pointer
        [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
        [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-red-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-red-500/30"
    />
  </div>
  <p class="text-[9px] text-gray-600">Amber is clamped to stay at or below red.</p>
</div>

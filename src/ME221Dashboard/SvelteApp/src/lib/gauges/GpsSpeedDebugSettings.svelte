<script lang="ts">
  import { HybridBridge } from '../HybridBridge';

  let debugSpeedDraft = $state('100');
  let debugSpeedActive = $state(false);

  async function setFixedSpeed() {
    const v = parseFloat(debugSpeedDraft);
    if (isNaN(v) || v < 0) return;
    await HybridBridge.setDebugSpeed(v);
    debugSpeedActive = true;
  }

  async function resumeSimulation() {
    await HybridBridge.setDebugSpeed(null);
    debugSpeedActive = false;
  }
</script>

<div class="space-y-3">
  <p class="text-[10px] text-gray-500">Lock the simulated GPS to a constant speed for odometer verification.</p>
  <div class="flex items-center gap-2">
    <input type="number" min="0" step="10"
      bind:value={debugSpeedDraft}
      class="w-20 rounded border border-gray-600 bg-gray-800 px-2 py-2 text-center font-mono text-sm text-gray-200 focus:border-cyan-500 focus:outline-none min-h-[36px]"
    />
    <span class="text-xs text-gray-400">km/h</span>
    <button
      class="rounded px-3 py-2 text-xs font-medium transition-colors min-h-[36px]
        {debugSpeedActive ? 'bg-cyan-600 text-white' : 'bg-gray-700 text-gray-300 hover:bg-gray-600'}"
      onclick={setFixedSpeed}
    >Set</button>
    {#if debugSpeedActive}
      <button
        class="rounded bg-gray-700/80 px-3 py-2 text-xs font-medium text-gray-300 transition-colors hover:bg-gray-600 min-h-[36px]"
        onclick={resumeSimulation}
      >Resume simulation</button>
    {/if}
  </div>
  {#if debugSpeedActive}
    <p class="text-[10px] text-cyan-400">Locked at {debugSpeedDraft} km/h — at this speed, odometer increases ~{(parseFloat(debugSpeedDraft) / 60).toFixed(2)} km per minute.</p>
  {/if}
</div>

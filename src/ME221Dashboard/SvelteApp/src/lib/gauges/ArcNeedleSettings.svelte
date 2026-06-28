<script lang="ts">
  import { onMount } from 'svelte';
  import type { GaugeConfigEntry } from '../HybridBridge';
  import { HybridBridge } from '../HybridBridge';
  import NeedleCurveSettings from './NeedleCurveSettings.svelte';

  let { gaugeDef, onchange, minValue = 0, maxValue = 10000, unit = '', previewValue }: {
    gaugeDef: GaugeConfigEntry;
    onchange: (def: GaugeConfigEntry) => void;
    minValue?: number;
    maxValue?: number;
    unit?: string;
    previewValue?: number;
  } = $props();

  let texturePicking = $state(false);

  // Local draft strings for needle physical inputs — commit only on blur
  let offsetXDraft = $state('0');
  let offsetYDraft = $state('0');
  let widthDraft = $state('1');
  let lengthDraft = $state('0.1');

  onMount(() => {
    offsetXDraft = String(gaugeDef.needleOffsetX);
    offsetYDraft = String(gaugeDef.needleOffsetY);
    widthDraft = String(gaugeDef.needleWidth);
    lengthDraft = String(gaugeDef.needleLength);
  });

  function set<K extends keyof GaugeConfigEntry>(key: K, value: GaugeConfigEntry[K]) {
    onchange({ ...gaugeDef, [key]: value });
  }

  async function pickTexture() {
    texturePicking = true;
    try {
      const result = await HybridBridge.pickGaugeTexture(String(gaugeDef.entityId));
      if (result.picked && result.path) {
        set('texturePath', result.path);
      }
    } finally {
      texturePicking = false;
    }
  }

  function removeTexture() {
    set('texturePath', null);
  }
</script>

<div class="space-y-4">

  <!-- Needle Curve -->
  <div>
    <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Needle Curve</p>
    <NeedleCurveSettings
      curve={gaugeDef.needleCurve ?? []}
      {unit}
      minRaw={minValue}
      maxRaw={maxValue}
      sweepAngle={gaugeDef.sweepAngle}
      arcPosition={gaugeDef.arcPosition}
      {previewValue}
      onchange={(curve) => set('needleCurve', curve)}
    />
  </div>

  <!-- Needle Physical -->
  <div>
    <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Needle</p>
    <div class="grid grid-cols-2 gap-x-3 gap-y-2">
      <div>
        <label for="nox-{gaugeDef.entityId}" class="mb-0.5 block text-[10px] text-gray-500">Offset X</label>
        <input id="nox-{gaugeDef.entityId}" type="number" step="0.5"
          bind:value={offsetXDraft}
          onblur={() => { const v = parseFloat(offsetXDraft); if (!isNaN(v)) set('needleOffsetX', v); offsetXDraft = String(gaugeDef.needleOffsetX); }}
          class="w-full rounded border border-gray-600 bg-gray-800 px-2 py-1.5 text-xs text-gray-200 outline-none focus:border-cyan-500 min-h-[36px]" />
      </div>
      <div>
        <label for="noy-{gaugeDef.entityId}" class="mb-0.5 block text-[10px] text-gray-500">Offset Y</label>
        <input id="noy-{gaugeDef.entityId}" type="number" step="0.5"
          bind:value={offsetYDraft}
          onblur={() => { const v = parseFloat(offsetYDraft); if (!isNaN(v)) set('needleOffsetY', v); offsetYDraft = String(gaugeDef.needleOffsetY); }}
          class="w-full rounded border border-gray-600 bg-gray-800 px-2 py-1.5 text-xs text-gray-200 outline-none focus:border-cyan-500 min-h-[36px]" />
      </div>
      <div>
        <label for="nw-{gaugeDef.entityId}" class="mb-0.5 block text-[10px] text-gray-500">Width</label>
        <input id="nw-{gaugeDef.entityId}" type="number" step="0.5" min="1" max="10"
          bind:value={widthDraft}
          onblur={() => { const v = parseFloat(widthDraft); set('needleWidth', isNaN(v) ? 1 : Math.max(1, Math.min(10, v))); widthDraft = String(gaugeDef.needleWidth); }}
          class="w-full rounded border border-gray-600 bg-gray-800 px-2 py-1.5 text-xs text-gray-200 outline-none focus:border-cyan-500 min-h-[36px]" />
      </div>
      <div>
        <label for="nl-{gaugeDef.entityId}" class="mb-0.5 block text-[10px] text-gray-500">Length</label>
        <input id="nl-{gaugeDef.entityId}" type="number" step="0.05" min="0.1" max="2"
          bind:value={lengthDraft}
          onblur={() => { const v = parseFloat(lengthDraft); set('needleLength', isNaN(v) ? 0.1 : Math.max(0.1, Math.min(2, v))); lengthDraft = String(gaugeDef.needleLength); }}
          class="w-full rounded border border-gray-600 bg-gray-800 px-2 py-1.5 text-xs text-gray-200 outline-none focus:border-cyan-500 min-h-[36px]" />
      </div>
    </div>
  </div>

  <!-- Texture -->
  <div>
    <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Face Texture</p>
    <div class="flex items-center gap-2">
      <button
        class="rounded-lg border border-gray-600 px-3 py-2 text-xs font-medium text-gray-300 transition-colors hover:border-cyan-500 hover:text-cyan-300 disabled:opacity-50 min-h-[36px]"
        onclick={pickTexture}
        disabled={texturePicking}
      >
        {texturePicking ? 'Picking…' : 'Pick Image'}
      </button>
      {#if gaugeDef.texturePath}
        <button
          class="rounded-lg border border-red-600/50 px-3 py-2 text-xs font-medium text-red-400 transition-colors hover:border-red-500 hover:text-red-300 min-h-[36px]"
          onclick={removeTexture}
        >Remove</button>
      {/if}
    </div>
    {#if gaugeDef.texturePath}
      <p class="mt-1.5 text-[10px] text-gray-500 truncate max-w-[220px]">{gaugeDef.texturePath.split(/[/\\]/).pop()}</p>
    {/if}
  </div>
</div>

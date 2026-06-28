<script lang="ts">
  import type { GaugeConfigEntry } from '../HybridBridge';
  import { HybridBridge } from '../HybridBridge';
  import SmoothingSettings from './SmoothingSettings.svelte';
  import NeedleCurveSettings from './NeedleCurveSettings.svelte';

  let { gaugeDef, onchange, minValue = 0, maxValue = 10000, unit = '' }: {
    gaugeDef: GaugeConfigEntry;
    onchange: (def: GaugeConfigEntry) => void;
    minValue?: number;
    maxValue?: number;
    unit?: string;
  } = $props();

  let texturePicking = $state(false);

  function set<K extends keyof GaugeConfigEntry>(key: K, value: GaugeConfigEntry[K]) {
    onchange({ ...gaugeDef, [key]: value });
  }

  function positionToCenterAngle(pos: number): number {
    switch (pos) {
      case 0: return 270; case 1: return 315; case 2: return 0; case 3: return 45;
      case 4: return 90; case 5: return 135; case 6: return 180; case 7: return 225;
      default: return 270;
    }
  }

  function arcStartAngle(sweep: number, pos: number): number {
    const center = positionToCenterAngle(pos);
    let start = center - sweep / 2;
    if (start < 0) start += 360;
    return start;
  }

  function isDefaultNeedleCurve(): boolean {
    const curve = gaugeDef.needleCurve;
    if (!curve || curve.length === 0) return true;
    if (curve.length !== 3) return false;
    const start = arcStartAngle(gaugeDef.sweepAngle, gaugeDef.arcPosition);
    const mid = minValue + (maxValue - minValue) / 2;
    return (
      Math.abs(curve[0].rawValue - minValue) < 0.001 && Math.abs(curve[0].angle - start) < 1 &&
      Math.abs(curve[1].rawValue - mid) < 0.001 && Math.abs(curve[1].angle - (start + gaugeDef.sweepAngle / 2)) < 1 &&
      Math.abs(curve[2].rawValue - maxValue) < 0.001 && Math.abs(curve[2].angle - (start + gaugeDef.sweepAngle)) < 1
    );
  }

  function makeDefaultCurve(sweep: number, pos: number): { rawValue: number; angle: number }[] {
    const start = arcStartAngle(sweep, pos);
    const mid = minValue + (maxValue - minValue) / 2;
    return [
      { rawValue: minValue, angle: start },
      { rawValue: mid, angle: start + sweep / 2 },
      { rawValue: maxValue, angle: start + sweep },
    ];
  }

  function setSweepAngle(angle: number) {
    const start = arcStartAngle(angle, gaugeDef.arcPosition);
    const curve = isDefaultNeedleCurve() ? makeDefaultCurve(angle, gaugeDef.arcPosition) : gaugeDef.needleCurve;
    onchange({
      ...gaugeDef,
      sweepAngle: angle,
      needleStartAngle: start,
      needleEndAngle: start + angle,
      needleCurve: curve,
    });
  }

  function setArcPosition(pos: number) {
    const start = arcStartAngle(gaugeDef.sweepAngle, pos);
    const curve = isDefaultNeedleCurve() ? makeDefaultCurve(gaugeDef.sweepAngle, pos) : gaugeDef.needleCurve;
    onchange({
      ...gaugeDef,
      arcPosition: pos,
      needleStartAngle: start,
      needleEndAngle: start + gaugeDef.sweepAngle,
      needleCurve: curve,
    });
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

  // Position wheel: 8 positions around a circle, angles in degrees (0 = top, clockwise)
  const positionAngles = [0, 45, 90, 135, 180, 225, 270, 315];
  const positionLabels = ['Top', 'Top R', 'Right', 'Bot R', 'Bottom', 'Bot L', 'Left', 'Top L'];
  const WHEEL_R = 44; // radius in px from center
  const WHEEL_C = 56; // center coordinate (half of 112)

  const sweepOptions = [
    { angle: 90, label: '90°', icon: '¼' },
    { angle: 180, label: '180°', icon: '½' },
    { angle: 270, label: '270°', icon: '¾' },
    { angle: 360, label: '360°', icon: 'Full' },
  ];
</script>

<div class="space-y-4">

  <!-- Arc Geometry: Position + Sweep side by side -->
  <div class="flex gap-4 sm:gap-6">
    <!-- Position Wheel -->
    <div class="shrink-0">
      <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Position</p>
      <div class="relative" style="width: {WHEEL_C * 2}px; height: {WHEEL_C * 2}px;">
        <!-- Ring -->
        <div class="absolute inset-2 rounded-full border border-gray-600/60"></div>
        <!-- Dots -->
        {#each positionAngles as angle, i}
          {@const rad = angle * Math.PI / 180}
          {@const x = WHEEL_C + WHEEL_R * Math.sin(rad) - 10}
          {@const y = WHEEL_C - WHEEL_R * Math.cos(rad) - 10}
          <button
            class="absolute flex items-center justify-center w-5 h-5 rounded-full text-[8px] font-bold transition-all
              {gaugeDef.arcPosition === i
                ? 'bg-cyan-500 text-white shadow-lg shadow-cyan-500/40 scale-110'
                : 'bg-gray-700 text-gray-400 hover:bg-gray-600 hover:text-gray-200'}"
            style="left: {x}px; top: {y}px;"
            onclick={() => setArcPosition(i)}
            title={positionLabels[i]}
          >{i % 2 === 0 ? positionLabels[i][0] : ''}</button>
        {/each}
        <!-- Center label -->
        <div class="absolute inset-0 flex items-center justify-center pointer-events-none">
          <span class="text-[10px] font-medium text-gray-400">{positionLabels[gaugeDef.arcPosition]}</span>
        </div>
      </div>
    </div>

    <!-- Sweep Angle -->
    <div class="flex-1 min-w-0">
      <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Sweep</p>
      <div class="grid grid-cols-2 gap-1.5">
        {#each sweepOptions as opt}
          <button
            class="flex flex-col items-center justify-center rounded-lg border px-2 py-2.5 text-xs font-medium transition-all min-h-[44px]
              {gaugeDef.sweepAngle === opt.angle
                ? 'border-cyan-500/50 bg-cyan-500/10 text-cyan-300'
                : 'border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
            onclick={() => setSweepAngle(opt.angle)}
          >
            <span class="text-[10px] text-gray-500">{opt.icon}</span>
            <span>{opt.label}</span>
          </button>
        {/each}
      </div>
    </div>
  </div>

  <!-- Scale: slider with labels -->
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Scale</p>
      <span class="text-xs font-mono text-cyan-400">{gaugeDef.scale.toFixed(2)}x</span>
    </div>
    <input
      type="range" step="0.05" min="0.2" max="3"
      value={gaugeDef.scale}
      oninput={(e) => set('scale', parseFloat((e.target as HTMLInputElement).value))}
      class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
        [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
        [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
    />
    <div class="flex justify-between text-[9px] text-gray-600 mt-0.5 px-0.5">
      <span>0.2x</span>
      <span>1.0x</span>
      <span>3.0x</span>
    </div>
  </div>

  <!-- Label Offset -->
  <div>
    <div class="flex items-center justify-between mb-1.5">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Label Offset</p>
      <span class="text-xs font-mono text-gray-400">{gaugeDef.labelVerticalOffset}px</span>
    </div>
    <input
      type="range" step="1" min="-20" max="40"
      value={gaugeDef.labelVerticalOffset}
      oninput={(e) => set('labelVerticalOffset', parseInt((e.target as HTMLInputElement).value))}
      class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
        [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
        [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:shadow-cyan-500/30"
    />
  </div>

  <!-- Needle Curve -->
  <div>
    <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Needle Curve</p>
    <NeedleCurveSettings
      curve={gaugeDef.needleCurve ?? []}
      {unit}
      minRaw={minValue}
      maxRaw={maxValue}
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
          value={gaugeDef.needleOffsetX}
          oninput={(e) => { const v = parseFloat((e.target as HTMLInputElement).value); if (!isNaN(v)) set('needleOffsetX', v); }}
          onblur={(e) => { const v = parseFloat((e.target as HTMLInputElement).value); set('needleOffsetX', isNaN(v) ? 0 : v); }}
          class="w-full rounded border border-gray-600 bg-gray-800 px-2 py-1.5 text-xs text-gray-200 outline-none focus:border-cyan-500 min-h-[36px]" />
      </div>
      <div>
        <label for="noy-{gaugeDef.entityId}" class="mb-0.5 block text-[10px] text-gray-500">Offset Y</label>
        <input id="noy-{gaugeDef.entityId}" type="number" step="0.5"
          value={gaugeDef.needleOffsetY}
          oninput={(e) => { const v = parseFloat((e.target as HTMLInputElement).value); if (!isNaN(v)) set('needleOffsetY', v); }}
          onblur={(e) => { const v = parseFloat((e.target as HTMLInputElement).value); set('needleOffsetY', isNaN(v) ? 0 : v); }}
          class="w-full rounded border border-gray-600 bg-gray-800 px-2 py-1.5 text-xs text-gray-200 outline-none focus:border-cyan-500 min-h-[36px]" />
      </div>
      <div>
        <label for="nw-{gaugeDef.entityId}" class="mb-0.5 block text-[10px] text-gray-500">Width</label>
        <input id="nw-{gaugeDef.entityId}" type="number" step="0.5" min="1" max="10"
          value={gaugeDef.needleWidth}
          oninput={(e) => { const v = parseFloat((e.target as HTMLInputElement).value); if (!isNaN(v)) set('needleWidth', v); }}
          onblur={(e) => { const v = parseFloat((e.target as HTMLInputElement).value); set('needleWidth', isNaN(v) ? 1 : Math.max(1, Math.min(10, v))); }}
          class="w-full rounded border border-gray-600 bg-gray-800 px-2 py-1.5 text-xs text-gray-200 outline-none focus:border-cyan-500 min-h-[36px]" />
      </div>
      <div>
        <label for="nl-{gaugeDef.entityId}" class="mb-0.5 block text-[10px] text-gray-500">Length</label>
        <input id="nl-{gaugeDef.entityId}" type="number" step="0.05" min="0.1" max="2"
          value={gaugeDef.needleLength}
          oninput={(e) => { const v = parseFloat((e.target as HTMLInputElement).value); if (!isNaN(v)) set('needleLength', v); }}
          onblur={(e) => { const v = parseFloat((e.target as HTMLInputElement).value); set('needleLength', isNaN(v) ? 0.1 : Math.max(0.1, Math.min(2, v))); }}
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

  <!-- Smoothing -->
  <SmoothingSettings {gaugeDef} {onchange} />
</div>

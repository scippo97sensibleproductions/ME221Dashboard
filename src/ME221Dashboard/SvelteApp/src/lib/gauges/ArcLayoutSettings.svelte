<script lang="ts">
  import type { GaugeConfigEntry, NeedleCurvePoint } from '../HybridBridge';

  let { gaugeDef, onchange, minValue = 0, maxValue = 10000 }: {
    gaugeDef: GaugeConfigEntry;
    onchange: (def: GaugeConfigEntry) => void;
    minValue?: number;
    maxValue?: number;
  } = $props();

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

  function makeDefaultCurve(startAngle: number, endAngle: number): NeedleCurvePoint[] {
    return [
      { rawValue: minValue, angle: startAngle },
      { rawValue: (minValue + maxValue) / 2, angle: (startAngle + endAngle) / 2 },
      { rawValue: maxValue, angle: endAngle },
    ];
  }

  function setSweepAngle(angle: number) {
    const start = arcStartAngle(angle, gaugeDef.arcPosition);
    onchange({
      ...gaugeDef,
      sweepAngle: angle,
      needleStartAngle: start,
      needleEndAngle: start + angle,
      needleCurve: makeDefaultCurve(start, start + angle),
    });
  }

  function setArcPosition(pos: number) {
    const start = arcStartAngle(gaugeDef.sweepAngle, pos);
    onchange({
      ...gaugeDef,
      arcPosition: pos,
      needleStartAngle: start,
      needleEndAngle: start + gaugeDef.sweepAngle,
      needleCurve: makeDefaultCurve(start, start + gaugeDef.sweepAngle),
    });
  }

  const positionAngles = [0, 45, 90, 135, 180, 225, 270, 315];
  const positionLabels = ['Top', 'Top R', 'Right', 'Bot R', 'Bottom', 'Bot L', 'Left', 'Top L'];
  const WHEEL_R = 44;
  const WHEEL_C = 56;

  const sweepOptions = [
    { angle: 90, label: '90°', icon: '¼' },
    { angle: 180, label: '180°', icon: '½' },
    { angle: 270, label: '270°', icon: '¾' },
    { angle: 360, label: '360°', icon: 'Full' },
  ];
</script>

<div class="space-y-4">

  <!-- Position + Sweep side by side -->
  <div class="flex gap-4 sm:gap-6">
    <div class="shrink-0">
      <p class="mb-2 text-[10px] font-semibold uppercase tracking-wider text-gray-500">Position</p>
      <div class="relative" style="width: {WHEEL_C * 2}px; height: {WHEEL_C * 2}px;">
        <div class="absolute inset-2 rounded-full border border-gray-600/60"></div>
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
        <div class="absolute inset-0 flex items-center justify-center pointer-events-none">
          <span class="text-[10px] font-medium text-gray-400">{positionLabels[gaugeDef.arcPosition]}</span>
        </div>
      </div>
    </div>

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
</div>

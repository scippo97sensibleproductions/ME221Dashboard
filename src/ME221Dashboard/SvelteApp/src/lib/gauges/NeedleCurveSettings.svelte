<script lang="ts">
  import type { NeedleCurvePoint } from './types';
  import { interpolateNeedleAngle, positionToCenterAngle } from './types';
  import { IconPlus, IconTrash, IconTarget } from '@tabler/icons-svelte';
  import NumberInput from '../NumberInput.svelte';

  let { curve, unit = '', minRaw = 0, maxRaw = 10000, sweepAngle = 270, arcPosition = 0, previewValue, onchange }: {
    curve: NeedleCurvePoint[];
    unit?: string;
    minRaw?: number;
    maxRaw?: number;
    sweepAngle?: number;
    arcPosition?: number;
    previewValue?: number;
    onchange: (curve: NeedleCurvePoint[]) => void;
  } = $props();

  function toSorted(pts: NeedleCurvePoint[]): NeedleCurvePoint[] {
    return [...pts].sort((a, b) => a.rawValue - b.rawValue);
  }

  let draft = $state<NeedleCurvePoint[]>([]);
  let _lastEmitted: NeedleCurvePoint[] | undefined = undefined;

  $effect(() => {
    if (curve !== _lastEmitted) {
      draft = toSorted(curve);
      _lastEmitted = curve;
    }
  });

  function emit() {
    const sorted = toSorted(draft);
    draft = sorted;
    _lastEmitted = [...sorted];
    onchange([...sorted]);
  }

  function setAngle(i: number, v: number) {
    draft[i] = { ...draft[i], angle: v };
    emit();
  }

  function removePoint(idx: number) {
    if (idx === 0 || idx === draft.length - 1) return;
    emit();
    draft = draft.filter((_, i) => i !== idx);
    emit();
  }

  function resetToDefault() {
    const start = 135;
    const sweep = 270;
    const mid = minRaw + (maxRaw - minRaw) / 2;
    draft = [
      { rawValue: minRaw, angle: start },
      { rawValue: mid, angle: start + sweep / 2 },
      { rawValue: maxRaw, angle: start + sweep },
    ];
    emit();
  }

  const arcStart = $derived(positionToCenterAngle(arcPosition) - sweepAngle / 2);

  const currentAngle = $derived(
    previewValue !== undefined ? interpolateNeedleAngle(previewValue, draft) : null
  );

  function captureExistingPoint(i: number) {
    if (currentAngle === null) return;
    draft[i] = { ...draft[i], angle: currentAngle };
    emit();
  }

  function addPointAtPreview() {
    if (currentAngle === null || previewValue === undefined) return;
    const newPoint = { rawValue: previewValue, angle: currentAngle };
    const next = [...draft, newPoint];
    draft = toSorted(next);
    emit();
  }

  const extrapolatedAngle = $derived.by(() => {
    if (draft.length < 3) return null;
    const sorted = toSorted(draft);
    const maxPt = sorted[sorted.length - 1];
    const prev = sorted[sorted.length - 2];
    const before = sorted[sorted.length - 3];
    const dx = prev.rawValue - before.rawValue;
    if (dx === 0) return null;
    const slope = (prev.angle - before.angle) / dx;
    return prev.angle + slope * (maxPt.rawValue - prev.rawValue);
  });

  function applyExtrapolation() {
    if (extrapolatedAngle === null) return;
    const lastIdx = draft.length - 1;
    draft[lastIdx] = { ...draft[lastIdx], angle: extrapolatedAngle };
    emit();
  }

  function miniArcPath(cx: number, cy: number, r: number, start: number, end: number): string {
    const sr = start * Math.PI / 180;
    const er = end * Math.PI / 180;
    const x1 = cx + r * Math.cos(sr);
    const y1 = cy + r * Math.sin(sr);
    const x2 = cx + r * Math.cos(er);
    const y2 = cy + r * Math.sin(er);
    let diff = end - start;
    if (diff < 0) diff += 360;
    return `M ${x1} ${y1} A ${r} ${r} 0 ${diff > 180 ? 1 : 0} 1 ${x2} ${y2}`;
  }
</script>

<div class="space-y-3">
  <!-- Current preview info -->
  {#if currentAngle !== null && previewValue !== undefined}
    <div class="rounded-lg border border-cyan-500/30 bg-cyan-500/5 p-3">
      <div class="flex items-center justify-between">
        <div>
          <div class="text-[10px] font-semibold uppercase tracking-wider text-cyan-500/70">Preview Position</div>
          <div class="text-sm font-mono font-bold text-cyan-400 tabular-nums">
            {previewValue.toFixed(1)}{unit ? ` ${unit}` : ''} → {currentAngle.toFixed(1)}°
          </div>
        </div>
        <button
          class="flex items-center gap-1.5 rounded-lg border border-cyan-500/50 px-3 py-2 text-xs font-medium text-cyan-300 transition-colors hover:bg-cyan-500/10 active:bg-cyan-500/20 min-h-[44px]"
          onclick={addPointAtPreview}
        >
          <IconTarget size={14} />
          Add Point
        </button>
      </div>
    </div>
  {/if}

  <!-- Curve points -->
  <div class="space-y-2">
    {#each draft as pt, i (i)}
      {@const isFirst = i === 0}
      {@const isLast = i === draft.length - 1}
      {@const needleAngle = interpolateNeedleAngle(pt.rawValue, draft)}

      <div class="rounded-lg border border-gray-700/50 bg-gray-800/30 p-3 space-y-2.5">
        <!-- Header row: gauge preview + index label + delete -->
        <div class="flex items-center gap-3">
          <svg class="shrink-0" width="40" height="40" viewBox="0 0 48 48">
            <path d={miniArcPath(24, 24, 18, arcStart, arcStart + sweepAngle)}
                  fill="none" stroke="#374151" stroke-width="3" stroke-linecap="round" />
            <line x1={24} y1={24}
                  x2={24 + 12 * Math.cos(needleAngle * Math.PI / 180)}
                  y2={24 + 12 * Math.sin(needleAngle * Math.PI / 180)}
                  stroke="#22d3ee" stroke-width="1.5" stroke-linecap="round" />
            <circle cx={24} cy={24} r="2" fill="#22d3ee" />
          </svg>

          <div class="flex-1 min-w-0">
            <span class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">
              {isFirst ? 'Min' : isLast ? 'Max' : `Point ${i}`}
            </span>
            <div class="text-xs font-mono text-gray-400 tabular-nums">
              {pt.rawValue.toFixed(1)}{unit ? ` ${unit}` : ''} → {pt.angle.toFixed(0)}°
            </div>
          </div>

          <!-- Set Angle from preview -->
          {#if currentAngle !== null}
            <button
              class="rounded-lg border border-gray-600 px-2.5 py-1.5 text-[10px] font-medium text-gray-400 transition-colors hover:border-cyan-500 hover:text-cyan-300 active:bg-gray-700 min-h-[36px] flex items-center gap-1"
              onclick={() => captureExistingPoint(i)}
              title="Set this point to the current preview position"
            >
              <IconTarget size={12} />
              Set
            </button>
          {/if}

          <!-- Extrapolate button on Max card -->
          {#if isLast && extrapolatedAngle !== null}
            <button
              class="rounded-lg border border-amber-500/50 px-2.5 py-1.5 text-[10px] font-medium text-amber-400 transition-colors hover:bg-amber-500/10 active:bg-amber-500/20 min-h-[36px] flex items-center gap-1"
              onclick={applyExtrapolation}
              title="Extrapolate angle from last 2 points trend"
            >
              Extrapolate → {extrapolatedAngle.toFixed(0)}°
            </button>
          {/if}

          <button
            class="rounded-lg p-2 text-gray-500 active:bg-gray-700 active:text-red-400 transition-colors disabled:opacity-20 disabled:cursor-not-allowed min-w-[40px] min-h-[40px] flex items-center justify-center"
            disabled={isFirst || isLast}
            onclick={() => removePoint(i)}
          >
            <IconTrash size={16} />
          </button>
        </div>

        <!-- Angle (editable for all points) -->
        <div>
          <div class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-1">
            Needle Angle
          </div>
          <NumberInput
            value={pt.angle}
            step={1}
            unit="°"
            onchange={(v) => setAngle(i, v)}
          />
        </div>

        <!-- Value -->
        <div>
          <div class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-1">
            Value {unit ? `(${unit})` : ''}
          </div>
          <NumberInput
            value={pt.rawValue}
            {unit}
            disabled={isFirst || isLast}
            onchange={(v) => { draft[i] = { ...draft[i], rawValue: v }; emit(); }}
          />
        </div>
      </div>
    {/each}
  </div>

  <!-- Actions -->
  <div class="flex items-center gap-2">
    <button
      class="rounded-lg border border-gray-600 px-3 py-2 text-xs font-medium text-gray-400 transition-colors hover:border-gray-500 hover:text-gray-200 min-h-[44px]"
      onclick={resetToDefault}
    >
      Reset
    </button>
  </div>
</div>

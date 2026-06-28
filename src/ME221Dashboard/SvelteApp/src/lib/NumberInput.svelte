<script lang="ts">
  import { IconMinus, IconPlus } from '@tabler/icons-svelte';

  let {
    value,
    min = -Infinity,
    max = Infinity,
    step,
    unit = '',
    disabled = false,
    onchange,
  }: {
    value: number;
    min?: number;
    max?: number;
    step?: number;
    unit?: string;
    disabled?: boolean;
    onchange: (v: number) => void;
  } = $props();

  let draft = $state('0');
  let _lastEmitted: number | null = $state(null);

  $effect(() => {
    if (_lastEmitted === null || value !== _lastEmitted) {
      draft = String(value);
      _lastEmitted = value;
    }
  });

  function commit() {
    const v = parseFloat(draft);
    if (!isNaN(v)) {
      const clamped = Math.max(min, Math.min(max, v));
      _lastEmitted = clamped;
      draft = String(clamped);
      onchange(clamped);
    } else {
      draft = String(_lastEmitted);
    }
  }

  function nudge(delta: number) {
    const v = Math.max(min, Math.min(max, value + delta));
    _lastEmitted = v;
    draft = String(v);
    onchange(v);
  }

  const range = $derived(max - min > 0 ? max - min : 1);
  const magSmall = $derived(step ?? (range >= 100 ? 1 : range >= 10 ? 0.1 : range >= 1 ? 0.01 : 0.001));
  const magMed = $derived(Math.round(magSmall * 10 * 1000) / 1000);
  const magBig = $derived(Math.round(magSmall * 100 * 1000) / 1000);

  let activeMag = $state<'small' | 'med' | 'big'>('small');
  const currentDelta = $derived(activeMag === 'big' ? magBig : activeMag === 'med' ? magMed : magSmall);

  function formatMag(m: number): string {
    if (m >= 1000) return `${m / 1000}k`;
    if (m >= 1) return String(m);
    return m.toFixed(3).replace(/0+$/, '').replace(/\.$/, '');
  }

  function numberNoWheel(node: HTMLInputElement) {
    function handle(e: WheelEvent) { e.preventDefault(); }
    node.addEventListener('wheel', handle, { passive: false });
    return { destroy() { node.removeEventListener('wheel', handle); } };
  }
</script>

<div class="space-y-1">
  <!-- Row: [-] [input] [+] — always 3 elements, no overflow -->
  <div class="flex items-stretch gap-1">
    <button
      class="shrink-0 rounded-lg border border-gray-600 px-3 text-gray-400 active:bg-gray-700 active:text-white transition-colors disabled:opacity-30 disabled:cursor-not-allowed min-h-[44px] min-w-[44px] flex items-center justify-center flex-1 max-w-[52px]"
      disabled={disabled}
      onclick={() => nudge(-currentDelta)}
    ><IconMinus size={18} /></button>

    <div class="flex-1 min-w-0 flex items-center rounded-lg border border-gray-600 bg-gray-800 overflow-hidden">
      <input type="number" step={magSmall}
        value={draft}
        oninput={(e) => { draft = (e.target as HTMLInputElement).value; }}
        onblur={commit}
        onkeydown={(e) => { if (e.key === 'Enter') (e.target as HTMLInputElement).blur(); }}
        use:numberNoWheel
        {disabled}
        class="w-full min-w-0 bg-transparent px-2 py-2.5 text-center text-base font-mono font-bold text-gray-200 outline-none disabled:opacity-40 min-h-[44px] tabular-nums" />
      {#if unit}
        <span class="px-2 text-xs text-gray-500 bg-gray-750/50 shrink-0 text-center">{unit}</span>
      {/if}
    </div>

    <button
      class="shrink-0 rounded-lg border border-gray-600 px-3 text-gray-400 active:bg-gray-700 active:text-white transition-colors disabled:opacity-30 disabled:cursor-not-allowed min-h-[44px] min-w-[44px] flex items-center justify-center flex-1 max-w-[52px]"
      disabled={disabled}
      onclick={() => nudge(currentDelta)}
    ><IconPlus size={18} /></button>
  </div>

  <!-- Magnitude selector below — separate row, not cramped -->
  <div class="flex gap-1 justify-center">
    {#each [
      { key: 'small' as const, label: formatMag(magSmall) },
      { key: 'med' as const, label: formatMag(magMed) },
      { key: 'big' as const, label: formatMag(magBig) },
    ] as opt}
      <button
        class="rounded-md px-3 py-1.5 text-[11px] font-bold transition-colors min-h-[32px] tabular-nums
          {activeMag === opt.key
            ? 'bg-cyan-600 text-white'
            : 'bg-gray-700 text-gray-400 active:bg-gray-600'}"
        onclick={() => { activeMag = opt.key; }}
      >{opt.label}</button>
    {/each}
  </div>
</div>

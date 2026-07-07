<script lang="ts">
  import { ValueTransformOperation, applyTransform, stepError, isTransformable } from './transformUtils';
  import type { ValueTransformStep } from './transformUtils';
  import { formatValue } from './types';
  import { IconX, IconPlus, IconChevronUp, IconChevronDown } from '@tabler/icons-svelte';

  let { gaugeDef, entityInfo, onchange }: {
    gaugeDef: { entityId: number; transformSteps?: ValueTransformStep[]; customUnitLabel?: string | null };
    entityInfo: { name: string; unit: string; minValue?: number; maxValue?: number } | null;
    onchange: (def: any) => void;
  } = $props();

  let steps = $state<ValueTransformStep[]>(
    gaugeDef.transformSteps?.map(s => ({ ...s })) ?? []
  );
  let customUnit = $state(gaugeDef.customUnitLabel ?? '');

  const eligible = $derived(isTransformable(gaugeDef.entityId));
  const rawPreviewValue = $derived(
    entityInfo ? ((entityInfo.minValue ?? 0) + (entityInfo.maxValue ?? 10000)) / 2 : 5000
  );
  const transformedPreview = $derived(
    steps.length > 0 ? applyTransform(rawPreviewValue, steps) : rawPreviewValue
  );
  const previewIsValid = $derived(Number.isFinite(transformedPreview));

  function emit() {
    onchange({
      ...gaugeDef,
      transformSteps: steps,
      customUnitLabel: customUnit || null,
    });
  }

  function addStep() {
    steps = [...steps, { operation: ValueTransformOperation.Multiply, operand: 1 }];
    emit();
  }

  function removeStep(i: number) {
    steps = steps.filter((_, idx) => idx !== i);
    emit();
  }

  function moveStep(i: number, dir: -1 | 1) {
    const j = i + dir;
    if (j < 0 || j >= steps.length) return;
    const next = [...steps];
    [next[i], next[j]] = [next[j], next[i]];
    steps = next;
    emit();
  }

  function updateOp(i: number, op: number) {
    const next = [...steps];
    next[i] = op === ValueTransformOperation.InvertSign
      ? { operation: op, operand: 0 }
      : { ...next[i], operation: op as ValueTransformOperation };
    steps = next;
    emit();
  }

  function updateOperand(i: number, val: string) {
    const num = parseFloat(val);
    if (!Number.isFinite(num)) return;
    const next = [...steps];
    next[i] = { ...next[i], operand: num };
    steps = next;
    emit();
  }

  function clearCustomUnit() {
    customUnit = '';
    emit();
  }

  const operations = [
    { value: ValueTransformOperation.Multiply, label: 'Multiply by' },
    { value: ValueTransformOperation.Add, label: 'Add' },
    { value: ValueTransformOperation.Divide, label: 'Divide by' },
    { value: ValueTransformOperation.MinClamp, label: 'Min clamp' },
    { value: ValueTransformOperation.MaxClamp, label: 'Max clamp' },
    { value: ValueTransformOperation.InvertSign, label: 'Invert sign' },
  ];
</script>

{#if !eligible}
  <p class="text-xs text-gray-500 py-2">Transforms are not available for this gauge type.</p>
{:else}
  <!-- Custom unit label -->
  <div class="mb-3">
    <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-1">Unit Label Override</p>
    <div class="relative">
      <input
        type="text"
        maxlength="50"
        value={customUnit}
        oninput={(e) => { customUnit = (e.target as HTMLInputElement).value; emit(); }}
        placeholder={entityInfo?.unit ?? 'Default'}
        class="w-full rounded bg-gray-700/80 px-3 py-2 pr-7 text-sm text-gray-100 placeholder-gray-500 outline-none focus:ring-1 focus:ring-cyan-500/50"
      />
      {#if customUnit}
        <button
          class="absolute right-1.5 top-1/2 -translate-y-1/2 rounded p-1 text-gray-400 hover:text-gray-200 hover:bg-gray-600"
          onclick={clearCustomUnit}
        >
          <IconX size={12} />
        </button>
      {/if}
    </div>
  </div>

  <!-- Operation list -->
  <div class="space-y-2 mb-3">
    {#each steps as step, i (i)}
      <div class="flex items-center gap-1.5 rounded bg-gray-800/60 px-2 py-1.5">
        <div class="flex flex-col gap-0.5">
          <button
            class="rounded p-0.5 text-gray-500 hover:text-gray-300 disabled:opacity-30 disabled:cursor-not-allowed"
            onclick={() => moveStep(i, -1)}
            disabled={i === 0}
          >
            <IconChevronUp size={12} />
          </button>
          <button
            class="rounded p-0.5 text-gray-500 hover:text-gray-300 disabled:opacity-30 disabled:cursor-not-allowed"
            onclick={() => moveStep(i, 1)}
            disabled={i === steps.length - 1}
          >
            <IconChevronDown size={12} />
          </button>
        </div>

        <select
          value={step.operation}
          onchange={(e) => updateOp(i, parseInt((e.target as HTMLSelectElement).value))}
          class="rounded bg-gray-700/80 px-2 py-1.5 text-xs text-gray-200 outline-none focus:ring-1 focus:ring-cyan-500/50 min-w-0"
        >
          {#each operations as op}
            <option value={op.value}>{op.label}</option>
          {/each}
        </select>

        {#if step.operation !== ValueTransformOperation.InvertSign}
          <input
            type="number"
            step="any"
            value={step.operand}
            oninput={(e) => updateOperand(i, (e.target as HTMLInputElement).value)}
            class="w-20 rounded bg-gray-700/80 px-2 py-1.5 text-xs font-mono text-gray-200 outline-none focus:ring-1 focus:ring-cyan-500/50
              {stepError(step) ? 'ring-1 ring-red-500/50' : ''}"
          />
        {:else}
          <span class="w-20 text-[10px] text-gray-500 italic">—</span>
        {/if}

        <button
          class="ml-auto shrink-0 rounded p-1 text-gray-500 hover:text-red-400 hover:bg-gray-700"
          onclick={() => removeStep(i)}
        >
          <IconX size={14} />
        </button>
      </div>
      {#if stepError(step)}
        <p class="text-[10px] text-red-400 ml-8 -mt-1">{stepError(step)}</p>
      {/if}
    {/each}
  </div>

  {#if steps.length === 0}
    <p class="text-xs text-gray-500 mb-2">Add your first operation</p>
  {/if}

  <button
    class="flex items-center gap-1.5 rounded border border-dashed border-gray-600 px-3 py-2 text-xs font-medium text-gray-400 transition-colors hover:border-cyan-500 hover:text-cyan-300 w-full justify-center min-h-[36px] disabled:opacity-30 disabled:cursor-not-allowed disabled:hover:border-gray-600 disabled:hover:text-gray-400"
    onclick={addStep}
    disabled={steps.length >= 20}
  >
    <IconPlus size={14} />
    Add operation
  </button>
  {#if steps.length >= 20}
    <p class="text-[10px] text-gray-500 mt-1">Maximum 20 operations</p>
  {/if}

  <!-- Preview -->
  {#if steps.length > 0}
    <div class="mt-3 rounded bg-gray-800/60 px-3 py-2">
      <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-1">Preview</p>
      <div class="flex items-baseline gap-2 text-sm font-mono">
        <span class="text-gray-400">{rawPreviewValue.toFixed(1)}</span>
        <span class="text-gray-600">→</span>
        {#if previewIsValid}
          <span class="text-cyan-300 font-bold">{transformedPreview.toFixed(1)}</span>
          {#if customUnit}
            <span class="text-gray-500 text-xs">{customUnit}</span>
          {/if}
        {:else}
          <span class="text-gray-500">—</span>
        {/if}
      </div>
    </div>
  {/if}
{/if}

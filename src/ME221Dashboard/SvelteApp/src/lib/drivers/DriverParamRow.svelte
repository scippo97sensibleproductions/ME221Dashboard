<script lang="ts">
  import type { DriverParamDefinition } from '../HybridBridgeTypes';

  let { param, value, disabled = false, onValueChange, onOpenPrecisionModal, onOpenComboBox }: {
    param: DriverParamDefinition;
    value: number;
    disabled?: boolean;
    onValueChange: (newValue: number) => void;
    onOpenPrecisionModal: () => void;
    onOpenComboBox: () => void;
  } = $props();

  let editing = $state(false);
  let editValue = $state('');
  let validationError = $state(false);
  let longPressTimer = $state<ReturnType<typeof setTimeout> | null>(null);

  function handleTap() {
    if (param.readOnly || disabled) return;
    if (param.paramType === 'InputBox') {
      editing = true;
      editValue = formatValue(value);
    }
  }

  function handleBlur() {
    if (!editing) return;
    const parsed = parseFloat(editValue);
    if (isNaN(parsed)) {
      validationError = true;
      setTimeout(() => { validationError = false; editing = false; }, 600);
      return;
    }
    if (param.checkRange && (parsed < param.min || parsed > param.max)) {
      validationError = true;
      setTimeout(() => { validationError = false; editing = false; }, 600);
      return;
    }
    editing = false;
    if (parsed !== value) {
      onValueChange(parsed);
    }
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'Enter') {
      (e.target as HTMLInputElement).blur();
    } else if (e.key === 'Escape') {
      editing = false;
    }
  }

  function formatValue(v: number): string {
    return v.toFixed(2);
  }

  function handlePointerDown() {
    if (param.readOnly || param.paramType !== 'InputBox') return;
    longPressTimer = setTimeout(() => {
      onOpenPrecisionModal();
      longPressTimer = null;
    }, 500);
  }

  function handlePointerUp() {
    if (longPressTimer) {
      clearTimeout(longPressTimer);
      longPressTimer = null;
    }
  }

  function getOptionName(): string {
    if (!param.options) return 'No options';
    const opt = param.options.find(o => o.id === Math.round(value));
    return opt ? opt.name : `Unknown (${formatValue(value)})`;
  }
</script>

<div class="flex items-center justify-between py-2.5 px-3 {(param.readOnly || disabled) ? 'opacity-50' : ''}">
  <div class="min-w-0 flex-1 mr-3">
    <div class="flex items-center gap-1.5">
      <span class="text-sm text-gray-200 truncate">{param.displayName || param.name}</span>
      {#if param.requiresReset}
        <span class="shrink-0 rounded bg-amber-500/15 px-1 py-0.5 text-[10px] font-medium text-amber-300">Reset</span>
      {/if}
      {#if param.toolTipText}
        <span class="shrink-0 text-gray-500 text-xs cursor-help" title={param.toolTipText}>?</span>
      {/if}
    </div>
    {#if param.checkRange}
      <div class="text-[10px] text-gray-500">Range: {param.min.toFixed(2)} - {param.max.toFixed(2)}</div>
    {/if}
  </div>

  <div class="shrink-0">
    {#if param.paramType === 'ComboBox'}
      {#if param.options && param.options.length > 0}
        <button
          class="rounded-lg border border-gray-600/60 bg-gray-700/50 px-3 py-1.5 text-sm text-gray-100 transition-colors hover:border-gray-500 hover:bg-gray-700"
          onclick={() => onOpenComboBox()}
        >
          {getOptionName()}
        </button>
      {:else}
        <span class="text-sm text-gray-500 italic">No options</span>
      {/if}
    {:else if param.paramType === 'InputBox'}
      {#if editing}
        <input
          type="text"
          bind:value={editValue}
          onblur={handleBlur}
          onkeydown={handleKeydown}
          class="w-24 rounded border {validationError ? 'border-red-500 bg-red-500/10' : 'border-emerald-500 bg-gray-800'} px-2 py-1 text-sm text-gray-100 outline-none"
        />
      {:else}
        <div
          class="flex items-center gap-1 cursor-pointer rounded-lg border border-gray-600/60 bg-gray-700/50 px-3 py-1.5 transition-colors hover:border-gray-500 hover:bg-gray-700"
          role="button"
          tabindex="-1"
          onclick={handleTap}
          onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); handleTap(); } }}
          onpointerdown={handlePointerDown}
          onpointerup={handlePointerUp}
          onpointerleave={handlePointerUp}
        >
          <span class="text-sm text-gray-100 tabular-nums">{formatValue(value)}</span>
          <button
            class="ml-1 text-gray-400 hover:text-gray-200"
            onclick={(e) => { e.stopPropagation(); onOpenPrecisionModal(); }}
            title="Precision edit"
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"/></svg>
          </button>
        </div>
      {/if}
    {:else}
      <span class="text-sm text-gray-400 tabular-nums">{formatValue(value)}</span>
    {/if}
  </div>
</div>

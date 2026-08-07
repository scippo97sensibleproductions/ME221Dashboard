<script lang="ts">
  import { IconChevronDown, IconCheck, IconSearch } from '@tabler/icons-svelte';
  import type { DriverParamDefinition } from '../HybridBridgeTypes';
  import { formatDriverValue, unitSuffix } from './driverUnits';

  let {
    param,
    value,
    disabled = false,
    dirty = false,
    disabledReason = '',
    onValueChange,
  }: {
    param: DriverParamDefinition;
    value: number;
    disabled?: boolean;
    dirty?: boolean;
    disabledReason?: string;
    onValueChange: (newValue: number) => void;
  } = $props();

  const inactive = $derived(disabled || param.readOnly);

  let editing = $state(false);
  let editValue = $state('');
  let validationError = $state('');
  let showList = $state(false);
  let searchQuery = $state('');
  let inputEl: HTMLInputElement | null = $state(null);
  let holdTimer: ReturnType<typeof setTimeout> | null = null;
  let holdInterval: ReturnType<typeof setInterval> | null = null;

  $effect(() => {
    if (editing && inputEl) {
      inputEl.focus();
      inputEl.select();
    }
  });

  const isComboBox = $derived(param.paramType === 'ComboBox');
  const isInputBox = $derived(param.paramType === 'InputBox');

  const label = $derived(param.displayName || param.name);
  const unitLabel = $derived(unitSuffix(param.measurementUnitTypes));
  const rangeCaption = $derived(
    param.checkRange ? `Range: ${formatDriverValue(param.min)} – ${formatDriverValue(param.max)}` : ''
  );

  function clampToRange(v: number): number {
    if (param.checkRange && param.min < param.max) {
      return Math.min(param.max, Math.max(param.min, v));
    }
    return v;
  }

  function stepFor(): number {
    if (param.checkRange && param.max > param.min) {
      const step = (param.max - param.min) / 100;
      return Math.min(5, Math.max(0.01, parseFloat(step.toPrecision(1))));
    }
    const abs = Math.abs(value);
    if (abs < 10) return 0.1;
    if (abs < 100) return 1;
    if (abs < 1000) return 10;
    return 100;
  }

  function applyStep(dir: 1 | -1) {
    const next = clampToRange(parseFloat((value + dir * stepFor()).toPrecision(10)));
    if (next !== value) onValueChange(next);
  }

  function startHold(dir: 1 | -1) {
    applyStep(dir);
    holdTimer = setTimeout(() => {
      holdInterval = setInterval(() => applyStep(dir), 90);
    }, 380);
  }

  function stopHold() {
    if (holdTimer) { clearTimeout(holdTimer); holdTimer = null; }
    if (holdInterval) { clearInterval(holdInterval); holdInterval = null; }
  }

  function openInput() {
    if (inactive) return;
    editing = true;
    editValue = formatDriverValue(value);
    validationError = '';
  }

  function commitInput() {
    if (!editing) return;
    const parsed = parseFloat(editValue);
    if (isNaN(parsed)) {
      validationError = 'Invalid number';
      return;
    }
    if (param.checkRange && (parsed < param.min || parsed > param.max)) {
      validationError = `Outside range (${formatDriverValue(param.min)} – ${formatDriverValue(param.max)})`;
      return;
    }
    editing = false;
    if (parsed !== value) onValueChange(parsed);
  }

  function cancelInput() {
    editing = false;
    validationError = '';
  }

  function handleInputKeydown(e: KeyboardEvent) {
    if (e.key === 'Enter') {
      (e.target as HTMLInputElement).blur();
    } else if (e.key === 'Escape') {
      cancelInput();
    }
  }

  const filteredOptions = $derived(
    searchQuery.trim()
      ? (param.options || []).filter(o => o.name.toLowerCase().includes(searchQuery.toLowerCase()))
      : (param.options || [])
  );

  function toggleList() {
    if (inactive) return;
    showList = !showList;
    if (showList) searchQuery = '';
  }

  function selectOption(optionId: number) {
    showList = false;
    if (optionId !== value) onValueChange(optionId);
  }

  function getOptionName(): string {
    const opt = (param.options || []).find(o => o.id === Math.round(value));
    return opt ? opt.name : `Unknown (${formatDriverValue(value)})`;
  }

  function handleRowKeydown(e: KeyboardEvent) {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      if (isComboBox) toggleList();
      else if (isInputBox) openInput();
    }
  }
</script>

<div
  class="relative flex items-center gap-3 border-b border-metro-border-subtle py-2 pl-3 pr-2
    {inactive ? 'opacity-50' : ''} {dirty ? 'border-l-2 border-l-yellow-400' : ''}"
  class:bg-metro-card-hover={showList}
>
  <div class="min-w-0 flex-1" role="button" tabindex="0" onclick={isInputBox ? openInput : undefined} onkeydown={handleRowKeydown}>
    <div class="flex items-center gap-1.5">
      <span class="truncate text-[13px] text-metro-text">{label}{unitLabel}</span>
      {#if param.requiresReset}
        <span class="shrink-0 bg-red-500 px-1 py-px text-[9px] font-bold uppercase tracking-wider text-white" title="Changing this parameter requires a power cycle">Pwr reset</span>
      {/if}
      {#if param.toolTipText}
        <span class="shrink-0 cursor-help text-[11px] text-metro-text-muted" title={param.toolTipText}>?</span>
      {/if}
    </div>
    {#if disabledReason && inactive}
      <div class="truncate text-[10px] text-metro-text-muted">{disabledReason}</div>
    {:else if rangeCaption}
      <div class="text-[10px] text-metro-text-muted">{rangeCaption}</div>
    {/if}
    {#if editing && validationError}
      <div class="text-[10px] text-red-400">{validationError}</div>
    {/if}
  </div>

  <div class="shrink-0">
    {#if isComboBox}
      {#if param.options && param.options.length > 0}
        <button
          class="flex h-8 items-center gap-1 border border-metro-input-border bg-metro-input-bg px-2 text-[13px] text-metro-text transition-colors hover:border-metro-green disabled:cursor-not-allowed"
          disabled={inactive}
          onclick={toggleList}
          aria-haspopup="listbox"
          aria-expanded={showList}
        >
          <span class="max-w-40 truncate tabular-nums {dirty ? 'text-yellow-400' : 'text-metro-text'}">{getOptionName()}</span>
          <IconChevronDown size={14} class={showList ? 'rotate-180 text-metro-green' : 'text-metro-text-muted'} />
        </button>
        {#if showList}
          <div class="fixed inset-0 z-30" onclick={toggleList} onkeydown={(e) => { if (e.key === 'Escape') toggleList(); }} role="presentation"></div>
          <div class="absolute z-40 mt-1 w-72 max-w-[calc(100vw-24px)] border border-metro-border bg-metro-card">
            {#if param.options.length > 8}
              <div class="relative border-b border-metro-border-subtle">
                <IconSearch size={13} class="absolute left-2 top-1/2 -translate-y-1/2 text-metro-text-muted" />
                <input
                  type="text"
                  placeholder="Search options..."
                  bind:value={searchQuery}
                  class="h-8 w-full bg-metro-input-bg py-1 pl-7 pr-2 text-[12px] text-metro-text outline-none placeholder:text-metro-text-muted"
                />
              </div>
            {/if}
            <div class="max-h-56 overflow-y-auto" role="listbox">
              {#each filteredOptions as option (option.id)}
                <button
                  class="flex w-full items-center gap-2 px-2.5 py-2 text-left text-[13px] transition-colors hover:bg-metro-card-hover
                    {option.id === Math.round(value) ? 'bg-metro-green/15 text-metro-green' : 'text-metro-text'}"
                  onclick={() => selectOption(option.id)}
                  role="option"
                  aria-selected={option.id === Math.round(value)}
                >
                  <span class="flex-1 truncate">{option.name}</span>
                  {#if option.id === Math.round(value)}
                    <IconCheck size={14} />
                  {/if}
                </button>
              {:else}
                <div class="px-2.5 py-2 text-[12px] text-metro-text-muted">No options match</div>
              {/each}
            </div>
          </div>
        {/if}
      {:else}
        <span class="text-[13px] text-metro-text-muted">No options</span>
      {/if}
    {:else if isInputBox}
      {#if editing}
        <input
          type="text"
          inputmode="decimal"
          bind:this={inputEl}
          bind:value={editValue}
          onblur={commitInput}
          onkeydown={handleInputKeydown}
          class="h-8 w-28 border bg-metro-input-bg px-2 text-right font-mono text-[13px] text-metro-text outline-none
            {validationError ? 'border-red-500' : 'border-metro-green'}"
        />
      {:else}
        <div class="flex items-center gap-1">
          <button
            class="flex h-8 w-8 items-center justify-center border border-metro-input-border bg-metro-input-bg text-metro-text-secondary transition-colors hover:bg-metro-card-hover hover:text-metro-text disabled:cursor-not-allowed"
            style="touch-action: none; user-select: none;"
            disabled={inactive}
            aria-label="Decrease value"
            onpointerdown={() => startHold(-1)}
            onpointerup={stopHold}
            onpointerleave={stopHold}
            onpointercancel={stopHold}
          >−</button>
          <button
            class="flex h-8 min-w-16 items-center justify-end border border-metro-input-border bg-metro-input-bg px-2 font-mono text-[13px] tabular-nums transition-colors hover:border-metro-green disabled:cursor-not-allowed {dirty ? 'text-yellow-400' : 'text-metro-text'}"
            disabled={inactive}
            onclick={openInput}
            aria-label="Edit value"
          >{formatDriverValue(value)}</button>
          <button
            class="flex h-8 w-8 items-center justify-center border border-metro-input-border bg-metro-input-bg text-metro-text-secondary transition-colors hover:bg-metro-card-hover hover:text-metro-text disabled:cursor-not-allowed"
            style="touch-action: none; user-select: none;"
            disabled={inactive}
            aria-label="Increase value"
            onpointerdown={() => startHold(1)}
            onpointerup={stopHold}
            onpointerleave={stopHold}
            onpointercancel={stopHold}
          >+</button>
        </div>
      {/if}
    {:else}
      <span class="font-mono text-[13px] tabular-nums text-metro-text">{formatDriverValue(value)}</span>
    {/if}
  </div>
</div>

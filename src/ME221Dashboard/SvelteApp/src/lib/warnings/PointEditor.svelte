<script lang="ts">
  import { IconPlus, IconTrash } from '@tabler/icons-svelte';
  import type { WarningLevel, WarningPoint } from '../HybridBridgeTypes';

  let { points, levels, minValue, maxValue, onChange }: {
    points: WarningPoint[];
    levels: WarningLevel[];
    minValue: number | null;
    maxValue: number | null;
    onChange: (points: WarningPoint[]) => void;
  } = $props();

  let valueDrafts = $state<Record<string, string>>({});
  let draftError = $state<string | null>(null);
  let pickerError = $state<string | null>(null);
  let addError = $state<string | null>(null);

  const orderedLevels = $derived([...levels].sort((a, b) => a.order - b.order));
  const firstLevelId = $derived(orderedLevels[0]?.id ?? null);

  function isDuplicate(value: number, direction: 'min' | 'max', levelId: string, excludeId?: string): boolean {
    return points.some(p =>
      p.id !== excludeId && p.value === value && p.direction === direction && p.levelId === levelId
    );
  }

  function clampValue(value: number): number {
    if (minValue != null && value < minValue) return minValue;
    if (maxValue != null && value > maxValue) return maxValue;
    return value;
  }

  function parseDraft(raw: string): number | null {
    const trimmed = raw.trim();
    if (!trimmed) return null;
    const num = Number(trimmed);
    return Number.isFinite(num) ? num : null;
  }

  function addPoint() {
    addError = null;
    if (!firstLevelId) {
      addError = 'Create a level first to add points.';
      return;
    }
    const raw = valueDrafts['__new__'] ?? '';
    const value = parseDraft(raw);
    if (value === null) {
      addError = 'Enter a numeric value to add this point.';
      return;
    }
    const clamped = clampValue(value);
    const levelId = firstLevelId;
    if (isDuplicate(clamped, 'max', levelId)) {
      addError = 'A point with this value, direction and level already exists.';
      return;
    }
    const point: WarningPoint = {
      id: '',
      value: clamped,
      direction: 'max',
      levelId,
      enabled: true,
    };
    onChange([...points, point]);
    valueDrafts = { ...valueDrafts, __new__: '' };
  }

  function commitValue(point: WarningPoint, raw: string) {
    const value = parseDraft(raw);
    if (value === null) {
      delete valueDrafts[point.id];
      valueDrafts = { ...valueDrafts };
      draftError = 'Enter a numeric value.';
      return;
    }
    const clamped = clampValue(value);
    const next = { ...valueDrafts };
    delete next[point.id];
    valueDrafts = next;
    if (isDuplicate(clamped, point.direction, point.levelId, point.id)) {
      draftError = 'A point with this value, direction and level already exists.';
      delete valueDrafts[point.id];
      return;
    }
    draftError = null;
    if (clamped !== point.value) {
      onChange(points.map(p => (p.id === point.id ? { ...p, value: clamped } : p)));
    }
  }

  function toggleDirection(point: WarningPoint) {
    const direction = point.direction === 'max' ? 'min' : 'max';
    if (isDuplicate(point.value, direction, point.levelId, point.id)) {
      draftError = 'A point with this value, direction and level already exists.';
      return;
    }
    draftError = null;
    onChange(points.map(p => (p.id === point.id ? { ...p, direction } : p)));
  }

  function togglePointEnabled(point: WarningPoint) {
    onChange(points.map(p => (p.id === point.id ? { ...p, enabled: !p.enabled } : p)));
  }

  function changeLevel(point: WarningPoint, levelId: string) {
    pickerError = null;
    if (isDuplicate(point.value, point.direction, levelId, point.id)) {
      pickerError = 'A point with this value, direction and level already exists.';
      return; // picker reverts
    }
    onChange(points.map(p => (p.id === point.id ? { ...p, levelId } : p)));
  }

  function removePoint(point: WarningPoint) {
    onChange(points.filter(p => p.id !== point.id));
  }
</script>

<div class="rounded border" style="border-color: var(--metro-border);">
  {#if levels.length === 0}
    <p class="px-3 py-4 text-xs" style="color: var(--metro-text-muted);">
      Create a level first to add points.
    </p>
  {:else}
    {#each points as point (point.id)}
      <div class="flex min-h-11 items-center gap-2 border-b px-3 py-1.5 last:border-b-0" style="border-color: var(--metro-border);">
        <button
          class="h-11 shrink-0 rounded border px-2 text-[11px] font-medium transition-colors"
          style="border-color: var(--metro-border); color: var(--metro-text);"
          onclick={() => toggleDirection(point)}
          aria-label="Trigger direction: {point.direction === 'max' ? 'above' : 'below'}"
        >
          {point.direction === 'max' ? '>' : '<'}
        </button>
        <input
          type="number"
          step="any"
          value={valueDrafts[point.id] ?? point.value}
          oninput={(e) => { valueDrafts = { ...valueDrafts, [point.id]: (e.target as HTMLInputElement).value }; }}
          onblur={(e) => commitValue(point, (e.target as HTMLInputElement).value)}
          onkeydown={(e) => { if (e.key === 'Enter') commitValue(point, (e.target as HTMLInputElement).value); }}
          class="h-11 w-20 rounded border bg-transparent px-2 text-xs outline-none"
          style="border-color: var(--metro-border); color: var(--metro-text);"
        />
        <select
          value={point.levelId}
          class="h-11 min-w-0 flex-1 rounded border bg-transparent px-1 text-xs outline-none"
          style="border-color: var(--metro-border); color: var(--metro-text);"
          onchange={(e) => changeLevel(point, (e.target as HTMLSelectElement).value)}
          aria-label="Level for this point"
        >
          {#each orderedLevels as level (level.id)}
            <option value={level.id}>{level.name}</option>
          {/each}
        </select>
        <label class="flex h-11 shrink-0 items-center gap-1 text-[11px]" style="color: var(--metro-text-secondary);">
          <input
            type="checkbox"
            checked={point.enabled}
            onchange={() => togglePointEnabled(point)}
            class="h-4 w-4 accent-amber-500"
          />
          On
        </label>
        <button
          class="flex h-11 w-11 shrink-0 items-center justify-center rounded transition-colors hover:bg-gray-800"
          style="color: var(--metro-text-muted);"
          aria-label="Remove point"
          onclick={() => removePoint(point)}
        >
          <IconTrash size={14} />
        </button>
      </div>
    {/each}

    <div class="flex min-h-11 items-center gap-2 border-t px-3 py-1.5" style="border-color: var(--metro-border);">
      <input
        type="number"
        step="any"
        placeholder="Value"
        value={valueDrafts['__new__'] ?? ''}
        oninput={(e) => { valueDrafts = { ...valueDrafts, __new__: (e.target as HTMLInputElement).value }; }}
        onkeydown={(e) => { if (e.key === 'Enter') addPoint(); }}
        class="h-11 w-20 rounded border bg-transparent px-2 text-xs outline-none"
        style="border-color: var(--metro-border); color: var(--metro-text);"
        aria-label="New point value"
      />
      <button
        class="flex h-11 shrink-0 items-center gap-1.5 rounded px-3 text-xs font-medium transition-colors"
        style="background-color: var(--metro-accent); color: var(--metro-text);"
        onclick={addPoint}
      >
        <IconPlus size={14} />
        Add
      </button>
      <span class="text-[11px]" style="color: var(--metro-text-muted);">
        {#if firstLevelId}{orderedLevels[0].name}{/if}
      </span>
    </div>
  {/if}

  {#if draftError}
    <p class="border-t px-3 py-1.5 text-[11px]" style="border-color: var(--metro-border); color: #fca5a5;">
      {draftError}
    </p>
  {/if}
  {#if pickerError}
    <p class="border-t px-3 py-1.5 text-[11px]" style="border-color: var(--metro-border); color: #fca5a5;">
      {pickerError}
    </p>
  {/if}
  {#if addError}
    <p class="border-t px-3 py-1.5 text-[11px]" style="border-color: var(--metro-border); color: #fca5a5;">
      {addError}
    </p>
  {/if}
</div>

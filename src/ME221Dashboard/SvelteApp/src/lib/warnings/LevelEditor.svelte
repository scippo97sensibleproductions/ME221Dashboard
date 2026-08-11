<script lang="ts">
  import { IconPlus, IconTrash, IconChevronUp, IconChevronDown } from '@tabler/icons-svelte';
  import type { WarningLevel } from '../HybridBridgeTypes';
  import { checkColorWarnings, generateDefaultColor } from './colorWarnings';
  import { navigationGate } from '../navigationGate.svelte';

  let { levels, onChange, onDisplayChange = onChange, onDeleteLevel, onMutate }: {
    levels: WarningLevel[];
    onChange: (levels: WarningLevel[]) => void;
    /** Name/color/autolog/flash edits (display-only evaluation-wise). */
    onDisplayChange?: (levels: WarningLevel[]) => void;
    /** Level deletion (R9 undo path). */
    onDeleteLevel: (levelId: string) => void;
    /** Level add/delete/reorder — expires the R9 undo window. */
    onMutate: () => void;
  } = $props();

  const colorSheet = navigationGate.registerModal('colorSheet');

  let colorSheetOpen = $state(false);
  let colorTargetId = $state<string | null>(null);
  let nameDrafts = $state<Record<string, string>>({});
  let confirmDeleteId = $state<string | null>(null);
  let deleteError = $state<string | null>(null);
  let colorError = $state<string | null>(null);

  const SWATCHES = [
    '#f59e0b', '#ef4444', '#22c55e', '#3b82f6', '#a855f7',
    '#ec4899', '#14b8a6', '#eab308', '#f97316', '#06b6d4',
    '#84cc16', '#f472b6', '#8b5cf6', '#0ea5e9', '#f43f5e',
  ];

  $effect(() => {
    if (colorSheetOpen) colorSheet.open();
    else colorSheet.close();
  });

  function addLevel() {
    const used = levels.map(l => l.color);
    const level: WarningLevel = {
      id: '',
      name: nextDefaultName(levels),
      color: generateDefaultColor(used),
      autolog: true,
      flash: true,
      order: levels.length,
    };
    onChange([...levels, level]);
    onMutate();
  }

  function nextDefaultName(ls: WarningLevel[]): string {
    const used = new Set(ls.map(l => l.name));
    let name = 'warning';
    let n = 2;
    while (used.has(name)) {
      name = `warning ${n++}`;
    }
    return name;
  }

  function commitName(level: WarningLevel, value: string) {
    const trimmed = value.trim();
    if (!trimmed) {
      delete nameDrafts[level.id];
      nameDrafts = { ...nameDrafts };
      return; // empty-name reject: previous name kept
    }
    const next = { ...nameDrafts };
    delete next[level.id];
    nameDrafts = next;
    if (trimmed === level.name) return;
    onDisplayChange(levels.map(l => (l.id === level.id ? { ...l, name: trimmed } : l)));
  }

  function openColorSheet(level: WarningLevel) {
    colorTargetId = level.id;
    colorError = null;
    colorSheetOpen = true;
  }

  function selectColor(color: string) {
    const target = levels.find(l => l.id === colorTargetId);
    if (!target) return;
    const check = checkColorWarnings({
      levels,
      candidateColor: color,
      candidateId: target.id,
      background: '#0A0A0A',
    });
    if (check.duplicate) colorError = 'This color is already used by another level of this datalink.';
    else if (check.nearDuplicate) colorError = 'This color is very close to another level\u2019s color.';
    else if (check.cvdConfusable) colorError = 'This color may be confusable for color-vision-deficient users.';
    else if (check.lowContrast) colorError = 'This color has low contrast against the dark background.';
    else colorError = null;
    onDisplayChange(levels.map(l => (l.id === target.id ? { ...l, color } : l)));
    colorSheetOpen = false;
  }

  function toggleAutolog(level: WarningLevel) {
    onDisplayChange(levels.map(l => (l.id === level.id ? { ...l, autolog: !l.autolog } : l)));
  }

  function toggleFlash(level: WarningLevel) {
    onDisplayChange(levels.map(l => (l.id === level.id ? { ...l, flash: !l.flash } : l)));
  }

  function moveLevel(index: number, delta: number) {
    const target = index + delta;
    if (target < 0 || target >= levels.length) return;
    const next = [...levels];
    const [item] = next.splice(index, 1);
    next.splice(target, 0, item);
    next.forEach((l, i) => (l.order = i));
    onChange(next);
    onMutate();
  }

  function confirmDelete(level: WarningLevel) {
    confirmDeleteId = level.id;
    deleteError = null;
  }

  function performDelete() {
    const level = levels.find(l => l.id === confirmDeleteId);
    if (!level) {
      confirmDeleteId = null;
      return;
    }
    onDeleteLevel(level.id);
    onMutate();
    confirmDeleteId = null;
  }

  function cancelDelete() {
    confirmDeleteId = null;
  }
</script>

<div class="rounded border" style="border-color: var(--metro-border);">
  {#if levels.length === 0}
    <div class="flex flex-col items-center gap-2 px-3 py-4">
      <p class="text-xs" style="color: var(--metro-text-muted);">No levels defined yet</p>
      <button
        class="flex h-11 items-center gap-1.5 rounded px-3 text-xs font-medium transition-colors"
        style="background-color: var(--metro-accent); color: var(--metro-text);"
        onclick={addLevel}
      >
        <IconPlus size={14} />
        Add level
      </button>
    </div>
  {:else}
    {#each levels as level, i (level.id)}
      <div class="border-b px-3 py-2 last:border-b-0" style="border-color: var(--metro-border);">
        <div class="flex min-h-11 items-center gap-2">
          <input
            type="text"
            value={nameDrafts[level.id] ?? level.name}
            placeholder="Level name"
            oninput={(e) => { nameDrafts = { ...nameDrafts, [level.id]: (e.target as HTMLInputElement).value }; }}
            onblur={(e) => commitName(level, (e.target as HTMLInputElement).value)}
            onkeydown={(e) => { if (e.key === 'Enter') commitName(level, (e.target as HTMLInputElement).value); }}
            class="h-11 min-w-0 flex-1 rounded border bg-transparent px-2 text-xs outline-none"
            style="border-color: var(--metro-border); color: var(--metro-text);"
          />
          <button
            class="h-11 w-11 shrink-0 rounded border transition-colors"
            style="background-color: {level.color}; border-color: var(--metro-border);"
            aria-label="Choose color for {level.name}"
            onclick={() => openColorSheet(level)}
          ></button>
          <button
            class="h-11 w-11 shrink-0 rounded transition-colors hover:bg-gray-800"
            style="color: var(--metro-text-muted);"
            aria-label="Delete level {level.name}"
            onclick={() => confirmDelete(level)}
          >
            <IconTrash size={16} class="mx-auto" />
          </button>
        </div>
        <div class="flex min-h-11 items-center gap-2">
          <label class="flex h-11 items-center gap-1.5 text-[11px]" style="color: var(--metro-text-secondary);">
            <input
              type="checkbox"
              checked={level.autolog}
              onchange={() => toggleAutolog(level)}
              class="h-4 w-4 accent-amber-500"
            />
            Autolog
          </label>
          <label class="flex h-11 items-center gap-1.5 text-[11px]" style="color: var(--metro-text-secondary);">
            <input
              type="checkbox"
              checked={level.flash}
              onchange={() => toggleFlash(level)}
              class="h-4 w-4 accent-amber-500"
            />
            Flash
          </label>
          <div class="ml-auto flex items-center gap-1">
            <button
              class="flex h-11 w-11 items-center justify-center rounded transition-colors hover:bg-gray-800 disabled:opacity-30"
              style="color: var(--metro-text-muted);"
              aria-label="Move {level.name} up"
              disabled={i === 0}
              onclick={() => moveLevel(i, -1)}
            >
              <IconChevronUp size={16} />
            </button>
            <button
              class="flex h-11 w-11 items-center justify-center rounded transition-colors hover:bg-gray-800 disabled:opacity-30"
              style="color: var(--metro-text-muted);"
              aria-label="Move {level.name} down"
              disabled={i === levels.length - 1}
              onclick={() => moveLevel(i, 1)}
            >
              <IconChevronDown size={16} />
            </button>
          </div>
        </div>
      </div>
    {/each}
    <button
      class="flex h-11 w-full items-center justify-center gap-1.5 rounded-b text-xs font-medium transition-colors hover:bg-gray-800"
      style="color: var(--metro-text-secondary); border-top: 1px solid var(--metro-border);"
      onclick={addLevel}
    >
      <IconPlus size={14} />
      Add level
    </button>
  {/if}
</div>

{#if confirmDeleteId !== null}
  <div
    class="fixed inset-0 z-[70] flex items-center justify-center bg-black/60"
    role="dialog"
    aria-modal="true"
    tabindex="-1"
    onclick={(e) => { if (e.target === e.currentTarget) confirmDeleteId = null; }}
    onkeydown={(e) => { if (e.key === 'Escape') confirmDeleteId = null; }}
  >
    <div class="w-72 rounded border p-4" style="background-color: var(--metro-card); border-color: var(--metro-border);">
      <p class="text-sm font-medium" style="color: var(--metro-text);">Delete level?</p>
      <p class="mt-1 text-xs" style="color: var(--metro-text-muted);">
        {#if deleteError}{deleteError}{:else}Its points are removed too; the datalink returns to "none". You can undo right after.{/if}
      </p>
      <div class="mt-3 flex justify-end gap-2">
        <button
          class="h-11 rounded px-3 text-xs transition-colors hover:bg-gray-800"
          style="color: var(--metro-text-secondary);"
          onclick={cancelDelete}
        >
          Cancel
        </button>
        <button
          class="h-11 rounded px-3 text-xs font-medium text-white transition-colors hover:bg-red-600"
          style="background-color: var(--metro-red, #ef4444);"
          onclick={performDelete}
        >
          Delete
        </button>
      </div>
    </div>
  </div>
{/if}

{#if colorSheetOpen}
  <div
    class="fixed inset-0 z-[70] flex items-end justify-center bg-black/60"
    role="dialog"
    aria-modal="true"
    tabindex="-1"
    onclick={(e) => { if (e.target === e.currentTarget) colorSheetOpen = false; }}
    onkeydown={(e) => { if (e.key === 'Escape') colorSheetOpen = false; }}
  >
    <div
      class="w-full max-w-md rounded-t border border-b-0 p-4"
      style="background-color: var(--metro-card); border-color: var(--metro-border);"
    >
      <p class="mb-2 text-xs font-medium uppercase tracking-wide" style="color: var(--metro-text-muted);">Choose color</p>
      {#if colorError}
        <p class="mb-2 rounded border px-2 py-1.5 text-[11px]" style="border-color: rgba(239,68,68,0.4); color: #fca5a5;">
          {colorError}
        </p>
      {/if}
      <div class="grid grid-cols-5 gap-2">
        {#each SWATCHES as swatch (swatch)}
          <button
            class="h-11 rounded border"
            style="background-color: {swatch}; border-color: var(--metro-border);"
            aria-label="Select color {swatch}"
            onclick={() => selectColor(swatch)}
          ></button>
        {/each}
      </div>
      <button
        class="mt-3 h-11 w-full rounded text-xs transition-colors hover:bg-gray-800"
        style="color: var(--metro-text-secondary);"
        onclick={() => { colorSheetOpen = false; }}
      >
        Cancel
      </button>
    </div>
  </div>
{/if}

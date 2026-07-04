<script lang="ts">
  import { IconX, IconBookmark, IconBookmarkFilled, IconRotate2, IconClock } from '@tabler/icons-svelte';
  import type { UndoEntry, Bookmark } from './tableUndoRedo';

  let { open, undoStack, redoStack, bookmarks, onJumpToGroup, onBookmark, onRemoveBookmark, onClose }: {
    open: boolean;
    undoStack: UndoEntry[];
    redoStack: UndoEntry[];
    bookmarks: Bookmark[];
    onJumpToGroup: (groupId: string, direction: 'undo' | 'redo') => void;
    onBookmark: (groupId: string, label: string) => void;
    onRemoveBookmark: (groupId: string) => void;
    onClose: () => void;
  } = $props();

  let bookmarkInput = $state('');
  let bookmarkTarget = $state<string | null>(null);

  interface HistoryEntry {
    groupId: string;
    label: string;
    timestamp: number;
    cellCount: number;
    direction: 'undo' | 'redo';
    bookmarked: boolean;
  }

  let entries = $derived.by(() => {
    const result: HistoryEntry[] = [];

    // Undo stack (most recent first)
    const undoGroups = new Map<string, { entries: UndoEntry[]; label?: string; timestamp?: number }>();
    for (const e of undoStack) {
      if (!undoGroups.has(e.groupId)) {
        undoGroups.set(e.groupId, { entries: [], label: e.label, timestamp: e.timestamp });
      }
      undoGroups.get(e.groupId)!.entries.push(e);
    }
    const undoIds = [...undoGroups.keys()].reverse();
    for (const gid of undoIds) {
      const g = undoGroups.get(gid)!;
      const bm = bookmarks.find(b => b.groupId === gid);
      result.push({
        groupId: gid,
        label: bm?.label ?? g.label ?? `Edit (${g.entries.length} cells)`,
        timestamp: bm?.timestamp ?? g.timestamp ?? 0,
        cellCount: g.entries.length,
        direction: 'undo',
        bookmarked: !!bm,
      });
    }

    // Redo stack (oldest first)
    const redoGroups = new Map<string, { entries: UndoEntry[]; label?: string; timestamp?: number }>();
    for (const e of redoStack) {
      if (!redoGroups.has(e.groupId)) {
        redoGroups.set(e.groupId, { entries: [], label: e.label, timestamp: e.timestamp });
      }
      redoGroups.get(e.groupId)!.entries.push(e);
    }
    for (const gid of redoGroups.keys()) {
      const g = redoGroups.get(gid)!;
      const bm = bookmarks.find(b => b.groupId === gid);
      result.push({
        groupId: gid,
        label: bm?.label ?? g.label ?? `Redo (${g.entries.length} cells)`,
        timestamp: bm?.timestamp ?? g.timestamp ?? 0,
        cellCount: g.entries.length,
        direction: 'redo',
        bookmarked: !!bm,
      });
    }

    return result;
  });

  function formatTime(ts: number): string {
    if (!ts) return '';
    const d = new Date(ts);
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  }

  function startBookmark(groupId: string) {
    bookmarkTarget = groupId;
    bookmarkInput = '';
  }

  function confirmBookmark() {
    if (bookmarkTarget && bookmarkInput.trim()) {
      onBookmark(bookmarkTarget, bookmarkInput.trim());
      bookmarkTarget = null;
      bookmarkInput = '';
    }
  }
</script>

{#if open}
  <div class="fixed inset-0 z-[80]" style="background-color: rgba(0,0,0,0.8);" role="button" tabindex="-1" onclick={onClose} onkeydown={(e) => { if (e.key === 'Escape') onClose(); }}></div>
  <div class="fixed inset-x-0 bottom-0 z-[81] border-t max-h-[70vh] overflow-auto" style="background-color: var(--metro-card); border-color: var(--metro-border);">
    <div class="mx-auto max-w-lg p-4">
      <div class="mb-3 flex items-center justify-between">
        <h3 class="text-sm font-bold uppercase tracking-wider" style="color: var(--metro-text-primary);">History</h3>
        <button class="p-1 transition-colors duration-150" style="color: var(--metro-text-muted);" onclick={onClose}>
          <IconX size={18} />
        </button>
      </div>

      {#if entries.length === 0}
        <p class="py-6 text-center text-[13px]" style="color: var(--metro-text-muted);">No history yet</p>
      {:else}
        <div class="space-y-1">
          {#each entries as entry}
            <div
              class="group flex items-center gap-2 rounded px-3 py-2 transition-colors duration-150"
              style="background-color: {entry.bookmarked ? 'rgba(216,59,1,0.1)' : 'transparent'};"
            >
              <button
                class="min-w-0 flex-1 text-left"
                onclick={() => onJumpToGroup(entry.groupId, entry.direction)}
              >
                <div class="flex items-center gap-2">
                  {#if entry.bookmarked}
                    <IconBookmarkFilled size={12} style="color: var(--metro-orange);" />
                  {:else}
                    <IconRotate2 size={12} style="color: var(--metro-text-muted);" />
                  {/if}
                  <span class="truncate text-[13px]" style="color: var(--metro-text-primary);">{entry.label}</span>
                  <span class="ml-auto shrink-0 text-[10px]" style="color: var(--metro-text-muted);">{entry.cellCount} cells</span>
                </div>
                {#if entry.timestamp}
                  <div class="mt-0.5 flex items-center gap-1 pl-5">
                    <IconClock size={10} style="color: var(--metro-text-muted);" />
                    <span class="text-[10px]" style="color: var(--metro-text-muted);">{formatTime(entry.timestamp)}</span>
                  </div>
                {/if}
              </button>

              {#if entry.direction === 'undo'}
                <div class="flex shrink-0 gap-1">
                  {#if entry.bookmarked}
                    <button
                      class="p-1 transition-colors duration-150"
                      style="color: var(--metro-text-muted);"
                      onclick={() => onRemoveBookmark(entry.groupId)}
                      title="Remove bookmark"
                    >
                      <IconBookmarkFilled size={14} />
                    </button>
                  {:else}
                    <button
                      class="p-1 transition-colors duration-150"
                      style="color: var(--metro-text-muted);"
                      onclick={() => startBookmark(entry.groupId)}
                      title="Bookmark this state"
                    >
                      <IconBookmark size={14} />
                    </button>
                  {/if}
                </div>
              {/if}
            </div>
          {/each}
        </div>
      {/if}

      {#if bookmarkTarget}
        <div class="mt-3 flex gap-2">
          <input
            bind:value={bookmarkInput}
            type="text"
            placeholder="Bookmark label..."
            class="metro-input flex-1 text-[13px]"
            onkeydown={(e) => { if (e.key === 'Enter') confirmBookmark(); if (e.key === 'Escape') { bookmarkTarget = null; bookmarkInput = ''; } }}
          />
          <button class="metro-btn-primary px-3 py-1 text-[12px]" onclick={confirmBookmark}>Save</button>
          <button class="metro-btn-secondary px-3 py-1 text-[12px]" onclick={() => { bookmarkTarget = null; bookmarkInput = ''; }}>Cancel</button>
        </div>
      {/if}
    </div>
  </div>
{/if}

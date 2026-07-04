<script lang="ts">
  import { IconArrowLeft, IconDeviceFloppy, IconRefresh, IconRotate2, IconRotateClockwise2, IconRotate, IconDotsVertical, IconFileExport, IconFileImport, IconArrowsDiff, IconNotebook, IconPalette, IconGridDots, IconHistory, IconActivity } from '@tabler/icons-svelte';
  import { Dropdown, DropdownItem } from 'flowbite-svelte';

  let { tableName, dimensions, dirtyCount, undoCount = 0, redoCount = 0, writing = false, diffMode = false, hasNote = false, selecting = false, colorScheme = 'thermal', showContours = false, livePanelOpen = false, onToggleDiffMode, onWrite, onRead, onUndo, onRedo, onRevertAll, onBack, onExportCsv, onExportYaml, onImportCsv, onImportYaml, onOpenNotes, onColorSchemeChange, onTableNameClick, onToggleContours, onOpenHistory, onToggleLivePanel }: {
    tableName: string;
    dimensions: string;
    dirtyCount: number;
    undoCount?: number;
    redoCount?: number;
    writing?: boolean;
    diffMode?: boolean;
    hasNote?: boolean;
    selecting?: boolean;
    colorScheme?: string;
    showContours?: boolean;
    onToggleDiffMode?: () => void;
    onWrite: () => void;
    onRead: () => void;
    onUndo: () => void;
    onRedo: () => void;
    onRevertAll: () => void;
    onBack: () => void;
    onExportCsv?: () => void;
    onExportYaml?: () => void;
    onImportCsv?: () => void;
    onImportYaml?: () => void;
    onOpenNotes?: () => void;
    onColorSchemeChange?: (scheme: string) => void;
    onTableNameClick?: () => void;
    onToggleContours?: () => void;
    onOpenHistory?: () => void;
    livePanelOpen?: boolean;
    onToggleLivePanel?: () => void;
  } = $props();
</script>

<div
  class="flex items-center gap-2 border-b px-3 py-2"
  style="border-color: var(--metro-border); background-color: var(--metro-surface);"
>
  <button
    class="flex h-8 w-8 shrink-0 items-center justify-center text-[var(--metro-text-secondary)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-white"
    onclick={onBack}
    aria-label="Back to table list"
  >
    <IconArrowLeft size={16} />
  </button>

  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <div class="min-w-0 flex-1 cursor-pointer" role="button" tabindex="0" onclick={onTableNameClick} onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onTableNameClick?.(); } }}>
    <div class="truncate text-[13px] font-bold text-white hover:text-[var(--metro-orange)] transition-colors">{tableName}</div>
    <div class="text-[10px] uppercase tracking-wider text-[var(--metro-text-secondary)]">{dimensions}</div>
  </div>

  {#if dirtyCount > 0}
    <span
      class="shrink-0 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-white"
      style="background-color: var(--metro-orange);"
    >
      {dirtyCount}
    </span>
  {/if}
  {#if selecting}
    <span
      class="shrink-0 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-white animate-pulse"
      style="background-color: var(--metro-orange);"
    >
      Selecting...
    </span>
  {/if}

  <div class="flex shrink-0 gap-1">
    <button
      class="flex h-8 w-8 items-center justify-center text-[11px] transition-colors duration-150 {undoCount > 0 ? 'text-[var(--metro-text-secondary)] hover:bg-[var(--metro-hover)] hover:text-white' : 'text-[var(--metro-text-muted)]'}"
      onclick={onUndo}
      disabled={undoCount === 0}
      aria-label="Undo"
    >
      <IconRotate2 size={14} />
    </button>
    <button
      class="flex h-8 w-8 items-center justify-center text-[11px] transition-colors duration-150 {redoCount > 0 ? 'text-[var(--metro-text-secondary)] hover:bg-[var(--metro-hover)] hover:text-white' : 'text-[var(--metro-text-muted)]'}"
      onclick={onRedo}
      disabled={redoCount === 0}
      aria-label="Redo"
    >
      <IconRotateClockwise2 size={14} />
    </button>
    <button
      class="flex h-8 w-8 items-center justify-center text-[var(--metro-text-secondary)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-white"
      onclick={onRead}
      aria-label="Read from ECU"
    >
      <IconRefresh size={14} />
    </button>
    <button
      class="flex h-8 items-center gap-1 px-2 text-[11px] font-bold uppercase tracking-wider transition-colors duration-150 {dirtyCount > 0 ? 'text-white' : 'text-[var(--metro-text-muted)]'}"
      style={dirtyCount > 0 ? 'background-color: var(--metro-orange);' : ''}
      onclick={onWrite}
      disabled={dirtyCount === 0 || writing}
      aria-label="Write changes to ECU"
    >
      {#if writing}
        <span class="inline-block h-3 w-3 animate-spin rounded-full border-2 border-white border-t-transparent"></span>
      {:else}
        <IconDeviceFloppy size={14} />
      {/if}
      <span class="hidden sm:inline">Write</span>
    </button>
    <button
      id="table-editor-overflow"
      class="flex h-8 w-8 items-center justify-center text-[var(--metro-text-secondary)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-white"
      aria-label="More options"
    >
      <IconDotsVertical size={14} />
    </button>
    <Dropdown triggeredBy="#table-editor-overflow" placement="bottom-end" class="w-48 bg-[var(--metro-card)] border border-[var(--metro-border)]">
      {#if onToggleDiffMode}
        <DropdownItem onclick={onToggleDiffMode} class="flex items-center gap-2 text-[13px] {diffMode ? 'text-[var(--metro-orange)]' : 'text-[var(--metro-text-secondary)]'}">
          <IconArrowsDiff size={14} />
          {diffMode ? 'Show Values' : 'Show Diff'}
        </DropdownItem>
      {/if}
      {#if dirtyCount > 0}
        <DropdownItem onclick={onRevertAll} class="flex items-center gap-2 text-[13px] text-[var(--metro-orange)]">
          <IconRotate size={14} />
          Revert All
        </DropdownItem>
      {/if}
      {#if onOpenNotes}
        <DropdownItem onclick={onOpenNotes} class="flex items-center gap-2 text-[13px] {hasNote ? 'text-[var(--metro-orange)]' : 'text-[var(--metro-text-secondary)]'}">
          <IconNotebook size={14} />
          Notes {hasNote ? '*' : ''}
        </DropdownItem>
      {/if}
      {#if onExportCsv}
        <DropdownItem onclick={onExportCsv} class="flex items-center gap-2 text-[13px] text-[var(--metro-text-secondary)]">
          <IconFileExport size={14} />
          Export CSV
        </DropdownItem>
      {/if}
      {#if onExportYaml}
        <DropdownItem onclick={onExportYaml} class="flex items-center gap-2 text-[13px] text-[var(--metro-text-secondary)]">
          <IconFileExport size={14} />
          Export YAML
        </DropdownItem>
      {/if}
      {#if onImportCsv}
        <DropdownItem onclick={onImportCsv} class="flex items-center gap-2 text-[13px] text-[var(--metro-text-secondary)]">
          <IconFileImport size={14} />
          Import CSV
        </DropdownItem>
      {/if}
      {#if onImportYaml}
        <DropdownItem onclick={onImportYaml} class="flex items-center gap-2 text-[13px] text-[var(--metro-text-secondary)]">
          <IconFileImport size={14} />
          Import YAML
        </DropdownItem>
      {/if}
      {#if onToggleContours}
        <DropdownItem onclick={onToggleContours} class="flex items-center gap-2 text-[13px] {showContours ? 'text-[var(--metro-orange)]' : 'text-[var(--metro-text-secondary)]'}">
          <IconGridDots size={14} />
          Contour Lines
          {#if showContours}<span class="ml-auto text-[10px]">&#10003;</span>{/if}
        </DropdownItem>
      {/if}
      {#if onOpenHistory}
        <DropdownItem onclick={onOpenHistory} class="flex items-center gap-2 text-[13px] text-[var(--metro-text-secondary)]">
          <IconHistory size={14} />
          History
        </DropdownItem>
      {/if}
      {#if onToggleLivePanel}
        <DropdownItem onclick={onToggleLivePanel} class="flex items-center gap-2 text-[13px] {livePanelOpen ? 'text-[var(--metro-orange)]' : 'text-[var(--metro-text-secondary)]'}">
          <IconActivity size={14} />
          Live Data
          {#if livePanelOpen}<span class="ml-auto text-[10px]">&#10003;</span>{/if}
        </DropdownItem>
      {/if}
      {#if onColorSchemeChange}
        <div class="border-t border-[var(--metro-border)] px-3 py-1.5 text-[10px] font-bold uppercase tracking-wider text-[var(--metro-text-muted)]">Color Scheme</div>
        {#each [{ id: 'thermal', label: 'Thermal' }, { id: 'viridis', label: 'Viridis' }, { id: 'grayscale', label: 'Grayscale' }, { id: 'ember', label: 'Ember' }] as scheme}
          <DropdownItem onclick={() => onColorSchemeChange!(scheme.id)} class="flex items-center gap-2 text-[13px] {colorScheme === scheme.id ? 'text-[var(--metro-orange)]' : 'text-[var(--metro-text-secondary)]'}">
            <IconPalette size={14} />
            {scheme.label}
            {#if colorScheme === scheme.id}<span class="ml-auto text-[10px]">&#10003;</span>{/if}
          </DropdownItem>
        {/each}
      {/if}
    </Dropdown>
  </div>
</div>

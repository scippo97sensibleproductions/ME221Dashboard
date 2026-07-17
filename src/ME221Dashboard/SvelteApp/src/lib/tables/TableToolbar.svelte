<script lang="ts">
  import { IconArrowLeft, IconDeviceFloppy, IconRefresh, IconRotate2, IconRotateClockwise2, IconRotate, IconDotsVertical, IconFileExport, IconFileImport, IconArrowsDiff, IconNotebook, IconPalette, IconGridDots, IconHistory, IconActivity, IconBox } from '@tabler/icons-svelte';
  import { Dropdown, DropdownItem } from 'flowbite-svelte';

  let { tableName, dimensions, dirtyCount, undoCount = 0, redoCount = 0, writing = false, diffMode = false, hasNote = false, selecting = false, colorScheme = 'thermal', showContours = false, livePanelOpen = false, view3D = false, onToggleDiffMode, onWrite, onRead, onUndo, onRedo, onRevertAll, onBack, onExportCsv, onExportYaml, onImportCsv, onImportYaml, onOpenNotes, onColorSchemeChange, onTableNameClick, onToggleContours, onOpenHistory, onToggleLivePanel, onToggleView3D }: {
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
    livePanelOpen?: boolean;
    view3D?: boolean;
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
    onToggleView3D?: () => void;
  } = $props();
</script>

<div
  class="flex items-center gap-1 border-b px-2 py-1.5"
  style="border-color: var(--metro-border); background-color: var(--metro-surface);"
>
  <!-- Group 1: Navigation -->
  <button
    class="flex h-7 shrink-0 items-center gap-1 px-1.5 text-[11px] text-[var(--metro-text-secondary)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-white"
    onclick={onBack}
    aria-label="Back to table list"
  >
    <IconArrowLeft size={14} />
  </button>

  <!-- svelte-ignore a11y_click_events_have_key_events -->
  <div class="min-w-0 flex-1 cursor-pointer px-1" role="button" tabindex="0" onclick={onTableNameClick} onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onTableNameClick?.(); } }}>
    <div class="truncate text-[12px] font-bold text-white hover:text-[var(--metro-orange)] transition-colors">{tableName}</div>
    <div class="text-[9px] uppercase tracking-wider text-[var(--metro-text-secondary)]">{dimensions}</div>
  </div>

  {#if dirtyCount > 0}
    <span
      class="shrink-0 px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wider text-white"
      style="background-color: var(--metro-orange);"
    >
      {dirtyCount}
    </span>
  {/if}
  {#if selecting}
    <span
      class="shrink-0 px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wider text-white animate-pulse"
      style="background-color: var(--metro-orange);"
    >
      Selecting
    </span>
  {/if}

  <!-- Divider -->
  <div class="mx-0.5 h-5 w-px shrink-0" style="background-color: var(--metro-border);"></div>

  <!-- Group 2: Undo / Redo -->
  <button
    class="flex h-7 shrink-0 items-center gap-1 px-1.5 text-[10px] transition-colors duration-150 {undoCount > 0 ? 'text-[var(--metro-text-secondary)] hover:bg-[var(--metro-hover)] hover:text-white' : 'text-[var(--metro-text-muted)]'}"
    onclick={onUndo}
    disabled={undoCount === 0}
    aria-label="Undo"
  >
    <IconRotate2 size={13} />
    {#if undoCount > 0}
      <span class="font-mono text-[9px]">{undoCount}</span>
    {/if}
  </button>
  <button
    class="flex h-7 shrink-0 items-center gap-1 px-1.5 text-[10px] transition-colors duration-150 {redoCount > 0 ? 'text-[var(--metro-text-secondary)] hover:bg-[var(--metro-hover)] hover:text-white' : 'text-[var(--metro-text-muted)]'}"
    onclick={onRedo}
    disabled={redoCount === 0}
    aria-label="Redo"
  >
    <IconRotateClockwise2 size={13} />
    {#if redoCount > 0}
      <span class="font-mono text-[9px]">{redoCount}</span>
    {/if}
  </button>

  <!-- Divider -->
  <div class="mx-0.5 h-5 w-px shrink-0" style="background-color: var(--metro-border);"></div>

  <!-- Group 3: View toggle -->
  {#if onToggleView3D}
    <button
      class="flex h-7 shrink-0 items-center gap-1 px-1.5 text-[10px] transition-colors duration-150 {view3D ? 'text-[var(--metro-orange)]' : 'text-[var(--metro-text-secondary)] hover:bg-[var(--metro-hover)] hover:text-white'}"
      onclick={onToggleView3D}
      title={view3D ? 'Switch to 2D grid' : 'Switch to 3D surface'}
      aria-label="Toggle 3D view"
    >
      <IconBox size={13} />
      <span class="hidden sm:inline">{view3D ? '2D' : '3D'}</span>
    </button>
  {/if}

  <!-- Divider -->
  <div class="mx-0.5 h-5 w-px shrink-0" style="background-color: var(--metro-border);"></div>

  <!-- Group 4: ECU operations -->
  <button
    class="flex h-7 shrink-0 items-center gap-1 px-1.5 text-[10px] text-[var(--metro-text-secondary)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-white"
    onclick={onRead}
    aria-label="Read from ECU"
  >
    <IconRefresh size={13} />
    <span class="hidden sm:inline">Read</span>
  </button>
  <button
    class="flex h-7 shrink-0 items-center gap-1 px-2 text-[10px] font-bold uppercase tracking-wider transition-colors duration-150 {dirtyCount > 0 ? 'text-white' : 'text-[var(--metro-text-muted)]'}"
    style={dirtyCount > 0 ? 'background-color: var(--metro-orange);' : ''}
    onclick={onWrite}
    disabled={dirtyCount === 0 || writing}
    aria-label="Write changes to ECU"
  >
    {#if writing}
      <span class="inline-block h-3 w-3 animate-spin rounded-full border-2 border-white border-t-transparent"></span>
    {:else}
      <IconDeviceFloppy size={13} />
    {/if}
    <span class="hidden sm:inline">Write</span>
  </button>

  <!-- Divider -->
  <div class="mx-0.5 h-5 w-px shrink-0" style="background-color: var(--metro-border);"></div>

  <!-- Group 5: Quick toggles (visible on main bar) -->
  {#if onToggleDiffMode}
    <button
      class="flex h-7 shrink-0 items-center gap-1 px-1.5 text-[10px] transition-colors duration-150 {diffMode ? 'text-[var(--metro-orange)]' : 'text-[var(--metro-text-secondary)] hover:bg-[var(--metro-hover)] hover:text-white'}"
      onclick={onToggleDiffMode}
      title={diffMode ? 'Show values' : 'Show diff from original'}
    >
      <IconArrowsDiff size={13} />
      <span class="hidden md:inline">{diffMode ? 'Values' : 'Diff'}</span>
    </button>
  {/if}
  {#if onToggleContours}
    <button
      class="flex h-7 shrink-0 items-center gap-1 px-1.5 text-[10px] transition-colors duration-150 {showContours ? 'text-[var(--metro-orange)]' : 'text-[var(--metro-text-secondary)] hover:bg-[var(--metro-hover)] hover:text-white'}"
      onclick={onToggleContours}
      title="Toggle contour lines"
    >
      <IconGridDots size={13} />
      <span class="hidden md:inline">Contours</span>
    </button>
  {/if}
  {#if onOpenHistory}
    <button
      class="flex h-7 shrink-0 items-center gap-1 px-1.5 text-[10px] text-[var(--metro-text-secondary)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-white"
      onclick={onOpenHistory}
      title="Edit history"
    >
      <IconHistory size={13} />
      <span class="hidden md:inline">History</span>
    </button>
  {/if}
  {#if onToggleLivePanel}
    <button
      class="flex h-7 shrink-0 items-center gap-1 px-1.5 text-[10px] transition-colors duration-150 {livePanelOpen ? 'text-[var(--metro-orange)]' : 'text-[var(--metro-text-secondary)] hover:bg-[var(--metro-hover)] hover:text-white'}"
      onclick={onToggleLivePanel}
      title="Toggle live data panel"
    >
      <IconActivity size={13} />
      <span class="hidden md:inline">Live</span>
    </button>
  {/if}

  <!-- Group 6: Overflow (slimmed) -->
  <button
    id="table-editor-overflow"
    class="flex h-7 shrink-0 items-center justify-center px-1.5 text-[var(--metro-text-secondary)] transition-colors duration-150 hover:bg-[var(--metro-hover)] hover:text-white"
    aria-label="More options"
  >
    <IconDotsVertical size={13} />
  </button>
  <Dropdown triggeredBy="#table-editor-overflow" placement="bottom-end" class="w-44 bg-[var(--metro-card)] border border-[var(--metro-border)]">
    {#if dirtyCount > 0}
      <DropdownItem onclick={onRevertAll} class="flex items-center gap-2 text-[12px] text-[var(--metro-orange)]">
        <IconRotate size={14} />
        Revert All
      </DropdownItem>
    {/if}
    {#if onOpenNotes}
      <DropdownItem onclick={onOpenNotes} class="flex items-center gap-2 text-[12px] {hasNote ? 'text-[var(--metro-orange)]' : 'text-[var(--metro-text-secondary)]'}">
        <IconNotebook size={14} />
        Notes {hasNote ? '*' : ''}
      </DropdownItem>
    {/if}
    {#if onExportCsv || onExportYaml || onImportCsv || onImportYaml}
      <div class="border-t border-[var(--metro-border)] px-3 py-1 text-[9px] font-bold uppercase tracking-wider text-[var(--metro-text-muted)]">File</div>
    {/if}
    {#if onExportCsv}
      <DropdownItem onclick={onExportCsv} class="flex items-center gap-2 text-[12px] text-[var(--metro-text-secondary)]">
        <IconFileExport size={14} />
        Export CSV
      </DropdownItem>
    {/if}
    {#if onExportYaml}
      <DropdownItem onclick={onExportYaml} class="flex items-center gap-2 text-[12px] text-[var(--metro-text-secondary)]">
        <IconFileExport size={14} />
        Export YAML
      </DropdownItem>
    {/if}
    {#if onImportCsv}
      <DropdownItem onclick={onImportCsv} class="flex items-center gap-2 text-[12px] text-[var(--metro-text-secondary)]">
        <IconFileImport size={14} />
        Import CSV
      </DropdownItem>
    {/if}
    {#if onImportYaml}
      <DropdownItem onclick={onImportYaml} class="flex items-center gap-2 text-[12px] text-[var(--metro-text-secondary)]">
        <IconFileImport size={14} />
        Import YAML
      </DropdownItem>
    {/if}
    {#if onColorSchemeChange}
      <div class="border-t border-[var(--metro-border)] px-3 py-1 text-[9px] font-bold uppercase tracking-wider text-[var(--metro-text-muted)]">Color Scheme</div>
      {#each [{ id: 'thermal', label: 'Thermal' }, { id: 'viridis', label: 'Viridis' }, { id: 'grayscale', label: 'Grayscale' }, { id: 'ember', label: 'Ember' }] as scheme}
        <DropdownItem onclick={() => onColorSchemeChange!(scheme.id)} class="flex items-center gap-2 text-[12px] {colorScheme === scheme.id ? 'text-[var(--metro-orange)]' : 'text-[var(--metro-text-secondary)]'}">
          <IconPalette size={14} />
          {scheme.label}
          {#if colorScheme === scheme.id}<span class="ml-auto text-[10px]">&#10003;</span>{/if}
        </DropdownItem>
      {/each}
    {/if}
  </Dropdown>
</div>

<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { HybridBridge } from '../lib/HybridBridge';
  import { liveDataStore } from '../lib/stores/LiveDataStore.svelte';
  import type { TableDefinition, TableData, OperatingPoint, InterpolationRange } from '../lib/tables/types';
  import { is1DTable, cellKey, getOutputValue, heatColor, getDataRange, findNearestIndex, findInterpolationRange, fromRaw, MeasurementUnitType } from '../lib/tables/types';
  import type { ColorScheme } from '../lib/tables/types';
  import TableGrid from '../lib/tables/TableGrid.svelte';
  import CurveEditor from '../lib/tables/CurveEditor.svelte';
  import CellEditor from '../lib/tables/CellEditor.svelte';
  import TableToolbar from '../lib/tables/TableToolbar.svelte';
  import ConfirmDialog from '../lib/ConfirmDialog.svelte';
  import SelectionToolbar from '../lib/tables/SelectionToolbar.svelte';
  import OpsSheet from '../lib/tables/OpsSheet.svelte';
  import ContextContextMenu from '../lib/tables/ContextContextMenu.svelte';
  import { toast } from '../lib/toasts.svelte';
  import { buildExportBundle, generateYamlString, parseImportBundle } from '../lib/tableExport';
  import TableNotesSheet from '../lib/TableNotesSheet.svelte';
  import { handleSelectionComplete as calcSelectionComplete } from '../lib/tables/tableSelection';
  import { selBounds as calcSelBounds, applyTransform } from '../lib/tables/tableTransforms';
  import { recalculateDirty as calcDirty, loadSessionCache, saveSessionCache, clearSessionCache } from '../lib/tables/tableUndoRedo';
  import type { Bookmark } from '../lib/tables/tableUndoRedo';
  import HistoryViewer from '../lib/tables/HistoryViewer.svelte';
  import LiveSidePanel from '../lib/tables/LiveSidePanel.svelte';
  import { IconPointer, IconCopy, IconClipboard, IconTransform, IconRowInsertTop, IconColumnInsertRight, IconSelectAll, IconTarget } from '@tabler/icons-svelte';
  import TracePanel from '../lib/tables/TracePanel.svelte';
  import Graph3D from '../lib/tables/Graph3D.svelte';
  import type { DataLinkDefinition } from '../lib/HybridBridgeTypes';

  let { tableId, onNavigate, onBack }: {
    tableId: number;
    onNavigate: (page: string, params?: Record<string, unknown>) => void;
    onBack?: () => void;
  } = $props();

  let tableDef = $state<TableDefinition | null>(null);
  let tableData = $state<TableData | null>(null);
  let originalData = $state<TableData | null>(null);
  let is1D = $derived(tableDef ? is1DTable(tableDef) : false);
  let operatingPoint = $derived.by(() => {
    if (!tableDef) return { rpm: null, map: null, output: null } as OperatingPoint;
    const v = liveDataStore.values;
    const rpmRaw = v[String(tableDef.input0LinkId)] ?? null;
    const mapRaw = tableDef.input1LinkId ? (v[String(tableDef.input1LinkId)] ?? null) : null;
    const outRaw = tableDef.outputLinkId ? (v[String(tableDef.outputLinkId)] ?? null) : null;
    return {
      rpm: rpmRaw !== null ? fromRaw(rpmRaw, tableDef.input0UnitType) : null,
      map: is1D ? null : (mapRaw !== null ? fromRaw(mapRaw, tableDef.input1UnitType) : null),
      output: outRaw !== null ? fromRaw(outRaw, tableDef.outputUnitType) : null,
    };
  });
  let loading = $state(true);
  let error = $state<string | null>(null);
  let mounted = false;

  // Selection
  let selectedRow = $state(0);
  let selectedCol = $state(0);
  let editMode = $state<'output' | 'input0' | 'input1'>('output');

  // Selection (range) — two-tap model
  let anchor = $state<{ row: number; col: number } | null>(null);
  let selection = $state<{ startRow: number; startCol: number; endRow: number; endCol: number } | null>(null);
  let selectionType = $state<'output' | 'input0' | 'input1'>('output');
  let selectionToolbarOpen = $derived(selection !== null);
  let opsSheetOpen = $state(false);

  // Selection bounds helper — delegates to extracted module
  function selBounds() {
    if (!selection) return null;
    return calcSelBounds(selection);
  }

  function handleAnchorSet(row: number, col: number, type: 'output' | 'input0' | 'input1' = 'output') {
    anchor = { row, col };
    selectionType = type;
    selection = null;
    editorOpen = false;
  }

  function handleSelectionComplete(row: number, col: number) {
    if (!anchor) return;
    const result = calcSelectionComplete(anchor, row, col, tableDef, selectionType);
    selection = result.selection;
    selectionType = result.selectionType;
    editorOpen = false;
  }

  function clearSelection() {
    anchor = null;
    selection = null;
    selectionType = 'output';
  }

  // Internal clipboard
  let clipboard = $state<string>('');

  // Diff mode
  let diffMode = $state(false);

  // Color scheme
  function loadColorScheme(): ColorScheme {
    try {
      const stored = localStorage.getItem('table-color-scheme');
      if (stored === 'thermal' || stored === 'viridis' || stored === 'grayscale' || stored === 'ember') return stored;
    } catch {}
    return 'thermal';
  }

  let colorScheme = $state<ColorScheme>(loadColorScheme());

  function handleColorSchemeChange(scheme: string) {
    colorScheme = scheme as ColorScheme;
    try { localStorage.setItem('table-color-scheme', colorScheme); } catch {}
  }

  let showContours = $state(false);

  // Context menu
  let contextMenuOpen = $state(false);
  let contextMenuX = $state(0);
  let contextMenuY = $state(0);
  let contextMenuTarget = $state<{ row: number; col: number; type: 'output' | 'input0' | 'input1' } | null>(null);

  function handleContextMenu(e: MouseEvent, row: number, col: number, type: 'output' | 'input0' | 'input1') {
    e.preventDefault();
    contextMenuX = e.clientX;
    contextMenuY = e.clientY;
    contextMenuTarget = { row, col, type };
    contextMenuOpen = true;
  }

  function handleContextMenuAction(action: string) {
    if (!contextMenuTarget) return;
    const { row, col, type } = contextMenuTarget;
    switch (action) {
      case 'selectCell':
        if (anchor && !selection) {
          // Anchor exists, no range yet → complete the range
          selection = { startRow: anchor.row, startCol: anchor.col, endRow: row, endCol: col };
          selectionType = type;
        } else if (selection) {
          // Range already selected → reset to single cell
          clearSelection();
          handleAnchorSet(row, col, type);
        } else {
          // Nothing selected → anchor on this cell
          handleAnchorSet(row, col, type);
        }
        break;
      case 'copy':
        // Set single-cell selection and copy
        handleAnchorSet(row, col, type);
        selection = { startRow: row, startCol: col, endRow: row, endCol: col };
        selectionType = type;
        handleCopy();
        clearSelection();
        break;
      case 'paste':
        handleAnchorSet(row, col, type);
        selection = { startRow: row, startCol: col, endRow: row, endCol: col };
        selectionType = type;
        handlePaste();
        clearSelection();
        break;
      case 'transform':
        handleAnchorSet(row, col, type);
        selection = { startRow: row, startCol: col, endRow: row, endCol: col };
        selectionType = type;
        opsSheetOpen = true;
        break;
      case 'selectRow':
        if (tableDef) {
          anchor = { row, col: 0 };
          selection = { startRow: row, startCol: 0, endRow: row, endCol: tableDef.cols - 1 };
          selectionType = 'output';
        }
        break;
      case 'selectCol':
        if (tableDef) {
          anchor = { row: 0, col };
          selection = { startRow: 0, startCol: col, endRow: tableDef.rows - 1, endCol: col };
          selectionType = 'output';
        }
        break;
      case 'selectAll':
        selectAll();
        break;
      case 'jumpToOp':
        if (opRow >= 0 && opCol >= 0) {
          selectedRow = opRow;
          selectedCol = opCol;
          const cellEl = document.querySelector(`[data-cell="${opRow},${opCol}"]`);
          cellEl?.scrollIntoView({ block: 'center', inline: 'center' });
        }
        break;
    }
    contextMenuTarget = null;
  }

  // Notes
  let notesOpen = $state(false);
  let hasNote = $state(false);

  async function refreshHasNote() {
    if (!tableId) { hasNote = false; return; }
    try {
      const notes = await HybridBridge.getTableNotes();
      hasNote = !!notes[tableId];
    } catch { hasNote = false; }
  }

  // Cell editor
  let editorOpen = $state(false);

  // Dirty tracking
  let dirtyCells = $state(new Set<string>());
  let dirtyInput0 = $state(new Set<number>());
  let dirtyInput1 = $state(new Set<number>());
  let undoStack = $state<Array<{ type: string; key: string; row?: number; col?: number; idx?: number; oldVal: number; newVal: number; groupId: string }>>([]);
  let redoStack = $state<Array<{ type: string; key: string; row?: number; col?: number; idx?: number; oldVal: number; newVal: number; groupId: string }>>([]);
  let bookmarks = $state<Bookmark[]>([]);
  let historyOpen = $state(false);

  // Live side panel
  let livePanelOpen = $state(false);
  let livePanelSensorIds = $state<number[]>([]);
  let allDataLinks = $state<DataLinkDefinition[]>([]);
  let liveValues = $derived(liveDataStore.values);

  // Trace panel
  let tracePanelOpen = $state(true);
  let traceXLinkId = $state<number | null>(null);
  let traceYLinkId = $state<number | null>(null);
  let traceXLabel = $state('Time');
  let traceYLabel = $state('RPM');

  // 3D view
  let view3D = $state(false);
  let graph3dRef = $state<any>(null);

  function loadTracePanelState() {
    try {
      const raw = localStorage.getItem(`table-trace-panel-${tableId}`);
      if (raw) {
        const parsed = JSON.parse(raw);
        tracePanelOpen = parsed.open ?? true;
        traceXLinkId = parsed.xLinkId ?? null;
        traceYLinkId = parsed.yLinkId ?? null;
      }
    } catch {}
    updateTraceLabels();
  }

  function saveTracePanelState() {
    try {
      localStorage.setItem(`table-trace-panel-${tableId}`, JSON.stringify({
        open: tracePanelOpen,
        xLinkId: traceXLinkId,
        yLinkId: traceYLinkId,
      }));
    } catch {}
  }

  function updateTraceLabels() {
    if (traceXLinkId === null) {
      traceXLabel = 'Time';
    } else {
      const link = allDataLinks.find(l => l.id === traceXLinkId);
      traceXLabel = link ? link.name : 'X';
    }
    if (traceYLinkId === null) {
      traceYLabel = 'RPM';
    } else {
      const link = allDataLinks.find(l => l.id === traceYLinkId);
      traceYLabel = link ? link.name : 'Y';
    }
  }

  function loadLivePanelState() {
    try {
      const raw = localStorage.getItem(`table-live-panel-${tableId}`);
      if (raw) {
        const parsed = JSON.parse(raw);
        livePanelOpen = parsed.visible ?? false;
        livePanelSensorIds = parsed.sensorIds ?? [];
      }
    } catch {}
  }

  function saveLivePanelState() {
    try {
      localStorage.setItem(`table-live-panel-${tableId}`, JSON.stringify({
        visible: livePanelOpen,
        sensorIds: livePanelSensorIds,
      }));
    } catch {}
  }

  function handleAddLiveSensor(id: number) {
    if (!livePanelSensorIds.includes(id)) {
      livePanelSensorIds = [...livePanelSensorIds, id];
      saveLivePanelState();
    }
  }

  function handleRemoveLiveSensor(id: number) {
    livePanelSensorIds = livePanelSensorIds.filter(i => i !== id);
    saveLivePanelState();
  }

  let minVal = $derived(tableData ? getDataRange(tableData.output).min : 0);
  let maxVal = $derived(tableData ? getDataRange(tableData.output).max : 100);

  let dirtyCount = $derived(dirtyCells.size + dirtyInput0.size + dirtyInput1.size);

  // Operating point indices
  let opRow = $derived.by(() => {
    if (!tableData || !tableDef) return -1;
    if (is1D) return 0;
    if (operatingPoint.map === null) return -1;
    return findNearestIndex(operatingPoint.map, tableData.input1);
  });

  let opCol = $derived.by(() => {
    if (!tableData || operatingPoint.rpm === null) return -1;
    return findNearestIndex(operatingPoint.rpm, tableData.input0);
  });

  // Interpolation ranges for range-based highlighting
  let opColRange = $derived.by(() => {
    if (!tableData || operatingPoint.rpm === null) return null;
    return findInterpolationRange(operatingPoint.rpm, tableData.input0);
  });

  let opRowRange = $derived.by(() => {
    if (!tableData || !tableDef) return null;
    if (is1D) return null;
    if (operatingPoint.map === null) return null;
    return findInterpolationRange(operatingPoint.map, tableData.input1);
  });

  // Live values for side panel and cell overlay
  let liveInput0Value = $derived(operatingPoint.rpm);
  let liveInput1Value = $derived(is1D ? null : operatingPoint.map);
  let liveOutputValue = $derived(operatingPoint.output);

  // Undo/redo helpers
  let lastGroupId = '';
  function newGroupId(): string {
    lastGroupId = crypto.randomUUID();
    return lastGroupId;
  }

  function recalculateDirty() {
    if (!tableData || !originalData || !tableDef) return;
    const result = calcDirty(tableData, originalData, tableDef);
    dirtyCells = result.dirtyCells;
    dirtyInput0 = result.dirtyInput0;
    dirtyInput1 = result.dirtyInput1;
  }

  function handleUndo() {
    if (!tableData || undoStack.length === 0) return;
    const groupId = undoStack[undoStack.length - 1].groupId;
    const entries: typeof undoStack = [];
    let newOutput = [...tableData.output];
    let newInput0 = [...tableData.input0];
    let newInput1 = [...tableData.input1];
    while (undoStack.length > 0 && undoStack[undoStack.length - 1].groupId === groupId) {
      const entry = undoStack.pop()!;
      entries.push(entry);
      if (entry.type === 'output' && entry.row !== undefined && entry.col !== undefined) {
        newOutput[entry.row * tableDef!.cols + entry.col] = entry.oldVal;
      } else if (entry.type === 'input0' && entry.idx !== undefined) {
        newInput0[entry.idx] = entry.oldVal;
      } else if (entry.type === 'input1' && entry.idx !== undefined) {
        newInput1[entry.idx] = entry.oldVal;
      }
    }
    undoStack = [...undoStack];
    redoStack = [...redoStack, ...entries];
    tableData = { ...tableData, output: newOutput, input0: newInput0, input1: newInput1 };
    recalculateDirty();
    persistUndo();
  }

  function handleRedo() {
    if (!tableData || redoStack.length === 0) return;
    const groupId = redoStack[redoStack.length - 1].groupId;
    const entries: typeof redoStack = [];
    let newOutput = [...tableData.output];
    let newInput0 = [...tableData.input0];
    let newInput1 = [...tableData.input1];
    while (redoStack.length > 0 && redoStack[redoStack.length - 1].groupId === groupId) {
      const entry = redoStack.pop()!;
      entries.push(entry);
      if (entry.type === 'output' && entry.row !== undefined && entry.col !== undefined) {
        newOutput[entry.row * tableDef!.cols + entry.col] = entry.newVal;
      } else if (entry.type === 'input0' && entry.idx !== undefined) {
        newInput0[entry.idx] = entry.newVal;
      } else if (entry.type === 'input1' && entry.idx !== undefined) {
        newInput1[entry.idx] = entry.newVal;
      }
    }
    redoStack = [...redoStack];
    undoStack = [...undoStack, ...entries];
    tableData = { ...tableData, output: newOutput, input0: newInput0, input1: newInput1 };
    recalculateDirty();
    persistUndo();
  }

  function persistUndo() {
    if (tableDef && tableData && originalData) {
      saveSessionCache(tableId, undoStack, redoStack, originalData, bookmarks);
    }
  }

  // ─── Bookmarks & History ─────────────────────────────────────────────────

  function handleBookmark(groupId: string, label: string) {
    if (bookmarks.some(b => b.groupId === groupId)) return;
    bookmarks = [...bookmarks, { groupId, label, timestamp: Date.now() }];
    persistUndo();
    toast('Bookmark saved', 'success');
  }

  function handleRemoveBookmark(groupId: string) {
    bookmarks = bookmarks.filter(b => b.groupId !== groupId);
    persistUndo();
  }

  function handleJumpToGroup(groupId: string, direction: 'undo' | 'redo') {
    if (!tableData || !tableDef) return;

    if (direction === 'undo') {
      // Undo groups until we reach the target groupId
      while (undoStack.length > 0) {
        const topGroupId = undoStack[undoStack.length - 1].groupId;
        handleUndo();
        if (topGroupId === groupId) break;
      }
    } else {
      // Redo groups until we reach the target groupId
      while (redoStack.length > 0) {
        const topGroupId = redoStack[redoStack.length - 1].groupId;
        handleRedo();
        if (topGroupId === groupId) break;
      }
    }
    historyOpen = false;
  }

  // ─── Data loading ───────────────────────────────────────────────────────

  async function loadTable() {
    loading = true;
    error = null;
    try {
      const defs = await HybridBridge.getTableDefinitions();
      if (!mounted) return;
      const def = (defs.tables as TableDefinition[]).find(t => t.id === tableId);
      if (!def) {
        error = 'Table not found';
        return;
      }
      tableDef = def;

      // Default trace Y axis to this table's primary input (RPM) if not configured
      if (traceYLinkId === null && def.input0LinkId) {
        traceYLinkId = def.input0LinkId;
        updateTraceLabels();
        saveTracePanelState();
      }

      const data = await HybridBridge.readTableData(tableId);
      if (!mounted) return;
      if (!data.success || !data.input0 || !data.output) {
        error = data.error || 'Failed to read table data';
        return;
      }

      tableData = {
        enabled: data.enabled ?? true,
        input0: data.input0,
        input1: data.input1 ?? [],
        output: data.output,
      };

      // Always set originalData to fresh ECU data (dirty is computed against this)
      originalData = JSON.parse(JSON.stringify(tableData));

      // Restore bookmarks from session cache if it exists
      const cached = loadSessionCache(tableId);
      if (cached) {
        bookmarks = cached.bookmarks ?? [];
      }
      undoStack = [];
      redoStack = [];

      dirtyCells = new Set();
      dirtyInput0 = new Set();
      dirtyInput1 = new Set();
    } catch (e) {
      if (!mounted) return;
      error = e instanceof Error ? e.message : 'Unknown error';
    } finally {
      if (mounted) loading = false;
    }
  }

  // ─── Cell editing ──────────────────────────────────────────────────────

  function handleCellClick(row: number, col: number) {
    selectedRow = row;
    selectedCol = col;
    editMode = 'output';
    editorOpen = true;
  }

  function handleAxis0Click(col: number) {
    selectedRow = 0;
    selectedCol = col;
    editMode = 'input0';
    editorOpen = true;
  }

  function handleAxis1Click(row: number) {
    selectedRow = row;
    selectedCol = 0;
    editMode = 'input1';
    editorOpen = true;
  }

  function handleApplyValue(newValue: number) {
    if (!tableData) return;
    const groupId = newGroupId();

    if (editMode === 'output') {
      const key = cellKey(selectedRow, selectedCol);
      const oldVal = getOutputValue(tableData, selectedRow, selectedCol, tableDef!.cols);
      if (oldVal === newValue) return;
      const newOutput = [...tableData.output];
      newOutput[selectedRow * tableDef!.cols + selectedCol] = newValue;
      tableData = { ...tableData, output: newOutput };
      undoStack = [...undoStack, { type: 'output', key, row: selectedRow, col: selectedCol, oldVal, newVal: newValue, groupId }];
      redoStack = [];
    } else if (editMode === 'input0') {
      const oldVal = tableData.input0[selectedCol];
      if (oldVal === newValue) return;
      const newInput0 = [...tableData.input0];
      newInput0[selectedCol] = newValue;
      tableData = { ...tableData, input0: newInput0 };
      undoStack = [...undoStack, { type: 'input0', key: `input0[${selectedCol}]`, idx: selectedCol, oldVal, newVal: newValue, groupId }];
      redoStack = [];
    } else if (editMode === 'input1') {
      const oldVal = tableData.input1[selectedRow];
      if (oldVal === newValue) return;
      const newInput1 = [...tableData.input1];
      newInput1[selectedRow] = newValue;
      tableData = { ...tableData, input1: newInput1 };
      undoStack = [...undoStack, { type: 'input1', key: `input1[${selectedRow}]`, idx: selectedRow, oldVal, newVal: newValue, groupId }];
      redoStack = [];
    }
    recalculateDirty();
    persistUndo();
  }

  function handleRevertCell() {
    if (!tableData || !originalData) return;
    if (editMode === 'output') {
      const key = cellKey(selectedRow, selectedCol);
      if (dirtyCells.has(key)) {
        const oldVal = getOutputValue(originalData, selectedRow, selectedCol, tableDef!.cols);
        const newOutput = [...tableData.output];
        newOutput[selectedRow * tableDef!.cols + selectedCol] = oldVal;
        tableData = { ...tableData, output: newOutput };
      }
    } else if (editMode === 'input0') {
      if (dirtyInput0.has(selectedCol)) {
        const oldVal = originalData.input0[selectedCol];
        const newInput0 = [...tableData.input0];
        newInput0[selectedCol] = oldVal;
        tableData = { ...tableData, input0: newInput0 };
      }
    } else if (editMode === 'input1') {
      if (dirtyInput1.has(selectedRow)) {
        const oldVal = originalData.input1[selectedRow];
        const newInput1 = [...tableData.input1];
        newInput1[selectedRow] = oldVal;
        tableData = { ...tableData, input1: newInput1 };
      }
    }
    recalculateDirty();
    editorOpen = false;
  }

  function handleRevertAll() {
    if (!tableData || !originalData) return;
    tableData = JSON.parse(JSON.stringify(originalData));
    undoStack = [];
    redoStack = [];
    recalculateDirty();
    toast('All changes reverted', 'info');
  }

  // ─── 3D editing ──────────────────────────────────────────────────────────

  function handle3DEditCell(row: number, col: number, newVal: number) {
    if (!tableData || !tableDef) return;
    const groupId = newGroupId();
    const key = cellKey(row, col);
    const oldVal = getOutputValue(tableData, row, col, tableDef.cols);
    if (oldVal === newVal) return;
    const newOutput = [...tableData.output];
    newOutput[row * tableDef.cols + col] = newVal;
    tableData = { ...tableData, output: newOutput };
    undoStack = [...undoStack, { type: 'output', key, row, col, oldVal, newVal, groupId }];
    redoStack = [];
    recalculateDirty();
  }

  function handle3DSelectionChange(row: number, col: number) {
    selectedRow = row;
    selectedCol = col;
    editMode = 'output';
  }

  function handle3DBatchEdit(edits: Array<{ row: number; col: number; newVal: number }>) {
    if (!tableData || !tableDef) return;
    const groupId = newGroupId();
    const newOutput = [...tableData.output];
    const entries: typeof undoStack = [];
    for (const edit of edits) {
      const key = cellKey(edit.row, edit.col);
      const oldVal = getOutputValue(tableData, edit.row, edit.col, tableDef.cols);
      if (oldVal === edit.newVal) continue;
      newOutput[edit.row * tableDef.cols + edit.col] = edit.newVal;
      entries.push({ type: 'output', key, row: edit.row, col: edit.col, oldVal, newVal: edit.newVal, groupId });
    }
    if (entries.length > 0) {
      tableData = { ...tableData, output: newOutput };
      undoStack = [...undoStack, ...entries];
      redoStack = [];
      recalculateDirty();
    }
  }

  // ─── Copy/Paste ──────────────────────────────────────────────────────

  function getHeaderValue(type: 'input0' | 'input1', idx: number): number {
    if (!tableData) return 0;
    return type === 'input0' ? tableData.input0[idx] : tableData.input1[idx];
  }

  function setHeaderValue(type: 'input0' | 'input1', idx: number, val: number) {
    if (!tableData) return;
    if (type === 'input0') {
      const newInput0 = [...tableData.input0];
      newInput0[idx] = val;
      tableData = { ...tableData, input0: newInput0 };
    } else {
      const newInput1 = [...tableData.input1];
      newInput1[idx] = val;
      tableData = { ...tableData, input1: newInput1 };
    }
  }

  function handleCopy() {
    if (!selection || !tableData || !tableDef) return;
    const b = selBounds();
    if (!b) return;

    if (selectionType === 'input0') {
      const vals: string[] = [];
      for (let c = b.minCol; c <= b.maxCol; c++) {
        vals.push(tableData.input0[c].toFixed(1));
      }
      const tsv = vals.join('\t');
      clipboard = tsv;
      try { navigator.clipboard?.writeText(tsv); } catch {}
      toast('Copied header values', 'info');
      return;
    }
    if (selectionType === 'input1') {
      const vals: string[] = [];
      for (let r = b.minRow; r <= b.maxRow; r++) {
        vals.push(tableData.input1[r].toFixed(1));
      }
      const tsv = vals.join('\t');
      clipboard = tsv;
      try { navigator.clipboard?.writeText(tsv); } catch {}
      toast('Copied header values', 'info');
      return;
    }

    const rows: string[] = [];
    for (let r = b.minRow; r <= b.maxRow; r++) {
      const cols: string[] = [];
      for (let c = b.minCol; c <= b.maxCol; c++) {
        cols.push(getOutputValue(tableData, r, c, tableDef.cols).toFixed(1));
      }
      rows.push(cols.join('\t'));
    }
    const tsv = rows.join('\n');
    clipboard = tsv;
    try { navigator.clipboard?.writeText(tsv); } catch {}
    toast('Copied to clipboard', 'info');
  }

  function handlePaste() {
    if (!selection || !tableData || !tableDef) return;
    const b = selBounds();
    if (!b) return;
    const text = clipboard;
    if (!text) { toast('Nothing to paste', 'warning'); return; }
    const groupId = newGroupId();

    if (selectionType === 'input0') {
      const cells = text.split('\t').filter(c => c.length > 0);
      const newInput0 = [...tableData.input0];
      const entries: typeof undoStack = [];
      for (let c = 0; c < cells.length && (b.minCol + c) <= b.maxCol; c++) {
        const idx = b.minCol + c;
        const newVal = parseFloat(cells[c]);
        if (isNaN(newVal)) continue;
        const oldVal = newInput0[idx];
        if (oldVal === newVal) continue;
        newInput0[idx] = newVal;
        entries.push({ type: 'input0', key: `input0-${idx}`, idx, oldVal, newVal, groupId });
      }
      if (entries.length > 0) {
        tableData = { ...tableData, input0: newInput0 };
        undoStack = [...undoStack, ...entries];
        redoStack = [];
        recalculateDirty();
        toast(`Pasted ${entries.length} header values`, 'success');
      }
      return;
    }
    if (selectionType === 'input1') {
      const cells = text.split('\t').filter(c => c.length > 0);
      const newInput1 = [...tableData.input1];
      const entries: typeof undoStack = [];
      for (let r = 0; r < cells.length && (b.minRow + r) <= b.maxRow; r++) {
        const idx = b.minRow + r;
        const newVal = parseFloat(cells[r]);
        if (isNaN(newVal)) continue;
        const oldVal = newInput1[idx];
        if (oldVal === newVal) continue;
        newInput1[idx] = newVal;
        entries.push({ type: 'input1', key: `input1-${idx}`, idx, oldVal, newVal, groupId });
      }
      if (entries.length > 0) {
        tableData = { ...tableData, input1: newInput1 };
        undoStack = [...undoStack, ...entries];
        redoStack = [];
        recalculateDirty();
        toast(`Pasted ${entries.length} header values`, 'success');
      }
      return;
    }

    const rows = text.split('\n').filter(r => r.length > 0);
    const newOutput = [...tableData.output];
    const entries: typeof undoStack = [];
    for (let r = 0; r < rows.length && (b.minRow + r) <= b.maxRow; r++) {
      const cells = rows[r].split('\t');
      for (let c = 0; c < cells.length && (b.minCol + c) <= b.maxCol; c++) {
        const targetRow = b.minRow + r;
        const targetCol = b.minCol + c;
        const newVal = parseFloat(cells[c]);
        if (isNaN(newVal)) continue;
        const key = cellKey(targetRow, targetCol);
        const oldVal = getOutputValue(tableData, targetRow, targetCol, tableDef.cols);
        if (oldVal === newVal) continue;
        newOutput[targetRow * tableDef.cols + targetCol] = newVal;
        entries.push({ type: 'output', key, row: targetRow, col: targetCol, oldVal, newVal, groupId });
      }
    }
    if (entries.length > 0) {
      tableData = { ...tableData, output: newOutput };
      undoStack = [...undoStack, ...entries];
      redoStack = [];
      recalculateDirty();
      toast(`Pasted ${entries.length} cells`, 'success');
    }
  }

  // ─── Transform operations (Scale, Offset, Set, Fill, Interpolate, Smooth, Clamp) ──

  function handleTransform(operation: string, params: Record<string, number>) {
    if (!selection || !tableData || !tableDef) return;
    const groupId = newGroupId();
    const result = applyTransform(operation, params, selection, selectionType, tableData, tableDef, groupId);
    if (result) {
      tableData = result.tableData;
      undoStack = [...undoStack, ...result.entries];
      redoStack = [];
      recalculateDirty();
      toast(`${operation}: ${result.entries.length} ${selectionType === 'output' ? 'cells' : 'axis values'} modified`, 'success');
      persistUndo();
    }
  }

  // ─── CSV export ─────────────────────────────────────────────────────

  async function handleExportCsv() {
    if (!tableData || !tableDef) return;
    const lines: string[] = [];
    lines.push(`# Table: ${tableDef.name}, ${tableDef.rows}×${tableDef.cols}`);
    lines.push(`# Output: ${tableDef.outputName}`);
    // Header row: column headers (input0 axis name + values)
    const headerCells = [tableDef.input0Name, ...tableData.input0.map(v => v.toLocaleString())];
    lines.push(headerCells.join(','));
    // Data rows
    for (let r = 0; r < tableDef.rows; r++) {
      const rowHeader = tableDef.rows > 1 ? `${tableDef.input1Name}: ${tableData.input1[r]}` : '';
      const rowCells = [rowHeader];
      for (let c = 0; c < tableDef.cols; c++) {
        rowCells.push(getOutputValue(tableData, r, c, tableDef.cols).toFixed(1));
      }
      lines.push(rowCells.join(','));
    }
    const csv = lines.join('\n');
    const date = new Date().toISOString().slice(0, 10);
    const safeName = tableDef.name.replace(/[^a-zA-Z0-9_-]/g, '_');
    const filename = `${safeName}_${date}.csv`;
    const result = await HybridBridge.saveFile(filename, csv, '.csv');
    if (result.success) {
      toast('CSV exported', 'success');
    } else {
      toast(`Export failed: ${result.error || 'Unknown error'}`, 'error');
    }
  }

  // ─── YAML export ───────────────────────────────────────────────────

  async function handleExportYaml() {
    if (!tableData || !tableDef) return;
    const bundle = buildExportBundle([{ def: tableDef, data: tableData }]);
    const yaml = generateYamlString(bundle);
    const date = new Date().toISOString().slice(0, 10);
    const safeName = tableDef.name.replace(/[^a-zA-Z0-9_-]/g, '_');
    const filename = `${safeName}_${date}.yaml`;
    const result = await HybridBridge.saveFile(filename, yaml, '.yaml');
    if (result.success) {
      toast('Full table exported as headless YAML', 'success');
    } else {
      toast(`Export failed: ${result.error || 'Unknown error'}`, 'error');
    }
  }

  async function handleImportYaml() {
    if (!tableDef) return;
    try {
      const result = await HybridBridge.importYamlTable();
      if (!result.picked) {
        if (result.error) toast(result.error, 'error');
        return;
      }
      if (!result.content) {
        toast('Failed to read file', 'error');
        return;
      }
      const bundle = parseImportBundle(result.content);
      if (!bundle) {
        toast('Invalid YAML format', 'error');
        return;
      }
      // Find matching table by name
      const imported = bundle.tables.find(t => t.name === tableDef!.name);
      if (!imported) {
        toast(`No table named "${tableDef!.name}" found in YAML. This file contains: ${bundle.tables.map(t => t.name).join(', ')}`, 'error');
        return;
      }
      if (!imported.output.values || imported.output.values.length === 0) {
        toast('YAML table has no output values', 'error');
        return;
      }
      // Show confirmation with warning
      openConfirm(
        'Import table values',
        `This will replace values in "${tableDef!.name}" with data from the YAML file. You can undo this operation if needed. Do not write to ECU until you have verified the result.`,
        'warning',
        async () => {
          const groupId = newGroupId();
          const newOutput = [...tableData!.output];
          const entries: typeof undoStack = [];
          const rows = Math.min(imported.output.values.length, tableDef!.rows);
          const cols = Math.min(imported.output.values[0].length, tableDef!.cols);
          for (let r = 0; r < rows; r++) {
            for (let c = 0; c < cols; c++) {
              const oldVal = getOutputValue(tableData!, r, c, tableDef!.cols);
              const newVal = imported.output.values[r][c];
              if (oldVal === newVal) continue;
              newOutput[r * tableDef!.cols + c] = newVal;
              entries.push({ type: 'output', key: cellKey(r, c), row: r, col: c, oldVal, newVal, groupId });
            }
          }
          if (entries.length > 0) {
            tableData = { ...tableData!, output: newOutput };
            undoStack = [...undoStack, ...entries];
            redoStack = [];
            recalculateDirty();
            toast(`Imported ${entries.length} cells. Verify before writing to ECU.`, 'success');
          } else {
            toast('Table values are already up to date', 'info');
          }
        }
      );
    } catch (e) {
      toast(`Import failed: ${e instanceof Error ? e.message : 'Unknown error'}`, 'error');
    }
  }

  async function handleImportCsv() {
    if (!tableDef || !tableData) return;
    try {
      const result = await HybridBridge.importCsvTable();
      if (!result.picked) {
        if (result.error) toast(result.error, 'error');
        return;
      }
      if (!result.content) {
        toast('Failed to read file', 'error');
        return;
      }
      const lines = result.content.split('\n').filter((l: string) => l.trim().length > 0);
      const dataLines = lines.filter((l: string) => !l.startsWith('#'));
      if (dataLines.length < 2) {
        toast('CSV file appears empty or invalid', 'error');
        return;
      }
      const outputValues: number[][] = [];
      for (let i = 1; i < dataLines.length; i++) {
        const cells = dataLines[i].split(',').slice(1).map((s: string) => parseFloat(s.trim()));
        outputValues.push(cells);
      }
      if (outputValues.length === 0 || outputValues[0].length === 0) {
        toast('CSV has no data values', 'error');
        return;
      }
      openConfirm(
        'Import CSV values',
        `This will replace values in "${tableDef!.name}" with data from the CSV file. You can undo this operation. Do not write to ECU until you have verified the result.`,
        'warning',
        () => {
          const groupId = newGroupId();
          const newOutput = [...tableData!.output];
          const entries: typeof undoStack = [];
          const rows = Math.min(outputValues.length, tableDef!.rows);
          const cols = Math.min(outputValues[0].length, tableDef!.cols);
          for (let r = 0; r < rows; r++) {
            for (let c = 0; c < cols; c++) {
              const oldVal = getOutputValue(tableData!, r, c, tableDef!.cols);
              const newVal = outputValues[r][c];
              if (isNaN(newVal) || oldVal === newVal) continue;
              newOutput[r * tableDef!.cols + c] = newVal;
              entries.push({ type: 'output', key: cellKey(r, c), row: r, col: c, oldVal, newVal, groupId });
            }
          }
          if (entries.length > 0) {
            tableData = { ...tableData!, output: newOutput };
            undoStack = [...undoStack, ...entries];
            redoStack = [];
            recalculateDirty();
            toast(`Imported ${entries.length} cells from CSV. Verify before writing to ECU.`, 'success');
          } else {
            toast('Table values are already up to date', 'info');
          }
        }
      );
    } catch (e) {
      toast(`Import failed: ${e instanceof Error ? e.message : 'Unknown error'}`, 'error');
    }
  }

  // ─── Safety confirmations ──────────────────────────────────────────────

  let confirmDialogOpen = $state(false);
  let confirmDialogTitle = $state('');
  let confirmDialogMessage = $state('');
  let confirmDialogVariant = $state<'danger' | 'warning' | 'default'>('default');
  let confirmDialogAction = $state<(() => void) | null>(null);

  function openConfirm(title: string, message: string, variant: 'danger' | 'warning' | 'default', action: () => void) {
    confirmDialogTitle = title;
    confirmDialogMessage = message;
    confirmDialogVariant = variant;
    confirmDialogAction = action;
    confirmDialogOpen = true;
  }

  function handleConfirm() {
    confirmDialogOpen = false;
    confirmDialogAction?.();
    confirmDialogAction = null;
  }

  function handleConfirmCancel() {
    confirmDialogOpen = false;
    confirmDialogAction = null;
  }

  // ─── ECU write/read ────────────────────────────────────────────────────

  let writing = $state(false);

  async function handleWriteToEcu() {
    if (!tableData || !tableDef) return;
    if (dirtyCount === 0) return;

    const doWrite = async () => {
      writing = true;
      try {
        const result = await HybridBridge.writeTableData(tableDef!.id, tableData!.input0, tableData!.input1, tableData!.output);
        if (result.success) {
          // Update baseline to what's now on the ECU, but keep undo history
          // so the user can undo back to the pre-write state
          originalData = JSON.parse(JSON.stringify(tableData));
          dirtyCells = new Set();
          dirtyInput0 = new Set();
          dirtyInput1 = new Set();
          toast('Table written to ECU', 'success');
        } else {
          toast(`Write failed: ${result.error || 'Unknown error'}`, 'error');
        }
      } catch (e) {
        toast(`Write failed: ${e instanceof Error ? e.message : 'Unknown error'}`, 'error');
      } finally {
        writing = false;
      }
    };

    openConfirm('Write to ECU', `Write ${dirtyCount} change${dirtyCount !== 1 ? 's' : ''} to ECU?`, 'warning', doWrite);
  }

  async function handleReadFromEcu() {
    if (dirtyCount > 0) {
      openConfirm('Re-read from ECU', `You have ${dirtyCount} unsaved change${dirtyCount !== 1 ? 's' : ''}. Discard and re-read from ECU?`, 'danger', async () => {
        await loadTable();
        toast('Table read from ECU', 'success');
      });
    } else {
      await loadTable();
      toast('Table read from ECU', 'success');
    }
  }

  // ─── Editor value computation ──────────────────────────────────────────

  let editorValue = $derived.by(() => {
    if (!tableData || !tableDef) return 0;
    if (editMode === 'output') {
      return getOutputValue(tableData, selectedRow, selectedCol, tableDef.cols);
    } else if (editMode === 'input0') {
      return tableData.input0[selectedCol];
    } else {
      return tableData.input1[selectedRow];
    }
  });

  let editorOriginalValue = $derived.by(() => {
    if (!originalData || !tableDef) return editorValue;
    if (editMode === 'output') {
      return getOutputValue(originalData, selectedRow, selectedCol, tableDef.cols);
    } else if (editMode === 'input0') {
      return originalData.input0[selectedCol];
    } else {
      return originalData.input1[selectedRow];
    }
  });

  let editorLabel = $derived.by(() => {
    if (!tableData || !tableDef) return '';
    if (editMode === 'output') {
      const rpm = tableData.input0[selectedCol];
      const map = tableData.input1[selectedRow];
      return is1D
        ? `${tableDef.input0Name}: ${rpm.toLocaleString()}${tableDef.input0Unit ? ` (${tableDef.input0Unit})` : ''}`
        : `${tableDef.input0Name}: ${rpm.toLocaleString()}${tableDef.input0Unit ? ` (${tableDef.input0Unit})` : ''} / ${tableDef.input1Name}: ${map}${tableDef.input1Unit ? ` (${tableDef.input1Unit})` : ''}`;
    } else if (editMode === 'input0') {
      return `${tableDef.input0Name}${tableDef.input0Unit ? ` (${tableDef.input0Unit})` : ''} axis [${selectedCol}]`;
    } else {
      return `${tableDef.input1Name}${tableDef.input1Unit ? ` (${tableDef.input1Unit})` : ''} axis [${selectedRow}]`;
    }
  });

  // ─── Info row ─────────────────────────────────────────────────────────

  let infoText = $derived.by(() => {
    if (!tableData || !tableDef) return '';
    const outVal = getOutputValue(tableData, selectedRow, selectedCol, tableDef.cols);
    const outFmt = outVal.toFixed(1);
    if (is1D) {
      const axisVal = tableData.input0[selectedCol];
      return `${tableDef.input0Name}: ${axisVal.toLocaleString()}${tableDef.input0Unit ? ` (${tableDef.input0Unit})` : ''} → ${outFmt}${tableDef.outputUnit ? ` (${tableDef.outputUnit})` : ''}`;
    }
    const axis0Val = tableData.input0[selectedCol];
    const axis1Val = tableData.input1[selectedRow];
    return `${tableDef.input1Name}: ${axis1Val}${tableDef.input1Unit ? ` (${tableDef.input1Unit})` : ''} × ${tableDef.input0Name}: ${axis0Val.toLocaleString()}${tableDef.input0Unit ? ` (${tableDef.input0Unit})` : ''} → ${outFmt}${tableDef.outputUnit ? ` (${tableDef.outputUnit})` : ''}`;
  });

  let infoDelta = $derived.by(() => {
    if (!tableData || !originalData || !tableDef) return null;
    const current = getOutputValue(tableData, selectedRow, selectedCol, tableDef.cols);
    const original = getOutputValue(originalData, selectedRow, selectedCol, tableDef.cols);
    const delta = current - original;
    if (Math.abs(delta) < 0.001) return null;
    return delta;
  });

  // Quick-jump table switcher
  let allTables = $state<{ id: number; name: string; category: string }[]>([]);
  let quickJumpOpen = $state(false);

  async function loadAllTables() {
    try {
      const defs = await HybridBridge.getTableDefinitions();
      if (!mounted) return;
      allTables = (defs.tables as TableDefinition[]).map(t => ({ id: t.id, name: t.name, category: t.category }));
    } catch {}
  }

  function handleQuickJump(tableId: number) {
    if (tableId === tableId) return;
    if (dirtyCount > 0) {
      openConfirm('Switch Table', `You have ${dirtyCount} unsaved change${dirtyCount !== 1 ? 's' : ''}. Discard and switch?`, 'warning', () => {
        quickJumpOpen = false;
        onNavigate('tableEditor', { tableId });
      });
    } else {
      quickJumpOpen = false;
      onNavigate('tableEditor', { tableId });
    }
  }

  onMount(() => {
    mounted = true;
    loadTable();
    loadAllTables();
    loadLivePanelState();
    loadTracePanelState();
    refreshHasNote();

    // Load data links for side panel sensor picker and trace axis labels
    HybridBridge.getDataLinks().then(result => {
      if (!mounted) return;
      allDataLinks = result.dataLinks ?? [];
      updateTraceLabels();
    }).catch(() => {});
  });

  onDestroy(() => {
    mounted = false;
    // Persist undo/redo + original data so it survives page navigation
    if (tableDef && tableData && originalData) {
      saveSessionCache(tableId, undoStack, redoStack, originalData);
    }
  });

  // ─── Keyboard shortcuts ────────────────────────────────────────────────

  function handleKeydown(e: KeyboardEvent) {
    if (!tableData || !tableDef) return;
    if (e.key === 'Escape') {
      if (anchor || selection) {
        clearSelection();
      } else {
        editorOpen = false;
      }
      return;
    }
    if ((e.ctrlKey || e.metaKey) && e.key === 'z' && !e.shiftKey) {
      e.preventDefault();
      handleUndo();
      return;
    }
    if ((e.ctrlKey || e.metaKey) && (e.key === 'y' || (e.key === 'z' && e.shiftKey))) {
      e.preventDefault();
      handleRedo();
      return;
    }
    if ((e.ctrlKey || e.metaKey) && e.key === 'a') {
      e.preventDefault();
      selectAll();
      return;
    }
    // Arrow key navigation (only when editor is closed)
    if (!editorOpen && ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(e.key)) {
      e.preventDefault();

      let newMode = editMode;
      let newRow = selectedRow;
      let newCol = selectedCol;

      if (editMode === 'output') {
        // In data cells — allow moving to headers at edges
        if (e.key === 'ArrowUp' && selectedRow === 0) {
          newMode = 'input1';
          newRow = selectedCol; // row header tracks the column we were on
        } else if (e.key === 'ArrowLeft' && selectedCol === 0) {
          newMode = 'input0';
          newCol = selectedRow; // column header tracks the row we were on
        } else {
          if (e.key === 'ArrowUp') newRow = Math.max(0, selectedRow - 1);
          else if (e.key === 'ArrowDown') newRow = Math.min(tableDef.rows - 1, selectedRow + 1);
          else if (e.key === 'ArrowLeft') newCol = Math.max(0, selectedCol - 1);
          else if (e.key === 'ArrowRight') newCol = Math.min(tableDef.cols - 1, selectedCol + 1);
        }
      } else if (editMode === 'input1') {
        // On a row header — ArrowDown goes to output, ArrowLeft/Right moves between headers
        if (e.key === 'ArrowDown') {
          newMode = 'output';
          newRow = 0;
          newCol = selectedRow; // the row header index was stored as selectedRow
        } else if (e.key === 'ArrowLeft') {
          newRow = Math.max(0, selectedRow - 1);
        } else if (e.key === 'ArrowRight') {
          newRow = Math.min(tableDef.cols - 1, selectedRow + 1);
        }
      } else if (editMode === 'input0') {
        // On a column header — ArrowRight goes to output, ArrowUp/Down moves between headers
        if (e.key === 'ArrowRight') {
          newMode = 'output';
          newRow = selectedCol; // the col header index was stored as selectedCol
          newCol = 0;
        } else if (e.key === 'ArrowUp') {
          newCol = Math.max(0, selectedCol - 1);
        } else if (e.key === 'ArrowDown') {
          newCol = Math.min(tableDef.rows - 1, selectedCol + 1);
        }
      }

      if (e.shiftKey) {
        // Extend selection from anchor
        if (!anchor) {
          anchor = { row: selectedRow, col: selectedCol };
          selectionType = editMode;
        }
        selection = { startRow: anchor.row, startCol: anchor.col, endRow: newRow, endCol: newCol };
        selectionType = newMode;
      } else {
        if (anchor || selection) clearSelection();
        editorOpen = false;
      }

      editMode = newMode;
      if (newMode === 'output') {
        selectedRow = newRow;
        selectedCol = newCol;
      } else if (newMode === 'input1') {
        selectedRow = newRow;
      } else if (newMode === 'input0') {
        selectedCol = newCol;
      }

      // Scroll into view
      const cellEl = document.querySelector(`[data-cell="${newRow},${newCol}"]`);
      cellEl?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
      return;
    }
    if (!editorOpen && e.key === 'Enter') {
      e.preventDefault();
      editorOpen = true;
      return;
    }
    if (editorOpen && e.key === 'Tab') {
      e.preventDefault();
      // Tab applies and moves right
      if (selectedCol < tableDef.cols - 1) {
        selectedCol++;
      }
      return;
    }
  }

  function selectAll() {
    if (!tableDef) return;
    anchor = { row: 0, col: 0 };
    selection = { startRow: 0, startCol: 0, endRow: tableDef.rows - 1, endCol: tableDef.cols - 1 };
    selectionType = 'output';
    editorOpen = false;
  }
</script>

<svelte:window on:keydown={handleKeydown} />

<div class="flex h-full flex-col" style="background-color: var(--metro-bg);">
  {#if loading}
    <div class="flex flex-1 items-center justify-center">
      <span class="inline-block h-6 w-6 animate-spin rounded-full border-2" style="border-color: var(--metro-border); border-top-color: var(--metro-orange);"></span>
    </div>
  {:else if error}
    <div class="flex flex-1 items-center justify-center">
      <div class="text-center">
        <p class="mb-3 text-[13px] text-[var(--metro-red)]">{error}</p>
        <button
          class="metro-btn-secondary"
          onclick={() => onBack ? onBack() : onNavigate('tableList')}
        >
          Back
        </button>
      </div>
    </div>
  {:else if tableDef && tableData}
    <!-- Toolbar -->
    <TableToolbar
      tableName={tableDef.name}
      dimensions="{tableDef.rows}×{tableDef.cols}"
      {dirtyCount}
      undoCount={undoStack.length}
      redoCount={redoStack.length}
      {writing}
      {diffMode}
      {hasNote}
      {colorScheme}
      {showContours}
      selecting={anchor !== null && selection === null}
      onToggleDiffMode={() => { diffMode = !diffMode; }}
      onToggleContours={() => { showContours = !showContours; }}
      onOpenNotes={() => { notesOpen = true; }}
      onWrite={handleWriteToEcu}
      onRead={handleReadFromEcu}
      onUndo={handleUndo}
      onRedo={handleRedo}
      onRevertAll={handleRevertAll}
      onBack={onBack ?? (() => onNavigate('tableList'))}
      onExportCsv={handleExportCsv}
      onExportYaml={handleExportYaml}
      onImportCsv={handleImportCsv}
      onImportYaml={handleImportYaml}
      onColorSchemeChange={handleColorSchemeChange}
      onTableNameClick={() => { quickJumpOpen = true; }}
      onOpenHistory={() => { historyOpen = true; }}
      {livePanelOpen}
      onToggleLivePanel={() => { livePanelOpen = !livePanelOpen; saveLivePanelState(); }}
      {view3D}
      onToggleView3D={is1D ? undefined : () => { view3D = !view3D; }}
    />

    <!-- Selection hint -->
    {#if anchor && !selection}
      <button
        class="flex items-center gap-2 px-3 py-1.5 text-[11px] font-bold uppercase tracking-wider transition-colors duration-150"
        style="border-bottom: 1px solid rgba(216,59,1,0.3); background-color: rgba(216,59,1,0.1); color: var(--metro-orange);"
        onclick={clearSelection}
      >
        <span class="inline-block h-2 w-2 animate-pulse rounded-full" style="background-color: var(--metro-orange);"></span>
        Tap another cell to select a range, or tap here to cancel
      </button>
    {/if}

    <!-- Selection info row -->
    {#if editorOpen || dirtyCount > 0}
      <div
        class="flex items-center gap-2 px-3 py-1.5 text-[11px]"
        style="border-bottom: 1px solid var(--metro-border-subtle); background-color: var(--metro-surface);"
      >
        <span class="text-[var(--metro-text-secondary)]">{infoText}</span>
        {#if infoDelta !== null}
          <span class="font-medium" style="color: {infoDelta > 0 ? 'var(--metro-green)' : 'var(--metro-red)'};">
            {infoDelta > 0 ? '+' : ''}{infoDelta.toFixed(1)}
          </span>
        {/if}
        {#if dirtyCount > 0}
          <span class="ml-auto" style="color: var(--metro-orange);">{dirtyCount} changed</span>
        {/if}
      </div>
    {/if}

    <!-- Editor area + Live side panel -->
    <div class="flex flex-1 min-h-0 overflow-hidden flex-col">
      <div class="flex flex-1 min-h-0 overflow-hidden">
        <div class="relative flex-1 min-h-0 overflow-hidden">
        {#if is1D}
          <CurveEditor
            {tableDef}
            {tableData}
            {selectedCol}
            opColRange={opColRange}
            {minVal}
            {maxVal}
            {anchor}
            {selection}
            {selectionType}
            {dirtyCells}
            {dirtyInput0}
            {diffMode}
            {originalData}
            {colorScheme}
            {liveOutputValue}
            onCellClick={handleCellClick}
            onAxis0Click={handleAxis0Click}
            onAnchorSet={handleAnchorSet}
            onSelectionComplete={handleSelectionComplete}
            onSelectionClear={clearSelection}
            onContextMenu={handleContextMenu}
          />
        {:else if view3D}
          <Graph3D
            {tableDef}
            {tableData}
            {colorScheme}
            showContours={showContours}
            {opRow}
            {opCol}
            operatingPointHistory={liveDataStore.operatingPointHistory}
            onEditCell={handle3DEditCell}
            onBatchEdit={handle3DBatchEdit}
            onUndo={handleUndo}
            onRedo={handleRedo}
            onSelectionChange={handle3DSelectionChange}
          />
        {:else}
    <TableGrid
      {tableDef}
      {tableData}
      {selectedRow}
      {selectedCol}
      {editMode}
      opRowRange={opRowRange}
      opColRange={opColRange}
      {dirtyCells}
      {dirtyInput0}
      {dirtyInput1}
      {minVal}
      {maxVal}
      {anchor}
      {selection}
      {selectionType}
      {diffMode}
      {originalData}
      {colorScheme}
      {showContours}
      {liveOutputValue}
      opHistory={liveDataStore.operatingPointHistory}
      onCellClick={handleCellClick}
            onAxis0Click={handleAxis0Click}
            onAxis1Click={handleAxis1Click}
            onAnchorSet={handleAnchorSet}
            onSelectionComplete={handleSelectionComplete}
            onSelectionClear={clearSelection}
            onContextMenu={handleContextMenu}
          />
        {/if}

        <CellEditor
          open={editorOpen}
          tableName={tableDef.name}
          label={editorLabel}
          value={editorValue}
          originalValue={editorOriginalValue}
          increment={tableDef.incrementValue}
          {editMode}
          axisName={editMode === 'input0' ? tableDef.input0Name : editMode === 'input1' ? tableDef.input1Name : ''}
          onApply={handleApplyValue}
          onRevert={handleRevertCell}
          onClose={() => { editorOpen = false; }}
        />
        </div>

        <LiveSidePanel
          open={livePanelOpen}
          {tableDef}
          {liveValues}
          dataLinks={allDataLinks}
          sensorIds={livePanelSensorIds}
          onAddSensor={handleAddLiveSensor}
          onRemoveSensor={handleRemoveLiveSensor}
          onClose={() => { livePanelOpen = false; saveLivePanelState(); }}
        />
      </div>

      <!-- Trace Panel -->
      <div class="flex-shrink-0" style="border-top: 1px solid var(--metro-border-subtle, rgba(255,255,255,0.08));">
        <button
          class="flex w-full items-center gap-2 px-3 py-1.5 text-[11px] font-semibold uppercase tracking-wider transition-colors duration-150"
          style="background-color: var(--metro-surface, rgba(0,0,0,0.3)); color: var(--metro-text-secondary, rgba(255,255,255,0.5));"
          onclick={() => { tracePanelOpen = !tracePanelOpen; saveTracePanelState(); }}
        >
          <span class="inline-block transition-transform duration-150 {tracePanelOpen ? 'rotate-90' : ''}">&#9654;</span>
          Trace Panel
          {#if tracePanelOpen}
            <span class="ml-auto text-[10px] opacity-60">{traceXLabel} vs {traceYLabel}</span>
          {/if}
        </button>
        {#if tracePanelOpen}
          <div class="px-2 pb-2" style="background-color: var(--metro-bg);">
            <TracePanel
              history={liveDataStore.operatingPointHistory}
              xAxisLabel={traceXLabel}
              yAxisLabel={traceYLabel}
              xAxisLinkId={traceXLinkId}
              yAxisLinkId={traceYLinkId}
              width={600}
              height={220}
            />
          </div>
        {/if}
      </div>
    </div>
    {/if}
</div>

<ConfirmDialog
  open={confirmDialogOpen}
  title={confirmDialogTitle}
  message={confirmDialogMessage}
  variant={confirmDialogVariant}
  onConfirm={handleConfirm}
  onCancel={handleConfirmCancel}
/>

{#if selectionToolbarOpen && selection}
  <SelectionToolbar
    {selection}
    onCopy={handleCopy}
    onPaste={handlePaste}
    onTransform={() => { opsSheetOpen = true; }}
    onClose={clearSelection}
  />
{/if}

{#if selection}
  <OpsSheet
    open={opsSheetOpen}
    cellCount={selectionType === 'output'
      ? (Math.abs(selection.endRow - selection.startRow) + 1) * (Math.abs(selection.endCol - selection.startCol) + 1)
      : selectionType === 'input0'
        ? Math.abs(selection.endCol - selection.startCol) + 1
        : Math.abs(selection.endRow - selection.startRow) + 1}
    {selectionType}
    onApply={handleTransform}
    onClose={() => { opsSheetOpen = false; }}
  />
{/if}

{#if tableDef}
  <TableNotesSheet
    open={notesOpen}
    tableId={tableDef.id}
    tableName={tableDef.name}
    onclose={() => { notesOpen = false; refreshHasNote(); }}
  />
{/if}

<ContextContextMenu
  open={contextMenuOpen}
  x={contextMenuX}
  y={contextMenuY}
  items={[
    { label: 'Select Cell', action: 'selectCell', icon: IconPointer },
    { label: 'Copy', action: 'copy', icon: IconCopy },
    { label: 'Paste', action: 'paste', icon: IconClipboard },
    { label: 'Transform', action: 'transform', icon: IconTransform },
    { label: 'Select Row', action: 'selectRow', icon: IconRowInsertTop },
    { label: 'Select Column', action: 'selectCol', icon: IconColumnInsertRight },
    { label: 'Select All', action: 'selectAll', icon: IconSelectAll },
    { label: 'Jump to Op Point', action: 'jumpToOp', icon: IconTarget, disabled: opRow < 0 || opCol < 0 },
  ]}
  onSelect={handleContextMenuAction}
  onClose={() => { contextMenuOpen = false; }}
/>

{#if quickJumpOpen}
  <!-- svelte-ignore a11y_no_static_element_interactions a11y_click_events_have_key_events -->
  <div class="fixed inset-0 z-[80]" style="background-color: rgba(0,0,0,0.8);" role="button" tabindex="-1" onclick={() => { quickJumpOpen = false; }}></div>
  <div class="fixed inset-x-0 bottom-0 z-[81] border-t max-h-[60vh] overflow-auto" style="background-color: var(--metro-card); border-color: var(--metro-border);">
    <div class="mx-auto max-w-lg p-4">
      <div class="mb-3 flex items-center justify-between">
        <h3 class="text-[13px] font-bold uppercase tracking-wider text-white">Switch Table</h3>
        <button class="flex h-8 w-8 items-center justify-center text-[var(--metro-text-secondary)] hover:text-white" onclick={() => { quickJumpOpen = false; }}>&#x2715;</button>
      </div>
      {#each allTables as t (t.id)}
        <button
          class="flex w-full items-center gap-3 px-3 py-2 text-left text-[12px] transition-colors duration-100 {t.id === tableId ? 'text-[var(--metro-orange)]' : 'text-[var(--metro-text-secondary)]'}"
          style="border-bottom: 1px solid var(--metro-border-subtle);"
          onclick={() => handleQuickJump(t.id)}
          onmouseenter={(e) => { (e.currentTarget as HTMLElement).style.backgroundColor = 'var(--metro-hover)'; }}
          onmouseleave={(e) => { (e.currentTarget as HTMLElement).style.backgroundColor = 'transparent'; }}
        >
          <span class="flex-1 truncate">{t.name}</span>
          <span class="text-[10px] uppercase text-[var(--metro-text-muted)]">{t.category}</span>
          {#if t.id !== tableId}
            <span class="text-[10px] text-[var(--metro-text-muted)]">{allTables.find(x => x.id === t.id) ? '' : ''}</span>
          {/if}
        </button>
      {/each}
    </div>
  </div>
{/if}

<HistoryViewer
  open={historyOpen}
  {undoStack}
  {redoStack}
  {bookmarks}
  onJumpToGroup={handleJumpToGroup}
  onBookmark={handleBookmark}
  onRemoveBookmark={handleRemoveBookmark}
  onClose={() => { historyOpen = false; }}
/>

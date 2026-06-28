<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { HybridBridge, type BridgeEvent } from '../lib/HybridBridge';
  import type { TableDefinition, TableData, OperatingPoint } from '../lib/tables/types';
  import { is1DTable, cellKey, getOutputValue, heatColor, getDataRange, findNearestIndex } from '../lib/tables/types';
  import TableGrid from '../lib/tables/TableGrid.svelte';
  import CurveEditor from '../lib/tables/CurveEditor.svelte';
  import CellEditor from '../lib/tables/CellEditor.svelte';
  import TableToolbar from '../lib/tables/TableToolbar.svelte';
  import ConfirmDialog from '../lib/ConfirmDialog.svelte';
  import SelectionToolbar from '../lib/tables/SelectionToolbar.svelte';
  import OpsSheet from '../lib/tables/OpsSheet.svelte';
  import { toast } from '../lib/toasts.svelte';
  import { buildExportBundle, generateYamlString, parseImportBundle } from '../lib/tableExport';
  import TableNotesSheet from '../lib/TableNotesSheet.svelte';
  import { handleSelectionComplete as calcSelectionComplete } from '../lib/tables/tableSelection';
  import { selBounds as calcSelBounds, applyTransform } from '../lib/tables/tableTransforms';
  import { recalculateDirty as calcDirty, loadSessionCache, saveSessionCache, clearSessionCache } from '../lib/tables/tableUndoRedo';

  let { tableId, onNavigate }: {
    tableId: number;
    onNavigate: (page: string, params?: Record<string, unknown>) => void;
  } = $props();

  let tableDef = $state<TableDefinition | null>(null);
  let tableData = $state<TableData | null>(null);
  let originalData = $state<TableData | null>(null);
  let operatingPoint = $state<OperatingPoint>({ rpm: null, map: null });
  let loading = $state(true);
  let error = $state<string | null>(null);

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

  // Notes
  let notesOpen = $state(false);
  let hasNote = $derived.by(() => {
    if (!tableId) return false;
    try {
      return !!localStorage.getItem(`me221-note-${tableId}`);
    } catch {
      return false;
    }
  });

  // Cell editor
  let editorOpen = $state(false);

  // Dirty tracking
  let dirtyCells = $state(new Set<string>());
  let dirtyInput0 = $state(new Set<number>());
  let dirtyInput1 = $state(new Set<number>());
  let undoStack = $state<Array<{ type: string; key: string; row?: number; col?: number; idx?: number; oldVal: number; newVal: number; groupId: string }>>([]);
  let redoStack = $state<Array<{ type: string; key: string; row?: number; col?: number; idx?: number; oldVal: number; newVal: number; groupId: string }>>([]);

  let is1D = $derived(tableDef ? is1DTable(tableDef) : false);

  let minVal = $derived(tableData ? getDataRange(tableData.output).min : 0);
  let maxVal = $derived(tableData ? getDataRange(tableData.output).max : 100);

  let dirtyCount = $derived(dirtyCells.size + dirtyInput0.size + dirtyInput1.size);

  // Operating point indices
  let opRow = $derived.by(() => {
    if (!tableData || !tableDef || operatingPoint.map === null) return -1;
    if (is1D) return 0;
    return findNearestIndex(operatingPoint.map, tableData.input1);
  });

  let opCol = $derived.by(() => {
    if (!tableData || operatingPoint.rpm === null) return -1;
    return findNearestIndex(operatingPoint.rpm, tableData.input0);
  });

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
  }

  // ─── Data loading ───────────────────────────────────────────────────────

  async function loadTable() {
    loading = true;
    error = null;
    try {
      const defs = await HybridBridge.getTableDefinitions();
      const def = (defs.tables as TableDefinition[]).find(t => t.id === tableId);
      if (!def) {
        error = 'Table not found';
        return;
      }
      tableDef = def;

      const data = await HybridBridge.readTableData(tableId);
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

      // Restore session cache if it exists (user navigated away and came back)
      const cached = loadSessionCache(tableId);
      if (cached) {
        undoStack = cached.undoStack;
        redoStack = cached.redoStack;
        originalData = cached.originalData;
      } else {
        originalData = JSON.parse(JSON.stringify(tableData));
        undoStack = [];
        redoStack = [];
      }

      dirtyCells = new Set();
      dirtyInput0 = new Set();
      dirtyInput1 = new Set();
      recalculateDirty();
    } catch (e) {
      error = e instanceof Error ? e.message : 'Unknown error';
    } finally {
      loading = false;
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
    selectedCol = col;
    editMode = 'input0';
    editorOpen = true;
  }

  function handleAxis1Click(row: number) {
    selectedRow = row;
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

  // ─── Copy/Paste ──────────────────────────────────────────────────────

  function handleCopy() {
    if (!selection || !tableData || !tableDef) return;
    const b = selBounds();
    if (!b) return;
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
    const rows = text.split('\n').filter(r => r.length > 0);
    const groupId = newGroupId();
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
        ? `${tableDef.input0Name}: ${rpm.toLocaleString()}`
        : `${tableDef.input0Name}: ${rpm.toLocaleString()} / ${tableDef.input1Name}: ${map} ${tableDef.input1Name}`;
    } else if (editMode === 'input0') {
      return `${tableDef.input0Name} axis [${selectedCol}]`;
    } else {
      return `${tableDef.input1Name} axis [${selectedRow}]`;
    }
  });

  // ─── Info row ─────────────────────────────────────────────────────────

  let infoText = $derived.by(() => {
    if (!tableData || !tableDef) return '';
    const outVal = getOutputValue(tableData, selectedRow, selectedCol, tableDef.cols);
    if (is1D) {
      const axisVal = tableData.input0[selectedCol];
      return `${tableDef.input0Name}: ${axisVal.toLocaleString()} → ${outVal.toFixed(1)}`;
    }
    const axis0Val = tableData.input0[selectedCol];
    const axis1Val = tableData.input1[selectedRow];
    return `${tableDef.input1Name}: ${axis1Val} × ${tableDef.input0Name}: ${axis0Val.toLocaleString()} → ${outVal.toFixed(1)}`;
  });

  let infoDelta = $derived.by(() => {
    if (!tableData || !originalData || !tableDef) return null;
    const current = getOutputValue(tableData, selectedRow, selectedCol, tableDef.cols);
    const original = getOutputValue(originalData, selectedRow, selectedCol, tableDef.cols);
    const delta = current - original;
    if (Math.abs(delta) < 0.001) return null;
    return delta;
  });

  // ─── Live operating point ──────────────────────────────────────────────

  let unsubscribe: (() => void) | null = null;
  let lastOpUpdate = 0;

  onMount(() => {
    loadTable();

    unsubscribe = HybridBridge.onMessage((event: BridgeEvent) => {
      if (event.event === 'liveDataUpdate') {
        const now = performance.now();
        if (now - lastOpUpdate < 100) return; // 10fps throttle
        lastOpUpdate = now;
        const vals = event.values;
        const rpm = vals['rpm'] ?? vals['RPM'] ?? null;
        const map = vals['map'] ?? vals['MAP'] ?? null;
        if (rpm !== null || map !== null) {
          operatingPoint = { rpm, map };
        }
      }
    });
  });

  onDestroy(() => {
    // Persist undo/redo + original data so it survives page navigation
    if (tableDef && tableData && originalData) {
      saveSessionCache(tableId, undoStack, redoStack, originalData);
    }
    unsubscribe?.();
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
    }
    if ((e.ctrlKey || e.metaKey) && e.key === 'z' && !e.shiftKey) {
      e.preventDefault();
      handleUndo();
    }
    if ((e.ctrlKey || e.metaKey) && (e.key === 'y' || (e.key === 'z' && e.shiftKey))) {
      e.preventDefault();
      handleRedo();
    }
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
          onclick={() => onNavigate('tableList')}
        >
          Back to Tables
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
      selecting={anchor !== null && selection === null}
      onToggleDiffMode={() => { diffMode = !diffMode; }}
      onOpenNotes={() => { notesOpen = true; }}
      onWrite={handleWriteToEcu}
      onRead={handleReadFromEcu}
      onUndo={handleUndo}
      onRedo={handleRedo}
      onRevertAll={handleRevertAll}
      onBack={() => onNavigate('tableList')}
      onExportCsv={handleExportCsv}
      onExportYaml={handleExportYaml}
      onImportCsv={handleImportCsv}
      onImportYaml={handleImportYaml}
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

    <!-- Editor area -->
    <div class="relative flex-1 min-h-0 overflow-hidden">
      {#if is1D}
        <CurveEditor
          {tableDef}
          {tableData}
          {selectedCol}
          {opCol}
          {minVal}
          {maxVal}
          {anchor}
          {selection}
          {selectionType}
          {dirtyCells}
          {dirtyInput0}
          {diffMode}
          {originalData}
          onCellClick={handleCellClick}
          onAxis0Click={handleAxis0Click}
          onAnchorSet={handleAnchorSet}
          onSelectionComplete={handleSelectionComplete}
          onSelectionClear={clearSelection}
        />
      {:else}
        <TableGrid
          {tableDef}
          {tableData}
          {selectedRow}
          {selectedCol}
          {editMode}
          {opRow}
          {opCol}
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
          onCellClick={handleCellClick}
          onAxis0Click={handleAxis0Click}
          onAxis1Click={handleAxis1Click}
          onAnchorSet={handleAnchorSet}
          onSelectionComplete={handleSelectionComplete}
          onSelectionClear={clearSelection}
        />
      {/if}
    </div>

    <!-- Cell editor -->
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
    onImportYaml={handleImportYaml}
    onExportYaml={handleExportYaml}
    onClose={() => { opsSheetOpen = false; }}
  />
{/if}

{#if tableDef}
  <TableNotesSheet
    open={notesOpen}
    tableId={tableDef.id}
    tableName={tableDef.name}
    onclose={() => { notesOpen = false; hasNote = !!localStorage.getItem(`me221-note-${tableDef!.id}`); }}
  />
{/if}

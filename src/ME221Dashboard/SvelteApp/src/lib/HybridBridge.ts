/**
 * HybridWebView Bridge - TypeScript wrapper for C# <-> Svelte communication
 */

import { defaultDerivedConfig } from './derived/types';
import type {
  ConnectionStateInfo,
  ConnectionResult,
  AvailablePortsResult,
  EcuInfoResult,
  PersistedCalibrationResult,
  CalibrationMatchResult,
  PickCalibrationResult,
  DashboardConfigResult,
  VehicleConfig,
  AvailableSensor,
  SensorCustomization,
  AvailableSensorsResult,
  BridgeEvent,
  GpsStatus,
  SaveLayoutPayload,
  DriverDefinitionsResult,
  DriverDataResult,
  DriverSetResult,
  DataLinksResult,
  UpdateCheckResult,
} from './HybridBridgeTypes';

// Re-export all types for backward compatibility
export type {
  ConnectionStateInfo,
  ConnectionResult,
  AvailablePortsResult,
  LiveDataEvent,
  ConnectionStateChangedEvent,
  EcuInfoResult,
  PersistedCalibrationResult,
  CalibrationMatchResult,
  PickCalibrationResult,
  DashboardConfigResult,
  VehicleConfig,
  GaugeConfigEntry,
  EntityInfo,
  SaveLayoutPayload,
  AvailableSensor,
  SensorCustomization,
  AvailableSensorsResult,
  BridgeEvent,
  GpsLocation,
  GpsUpdateEvent,
  OdometerUpdateEvent,
  LogEntryEvent,
  GpsStatus,
  DriverDefinition,
  DriverParamDefinition,
  ComboOption,
  DriverDataResult,
  DriverDefinitionsResult,
  DataLinkDefinition,
  DataLinksResult,
  UpdateCheckResult,
} from './HybridBridgeTypes';

// ─── Bridge Implementation ──────────────────────────────────────────────────

let _bridgeAlive = true;
let _pendingCalls = 0;
const _activeCalls = new Map<string, { method: string; start: number }>();

// Serial queue for ALL bridge calls — the WebView2 InvokeDotNet handler
// processes one HTTP request at a time. Concurrent calls cause
// ERR_ADDRESS_UNREACHABLE and permanently kill the bridge.
let _bridgeQueue: Promise<any> = Promise.resolve();

function isWebViewAvailable(): boolean {
  return typeof window !== 'undefined' && 'HybridWebView' in window;
}

/** Queue any bridge call through the serial queue. */
function queuedInvoke<T>(fn: () => Promise<T>): Promise<T> {
  const p = _bridgeQueue.then(fn, fn);
  _bridgeQueue = p.catch(() => {});
  return p;
}

async function invokeDotNetLogged(method: string, params?: any[]): Promise<any> {
  return queuedInvoke(async () => {
    const callId = `${method}_${Date.now()}_${Math.random().toString(36).slice(2, 6)}`;
    const start = performance.now();
    _pendingCalls++;
    _activeCalls.set(callId, { method, start });

    if (!_bridgeAlive) {
      console.warn(`[Bridge] SKIP ${method} — bridge is DEAD`);
      _pendingCalls--;
      _activeCalls.delete(callId);
      throw new Error('Bridge is dead — invokeDotNet was called after a previous ERR_ADDRESS_UNREACHABLE');
    }

    try {
      const result = await window.HybridWebView.InvokeDotNet(method, params);
      return result;
    } catch (err: any) {
      const msg = err?.message ?? String(err);

      if (msg.includes('ERR_ADDRESS_UNREACHABLE') || msg.includes('Failed to fetch') || msg.includes('NetworkError') || msg.includes('Maximum call stack size exceeded')) {
        console.error(`[Bridge] ${method} FAILED — BRIDGE DIED: ${msg}`);
        _bridgeAlive = false;
      }
      throw err;
    } finally {
      _pendingCalls--;
      _activeCalls.delete(callId);
    }
  });
}

/** Reset bridge state (call after recovery) */
export function resetBridgeState() {
  _bridgeAlive = true;
}

export function isBridgeAlive() {
  return _bridgeAlive;
}

export const HybridBridge = {
  connectTcp: async (host: string, port: number): Promise<ConnectionResult> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('ConnectTcp', [host, port]);
    return JSON.parse(result);
  },

  connectSerial: async (portName: string, baudRate: number = 230400): Promise<ConnectionResult> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('ConnectSerial', [portName, baudRate]);
    return JSON.parse(result);
  },

  disconnect: async (): Promise<ConnectionResult> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('Disconnect');
    return JSON.parse(result);
  },

  getConnectionState: async (): Promise<ConnectionStateInfo> => {
    if (!isWebViewAvailable()) return { state: 'Disconnected' };
    const result = await invokeDotNetLogged('GetConnectionState');
    return JSON.parse(result);
  },

  getAvailablePorts: async (): Promise<AvailablePortsResult> => {
    if (!isWebViewAvailable()) return { ports: [] };
    const result = await invokeDotNetLogged('GetAvailablePorts');
    return JSON.parse(result);
  },

  enableReporting: async (): Promise<{ success: boolean; error?: string }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    try {
      const result = await invokeDotNetLogged('EnableReporting');
      return JSON.parse(result);
    } catch {
      return { success: false, error: 'Bridge unavailable' };
    }
  },

  getEcuInfo: async (): Promise<EcuInfoResult> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('GetEcuInfo');
    return JSON.parse(result);
  },

  getPersistedCalibration: async (): Promise<PersistedCalibrationResult> => {
    if (!isWebViewAvailable()) return { type: 'Error', error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('GetPersistedCalibration');
    return JSON.parse(result);
  },

  checkCalibrationMatch: async (product: string, model: string, version: string): Promise<CalibrationMatchResult> => {
    if (!isWebViewAvailable()) return { matched: false, hasSaved: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('CheckCalibrationMatch', [product, model, version]);
    return JSON.parse(result);
  },

  pickAndLoadCalibration: async (): Promise<PickCalibrationResult> => {
    if (!isWebViewAvailable()) return { picked: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('PickAndLoadCalibration');
    return JSON.parse(result);
  },

  forceUseCalibration: async (): Promise<{ success: boolean; error?: string }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('ForceUseCalibration');
    return JSON.parse(result);
  },

  // ─── Permission Methods (Android Welcome Page) ────────────────────────

  getPlatform: async (): Promise<string> => {
    if (!isWebViewAvailable()) return 'Browser';
    const result = await invokeDotNetLogged('GetPlatform');
    return JSON.parse(result).platform;
  },

  getPermissionStatus: async (): Promise<{
    isAndroid: boolean;
    usbHostAvailable: boolean;
    usbPermissionGranted: boolean;
    locationGranted: boolean;
    storageGranted: boolean;
    allGranted: boolean;
  }> => {
    if (!isWebViewAvailable()) return { isAndroid: false, usbHostAvailable: false, usbPermissionGranted: false, locationGranted: true, storageGranted: true, allGranted: true };
    const result = await invokeDotNetLogged('GetPermissionStatus');
    return JSON.parse(result);
  },

  requestUsbPermission: async (): Promise<{ granted: boolean; error?: string }> => {
    if (!isWebViewAvailable()) return { granted: true };
    const result = await invokeDotNetLogged('RequestUsbPermission');
    return JSON.parse(result);
  },

  requestLocationPermission: async (): Promise<{ granted: boolean; status?: string; error?: string }> => {
    if (!isWebViewAvailable()) return { granted: true };
    const result = await invokeDotNetLogged('RequestLocationPermission');
    return JSON.parse(result);
  },

  requestStoragePermission: async (): Promise<{ granted: boolean; action?: string; message?: string; error?: string }> => {
    if (!isWebViewAvailable()) return { granted: true };
    const result = await invokeDotNetLogged('RequestStoragePermission');
    return JSON.parse(result);
  },

  getAvailableSensors: async (dashboardName: string = 'default'): Promise<AvailableSensorsResult> => {
    if (!isWebViewAvailable()) return { sensors: [], selectedCount: 0, totalCount: 0, gridRows: 4, gridColumns: 7, backgroundImagePath: null };
    const result = await invokeDotNetLogged('GetAvailableSensors', [dashboardName]);
    return JSON.parse(result);
  },

  saveSensorSelection: async (payload: {
    dashboardName?: string;
    selectedIds: number[];
    customizations: Record<string, SensorCustomization>;
    backgroundImagePath: string | null;
  }): Promise<{ success: boolean; error?: string }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('SaveSensorSelection', [JSON.stringify(payload)]);
    return JSON.parse(result);
  },

  getDashboardConfig: async (dashboardName: string = 'default'): Promise<DashboardConfigResult> => {
    if (!isWebViewAvailable()) return { found: false, gauges: [], gridRows: 4, gridColumns: 7 };
    const result = await invokeDotNetLogged('GetDashboardConfig', [dashboardName]);
    return JSON.parse(result);
  },

  saveDashboardLayout: async (dashboardName: string, gauges: SaveLayoutPayload[], tables?: { tableId: number; fractionX: number; fractionY: number; widthFraction: number; heightFraction: number; zIndex: number; colorScheme?: string; showLabels?: boolean; showDimensionBadge?: boolean }[]): Promise<{ success: boolean; error?: string }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const payload = JSON.stringify({ dashboardName, gauges, tables });
    const result = await invokeDotNetLogged('SaveDashboardLayout', [payload]);
    return JSON.parse(result);
  },

  saveDashboardTables: async (dashboardName: string, tables: { tableId: number; fractionX: number; fractionY: number; widthFraction: number; heightFraction: number; zIndex: number; colorScheme?: string; showLabels?: boolean; showDimensionBadge?: boolean }[]): Promise<{ success: boolean; error?: string }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const payload = JSON.stringify({ dashboardName, tables });
    const result = await invokeDotNetLogged('SaveDashboardTables', [payload]);
    return JSON.parse(result);
  },

  // ─── Dashboard Name CRUD ───────────────────────────────────────────────

  getDashboardNames: async (): Promise<{ names: string[]; activeDashboard: string }> => {
    if (!isWebViewAvailable()) return { names: ['default'], activeDashboard: 'default' };
    const result = await invokeDotNetLogged('GetDashboardNames');
    return JSON.parse(result);
  },

  createDashboard: async (name: string): Promise<{ success: boolean; error?: string }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('CreateDashboard', [name]);
    return JSON.parse(result);
  },

  deleteDashboard: async (name: string): Promise<{ success: boolean; activeDashboard?: string; error?: string }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('DeleteDashboard', [name]);
    return JSON.parse(result);
  },

  renameDashboard: async (oldName: string, newName: string): Promise<{ success: boolean; activeDashboard?: string; error?: string }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('RenameDashboard', [oldName, newName]);
    return JSON.parse(result);
  },

  setActiveDashboard: async (name: string): Promise<{ success: boolean; error?: string }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('SetActiveDashboard', [name]);
    return JSON.parse(result);
  },

  // ─── Image Picker Methods ───────────────────────────────────────────────

  pickDashboardBackground: async (): Promise<{ picked: boolean; path?: string; error?: string }> => {
    if (!isWebViewAvailable()) return { picked: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('PickDashboardBackground');
    return JSON.parse(result);
  },

  pickGaugeTexture: async (gaugeId: string): Promise<{ picked: boolean; path?: string; error?: string }> => {
    if (!isWebViewAvailable()) return { picked: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('PickGaugeTexture', [gaugeId]);
    return JSON.parse(result);
  },

  getImageBase64: async (path: string): Promise<{ success: boolean; dataUrl?: string; error?: string }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('GetImageBase64', [path]);
    return JSON.parse(result);
  },

  // ─── Table Methods ────────────────────────────────────────────────────────

  getTableDefinitions: async (): Promise<{ tables: Array<{
    id: number; name: string; category: string; viewInTree: boolean;
    enabled: boolean; tableType: string; cols: number; rows: number;
    input0Name: string; input1Name: string; outputName: string;
    input0LinkId: number; input1LinkId: number; outputLinkId: number;
    incrementValue: number; defaultValue: number | null;
  }>; error?: string }> => {
    if (!isWebViewAvailable()) return { tables: [] };
    const result = await invokeDotNetLogged('GetTableDefinitions');
    return JSON.parse(result);
  },

  readTableData: async (tableId: number): Promise<{
    success: boolean;
    enabled?: boolean;
    rows?: number;
    cols?: number;
    tableType?: string;
    input0?: number[];
    input1?: number[];
    output?: number[];
    error?: string;
  }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('ReadTableData', [tableId]);
    return JSON.parse(result);
  },

  readTableDataBatch: async (tableIds: number[]): Promise<{
    results: Record<number, { success: boolean; enabled?: boolean; input0?: number[]; input1?: number[]; output?: number[] }>;
  }> => {
    if (!isWebViewAvailable()) return { results: {} };
    const result = await invokeDotNetLogged('ReadTableDataBatch', [JSON.stringify(tableIds)]);
    return JSON.parse(result);
  },

  writeTableData: async (tableId: number, input0: number[], input1: number[], output: number[]): Promise<{
    success: boolean;
    error?: string;
  }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const payload = JSON.stringify({ tableId, input0, input1, output });
    const result = await invokeDotNetLogged('WriteTableData', [payload]);
    return JSON.parse(result);
  },

  // ─── File Export ──────────────────────────────────────────────────────────

  saveFile: async (filename: string, content: string, fileExtension: string): Promise<{
    success: boolean;
    path?: string;
    error?: string;
  }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    // Base64-encode content because InvokeDotNet sends params as HTTP headers
    // which must be ISO-8859-1 — Unicode chars break it
    const b64 = btoa(unescape(encodeURIComponent(content)));
    const payload = JSON.stringify({ filename, content: b64, fileExtension, encoding: 'base64' });
    const result = await invokeDotNetLogged('SaveFile', [payload]);
    return JSON.parse(result);
  },

  saveBinaryFile: async (filename: string, base64Content: string, fileExtension: string): Promise<{
    success: boolean;
    path?: string;
    error?: string;
  }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    // content is already base64 — send directly without re-encoding
    const payload = JSON.stringify({ filename, content: base64Content, fileExtension, encoding: 'base64' });
    const result = await invokeDotNetLogged('SaveFile', [payload]);
    return JSON.parse(result);
  },

  importYamlTable: async (): Promise<{
    picked: boolean;
    content?: string;
    error?: string;
  }> => {
    if (!isWebViewAvailable()) return { picked: false, error: 'HybridWebView not available' };
    try {
      const result = await invokeDotNetLogged('ImportYamlTable');
      return JSON.parse(result);
    } catch (e) {
      return { picked: false, error: e instanceof Error ? e.message : 'Bridge call failed' };
    }
  },

  importCsvTable: async (): Promise<{
    picked: boolean;
    content?: string;
    error?: string;
  }> => {
    if (!isWebViewAvailable()) return { picked: false, error: 'HybridWebView not available' };
    try {
      const result = await invokeDotNetLogged('ImportCsvTable');
      return JSON.parse(result);
    } catch (e) {
      return { picked: false, error: e instanceof Error ? e.message : 'Bridge call failed' };
    }
  },

  // ─── GPS Methods ──────────────────────────────────────────────────────────

  startGps: async (): Promise<{ success: boolean; error?: string }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('StartGps');
    return JSON.parse(result);
  },

  stopGps: async (): Promise<{ success: boolean; error?: string }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('StopGps');
    return JSON.parse(result);
  },

  getGpsStatus: async (): Promise<GpsStatus> => {
    if (!isWebViewAvailable()) return { available: false, isRunning: false };
    const result = await invokeDotNetLogged('GetGpsStatus');
    return JSON.parse(result);
  },

  // ─── Export / Import Dashboard ───────────────────────────────────────────

  exportDashboard: async (dashboardName: string): Promise<{ success: boolean; path?: string; message?: string; error?: string }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('ExportDashboard', [dashboardName]);
    return JSON.parse(result);
  },

  importDashboard: async (): Promise<{ picked: boolean; success?: boolean; dashboardName?: string; error?: string }> => {
    if (!isWebViewAvailable()) return { picked: false, error: 'HybridWebView not available' };
    const result = await invokeDotNetLogged('ImportDashboard');
    return JSON.parse(result);
  },

  // ─── Log Streaming ─────────────────────────────────────────────────────

  startLogStreaming: async (): Promise<{ success: boolean }> => {
    if (!isWebViewAvailable()) return { success: false };
    const result = await invokeDotNetLogged('StartLogStreaming');
    return JSON.parse(result);
  },

  stopLogStreaming: async (): Promise<{ success: boolean }> => {
    if (!isWebViewAvailable()) return { success: false };
    const result = await invokeDotNetLogged('StopLogStreaming');
    return JSON.parse(result);
  },

  getRecentLogs: async (count?: number): Promise<{ entries: Array<{
    timestamp: string; level: string; category: string; message: string; exception?: string;
  }> }> => {
    if (!isWebViewAvailable()) return { entries: [] };
    const result = await invokeDotNetLogged('GetRecentLogs', count !== undefined ? [count] : []);
    return JSON.parse(result);
  },

  clearLogs: async (): Promise<{ success: boolean }> => {
    if (!isWebViewAvailable()) return { success: false };
    const result = await invokeDotNetLogged('ClearLogs');
    return JSON.parse(result);
  },

  sendRawMessage: (message: string): void => {
    if (!isWebViewAvailable()) return;
    window.HybridWebView.SendRawMessage(message);
  },

  // Centralized message parser — JSON.parse once per message, cache for all listeners.
  _lastParsedEvent: null as BridgeEvent | null,
  _listeners: new Set<(event: BridgeEvent) => void>(),

  _initMessageRouter(): void {
    if (!isWebViewAvailable()) return;
    window.addEventListener('HybridWebViewMessageReceived', (e: Event) => {
      const customEvent = e as CustomEvent<{ message: string }>;
      try {
        const parsed: BridgeEvent = JSON.parse(customEvent.detail.message);
        HybridBridge._lastParsedEvent = parsed;
        for (const handler of HybridBridge._listeners) {
          try { handler(parsed); } catch (err) { console.error('Bridge listener error:', err); }
        }
      } catch (err) {
        console.error('Failed to parse bridge message:', err);
      }
    });
  },

  onMessage: (handler: (event: BridgeEvent) => void): (() => void) => {
    HybridBridge._listeners.add(handler);
    return () => { HybridBridge._listeners.delete(handler); };
  },

  // ─── Odometer ─────────────────────────────────────────────────────

  getOdometer: async (): Promise<{ value: number; unit: string; useKilometers: boolean; speedSource: string; vssSpeedInMph: boolean }> => {
    if (!isWebViewAvailable()) return { value: 0, unit: 'km', useKilometers: true, speedSource: 'gps', vssSpeedInMph: false };
    const result = await invokeDotNetLogged('GetOdometer');
    return JSON.parse(result);
  },

  resetOdometer: async (): Promise<void> => {
    if (!isWebViewAvailable()) return;
    await invokeDotNetLogged('ResetOdometer');
  },

  setOdometerValue: async (value: number): Promise<void> => {
    if (!isWebViewAvailable()) return;
    await invokeDotNetLogged('SetOdometerValue', [value]);
  },

  setOdometerUnit: async (useKilometers: boolean): Promise<void> => {
    if (!isWebViewAvailable()) return;
    await invokeDotNetLogged('SetOdometerUnit', [useKilometers]);
  },

  setOdometerSpeedSource: async (source: 'gps' | 'vss'): Promise<void> => {
    if (!isWebViewAvailable()) return;
    await invokeDotNetLogged('SetOdometerSpeedSource', [source === 'vss' ? 1 : 0]);
  },

  // ─── Vehicle Config (global — not per-dashboard) ─────────────────────
  // Cached to avoid ~50 JS→C# roundtrips/sec during live derived computation.
  // Invalidated on set so user edits take effect immediately.

  _configCache: null as VehicleConfig | null,

  getVehicleConfig: async (): Promise<VehicleConfig> => {
    if (HybridBridge._configCache) return HybridBridge._configCache;
    if (!isWebViewAvailable()) return defaultDerivedConfig();
    const result = await invokeDotNetLogged('GetVehicleConfig', []);
    const config: VehicleConfig = { ...defaultDerivedConfig(), ...JSON.parse(result) };
    HybridBridge._configCache = config;
    return config;
  },

  setVehicleConfig: async (config: VehicleConfig): Promise<{ success: boolean; error?: string }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'No WebView' };
    console.log('[VEHCFG] setVehicleConfig', JSON.parse(JSON.stringify(config)));
    const result = await invokeDotNetLogged('SetVehicleConfig', [JSON.stringify(config)]);
    const parsed = JSON.parse(result);
    if (parsed.success) {
      HybridBridge._configCache = config;
      console.log('[VEHCFG] setVehicleConfig OK');
    } else {
      console.error('[VEHCFG] setVehicleConfig FAILED:', parsed.error);
    }
    return parsed;
  },

  // ─── Debug ─────────────────────────────────────────────────────────

  setDebugSpeed: async (speedKmh: number | null): Promise<{ success: boolean; error?: string }> => {
    if (!isWebViewAvailable()) return { success: false, error: 'No WebView' };
    const result = await invokeDotNetLogged('SetDebugSpeed', [speedKmh]);
    return JSON.parse(result);
  },

  // ─── Drivers ───────────────────────────────────────────────────────

  getDriverDefinitions: async (): Promise<DriverDefinitionsResult> => {
    if (!isWebViewAvailable()) return { drivers: [], error: 'No WebView' };
    const result = await invokeDotNetLogged('GetDriverDefinitions');
    return JSON.parse(result);
  },

  readDriverData: async (driverId: number): Promise<DriverDataResult> => {
    if (!isWebViewAvailable()) return { configs: [], outputLinkIds: [], inputLinkIds: [], error: 'No WebView' };
    const result = await invokeDotNetLogged('ReadDriverData', [JSON.stringify({ driverId })]);
    return JSON.parse(result);
  },

  setDriverConfig: async (
    driverId: number,
    configs: number[],
    outputLinkIds: number[],
    inputLinkIds: number[],
  ): Promise<DriverSetResult> => {
    if (!isWebViewAvailable()) return { success: false, error: 'No WebView' };
    const result = await invokeDotNetLogged('SetDriverConfig', [
      JSON.stringify({ driverId, configs, outputLinkIds, inputLinkIds }),
    ]);
    return JSON.parse(result);
  },

  storeDriver: async (driverId: number): Promise<DriverSetResult> => {
    if (!isWebViewAvailable()) return { success: false, error: 'No WebView' };
    const result = await invokeDotNetLogged('StoreDriver', [JSON.stringify({ driverId })]);
    return JSON.parse(result);
  },

  getDataLinks: async (): Promise<DataLinksResult> => {
    if (!isWebViewAvailable()) return { dataLinks: [], error: 'No WebView' };
    const result = await invokeDotNetLogged('GetDataLinks');
    return JSON.parse(result);
  },

  checkForUpdate: async (): Promise<UpdateCheckResult> => {
    if (!isWebViewAvailable()) return { updateAvailable: false, currentVersion: '', latestVersion: '', releaseUrl: '', releaseName: '', publishedAt: null };
    const result = await invokeDotNetLogged('CheckForUpdate');
    return JSON.parse(result);
  },

  openExternalUrl: async (url: string): Promise<void> => {
    if (!isWebViewAvailable()) {
      window.open(url, '_blank');
      return;
    }
    await invokeDotNetLogged('OpenExternalUrl', [url]);
  },
};

// Initialize the centralized message router at module load time,
// before any Svelte components call onMessage().
HybridBridge._initMessageRouter();

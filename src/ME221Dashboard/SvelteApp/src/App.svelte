<script lang="ts">
  import { onMount } from 'svelte';
  import { HybridBridge, type ConnectionStateInfo, type BridgeEvent, type GpsLocation } from './lib/HybridBridge';
  import WelcomePage from './pages/WelcomePage.svelte';
  import ConnectionPage from './pages/ConnectionPage.svelte';
  import CalibrationPage from './pages/CalibrationPage.svelte';
  import DashboardPage from './pages/DashboardPage.svelte';
  import DashboardConfigPage from './pages/DashboardConfigPage.svelte';
  import TableListPage from './pages/TableListPage.svelte';
  import TableEditorPage from './pages/TableEditorPage.svelte';
  import DriverListPage from './pages/DriverListPage.svelte';
  import DriverEditorPage from './pages/DriverEditorPage.svelte';
  import LogsPage from './pages/LogsPage.svelte';
  import NotificationModal from './lib/NotificationModal.svelte';
  import type { NotificationType } from './lib/NotificationModal.svelte';
  import ToastContainer from './lib/ToastContainer.svelte';
  import AppHeader from './lib/AppHeader.svelte';
  import Sidebar from './lib/Sidebar.svelte';
  import NewDashboardDialog from './lib/NewDashboardDialog.svelte';
  import VehicleConfigModal from './lib/VehicleConfigModal.svelte';

  let connectionState: ConnectionStateInfo = $state({ state: 'Disconnected' });
  let notification = $state<{
    show: boolean;
    type: NotificationType;
    title: string;
    message: string;
  }>({ show: false, type: 'error', title: '', message: '' });
  let isConnected = $derived(connectionState.state === 'Connected');
  let initializing = $state(true);
  let hasCalibratedThisSession = $state(false);

  // ─── Auto-reconnect state ─────────────────────────────────────────────
  let reconnectAttempt = $state(0);
  let reconnectTimer = $state<ReturnType<typeof setTimeout> | null>(null);
  let isReconnecting = $state(false);
  let isManualDisconnect = $state(false);
  let pageBeforeDisconnect = $state<Page>('connection');

  type Page = 'splash' | 'welcome' | 'connection' | 'calibration' | 'config' | 'dashboard' | 'tableList' | 'tableEditor' | 'driverList' | 'driverEditor' | 'logs';
  let currentPage = $state<Page>('splash');
  let pageSource = $state<Page | null>(null);
  let selectedTableId = $state<number>(0);
  let selectedDriverId = $state<number>(0);
  import { liveDataStore } from './lib/stores/LiveDataStore.svelte';

  // Remove local gpsLocation — use liveDataStore.gps instead
  let gpsLocation = $derived(liveDataStore.gps);

  // ─── Dashboard management ──────────────────────────────────────────────

  let dashboardNames = $state<string[]>(['default']);
  let activeDashboard = $state('default');
  let sidebarVisible = $state(true);
  let headerVisible = $state(true);
  let newDashboardDialog = $state(false);
  let newDashboardName = $state('');
  let newDashboardError = $state<string | null>(null);
  let vehicleConfigOpen = $state(false);
  let allSensors = $state<{ id: number; name: string }[]>([]);

  let showBottomBar = $derived(sidebarVisible && isConnected && currentPage !== 'welcome' && currentPage !== 'connection' && currentPage !== 'calibration');

  async function openVehicleConfig() {
    if (allSensors.length === 0 && isConnected) {
      try {
        const result = await HybridBridge.getAvailableSensors(activeDashboard);
        allSensors = result.sensors.map(s => ({ id: s.id, name: s.name }));
      } catch { }
    }
    vehicleConfigOpen = true;
  }

  let tick = () => new Promise(r => requestAnimationFrame(r));

  async function loadDashboardNames() {
    if (!isConnected) return;
    try {
      const result = await HybridBridge.getDashboardNames();
      dashboardNames = result.names;
      if (result.activeDashboard && result.activeDashboard !== activeDashboard) {
        activeDashboard = result.activeDashboard;
      }
    } catch {
      // ignore — will retry on next connect
    }
  }

  async function switchDashboard(name: string) {
    // Always set active on the C# side if the name actually changed
    if (name !== activeDashboard) {
      try {
        await HybridBridge.setActiveDashboard(name);
      } catch {
        // proceed anyway — set active locally
      }
      activeDashboard = name;
    }
    // Always navigate to the dashboard page — user tapped the dashboard
    // selector, they expect to see that dashboard regardless of current page.
    navigateTo('dashboard');
  }

  async function createDashboard(name: string) {
    newDashboardError = null;
    const trimmed = name.trim();
    if (!trimmed) {
      newDashboardError = 'Name is required';
      return;
    }
    const result = await HybridBridge.createDashboard(trimmed);
    if (result.success) {
      await HybridBridge.setActiveDashboard(trimmed);
      dashboardNames = [...dashboardNames, trimmed];
      activeDashboard = trimmed;
      newDashboardDialog = false;
      newDashboardName = '';
      currentPage = 'config';
    } else {
      newDashboardError = result.error || 'Failed to create dashboard';
    }
  }

  async function deleteDashboard(name: string) {
    if (dashboardNames.length <= 1) return;
    const result = await HybridBridge.deleteDashboard(name);
    if (result.success) {
      dashboardNames = dashboardNames.filter(n => n !== name);
      if (result.activeDashboard) {
        activeDashboard = result.activeDashboard;
      }
      if (currentPage === 'dashboard' || currentPage === 'config') {
        const p = currentPage;
        currentPage = 'splash';
        await tick();
        currentPage = p;
      }
    }
  }

  async function disconnectEcu() {
    isManualDisconnect = true;
    cancelReconnect();
    try {
      await HybridBridge.disconnect();
    } catch {}
    connectionState = { state: 'Disconnected' };
    currentPage = 'connection';
    isManualDisconnect = false;
  }

  async function exportDashboard() {
    try {
      const result = await HybridBridge.exportDashboard(activeDashboard);
      if (!result.success) {
        notification = {
          show: true,
          type: 'error',
          title: 'Export Failed',
          message: result.error || 'Unknown error',
        };
      } else if (result.message) {
        notification = {
          show: true,
          type: 'success',
          title: 'Dashboard Exported',
          message: result.message,
        };
      }
    } catch (err) {
      notification = { show: true, type: 'error', title: 'Export Failed', message: String(err) };
    }
  }

  async function importDashboard() {
    try {
      const result = await HybridBridge.importDashboard();
      if (result.picked && result.success && result.dashboardName) {
        await loadDashboardNames();
        if (result.dashboardName) {
          activeDashboard = result.dashboardName;
        }
        navigateTo('config');
      } else if (result.picked && !result.success) {
        notification = {
          show: true,
          type: 'error',
          title: 'Import Failed',
          message: result.error || 'Unknown error',
        };
      }
    } catch (err) {
      notification = { show: true, type: 'error', title: 'Import Failed', message: String(err) };
    }
  }

  function navigateTo(page: string, params?: Record<string, unknown>) {
    if (isConnected && page === 'connection') return;
    if (page === 'calibration' && hasCalibratedThisSession) return;
    if (page === 'tableEditor' && params?.tableId != null) {
      selectedTableId = params.tableId as number;
    }
    if (page === 'driverEditor' && params?.driverId != null) {
      selectedDriverId = params.driverId as number;
    }
    // Track where we came from for sub-pages that need proper back navigation
    if (currentPage === 'dashboard' || currentPage === 'config' || currentPage === 'tableList' || currentPage === 'driverList') {
      pageSource = currentPage;
    }
    currentPage = page as Page;
  }

  function navigateBack() {
    const target = pageSource ?? 'dashboard';
    pageSource = null;
    navigateTo(target);
  }

  function navigateToWithSource(page: string, params?: Record<string, unknown>) {
    navigateTo(page, params);
  }

  const DASHBOARD_PAGES: Page[] = ['dashboard', 'config', 'tableList', 'tableEditor', 'driverList', 'driverEditor'];
  const MAX_RECONNECT_ATTEMPTS = 5;
  const RECONNECT_BASE_DELAY_MS = 1500;

  async function handleConnectionChange(state: ConnectionStateInfo) {
    const wasConnected = connectionState.state === 'Connected';
    connectionState = state;
    if (state.state === 'Connected') {
      // Cancel any pending reconnect — we're back
      cancelReconnect();
      // Only act on the FIRST Connected transition.
      // Duplicate events (e.g. from C# after tryReconnect already handled it)
      // must NOT navigate — they'd clobber the page restore.
      if (!wasConnected) {
        // Serialize: dashboard names → reporting → navigate.
        // The bridge queue handles concurrency, but awaiting here
        // ensures calibration page doesn't mount and fire getEcuInfo()
        // before enableReporting() has finished.
        await loadDashboardNames();
        await liveDataStore.enableReporting();
        if (hasCalibratedThisSession) {
          navigateTo('dashboard');
        } else {
          navigateTo('calibration');
        }
      }
    } else if (state.state === 'Disconnected') {
      // Manual disconnect: go straight to connection page, no retry
      if (isManualDisconnect) {
        cancelReconnect();
        navigateTo('connection');
        return;
      }
      // If we're in a reconnect attempt, let the retry loop handle it
      if (isReconnecting) return;

      // If we're already on a non-dashboard page, go to connection page
      if (!DASHBOARD_PAGES.includes(currentPage) && currentPage !== 'logs') {
        navigateTo('connection');
        return;
      }

      // Auto-reconnect: we were on a dashboard/functional page
      pageBeforeDisconnect = currentPage;
      startReconnect();
    } else if (state.state === 'Error') {
      // If we're already reconnecting or on a dashboard page, suppress — retry loop handles it
      if (isReconnecting) return;
      if (DASHBOARD_PAGES.includes(currentPage) || currentPage === 'logs') {
        pageBeforeDisconnect = currentPage;
        startReconnect();
        return;
      }
      cancelReconnect();
      notification = {
        show: true,
        type: 'error',
        title: 'Connection Failed',
        message: state.error || 'Could not connect to the ECU. Check your connection settings and try again.',
      };
    }
  }

  function cancelReconnect() {
    isReconnecting = false;
    reconnectAttempt = 0;
    if (reconnectTimer) {
      clearTimeout(reconnectTimer);
      reconnectTimer = null;
    }
  }

  async function startReconnect() {
    cancelReconnect();
    isReconnecting = true;
    reconnectAttempt = 0;
    tryReconnect();
  }

  async function tryReconnect() {
    if (!isReconnecting) return;
    reconnectAttempt++;

    if (reconnectAttempt > MAX_RECONNECT_ATTEMPTS) {
      cancelReconnect();
      notification = {
        show: true,
        type: 'error',
        title: 'Connection Lost',
        message: 'Could not reconnect to the ECU after multiple attempts. Check your connection and try again.',
      };
      navigateTo('connection');
      return;
    }

    // Read last connection params from localStorage
    let params: { type?: string; host?: string; port?: number; serialPort?: string } = {};
    try {
      const raw = localStorage.getItem('me221_lastConnection');
      if (raw) params = JSON.parse(raw);
    } catch {}

    if (!params.type) {
      cancelReconnect();
      navigateTo('connection');
      return;
    }

    try {
      let result;
      if (params.type === 'tcp' && params.host && params.port) {
        result = await HybridBridge.connectTcp(params.host, params.port);
      } else if (params.type === 'serial' && params.serialPort) {
        result = await HybridBridge.connectSerial(params.serialPort);
      } else {
        cancelReconnect();
        navigateTo('connection');
        return;
      }

      if (result.success) {
        // Reconnected — serialize: reporting → names → restore page
        const restorePage = pageBeforeDisconnect;
        pageBeforeDisconnect = 'connection';
        connectionState = { state: 'Connected' };
        await liveDataStore.enableReporting();
        await loadDashboardNames();
        // Restore the page the user was on (skip pre-connection pages)
        if (restorePage !== 'connection' && restorePage !== 'splash' && restorePage !== 'welcome' && restorePage !== 'calibration') {
          currentPage = restorePage as Page;
        } else {
          currentPage = 'dashboard';
        }
        cancelReconnect();
      } else {
        // Retry with exponential backoff
        const delay = Math.min(RECONNECT_BASE_DELAY_MS * Math.pow(2, reconnectAttempt - 1), 15000);
        reconnectTimer = setTimeout(tryReconnect, delay);
      }
    } catch {
      const delay = Math.min(RECONNECT_BASE_DELAY_MS * Math.pow(2, reconnectAttempt - 1), 15000);
      reconnectTimer = setTimeout(tryReconnect, delay);
    }
  }

  function handleCalibrationDone(page: string) {
    hasCalibratedThisSession = true;
    loadDashboardNames();
    navigateTo(page);
  }

  async function startup() {
    document.documentElement.classList.add('dark');
    initializing = true;

    // Start GPS early so sensors are available in config
    HybridBridge.startGps().catch(() => {});

    try {
      const state = await HybridBridge.getConnectionState();
      connectionState = state;
      if (state.state === 'Connected') {
        hasCalibratedThisSession = true;
        // Cold-start connected — serialize: names → reporting → navigate
        await loadDashboardNames();
        await liveDataStore.enableReporting();
        navigateTo('dashboard');
        initializing = false;
        return;
      }
    } catch {}

    try {
      const platform = await HybridBridge.getPlatform();
      if (platform === 'Android') {
        const status = await HybridBridge.getPermissionStatus();
        if (status.allGranted) {
          navigateTo('connection');
        } else {
          navigateTo('welcome');
        }
      } else {
        navigateTo('connection');
      }
    } catch {
      navigateTo('connection');
    }

    initializing = false;
  }

  onMount(() => {
    startup();
    liveDataStore.start();

    // App.svelte still monitors connection changes for reconnect logic
    const unsubscribe = HybridBridge.onMessage((event: BridgeEvent) => {
      if (event.event === 'connectionStateChanged') {
        handleConnectionChange({ state: event.state, error: event.error });
      }
    });

    return () => {
      cancelReconnect();
      unsubscribe();
      liveDataStore.stop();
    };
  });
</script>

<div class="flex h-screen flex-col bg-gray-900">
  <AppHeader
    {connectionState}
    {isConnected}
    {currentPage}
    {activeDashboard}
    {headerVisible}
    onHideHeader={() => { headerVisible = false; }}
    onShowHeader={() => { headerVisible = true; }}
  />

  <div class="flex flex-1 min-h-0">
    <Sidebar
      {isConnected}
      {dashboardNames}
      {activeDashboard}
      {currentPage}
      {sidebarVisible}
      onSwitchDashboard={switchDashboard}
      onDeleteDashboard={deleteDashboard}
      onNewDashboard={() => { newDashboardDialog = true; newDashboardName = ''; newDashboardError = null; }}
      onNavigate={navigateTo}
      onDisconnect={disconnectEcu}
      onExportDashboard={exportDashboard}
      onImportDashboard={importDashboard}
      onVehicleConfig={openVehicleConfig}
      onHideSidebar={() => { sidebarVisible = false; }}
      onShowSidebar={() => { sidebarVisible = true; }}
    />

    <main class="flex-1 h-full {currentPage === 'dashboard' && isConnected ? 'overflow-hidden' : 'overflow-auto'} {showBottomBar ? 'pb-14' : ''} {currentPage === 'dashboard' && isConnected ? 'dashboard-grid' : ''}">
      {#if isReconnecting}
        <div class="sticky top-0 z-50 flex items-center justify-center gap-2 bg-yellow-900/80 px-3 py-1.5 text-xs text-yellow-200 backdrop-blur-sm">
          <span class="inline-block h-3 w-3 animate-spin rounded-full border border-yellow-400 border-t-yellow-200"></span>
          Reconnecting to ECU (attempt {reconnectAttempt}/{MAX_RECONNECT_ATTEMPTS})...
          <button class="ml-2 rounded bg-yellow-800 px-2 py-0.5 text-yellow-100 hover:bg-yellow-700" onclick={cancelReconnect}>Cancel</button>
        </div>
      {/if}
      {#if currentPage === 'splash'}
        <div class="mx-auto max-w-4xl">
          <div class="flex min-h-[60vh] items-center justify-center">
            <span class="inline-block h-8 w-8 animate-spin rounded-full border-2 border-gray-500 border-t-cyan-400"></span>
          </div>
        </div>
      {:else if currentPage === 'welcome'}
        <div class="mx-auto max-w-4xl p-4 lg:p-6 h-full">
          <WelcomePage onContinue={() => navigateTo('connection')} />
        </div>
      {:else if currentPage === 'connection'}
        <ConnectionPage
          {connectionState}
          onConnectionChange={handleConnectionChange}
        />
      {:else if currentPage === 'calibration'}
        <div class="mx-auto max-w-4xl p-4 lg:p-6 h-full">
          <CalibrationPage
            {connectionState}
            onNavigate={handleCalibrationDone}
          />
        </div>
      {:else if currentPage === 'config'}
        {#key activeDashboard}
          <DashboardConfigPage
            onNavigate={navigateTo}
            dashboardName={activeDashboard}
            onDashboardCreated={(name: string) => {
              dashboardNames = [...dashboardNames, name];
              activeDashboard = name;
            }}
          />
        {/key}
      {:else if currentPage === 'dashboard'}
        {#key activeDashboard}
          <DashboardPage
            dashboardName={activeDashboard}
            onNavigate={navigateTo}
            {gpsLocation}
          />
        {/key}
      {:else if currentPage === 'tableList'}
        <TableListPage onNavigate={navigateTo} />
      {:else if currentPage === 'tableEditor'}
        <TableEditorPage tableId={selectedTableId} onNavigate={navigateTo} onBack={navigateBack} />
      {:else if currentPage === 'driverList'}
        <DriverListPage onNavigate={navigateTo} />
      {:else if currentPage === 'driverEditor'}
        <DriverEditorPage driverId={selectedDriverId} onNavigate={navigateTo} />
      {:else if currentPage === 'logs'}
        <LogsPage onNavigate={navigateTo} />
      {/if}
    </main>
  </div>

  <NewDashboardDialog
    open={newDashboardDialog}
    name={newDashboardName}
    error={newDashboardError}
    onCreate={createDashboard}
    onClose={() => { newDashboardDialog = false; }}
  />

  {#if vehicleConfigOpen}
    <VehicleConfigModal
      open={vehicleConfigOpen}
      sensors={allSensors}
      onclose={() => { vehicleConfigOpen = false; }}
    />
  {/if}

  <NotificationModal bind:open={notification.show} type={notification.type} title={notification.title} message={notification.message} />
  <ToastContainer />
</div>

<style>
  :global(.dashboard-grid) {
    background-image: radial-gradient(circle, rgba(255,255,255,0.04) 1px, transparent 1px);
    background-size: 24px 24px;
  }
</style>

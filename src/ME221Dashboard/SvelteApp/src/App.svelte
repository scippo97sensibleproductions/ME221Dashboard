<script lang="ts">
  import { onMount, untrack } from 'svelte';
  import { HybridBridge, type ConnectionStateInfo, type BridgeEvent, type UpdateCheckResult } from './lib/HybridBridge';
  import { warningEvaluator } from './lib/stores/warningEvaluator';
  import { shiftEvaluator } from './lib/shift/shiftEvaluator';
  import { shiftLightAnnouncer } from './lib/gauges/shiftLightAnnouncer';
  import { SHIFTER_COPY } from './lib/shift/shifterConfig';
  import { DerivedEntityId } from './lib/derived/types';
  import { Modal, Button } from 'flowbite-svelte';
  import { warningToasts } from './lib/warningToasts';
  import { navigationGate } from './lib/navigationGate.svelte';
  import { pulseCounter } from './lib/gauges/pulseCounter';
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
  import EcuMonitorPage from './pages/EcuMonitorPage.svelte';
  import SessionsPage from './pages/SessionsPage.svelte';
  import WarningSettingsPage from './pages/WarningSettingsPage.svelte';
  import AppSettingsPage from './pages/AppSettingsPage.svelte';
  import GaugeBuilderPage from './pages/GaugeBuilderPage.svelte';
  import NotificationModal from './lib/NotificationModal.svelte';
  import type { NotificationType } from './lib/NotificationModal.svelte';
  import ToastContainer from './lib/ToastContainer.svelte';
  import AppHeader from './lib/AppHeader.svelte';
  import Sidebar from './lib/Sidebar.svelte';
  import NewDashboardDialog from './lib/NewDashboardDialog.svelte';
  import VehicleConfigModal from './lib/VehicleConfigModal.svelte';
  import UpdateAvailableModal from './lib/UpdateAvailableModal.svelte';
  import { warningStore } from './lib/stores/warningStore.svelte';
  import { initDeviceMode } from './lib/stores/deviceMode.svelte';

  // Update check guards — sessionStorage-backed: Vite hot reloads remount
  // App.svelte and reset in-memory flags, which would re-fire checkForUpdate()
  // and re-pop the dismissed modal on every HMR. The tab's session survives
  // HMR, so these flags do too; a real app restart (new tab) resets them.
  const UPDATE_CHECK_KEY = 'me221.updateCheckDone';
  const UPDATE_DISMISS_KEY = 'me221.updateCheckDismissed';

  function storageFlagGet(key: string): boolean {
    try { return sessionStorage.getItem(key) === '1'; } catch { return false; }
  }

  function storageFlagSet(key: string): void {
    try { sessionStorage.setItem(key, '1'); } catch { /* non-fatal */ }
  }
  let toastDataId = $state<number | null>(null);

  const newDashboardModal = navigationGate.registerModal('newDashboard');

  $effect(() => {
    if (newDashboardDialog) newDashboardModal.open();
    else newDashboardModal.close();
  });

  let connectionState: ConnectionStateInfo = $state({ state: 'Disconnected' });
  let notification = $state<{
    show: boolean;
    type: NotificationType;
    title: string;
    message: string;
  }>({ show: false, type: 'error', title: '', message: '' });
  let isConnected = $derived(connectionState.state === 'Connected');
  let hasCalibratedThisSession = $state(false);

  // ─── Auto-reconnect state ─────────────────────────────────────────────
  let reconnectAttempt = $state(0);
  let reconnectTimer = $state<ReturnType<typeof setTimeout> | null>(null);
  let isReconnecting = $state(false);
  let isManualDisconnect = $state(false);
  let pageBeforeDisconnect = $state<Page>('connection');

  type Page = 'splash' | 'welcome' | 'connection' | 'calibration' | 'config' | 'dashboard' | 'tableList' | 'tableEditor' | 'driverList' | 'driverEditor' | 'logs' | 'ecuMonitor' | 'sessions' | 'warnings' | 'settings' | 'gaugeBuilder';
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
  let updateCheckResult = $state<UpdateCheckResult | null>(null);
  let updateModalOpen = $state(false);

  // ─── Shifter dirty-form gate (U8) ───────────────────────────────────────
  // The config page registers its single save/discard routine; the gate arms
  // while the form diverges from the persisted baseline (incl. uncommitted
  // text) and intercepts navigation, dashboard create/delete, and Android back.
  let shifterApi: { save: () => Promise<boolean>; discard: () => Promise<void>; isDirty: () => boolean } | null = null;
  let shifterDirtyArmed = $state(false);
  let dirtyDialogOpen = $state(false);
  let dirtyDialogDelete = $state(false);
  let dirtyDialogTitle = $state('');
  let dirtyDialogBody = $state('');
  let dirtyDialogDiscardLabel = $state('');

  function registerShifterApi(api: { save: () => Promise<boolean>; discard: () => Promise<void>; isDirty: () => boolean }) {
    shifterApi = api;
  }

  function setShifterDirty(dirty: boolean) {
    shifterDirtyArmed = dirty;
    navigationGate.setBlocked('dirty-form', dirty);
    // When the page disarms (unmount, save, discard), a dangling dialog must
    // never stay open — it could invoke a dead page's save routine.
    if (!dirty) dirtyDialogOpen = false;
  }

  function openDirtyDialog(deleteVariant: boolean) {
    dirtyDialogDelete = deleteVariant;
    dirtyDialogTitle = SHIFTER_COPY.dirtyTitle;
    dirtyDialogBody = SHIFTER_COPY.dirtyBody;
    dirtyDialogDiscardLabel = deleteVariant ? SHIFTER_COPY.dirtyDelete : SHIFTER_COPY.dirtyDiscard;
    dirtyDialogOpen = true;
  }

  async function resolveDirty(action: 'save' | 'discard') {
    const pending = navigationGate.takePendingNavigation();
    dirtyDialogOpen = false;
    if (action === 'save') {
      // The page's save routine reports persistence failure: stay on the page
      // (the gate stays armed and the error is surfaced inline) instead of
      // navigating away with the edits silently lost.
      const saved = await shifterApi?.save();
      if (saved === false) return;
    } else {
      await shifterApi?.discard();
    }
    // Clear the gate explicitly — the page's dirty effect may not have flushed
    // yet, and navigating while armed would re-open the dialog.
    navigationGate.setBlocked('dirty-form', false);
    shifterDirtyArmed = false;
    if (pending?.disconnect) {
      await performDisconnect();
    } else if (pending?.deleteName) {
      await performDeleteDashboard(pending.deleteName);
    } else if (pending?.createName) {
      await createDashboard(pending.createName);
    } else if (pending) {
      navigateTo(pending.page, pending.params);
    }
  }

  function dirtyStay() {
    dirtyDialogOpen = false;
    navigationGate.clearPendingNavigation();
  }

  // Vehicle config modal (R8): populate the sensor list from the active
  // dashboard on open — the modal was previously unreachable (never opened,
  // sensors never populated).
  async function openVehicleConfig() {
    try {
      const result = await HybridBridge.getAvailableSensors(activeDashboard);
      allSensors = (result.sensors ?? []).map(s => ({ id: s.id, name: s.name }));
    } catch {
      allSensors = [];
    }
    vehicleConfigOpen = true;
  }

  // ─── Warning evaluation (shared evaluator, raw values) ─────────────────
  // Load warning settings + delay when connected; reset on disconnect.
  $effect(() => {
    if (isConnected) {
      HybridBridge.getWarningSettings().then(p => {
        warningEvaluator.refresh(p);
        warningToasts.refreshDisplayLookup();
      }).catch(() => {});
    } else {
      warningEvaluator.reset();
    }
  });

  // ─── Shift-light machine (KTD4) ────────────────────────────────────────
  // Reset the tick-driven evaluator when the active dashboard changes, and
  // clear the −3005/−3006 entity slots so no stale values bleed into the
  // newly mounted dashboard's first frames (the tick re-fills them).
  // The slot clears are untracked: they must NOT make this effect depend on
  // the live values proxy (the 150 ms tick writes those keys every tick, so
  // a tracked write would reset the evaluator ~6×/s and the floor-crossing
  // edge could never fire).
  $effect(() => {
    void activeDashboard;
    shiftEvaluator.reset();
    // Reset the announcement coordinator too: its dedupe is session-scoped, so
    // a switch to another dashboard (or back) must re-allow announcements.
    shiftLightAnnouncer.reset();
    untrack(() => {
      const v = liveDataStore.values;
      v[String(DerivedEntityId.RpmToShift)] = null;
      v[String(DerivedEntityId.ShiftState)] = null;
    });
  });

  // Feed the evaluator raw datalink values every frame.
  $effect(() => {
    void liveDataStore.frameCount;
    const now = performance.now();
    warningEvaluator.step(now, liveDataStore.values);
    warningToasts.tick(now);
  });

  let showBottomBar = $derived(sidebarVisible && isConnected && currentPage !== 'welcome' && currentPage !== 'connection' && currentPage !== 'calibration');

  // Load per-dashboard view state (top/side bar visibility) when the dashboard page is shown.
  $effect(() => {
    if (!isConnected || currentPage !== 'dashboard') return;
    HybridBridge.getDashboardConfig(activeDashboard)
      .then(result => {
        if (result.error) return;
        // Only apply when the persisted config actually carries the fields,
        // so old dashboards keep the current session defaults.
        if (result.headerVisible !== undefined) headerVisible = result.headerVisible;
        if (result.sidebarVisible !== undefined) sidebarVisible = result.sidebarVisible;
      })
      .catch(() => {});
  });

  function persistViewState() {
    HybridBridge.saveDashboardViewState(activeDashboard, { headerVisible, sidebarVisible }).catch(() => {});
  }

  let tick = () => new Promise(r => requestAnimationFrame(r));

  async function loadDashboardNames() {
    if (!isConnected) return;
    try {
      const result = await HybridBridge.getDashboardNames();
      dashboardNames = result.names;
      if (result.activeDashboard && result.activeDashboard !== activeDashboard) {
        activeDashboard = result.activeDashboard;
        // Sync active dashboard to C# side so odometer/gps work correctly
        await HybridBridge.setActiveDashboard(result.activeDashboard);
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
    // Dirty check fires at the NewDashboardDialog confirm handler (U8): the
    // creation is queued until the gate resolves.
    if (navigationGate.isReasonActive('dirty-form')) {
      navigationGate.stashNavigation({ page: 'config', createName: name });
      openDirtyDialog(false);
      return;
    }
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
    // Route through the dirty gate: queued until it resolves, then the delete
    // confirm follows (the delete path skips the save routine — U8).
    if (navigationGate.isReasonActive('dirty-form')) {
      navigationGate.stashNavigation({ page: 'dashboard', deleteName: name });
      openDirtyDialog(true);
      return;
    }
    await performDeleteDashboard(name);
  }

  async function performDeleteDashboard(name: string) {
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
    // Route through the dirty gate: unsaved shifter edits must not be dropped
    // by a disconnect, and the gate must not stay armed on a dead page.
    if (navigationGate.isReasonActive('dirty-form')) {
      navigationGate.stashNavigation({ page: 'connection', disconnect: true });
      openDirtyDialog(false);
      return;
    }
    await performDisconnect();
  }

  async function performDisconnect() {
    isManualDisconnect = true;
    cancelReconnect();
    try {
      await HybridBridge.disconnect();
    } catch { /* proceed with local state reset */ }
    connectionState = { state: 'Disconnected' };
    currentPage = 'connection';
    isManualDisconnect = false;
    warningStore.reset();
  }

  function navigateTo(page: string, params?: Record<string, unknown>) {
    // Dirty-form gate: stash the target and let the dialog resolve it
    // (Stay clears the stash; Discard/Save-and-leave execute it).
    if (navigationGate.isReasonActive('dirty-form')) {
      navigationGate.stashNavigation({ page, params });
      openDirtyDialog(false);
      return;
    }
    if (navigationGate.isNavigationBlocked()) return;
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

  function navigateBackTarget(): string {
    return pageSource ?? 'dashboard';
  }

  // Android back interception (U8): the native OnBackPressed callback is
  // enabled only while the app actually handles back — the dirty gate is armed
  // or a back-capable page is mounted. At the root (dashboard, connection,
  // splash, …) interception stays off so back exits the app normally.
  const BACK_CAPABLE_PAGES: Page[] = ['config', 'dashboard', 'tableList', 'tableEditor', 'driverList', 'driverEditor', 'logs', 'ecuMonitor', 'sessions', 'warnings', 'settings', 'gaugeBuilder'];

  $effect(() => {
    const want = shifterDirtyArmed || BACK_CAPABLE_PAGES.includes(currentPage);
    HybridBridge.setBackInterceptionEnabled(want).catch(() => {});
  });

  const DASHBOARD_PAGES: Page[] = ['dashboard', 'config', 'tableList', 'tableEditor', 'driverList', 'driverEditor', 'ecuMonitor', 'warnings', 'gaugeBuilder'];
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
        warningToasts.firstRunCompleted();
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

    // Read last connection params from native file
    let params: { type?: string; host?: string; port?: number; serialPort?: string } = {};
    try {
      const last = await HybridBridge.getLastConnection();
      if (last) params = last;
    } catch { /* fall back to manual connection */ }

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
        // Update native file if device was renamed (Android USB re-enumeration)
        if (result.deviceName && result.deviceName !== params.serialPort) {
          HybridBridge.saveLastConnection({
            type: params.type ?? 'serial',
            serialPort: result.deviceName,
          }).catch(() => {});
        }
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

  async function handleCalibrationDone(page: string) {
    hasCalibratedThisSession = true;
    // Resolve the real active dashboard BEFORE the next page mounts: the config
    // page/dashboard query their dashboard by name on mount, and a 'default'
    // fallback here would load the wrong dashboard's sensors (no customizations).
    await loadDashboardNames();
    navigateTo(page);
  }

  async function startup() {
    document.documentElement.classList.add('dark');

    // Determine UI mode (mobile/desktop) from the native platform BEFORE
    // any page is shown — never guessed from CSS/viewport width.
    await initDeviceMode();

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
        return;
      }
    } catch { /* fall through to platform checks */ }

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
  }

  onMount(() => {
    startup();
    liveDataStore.start();
    warningStore.loadHistory();
    warningToasts.attachEvaluator(warningEvaluator);
    pulseCounter.attachEvaluator(warningEvaluator);

    // Check for updates once per session (non-blocking) — the storage flag is
    // set BEFORE the async check so a hot reload mid-flight cannot re-fire it.
    if (!storageFlagGet(UPDATE_CHECK_KEY)) {
      storageFlagSet(UPDATE_CHECK_KEY);
      HybridBridge.checkForUpdate().then(result => {
        if (result.updateAvailable && !storageFlagGet(UPDATE_DISMISS_KEY)) {
          updateCheckResult = result;
          updateModalOpen = true;
        }
      }).catch(() => {});
    }

    // App.svelte still monitors connection changes for reconnect logic
    const unsubscribe = HybridBridge.onMessage((event: BridgeEvent) => {
      if (event.event === 'connectionStateChanged') {
        if (event.state !== 'Connected') {
          warningEvaluator.reset();
          shiftEvaluator.reset();
          shiftLightAnnouncer.reset();
          warningToasts.reset();
        }
        handleConnectionChange({ state: event.state, error: event.error });
      } else if (event.event === 'appBackgrounded') {
        const now = performance.now();
        warningEvaluator.setPaused(now, true);
        warningToasts.setBackgrounded(true, now);
      } else if (event.event === 'appForegrounded') {
        const now = performance.now();
        warningEvaluator.setPaused(now, false);
        warningToasts.setBackgrounded(false, now);
        warningToasts.revalidate();
      } else if (event.event === 'calibrationLoaded') {
        warningEvaluator.reset();
        shiftEvaluator.reset();
        shiftLightAnnouncer.reset();
        warningToasts.reset();
      } else if (event.event === 'androidBack') {
        // Android back channel (U8): the dirty dialog fires with unsaved
        // edits; otherwise the router's back navigation runs. While the dialog
        // is already open, back stays on the dialog (never overwrites the
        // queued intent — the user must choose Stay/Discard/Save-and-leave).
        if (navigationGate.isReasonActive('dirty-form')) {
          if (!dirtyDialogOpen) {
            navigationGate.stashNavigation({ page: navigateBackTarget() });
            openDirtyDialog(false);
          }
        } else {
          navigateBack();
        }
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
    onHideHeader={() => { headerVisible = false; persistViewState(); }}
    onShowHeader={() => { headerVisible = true; persistViewState(); }}
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
      onHideSidebar={() => { sidebarVisible = false; persistViewState(); }}
      onShowSidebar={() => { sidebarVisible = true; persistViewState(); }}
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
            onOpenVehicleConfig={openVehicleConfig}
            onRegisterShifterApi={registerShifterApi}
            onShifterDirtyChange={setShifterDirty}
          />
        {/key}
      {:else if currentPage === 'dashboard'}
        {#key activeDashboard}
          <DashboardPage
            dashboardName={activeDashboard}
            onNavigate={navigateTo}
            {gpsLocation}
            {toastDataId}
            onToastDataIdHandled={() => { toastDataId = null; }}
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
      {:else if currentPage === 'ecuMonitor'}
        <EcuMonitorPage onNavigate={navigateTo} {connectionState} />
      {:else if currentPage === 'sessions'}
        <SessionsPage onNavigate={navigateTo} />
      {:else if currentPage === 'warnings'}
        <WarningSettingsPage onNavigate={navigateTo} />
      {:else if currentPage === 'settings'}
        <AppSettingsPage onNavigate={navigateTo} />
      {:else if currentPage === 'gaugeBuilder'}
        {#key activeDashboard}
          <GaugeBuilderPage onNavigate={navigateTo} dashboardName={activeDashboard} />
        {/key}
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

  {#if dirtyDialogOpen}
    <Modal
      bind:open={dirtyDialogOpen}
      size="xs"
      placement="center"
      outsideclose={false}
      dismissable={false}
      class="backdrop:bg-gray-900/80"
      onclose={dirtyStay}
    >
      {#snippet header()}
        <div class="flex w-full items-center justify-between">
          <h2 class="text-base font-semibold text-gray-100">{dirtyDialogTitle}</h2>
        </div>
      {/snippet}
      <p class="text-sm text-gray-300">{dirtyDialogBody}</p>
      {#snippet footer()}
        <div class="flex w-full justify-end gap-2">
          <Button color="alternative" class="!border-gray-600 !bg-gray-700 !text-gray-300 hover:!bg-gray-600" onclick={dirtyStay}>{SHIFTER_COPY.dirtyStay}</Button>
          <Button color="danger" class="!bg-red-800 !text-white" onclick={() => void resolveDirty('discard')}>{dirtyDialogDiscardLabel}</Button>
          {#if !dirtyDialogDelete}
            <Button class="!bg-cyan-600 hover:!bg-cyan-500 !text-white border-cyan-600" onclick={() => void resolveDirty('save')}>{SHIFTER_COPY.dirtySaveAndLeave}</Button>
          {/if}
        </div>
      {/snippet}
    </Modal>
  {/if}

  {#if vehicleConfigOpen}
    <VehicleConfigModal
      open={vehicleConfigOpen}
      sensors={allSensors}
      onclose={() => { vehicleConfigOpen = false; }}
    />
  {/if}

  {#if updateCheckResult}
    <UpdateAvailableModal
      open={updateModalOpen}
      update={updateCheckResult}
      onDismiss={() => { updateModalOpen = false; storageFlagSet(UPDATE_DISMISS_KEY); }}
    />
  {/if}

  <NotificationModal bind:open={notification.show} type={notification.type} title={notification.title} message={notification.message} />
  <ToastContainer
    gateBlocked={navigationGate.blockedReason === 'modal-sheet'}
    onNavigate={(page, dataId) => {
      toastDataId = dataId ?? null;
      if (dataId != null) navigateTo(page, { toastDataId: dataId });
      else navigateTo(page);
    }}
  />
</div>

<style>
  :global(.dashboard-grid) {
    background-image: radial-gradient(circle, rgba(255,255,255,0.04) 1px, transparent 1px);
    background-size: 24px 24px;
  }
</style>

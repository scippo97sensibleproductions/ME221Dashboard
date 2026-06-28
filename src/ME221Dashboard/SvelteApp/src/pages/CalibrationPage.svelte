<script lang="ts">
  import { onMount, onDestroy } from 'svelte';
  import { HybridBridge, type ConnectionStateInfo } from '../lib/HybridBridge';
  import { Card, Button, Alert, Badge } from 'flowbite-svelte';
  import { IconCircleCheck, IconAlertTriangle, IconUpload, IconFileSearch, IconCircleX, IconArrowsLeftRight, IconArrowLeft, IconDeviceDesktopAnalytics } from '@tabler/icons-svelte';

  let { connectionState, onNavigate }: {
    connectionState: ConnectionStateInfo;
    onNavigate: (page: string) => void;
  } = $props();

  // ─── Types ────────────────────────────────────────────────────────────────

  interface EcuData {
    product: string;
    model: string;
    version: string;
  }

  interface SavedMeta {
    productName: string;
    modelName: string;
    version: string;
  }

  type PageState = 'loading' | 'no_calibration' | 'mismatch' | 'matched' | 'error' | 'parsing' | 'force_loading';

  // ─── State ─────────────────────────────────────────────────────────────────

  let pageState = $state('loading' as PageState);
  let statusMessage = $state('Initializing...');
  let ecuInfo = $state(null as EcuData | null);
  let savedMeta = $state(null as SavedMeta | null);
  let errorMessage = $state(null as string | null);
  let parsingProgress = $state(0);
  let parsedMeta = $state(null as SavedMeta | null);

  // ─── Derived ───────────────────────────────────────────────────────────────

  let isEcuConnected = $derived(connectionState.state === 'Connected');
  let stepValue = $derived(
    pageState === 'matched' ? 100 :
    pageState === 'mismatch' || pageState === 'no_calibration' || pageState === 'parsing' ? 66 :
    pageState === 'loading' ? 33 : 0
  );

  let statusColor = $derived(() => {
    if (pageState === 'matched') return 'green';
    if (pageState === 'mismatch') return 'orange';
    if (pageState === 'error') return 'red';
    return 'blue';
  });

  let headerBg = $derived(() => {
    if (pageState === 'matched') return 'bg-green-800';
    if (pageState === 'mismatch') return 'bg-orange-800';
    if (pageState === 'error') return 'bg-red-800';
    return 'bg-blue-800';
  });

  // ─── Helpers ───────────────────────────────────────────────────────────────

  function fieldMatch(a: string | undefined | null, b: string | undefined | null): boolean {
    const ca = (a ?? '').toUpperCase().trim().replace(/^v/i, '');
    const cb = (b ?? '').toUpperCase().trim().replace(/^v/i, '');
    return ca === cb
      || ca === 'GENERIC' || cb === 'GENERIC'
      || ca.includes(cb) || cb.includes(ca);
  }

  function fmtVal(v: string | undefined | null): string {
    return v && v.trim() ? v.trim() : '-';
  }

  // ─── Actions ───────────────────────────────────────────────────────────────

  async function loadCalibrationPage() {
    pageState = 'loading';
    statusMessage = 'Reading ECU info...';

    // Step 1: Get ECU info
    const ecuResult = await HybridBridge.getEcuInfo();
    if (!ecuResult.success || !ecuResult.product) {
      pageState = 'error';
      errorMessage = ecuResult.error || 'Failed to read ECU info. Is the ECU connected?';
      statusMessage = 'ECU communication failed';
      return;
    }

    ecuInfo = {
      product: ecuResult.product,
      model: ecuResult.model ?? '',
      version: ecuResult.version ?? ''
    };

    // Step 2: Check persisted calibration
    statusMessage = 'Checking saved calibration...';
    const matchResult = await HybridBridge.checkCalibrationMatch(
      ecuResult.product,
      ecuResult.model ?? '',
      ecuResult.version ?? ''
    );

    if (matchResult.error) {
      pageState = 'error';
      errorMessage = matchResult.error;
      statusMessage = 'Calibration check failed';
      return;
    }

    if (!matchResult.hasSaved) {
      pageState = 'no_calibration';
      statusMessage = 'No saved calibration found';
      return;
    }

    savedMeta = matchResult.metadata ?? null;

    if (matchResult.matched) {
      pageState = 'matched';
      statusMessage = 'Calibration matched! Starting...';
      // Auto-navigate to dashboard after brief delay
      setTimeout(() => onNavigate('dashboard'), 800);
    } else {
      pageState = 'mismatch';
      statusMessage = 'Calibration mismatch detected';
    }
  }

  async function handleBrowse() {
    pageState = 'parsing';
    parsingProgress = 30;
    statusMessage = 'Opening file picker...';

    const result = await HybridBridge.pickAndLoadCalibration();

    if (!result.picked) {
      // User cancelled - go back to previous state
      if (savedMeta) {
        pageState = 'mismatch';
        statusMessage = 'Calibration mismatch detected';
      } else {
        pageState = 'no_calibration';
        statusMessage = 'No saved calibration found';
      }
      return;
    }

    if (!result.success) {
      pageState = 'error';
      errorMessage = result.error || 'Failed to read calibration file';
      statusMessage = 'File load failed';
      return;
    }

    parsingProgress = 70;
    statusMessage = 'Verifying calibration...';

    const loadedMeta = result.metadata;
    if (!loadedMeta) {
      pageState = 'error';
      errorMessage = 'Calibration file has no metadata';
      statusMessage = 'Invalid calibration file';
      return;
    }

    parsedMeta = loadedMeta;

    // Re-check match against ECU
    const matchResult = await HybridBridge.checkCalibrationMatch(
      ecuInfo!.product,
      ecuInfo!.model,
      ecuInfo!.version
    );

    if (matchResult.matched) {
      pageState = 'matched';
      statusMessage = 'Calibration matched! Starting...';
      savedMeta = loadedMeta;
      setTimeout(() => onNavigate('dashboard'), 800);
    } else {
      savedMeta = loadedMeta;
      pageState = 'mismatch';
      statusMessage = 'Calibration mismatch detected';
    }
  }

  async function handleForceUse() {
    pageState = 'force_loading';
    statusMessage = 'Starting with mismatched calibration...';
    const result = await HybridBridge.forceUseCalibration();
    if (result.success) {
      onNavigate('dashboard');
    } else {
      pageState = 'mismatch';
      errorMessage = result.error || 'Failed to start';
      statusMessage = 'Force use failed';
    }
  }

  function handleBack() {
    HybridBridge.disconnect().catch(() => {});
  }

  // ─── Lifecycle ─────────────────────────────────────────────────────────────

  onMount(() => {
    loadCalibrationPage();
  });
</script>

<div class="mx-auto flex h-full max-w-lg flex-col items-center justify-center p-4">
  <!-- Header card -->
  <Card class="w-full overflow-hidden !p-0" shadow="lg">
    <div class="flex items-center justify-between {headerBg()} px-5 py-3">
      <div class="min-w-0 flex-1">
        <p class="text-sm font-semibold text-white">{statusMessage}</p>
        {#if ecuInfo}
          <p class="truncate text-xs text-white/70">{ecuInfo.product} {ecuInfo.model}</p>
        {/if}
      </div>
      {#if pageState === 'loading' || pageState === 'parsing' || pageState === 'force_loading'}
        <span class="inline-block h-4 w-4 animate-spin rounded-full border-2 border-white/30 border-t-white"></span>
      {:else if pageState === 'matched'}
        <Badge color="green" class="shrink-0">OK</Badge>
      {:else if pageState === 'mismatch'}
        <Badge color="warning" class="shrink-0">Mismatch</Badge>
      {:else if pageState === 'error'}
        <Badge color="red" class="shrink-0">Error</Badge>
      {/if}
    </div>

    <!-- Progress bar -->
    <div class="h-1 w-full bg-gray-700">
      <div
        class="h-full transition-all duration-500"
        class:bg-green-500={statusColor() === 'green'}
        class:bg-orange-500={statusColor() === 'orange'}
        class:bg-red-500={statusColor() === 'red'}
        class:bg-blue-500={statusColor() === 'blue'}
        style="width: {stepValue}%"
      ></div>
    </div>

    <!-- Content -->
    <div class="space-y-4 p-5">
      {#if pageState === 'loading'}
        <div class="flex flex-col items-center gap-3 py-8">
          <span class="inline-block h-6 w-6 animate-spin rounded-full border-2 border-gray-600 border-t-cyan-400"></span>
          <p class="text-sm text-gray-300">Contacting ECU and checking calibration...</p>
        </div>

      {:else if pageState === 'error'}
        <Alert color="red" class="[&>div]:flex [&>div]:items-center [&>div]:gap-3">
          <IconCircleX size={20} class="shrink-0" />
          <span class="text-sm">{errorMessage || 'An unexpected error occurred'}</span>
        </Alert>
        <Button color="alternative" class="w-full" onclick={handleBrowse}>
          <IconFileSearch size={16} class="mr-2" /> Browse Calibration File
        </Button>

      {:else if pageState === 'no_calibration'}
        {#if ecuInfo}
          <div class="rounded-lg border border-gray-700 bg-gray-800/50 p-4">
            <div class="flex items-center gap-3">
              <IconDeviceDesktopAnalytics size={24} class="shrink-0 text-cyan-400" />
              <div>
                <p class="text-sm font-medium text-gray-200">Connected ECU</p>
                <p class="text-xs text-gray-300">
                  {fmtVal(ecuInfo.product)} {fmtVal(ecuInfo.model)} v{fmtVal(ecuInfo.version)}
                </p>
              </div>
            </div>
          </div>
        {/if}

        <div class="flex flex-col items-center gap-4 rounded-lg border-2 border-dashed border-gray-600 py-10">
          <div class="flex h-14 w-14 items-center justify-center rounded-2xl bg-blue-500/10">
            <IconUpload size={26} class="text-blue-400" />
          </div>
          <div class="text-center">
            <p class="text-sm font-medium text-gray-200">No calibration saved</p>
            <p class="mt-1 text-xs text-gray-300">Select a .mefw file matching your ECU</p>
          </div>
          <Button color="brand" onclick={handleBrowse}>
            <IconFileSearch size={16} class="mr-2" /> Browse Files
          </Button>
        </div>

      {:else if pageState === 'parsing'}
        <div class="flex flex-col items-center gap-4 py-8">
          <span class="inline-block h-6 w-6 animate-spin rounded-full border-2 border-gray-600 border-t-cyan-400"></span>
          <div class="text-center">
            <p class="text-sm font-medium text-gray-200">Parsing calibration file...</p>
            <p class="mt-1 text-xs text-gray-300">This may take a moment</p>
          </div>
          {#if parsedMeta}
            <div class="mt-2 text-center text-xs text-gray-300">
              <p>{parsedMeta.productName} {parsedMeta.modelName} v{parsedMeta.version}</p>
            </div>
          {/if}
        </div>

      {:else if pageState === 'force_loading'}
        <div class="flex flex-col items-center gap-4 py-8">
          <span class="inline-block h-6 w-6 animate-spin rounded-full border-2 border-gray-600 border-t-orange-400"></span>
          <p class="text-sm text-gray-300">Starting dashboard with current calibration...</p>
        </div>

      {:else if pageState === 'mismatch'}
        <Alert color="warning" class="[&>div]:flex [&>div]:items-center [&>div]:gap-3">
          <IconAlertTriangle size={20} class="shrink-0" />
          <span class="text-sm">Saved calibration does not match the connected ECU. Review the differences below.</span>
        </Alert>

        <!-- Comparison table -->
        <Card class="!p-0 overflow-hidden">
          <div class="flex items-center gap-2 border-b border-gray-700 bg-gray-800/50 px-4 py-2.5">
            <IconArrowsLeftRight size={16} class="text-gray-300" />
            <span class="text-xs font-semibold uppercase tracking-wider text-gray-300">ECU vs Saved Calibration</span>
          </div>

          {#each [
            { label: 'Product', ecu: ecuInfo?.product, saved: savedMeta?.productName },
            { label: 'Model', ecu: ecuInfo?.model, saved: savedMeta?.modelName },
            { label: 'Version', ecu: ecuInfo?.version, saved: savedMeta?.version }
          ] as row}
            {@const match = fieldMatch(row.ecu, row.saved)}
            <div class="flex items-center border-b border-gray-700/50 px-4 py-3 last:border-0">
              <span class="w-20 text-xs font-medium uppercase tracking-wider text-gray-300">{row.label}</span>
              <div class="flex flex-1 items-center gap-2">
                <span class="text-sm font-medium {match ? 'text-green-400' : 'text-red-400'}">{fmtVal(row.ecu)}</span>
                <span class="text-xs text-gray-500">→</span>
                <span class="text-sm font-medium {match ? 'text-green-400' : 'text-red-400'}">{fmtVal(row.saved)}</span>
              </div>
              <Badge color={match ? 'green' : 'red'}>
                {match ? 'MATCH' : 'MISMATCH'}
              </Badge>
            </div>
          {/each}
        </Card>

        <!-- Warning + actions -->
        <div class="rounded-lg border border-orange-700/40 bg-orange-900/20 p-4">
          <div class="flex flex-col items-center gap-3 text-center">
            <IconAlertTriangle size={28} class="text-orange-400" />
            <p class="text-xs text-gray-300">
              Using a mismatched calibration may cause incorrect readings or unexpected behavior.
              Only proceed if you are certain the calibration is compatible.
            </p>
            <div class="flex gap-2">
              <Button color="danger" onclick={handleForceUse}>
                <IconAlertTriangle size={15} class="mr-1.5" /> Use Anyway
              </Button>
              <Button color="alternative" onclick={handleBrowse}>
                <IconFileSearch size={15} class="mr-1.5" /> Browse Different
              </Button>
            </div>
          </div>
        </div>

      {:else if pageState === 'matched'}
        <div class="flex flex-col items-center gap-3 py-6">
          <div class="flex h-14 w-14 items-center justify-center rounded-2xl bg-green-500/10">
            <IconCircleCheck size={28} class="text-green-400" />
          </div>
          <div class="text-center">
            <p class="text-sm font-medium text-gray-200">Calibration verified</p>
            <p class="mt-1 text-xs text-gray-300">ECU and saved calibration match. Starting dashboard...</p>
          </div>
          {#if ecuInfo}
            <Badge color="green" class="mt-1">
              {ecuInfo.product} {ecuInfo.model} v{ecuInfo.version}
            </Badge>
          {/if}
        </div>
      {/if}

      <!-- Bottom actions -->
      {#if pageState !== 'loading' && pageState !== 'matched' && pageState !== 'parsing' && pageState !== 'force_loading'}
        <div class="flex justify-center pt-1">
          <button
            class="inline-flex items-center gap-1.5 text-xs font-medium text-gray-400 transition-colors hover:text-gray-200"
            onclick={handleBack}
          >
            <IconArrowLeft size={14} />
            Back to Connection
          </button>
        </div>
      {/if}
    </div>
  </Card>
</div>

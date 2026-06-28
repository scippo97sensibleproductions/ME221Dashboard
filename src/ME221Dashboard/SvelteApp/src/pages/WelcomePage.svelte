<script lang="ts">
  import { onMount } from 'svelte';
  import { HybridBridge } from '../lib/HybridBridge';
  import { Button, Alert } from 'flowbite-svelte';
  import {
    IconMapPin, IconFolderOpen,
    IconCircleCheck, IconCircleX, IconArrowRight,
    IconInfoCircle, IconUsb
  } from '@tabler/icons-svelte';

  let { onContinue }: { onContinue: () => void } = $props();

  // ─── Types ─────────────────────────────────────────────────────────────────

  type WizardStep = 'location' | 'storage' | 'done';

  interface StepMeta {
    key: WizardStep;
    icon: typeof IconMapPin;
    title: string;
    subtitle: string;
    color: string;
    btnClass: string;
    iconClass: string;
    iconBg: string;
  }

  const steps: StepMeta[] = [
    {
      key: 'location',
      icon: IconMapPin,
      title: 'Location Access',
      subtitle: 'For Bluetooth device discovery',
      color: 'emerald',
      btnClass: 'border-emerald-600 !bg-emerald-600 text-white hover:!bg-emerald-500',
      iconClass: 'text-emerald-400',
      iconBg: 'bg-emerald-500/10',
    },
    {
      key: 'storage',
      icon: IconFolderOpen,
      title: 'Full File Access',
      subtitle: 'Read calibration (.mefw) files',
      color: 'violet',
      btnClass: 'border-violet-600 !bg-violet-600 text-white hover:!bg-violet-500',
      iconClass: 'text-violet-400',
      iconBg: 'bg-violet-500/10',
    },
    {
      key: 'done',
      icon: IconCircleCheck,
      title: 'All Set!',
      subtitle: "You're ready to connect to your ECU",
      color: 'green',
      btnClass: '',
      iconClass: 'text-green-400',
      iconBg: 'bg-green-500/10',
    },
  ];

  let currentStepIdx = $state(0);
  let stepStatuses = $state<Record<string, 'pending' | 'granted' | 'failed' | 'waiting'>>({
    location: 'pending',
    storage: 'pending',
  });
  let errorMessage = $state<string | null>(null);
  let requesting = $state(false);
  let initialCheck = $state(true);
  let waitingForSettings = $state(false);
  let usbHostOk = $state(true);

  let currentStep = $derived(steps[currentStepIdx]);
  let currentStatus = $derived(stepStatuses[currentStep.key]);

  // ─── Permissions Status ────────────────────────────────────────────────────

  async function refreshStatus() {
    return await HybridBridge.getPermissionStatus();
  }

  // ─── Step Navigation ───────────────────────────────────────────────────────

  function goToStep(idx: number) {
    currentStepIdx = idx;
    errorMessage = null;
    requesting = false;
    waitingForSettings = false;
  }

  function advance() {
    if (currentStepIdx < steps.length - 1) {
      goToStep(currentStepIdx + 1);
    }
  }

  // ─── Handlers ──────────────────────────────────────────────────────────────

  async function handleGrantLocation() {
    requesting = true;
    errorMessage = null;
    const result = await HybridBridge.requestLocationPermission();
    requesting = false;

    if (result.granted) {
      stepStatuses.location = 'granted';
      await delay(400);
      advance();
    } else {
      errorMessage = result.error || 'Location permission denied. You can enable it later in Settings.';
    }
  }

  async function handleGrantStorage() {
    requesting = true;
    errorMessage = null;
    const result = await HybridBridge.requestStoragePermission();
    requesting = false;

    if (result.granted) {
      stepStatuses.storage = 'granted';
      await delay(400);
      advance();
    } else if (result.action === 'open_settings') {
      waitingForSettings = true;
      stepStatuses.storage = 'waiting';
    } else {
      errorMessage = result.error || 'Storage permission denied.';
    }
  }

  async function handleCheckStorageAgain() {
    requesting = true;
    const s = await refreshStatus();
    requesting = false;

    if (s.storageGranted) {
      waitingForSettings = false;
      stepStatuses.storage = 'granted';
      await delay(400);
      advance();
    } else {
      errorMessage = 'Not yet granted. Toggle the setting and come back.';
    }
  }

  async function handleReopenSettings() {
    errorMessage = null;
    await HybridBridge.requestStoragePermission();
  }

  function delay(ms: number) {
    return new Promise(r => setTimeout(r, ms));
  }

  // ─── Init ──────────────────────────────────────────────────────────────────

  onMount(async () => {
    const s = await refreshStatus();
    initialCheck = false;
    usbHostOk = s.usbHostAvailable;

    if (s.allGranted) {
      stepStatuses.location = 'granted';
      stepStatuses.storage = 'granted';
      goToStep(2);
      return;
    }

    // Start at first ungranted permission
    if (!s.locationGranted) {
      goToStep(0);
    } else {
      stepStatuses.location = 'granted';
      if (!s.storageGranted) {
        goToStep(1);
      } else {
        stepStatuses.storage = 'granted';
        goToStep(2);
      }
    }
  });
</script>

<div class="mx-auto flex min-h-[70vh] max-w-sm flex-col items-center justify-center px-4">
  <!-- USB Host info banner (non-blocking) -->
  {#if !initialCheck}
    <div class="mb-4 flex w-full max-w-sm items-center gap-2.5 rounded-xl border border-gray-700 bg-gray-800/50 px-4 py-2.5">
      {#if usbHostOk}
        <IconUsb size={18} class="shrink-0 text-cyan-400" />
        <span class="text-xs text-gray-300">USB Host supported — plug in your ECU to connect</span>
        <IconCircleCheck size={16} class="ml-auto shrink-0 text-green-500" />
      {:else}
        <IconInfoCircle size={18} class="shrink-0 text-amber-400" />
        <span class="text-xs text-amber-300">USB Host not available — use TCP emulator instead</span>
        <IconCircleX size={16} class="ml-auto shrink-0 text-amber-500" />
      {/if}
    </div>
  {/if}

  <!-- Progress dots -->
  <div class="mb-6 flex items-center gap-2">
    {#each steps as step, i}
      <div
        class="flex h-2.5 rounded-full transition-all duration-300 {i === currentStepIdx
          ? 'w-6 bg-cyan-400'
          : stepStatuses[step.key] === 'granted'
            ? 'w-2.5 bg-green-500'
            : i < currentStepIdx
              ? 'w-2.5 bg-green-500'
              : 'w-2.5 border border-gray-600 bg-transparent'}"
      ></div>
    {/each}
  </div>

  <!-- Wizard card -->
  <div class="w-full rounded-2xl border border-gray-700 bg-gray-800 p-8 shadow-xl">
    {#if initialCheck}
      <div class="flex flex-col items-center gap-4 py-10">
        <span class="inline-block h-8 w-8 animate-spin rounded-full border-2 border-gray-500 border-t-cyan-400"></span>
        <p class="text-sm text-gray-300">Checking permissions...</p>
      </div>

    {:else if currentStep.key === 'location'}
      <div class="flex flex-col items-center gap-6">
        <div class="flex h-24 w-24 items-center justify-center rounded-3xl {currentStep.iconBg}">
          <IconMapPin size={52} class={currentStep.iconClass} />
        </div>

        <div class="text-center">
          <h2 class="text-2xl font-bold text-white">{currentStep.title}</h2>
          <p class="mt-1 text-sm text-gray-400">{currentStep.subtitle}</p>
        </div>

        <p class="text-center text-sm leading-relaxed text-gray-300">
          Android requires location permission to <span class="font-medium text-white">scan for Bluetooth devices</span>
          near you. This lets you connect to wireless ECU adapters and other Bluetooth peripherals.
        </p>

        <p class="text-center text-xs text-gray-500">
          Your location is never stored or shared. Used only for device discovery.
        </p>

        {#if errorMessage}
          <Alert color="warning" class="w-full text-xs">
            {errorMessage}
          </Alert>
        {/if}

        <Button
          color="alternative"
          size="xl"
          class="w-full {currentStep.btnClass} !py-3 !text-base font-semibold"
          disabled={requesting}
          onclick={handleGrantLocation}
        >
          {requesting ? 'Requesting...' : 'GRANT PERMISSION'}
        </Button>
      </div>

    {:else if currentStep.key === 'storage'}
      <div class="flex flex-col items-center gap-6">
        <div class="flex h-24 w-24 items-center justify-center rounded-3xl {currentStep.iconBg}">
          <IconFolderOpen size={52} class={currentStep.iconClass} />
        </div>

        <div class="text-center">
          <h2 class="text-2xl font-bold text-white">{currentStep.title}</h2>
          <p class="mt-1 text-sm text-gray-400">{currentStep.subtitle}</p>
        </div>

        {#if waitingForSettings}
          <p class="text-center text-sm leading-relaxed text-gray-300">
            Open <span class="font-medium text-white">Settings → Apps → ME221 Dashboard → Permissions</span>
            and toggle <span class="font-medium text-white">"Allow access to manage all files"</span> on,
            then come back here.
          </p>

          <div class="w-full rounded-xl bg-amber-900/20 p-4 text-center text-xs text-amber-300">
            We opened your device settings — just flip the switch and come back.
          </div>

          {#if errorMessage}
            <Alert color="warning" class="w-full text-xs">
              {errorMessage}
            </Alert>
          {/if}

          <div class="flex w-full gap-2">
            <Button
              color="alternative"
              size="xl"
              class="flex-1 !py-3 !text-sm font-semibold"
              disabled={requesting}
              onclick={handleReopenSettings}
            >
              BACK TO SETTINGS
            </Button>
            <Button
              color="brand"
              size="xl"
              class="flex-1 !py-3 !text-sm font-semibold"
              disabled={requesting}
              onclick={handleCheckStorageAgain}
            >
              {requesting ? 'Checking...' : 'CHECK AGAIN'}
            </Button>
          </div>
        {:else}
          <p class="text-center text-sm leading-relaxed text-gray-300">
            Calibration files (.mefw) contain the tuning data for your ECU. We need
            <span class="font-medium text-white">full file access</span> to read these files from your
            device storage.
          </p>

          {#if errorMessage}
            <Alert color="warning" class="w-full text-xs">
              {errorMessage}
            </Alert>
          {/if}

          <Button
            color="alternative"
            size="xl"
            class="w-full {currentStep.btnClass} !py-3 !text-base font-semibold"
            disabled={requesting}
            onclick={handleGrantStorage}
          >
            {requesting ? 'Requesting...' : 'GRANT PERMISSION'}
          </Button>
        {/if}
      </div>

    {:else if currentStep.key === 'done'}
      <div class="flex flex-col items-center gap-6 py-4">
        <div class="flex h-24 w-24 items-center justify-center rounded-3xl {currentStep.iconBg}">
          <IconCircleCheck size={52} class="text-green-400" />
        </div>

        <div class="text-center">
          <h2 class="text-2xl font-bold text-white">{currentStep.title}</h2>
          <p class="mt-1 text-sm text-gray-400">{currentStep.subtitle}</p>
        </div>

        <p class="text-center text-sm leading-relaxed text-gray-300">
          All required permissions have been granted. You can now connect to your ECU and start tuning.
        </p>

        <div class="w-full space-y-2">
          {#each steps.slice(0, -1) as step}
            <div class="flex items-center gap-3 rounded-lg bg-gray-900/60 px-4 py-2.5">
              <IconCircleCheck size={18} class="shrink-0 text-green-400" />
              <span class="text-sm text-gray-200">{step.title}</span>
            </div>
          {/each}
        </div>

        <Button
          color="brand"
          size="xl"
          class="w-full !py-3 !text-base font-semibold"
          onclick={onContinue}
        >
          Continue to App
          <IconArrowRight size={18} class="ml-2" />
        </Button>
      </div>
    {/if}
  </div>
</div>

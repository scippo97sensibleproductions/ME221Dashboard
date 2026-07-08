<script lang="ts">
  import { Modal, Button } from 'flowbite-svelte';
  import { IconDownload, IconRefresh } from '@tabler/icons-svelte';
  import type { UpdateCheckResult } from './HybridBridgeTypes';
  import { HybridBridge } from './HybridBridge';

  let { open, update, onDismiss }: {
    open: boolean;
    update: UpdateCheckResult;
    onDismiss: () => void;
  } = $props();
</script>

<Modal form bind:open size="md" placement="center" outsideclose={true} class="backdrop:bg-gray-900/80" ontoggle={(e) => { if (e.newState === 'closed') onDismiss(); }}>
  <div class="text-center">
    <div class="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-none" style="background-color: var(--metro-blue);">
      <IconRefresh size={24} class="text-white" />
    </div>

    <h3 class="mb-1 text-base font-semibold text-gray-100">Update Available</h3>
    <p class="mb-4 text-sm text-gray-400">
      A new version of ME221 Dashboard is ready
    </p>

    <div class="mx-auto mb-5 rounded-none border p-3 text-left" style="background-color: var(--metro-surface); border-color: var(--metro-border);">
      <div class="flex items-baseline justify-between gap-3">
        <span class="text-[11px] font-semibold uppercase tracking-wider text-gray-400">Version</span>
        <span class="font-mono text-sm font-bold text-cyan-400">{update.latestVersion}</span>
      </div>
      {#if update.releaseName && update.releaseName !== update.latestVersion}
        <div class="mt-1.5 flex items-baseline justify-between gap-3">
          <span class="text-[11px] font-semibold uppercase tracking-wider text-gray-400">Release</span>
          <span class="text-sm text-gray-200">{update.releaseName}</span>
        </div>
      {/if}
      {#if update.publishedAt}
        <div class="mt-1.5 flex items-baseline justify-between gap-3">
          <span class="text-[11px] font-semibold uppercase tracking-wider text-gray-400">Published</span>
          <span class="text-sm text-gray-300">{update.publishedAt}</span>
        </div>
      {/if}
      <div class="mt-1.5 flex items-baseline justify-between gap-3">
        <span class="text-[11px] font-semibold uppercase tracking-wider text-gray-400">Current</span>
        <span class="text-sm text-gray-500">{update.currentVersion}</span>
      </div>
    </div>

    <div class="flex justify-center gap-3">
      <Button color="alternative" class="!border-gray-600 !bg-gray-700 !text-gray-300 hover:!bg-gray-600" onclick={onDismiss}>
        Remind Later
      </Button>
      <Button class="!bg-cyan-600 hover:!bg-cyan-500 !text-white border-cyan-600" onclick={() => HybridBridge.openExternalUrl(update.releaseUrl)}>
        <IconDownload size={16} class="mr-1.5" />
        Download
      </Button>
    </div>
  </div>
</Modal>

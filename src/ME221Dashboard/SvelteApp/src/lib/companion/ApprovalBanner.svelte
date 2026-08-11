<script module>
  export function approvalCountdown(startedAt: number, now: number, timeoutMs = 60000): number {
    const elapsed = now - startedAt;
    if (elapsed >= timeoutMs) return 0;
    return Math.ceil((timeoutMs - elapsed) / 1000);
  }
</script>

<script lang="ts">
  import { onDestroy, onMount } from 'svelte';
  import { fade } from 'svelte/transition';
  import { HybridBridge } from '../HybridBridge';
  import type { ApprovalRequestEvent, BridgeEvent } from '../HybridBridgeTypes';
  import { IconCheck, IconDeviceMobile, IconX } from '@tabler/icons-svelte';

  const TIMEOUT_MS = 60000;
  const TICK_MS = 250;

  interface PendingRequest {
    clientId: string;
    deviceName: string;
  }

  let pending = $state<PendingRequest | null>(null);
  let startedAt = $state(0);
  let remaining = $state(0);
  let acting = $state(false);

  const progressPercent = $derived(
    pending ? Math.max(0, Math.min(100, (remaining / (TIMEOUT_MS / 1000)) * 100)) : 0,
  );

  function show(request: PendingRequest): void {
    pending = request;
    startedAt = Date.now();
    remaining = approvalCountdown(startedAt, Date.now(), TIMEOUT_MS);
  }

  function hide(): void {
    pending = null;
    startedAt = 0;
    remaining = 0;
  }

  async function decide(approve: boolean): Promise<void> {
    const current = pending;
    if (!current || acting) return;
    acting = true;
    try {
      if (approve) await HybridBridge.approveDevice(current.clientId);
      else await HybridBridge.denyDevice(current.clientId);
    } catch {
      // Best-effort decision — the banner hides either way.
    } finally {
      acting = false;
      hide();
    }
  }

  let unsubscribe: (() => void) | null = null;
  let tickTimer: ReturnType<typeof setInterval> | null = null;

  onMount(() => {
    unsubscribe = HybridBridge.onMessage((event: BridgeEvent) => {
      if (event.event !== 'approvalRequest') return;
      const e = event as ApprovalRequestEvent;
      show({ clientId: e.clientId, deviceName: e.deviceName });
    });
    tickTimer = setInterval(() => {
      if (!pending) return;
      remaining = approvalCountdown(startedAt, Date.now(), TIMEOUT_MS);
      if (remaining === 0) void decide(false);
    }, TICK_MS);
  });

  onDestroy(() => {
    unsubscribe?.();
    if (tickTimer) clearInterval(tickTimer);
  });
</script>

{#if pending}
  <div
    transition:fade={{ duration: 150 }}
    class="fixed inset-x-0 top-0 z-50 flex justify-center p-3"
    style="pointer-events: none;"
  >
    <div
      class="w-full max-w-md"
      role="status"
      aria-live="polite"
      style="pointer-events: auto; background-color: var(--metro-card); border: 1px solid var(--metro-border); border-left: 4px solid var(--metro-blue);"
    >
      <div class="flex items-center gap-2 px-4 pt-3">
        <IconDeviceMobile size={16} style="color: var(--metro-blue);" />
        <span class="text-[10px] font-semibold uppercase tracking-wider" style="color: var(--metro-text-secondary);">
          Approval requested
        </span>
        <span class="ml-auto text-[10px] font-semibold uppercase tracking-wider" style="color: var(--metro-text-muted);">
          {remaining}s
        </span>
      </div>
      <p class="px-4 pt-1 text-[13px] font-semibold" style="color: var(--metro-text);">
        {pending.deviceName}
      </p>
      <div class="mx-4 mt-2 h-1" style="background-color: var(--metro-border-subtle);">
        <div
          role="progressbar"
          aria-label="Approval window"
          aria-valuemin={0}
          aria-valuemax={60}
          aria-valuenow={remaining}
          class="h-full"
          style="width: {progressPercent}%; background-color: var(--metro-orange); transition: width 250ms linear;"
        ></div>
      </div>
      <div class="flex gap-2 px-4 py-3">
        <button
          class="flex flex-1 items-center justify-center gap-2 rounded px-4 py-2 text-[10px] font-bold uppercase tracking-wider transition-colors duration-150"
          style="background-color: var(--metro-green); color: var(--metro-text-on-accent);"
          disabled={acting}
          onclick={() => void decide(true)}
        >
          <IconCheck size={14} />
          Approve
        </button>
        <button
          class="flex flex-1 items-center justify-center gap-2 rounded border px-4 py-2 text-[10px] font-bold uppercase tracking-wider transition-colors duration-150"
          style="border-color: var(--metro-red); color: var(--metro-red); background-color: transparent;"
          disabled={acting}
          onclick={() => void decide(false)}
        >
          <IconX size={14} />
          Deny
        </button>
      </div>
    </div>
  </div>
{/if}

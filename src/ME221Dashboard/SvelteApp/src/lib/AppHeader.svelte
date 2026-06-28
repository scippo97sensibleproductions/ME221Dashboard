<script lang="ts">
  import { Badge } from 'flowbite-svelte';
  import { IconChevronUp, IconChevronDown } from '@tabler/icons-svelte';
  import type { ConnectionStateInfo } from './HybridBridge';

  let { connectionState, isConnected, currentPage, activeDashboard, headerVisible, onHideHeader, onShowHeader }: {
    connectionState: ConnectionStateInfo;
    isConnected: boolean;
    currentPage: string;
    activeDashboard: string;
    headerVisible: boolean;
    onHideHeader: () => void;
    onShowHeader: () => void;
  } = $props();

  function statusColor(state: string): 'success' | 'warning' | 'danger' | 'gray' {
    if (state === 'Connected') return 'success';
    if (state === 'Connecting') return 'warning';
    if (state === 'Error') return 'danger';
    return 'gray';
  }
</script>

{#if currentPage !== 'splash' && headerVisible}
  <header
    class="flex items-center justify-between px-3 py-1.5 lg:px-4 lg:py-2"
    style="background-color: var(--metro-sidebar); border-bottom: 1px solid var(--metro-border);"
  >
    <div class="flex items-center gap-2">
      <h1 class="text-[13px] font-extrabold uppercase tracking-wider" style="color: var(--metro-blue);">ME221</h1>
      {#if isConnected && currentPage === 'dashboard'}
        <span class="ml-1 hidden text-[11px] lg:inline" style="color: var(--metro-text-muted);">/ {activeDashboard}</span>
      {/if}
    </div>
    <div class="flex items-center gap-2">
      <Badge color={statusColor(connectionState.state)}>{connectionState.state}</Badge>
      <button
        class="flex h-7 w-7 items-center justify-center transition-colors duration-150"
        style="color: var(--metro-text-secondary);"
        onclick={onHideHeader}
        onmouseenter={(e) => (e.currentTarget.style.backgroundColor = 'var(--metro-hover)')}
        onmouseleave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
        aria-label="Hide header"
      >
        <IconChevronUp size={16} />
      </button>
    </div>
  </header>
{/if}

{#if currentPage !== 'splash' && !headerVisible && isConnected}
  <button
    class="flex h-6 w-16 shrink-0 items-center justify-center self-center transition-colors duration-150"
    style="background-color: var(--metro-sidebar); color: var(--metro-text-muted);"
    onclick={onShowHeader}
    onmouseenter={(e) => (e.currentTarget.style.color = 'var(--metro-text)')}
    onmouseleave={(e) => (e.currentTarget.style.color = 'var(--metro-text-muted)')}
    aria-label="Show header"
  >
    <IconChevronDown size={14} />
  </button>
{/if}

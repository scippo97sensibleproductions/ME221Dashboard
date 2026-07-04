<script lang="ts">
  import { IconPower, IconSettings, IconPlus, IconTrash, IconTable, IconMessage, IconUpload, IconDownload, IconCar, IconLayoutDashboard, IconMenu2, IconChevronUp, IconChevronDown, IconAdjustments, IconPlugConnected } from '@tabler/icons-svelte';
  import { Dropdown, DropdownItem } from 'flowbite-svelte';

  let { isConnected, dashboardNames, activeDashboard, currentPage, sidebarVisible, onSwitchDashboard, onDeleteDashboard, onNewDashboard, onNavigate, onDisconnect, onHideSidebar, onShowSidebar, onExportDashboard, onImportDashboard, onVehicleConfig }: {
    isConnected: boolean;
    dashboardNames: string[];
    activeDashboard: string;
    currentPage: string;
    sidebarVisible: boolean;
    onSwitchDashboard: (name: string) => void;
    onDeleteDashboard: (name: string) => void;
    onNewDashboard: () => void;
    onNavigate: (page: string) => void;
    onDisconnect: () => void;
    onHideSidebar: () => void;
    onShowSidebar: () => void;
    onExportDashboard: () => void;
    onImportDashboard: () => void;
    onVehicleConfig: () => void;
  } = $props();

  function toggleBar() {
    if (sidebarVisible) onHideSidebar();
    else onShowSidebar();
  }

  let activePage = $derived(
    currentPage === 'config' ? 'config' :
    currentPage === 'tableList' || currentPage === 'tableEditor' ? 'tables' :
    currentPage === 'logs' ? 'logs' :
    currentPage === 'dashboard' ? 'dashboard' :
    currentPage === 'driverList' || currentPage === 'driverEditor' ? 'drivers' :
    ''
  );

  const pageAccents: Record<string, string> = {
    dashboard: '#0078D7',
    config:    '#6B2C91',
    tables:    '#D83B01',
    drivers:   '#107C10',
    logs:      '#E81123',
  };

  let showBar = $derived(sidebarVisible && currentPage !== 'welcome' && currentPage !== 'calibration');
</script>

<!-- Peek tab (shown when bar is hidden) -->
{#if !showBar && currentPage !== 'splash'}
  <button
    class="fixed bottom-0 left-1/2 z-50 flex h-5 w-16 -translate-x-1/2 items-center justify-center"
    style="background-color: var(--metro-sidebar, #1F2937); color: var(--metro-text-muted, #666);"
    onclick={toggleBar}
    onmouseenter={(e) => (e.currentTarget.style.color = 'var(--metro-text-secondary, #A0A0A0)')}
    onmouseleave={(e) => (e.currentTarget.style.color = 'var(--metro-text-muted, #666)')}
    aria-label="Show navigation"
  >
    <IconChevronUp size={12} />
  </button>
{/if}

<!-- Bottom nav bar -->
{#if showBar}
  <nav
    class="fixed bottom-0 left-0 right-0 z-50 flex items-stretch"
    style="background-color: var(--metro-sidebar, #1F2937); border-top: 1px solid var(--metro-border, #333); padding-bottom: env(safe-area-inset-bottom, 0px);"
  >
    {#if !isConnected}
      <!-- Disconnected: Connection + Logs + Style -->
      <button
        class="relative flex h-14 min-w-0 flex-1 flex-col items-center justify-center gap-0.5 px-2 transition-colors duration-150"
        style="color: var(--metro-blue, #0078D7);"
        onclick={() => onNavigate('connection')}
        aria-label="Connection"
      >
        <IconPlugConnected size={20} />
        <span class="text-[10px] leading-tight">Connect</span>
      </button>
      <button
        class="relative flex h-14 min-w-0 flex-1 flex-col items-center justify-center gap-0.5 px-2 transition-colors duration-150"
        style="color: var(--metro-text-secondary, #A0A0A0);"
        onclick={() => onNavigate('logs')}
        aria-label="Logs"
      >
        <IconMessage size={20} />
        <span class="text-[10px] leading-tight">Logs</span>
      </button>

    {:else}
      <!-- Connected: Full nav -->
      <!-- Dashboard selector -->
      <button
        id="sidebar-dashboard-btn"
        class="relative flex h-14 min-w-0 flex-1 flex-col items-center justify-center gap-0.5 px-2 transition-colors duration-150"
        style="color: {activePage === 'dashboard' ? pageAccents.dashboard : 'var(--metro-text-secondary, #A0A0A0)'};"
        aria-label="Dashboards"
      >
        <IconLayoutDashboard size={20} />
        <span class="max-w-[4rem] truncate text-[10px] leading-tight">{activeDashboard}</span>
        {#if activePage === 'dashboard'}
          <span class="absolute bottom-1 left-1/2 h-0.5 w-5 -translate-x-1/2" style="background-color: {pageAccents.dashboard};"></span>
        {/if}
      </button>
      <Dropdown triggeredBy="#sidebar-dashboard-btn" placement="top-start" class="w-56">
        <div class="flex items-center justify-between px-3 py-2">
          <span class="text-[11px] font-bold uppercase tracking-wider" style="color: var(--metro-text-muted, #666);">Dashboards</span>
          <button
            class="p-0.5 transition-colors duration-150"
            style="color: var(--metro-text-secondary, #A0A0A0);"
            onclick={onNewDashboard}
            onmouseenter={(e) => (e.currentTarget.style.color = 'var(--metro-text, #FFF)')}
            onmouseleave={(e) => (e.currentTarget.style.color = 'var(--metro-text-secondary, #A0A0A0)')}
            aria-label="New dashboard"
          >
            <IconPlus size={14} />
          </button>
        </div>
        {#each dashboardNames as name}
          <DropdownItem onclick={() => onSwitchDashboard(name)} class="flex items-center gap-2 text-[13px]">
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="shrink-0"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/></svg>
            <span class="truncate">{name}</span>
            {#if dashboardNames.length > 1}
              <button
                class="ml-auto shrink-0 p-0.5 transition-colors duration-150"
                style="color: var(--metro-text-muted, #666);"
                onclick={(e) => { e.stopPropagation(); onDeleteDashboard(name); }}
                onmouseenter={(e) => (e.currentTarget.style.color = 'var(--metro-red, #E81123)')}
                onmouseleave={(e) => (e.currentTarget.style.color = 'var(--metro-text-muted, #666)')}
                title="Delete"
              >
                <IconTrash size={12} />
              </button>
            {/if}
          </DropdownItem>
        {/each}
      </Dropdown>

      <!-- Config -->
      <button
        class="relative flex h-14 min-w-0 flex-1 flex-col items-center justify-center gap-0.5 px-2 transition-colors duration-150"
        style="color: {activePage === 'config' ? pageAccents.config : 'var(--metro-text-secondary, #A0A0A0)'};"
        onclick={() => onNavigate('config')}
        aria-label="Configure"
      >
        <IconSettings size={20} />
        <span class="text-[10px] leading-tight">Config</span>
        {#if activePage === 'config'}
          <span class="absolute bottom-1 left-1/2 h-0.5 w-5 -translate-x-1/2" style="background-color: {pageAccents.config};"></span>
        {/if}
      </button>

      <!-- Tables -->
      <button
        class="relative flex h-14 min-w-0 flex-1 flex-col items-center justify-center gap-0.5 px-2 transition-colors duration-150"
        style="color: {activePage === 'tables' ? pageAccents.tables : 'var(--metro-text-secondary, #A0A0A0)'};"
        onclick={() => onNavigate('tableList')}
        aria-label="Tables"
      >
        <IconTable size={20} />
        <span class="text-[10px] leading-tight">Tables</span>
        {#if activePage === 'tables'}
          <span class="absolute bottom-1 left-1/2 h-0.5 w-5 -translate-x-1/2" style="background-color: {pageAccents.tables};"></span>
        {/if}
      </button>

      <!-- Drivers -->
      <button
        class="relative flex h-14 min-w-0 flex-1 flex-col items-center justify-center gap-0.5 px-2 transition-colors duration-150"
        style="color: {activePage === 'drivers' ? pageAccents.drivers : 'var(--metro-text-secondary, #A0A0A0)'};"
        onclick={() => onNavigate('driverList')}
        aria-label="Drivers"
      >
        <IconAdjustments size={20} />
        <span class="text-[10px] leading-tight">Drivers</span>
        {#if activePage === 'drivers'}
          <span class="absolute bottom-1 left-1/2 h-0.5 w-5 -translate-x-1/2" style="background-color: {pageAccents.drivers};"></span>
        {/if}
      </button>

      <!-- Logs -->
      <button
        class="relative flex h-14 min-w-0 flex-1 flex-col items-center justify-center gap-0.5 px-2 transition-colors duration-150"
        style="color: {activePage === 'logs' ? pageAccents.logs : 'var(--metro-text-secondary, #A0A0A0)'};"
        onclick={() => onNavigate('logs')}
        aria-label="Logs"
      >
        <IconMessage size={20} />
        <span class="text-[10px] leading-tight">Logs</span>
        {#if activePage === 'logs'}
          <span class="absolute bottom-1 left-1/2 h-0.5 w-5 -translate-x-1/2" style="background-color: {pageAccents.logs};"></span>
        {/if}
      </button>

      <!-- More menu -->
      <button
        id="sidebar-more-btn"
        class="flex h-14 min-w-0 flex-1 flex-col items-center justify-center gap-0.5 px-2 transition-colors duration-150"
        style="color: var(--metro-text-secondary, #A0A0A0);"
        aria-label="More options"
      >
        <IconMenu2 size={20} />
        <span class="text-[10px] leading-tight">More</span>
      </button>
      <Dropdown triggeredBy="#sidebar-more-btn" placement="top-end" class="w-48">
        <DropdownItem onclick={onVehicleConfig} class="flex items-center gap-2.5 text-[13px]">
          <IconCar size={15} class="shrink-0" />
          <span>Vehicle</span>
        </DropdownItem>
        <DropdownItem onclick={onExportDashboard} class="flex items-center gap-2.5 text-[13px]">
          <IconUpload size={15} class="shrink-0" />
          <span>Export</span>
        </DropdownItem>
        <DropdownItem onclick={onImportDashboard} class="flex items-center gap-2.5 text-[13px]">
          <IconDownload size={15} class="shrink-0" />
          <span>Import</span>
        </DropdownItem>
        <DropdownItem onclick={onDisconnect} class="metro-danger-item flex items-center gap-2.5 text-[13px]" style="background-color: var(--metro-red); color: var(--metro-text-on-accent);">
          <IconPower size={15} class="shrink-0" />
          <span>Disconnect</span>
        </DropdownItem>
        <DropdownItem onclick={toggleBar} class="flex items-center gap-2.5 text-[13px]">
          <IconChevronDown size={15} class="shrink-0" />
          <span>Hide Bar</span>
        </DropdownItem>
      </Dropdown>
    {/if}
  </nav>
{/if}

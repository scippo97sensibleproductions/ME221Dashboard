<script lang="ts">
  import { Modal, Button } from 'flowbite-svelte';
  import { IconX, IconChevronDown } from '@tabler/icons-svelte';
  import type { DashboardTableEntry, ColorScheme } from '../HybridBridgeTypes';

  interface Props {
    open: boolean;
    tableName: string;
    entry: DashboardTableEntry | null;
    onclose: () => void;
    onchange: (updated: DashboardTableEntry) => void;
  }

  let { open, tableName, entry, onclose, onchange }: Props = $props();

  let draft = $derived(entry ? { ...entry } : null);
  let currentColorScheme = $derived<ColorScheme>((entry?.colorScheme) ?? 'thermal');
  let showLabels = $derived<boolean>((entry?.showLabels) ?? true);
  let showDimensionBadge = $derived<boolean>((entry?.showDimensionBadge) ?? true);

  let openSections = $state<Record<string, boolean>>({
    size: true,
    position: false,
    appearance: false,
  });

  function toggleSection(id: string) { openSections[id] = !openSections[id]; }

  const sectionColors: Record<string, { border: string; bg: string; text: string; headerBg: string }> = {
    size:      { border: 'border-l-violet-500',  bg: 'bg-violet-500/5',  text: 'text-white', headerBg: 'bg-violet-500/25', },
    position:  { border: 'border-l-cyan-500',    bg: 'bg-cyan-500/5',    text: 'text-white', headerBg: 'bg-cyan-500/25' },
    appearance:{ border: 'border-l-amber-500',   bg: 'bg-amber-500/5',   text: 'text-white', headerBg: 'bg-amber-500/25' },
  };

  const colorSchemes: { id: ColorScheme; label: string }[] = [
    { id: 'thermal', label: 'Thermal' },
    { id: 'viridis', label: 'Viridis' },
    { id: 'grayscale', label: 'Grayscale' },
    { id: 'ember', label: 'Ember' },
  ];

  function handleClose() { onclose(); }
  function updateSize(k: 'widthFraction'|'heightFraction', v: number) { if(!draft)return; onchange({...draft,[k]:v}); }
  function updatePos(k: 'fractionX'|'fractionY', v: number) { if(!draft)return; onchange({...draft,[k]:v}); }
  function updateZIndex(v: number) { if(!draft)return; onchange({...draft,zIndex:Math.max(-10,Math.min(100,v))}); }
  function updateColorScheme(s: ColorScheme) { if(!draft)return; onchange({...draft,colorScheme:s}); }
  function updateShowLabels(v: boolean) { if(!draft)return; onchange({...draft,showLabels:v}); }
  function updateShowDimensionBadge(v: boolean) { if(!draft)return; onchange({...draft,showDimensionBadge:v}); }
</script>

{#if open && draft}
  <Modal open={open} size="lg" placement="center" outsideclose={true} dismissable={false}
         class="backdrop:bg-gray-900/80" ontoggle={(e: CustomEvent) => { if (e.detail?.newState === 'closed') handleClose(); }}>
    <div class="flex items-center justify-between border-b border-gray-700/50 px-4 py-3">
      <div>
        <h3 class="text-sm font-bold text-cyan-400">{tableName}</h3>
        <p class="text-[10px] text-gray-500">Table #{draft.tableId}</p>
      </div>
      <button class="rounded-lg p-2 text-gray-400 transition-colors hover:bg-gray-700 hover:text-gray-200" onclick={handleClose}>
        <IconX size={18} />
      </button>
    </div>
    <div class="flex flex-col max-h-[70vh] overflow-y-auto px-4 py-3 space-y-2">
      <!-- Size -->
      <div class="rounded-lg border border-l-2 {sectionColors.size.border} border-gray-700/50 overflow-hidden">
        <button class="flex w-full items-center justify-between px-3 py-2.5 text-xs font-semibold uppercase tracking-wider {sectionColors.size.text} {sectionColors.size.headerBg} transition-colors"
                onclick={() => toggleSection('size')}>
          <span>Size</span>
          <IconChevronDown size={14} class="transition-transform duration-200 {openSections.size ? 'rotate-180' : ''}" />
        </button>
        {#if openSections.size}
          <div class="px-3 pb-3 pt-1 border-t border-gray-700/30 {sectionColors.size.bg} space-y-3">
            <div>
              <div class="flex items-center justify-between mb-1.5">
                <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Width</p>
                <span class="text-xs font-mono text-white">{(draft.widthFraction * 100).toFixed(0)}%</span>
              </div>
              <input type="range" step="0.05" min="0.05" max="1.0"
                value={draft.widthFraction}
                oninput={(e: Event) => updateSize('widthFraction', parseFloat((e.target as HTMLInputElement).value))}
                class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-violet-500 cursor-pointer" />
            </div>
            <div>
              <div class="flex items-center justify-between mb-1.5">
                <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Height</p>
                <span class="text-xs font-mono text-white">{(draft.heightFraction * 100).toFixed(0)}%</span>
              </div>
              <input type="range" step="0.05" min="0.05" max="1.0"
                value={draft.heightFraction}
                oninput={(e: Event) => updateSize('heightFraction', parseFloat((e.target as HTMLInputElement).value))}
                class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-violet-500 cursor-pointer" />
            </div>
          </div>
        {/if}
      </div>
      <!-- Position -->
      <div class="rounded-lg border border-l-2 {sectionColors.position.border} border-gray-700/50 overflow-hidden">
        <button class="flex w-full items-center justify-between px-3 py-2.5 text-xs font-semibold uppercase tracking-wider {sectionColors.position.text} {sectionColors.position.headerBg} transition-colors"
                onclick={() => toggleSection('position')}>
          <span>Position</span>
          <IconChevronDown size={14} class="transition-transform duration-200 {openSections.position ? 'rotate-180' : ''}" />
        </button>
        {#if openSections.position}
          <div class="px-3 pb-3 pt-1 border-t border-gray-700/30 {sectionColors.position.bg} space-y-3">
            <div>
              <div class="flex items-center justify-between mb-1.5">
                <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Left</p>
                <span class="text-xs font-mono text-white">{(draft.fractionX * 100).toFixed(0)}%</span>
              </div>
              <input type="range" step="0.01" min="0" max="{Math.max(0, 1 - draft.widthFraction)}"
                value={draft.fractionX}
                oninput={(e: Event) => updatePos('fractionX', parseFloat((e.target as HTMLInputElement).value))}
                class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer" />
            </div>
            <div>
              <div class="flex items-center justify-between mb-1.5">
                <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Top</p>
                <span class="text-xs font-mono text-white">{(draft.fractionY * 100).toFixed(0)}%</span>
              </div>
              <input type="range" step="0.01" min="0" max="{Math.max(0, 1 - draft.heightFraction)}"
                value={draft.fractionY}
                oninput={(e: Event) => updatePos('fractionY', parseFloat((e.target as HTMLInputElement).value))}
                class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer" />
            </div>
            <div>
              <div class="flex items-center justify-between mb-1.5">
                <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Layer</p>
                <span class="text-xs font-mono text-white">{draft.zIndex}</span>
              </div>
              <input type="range" step="1" min="-10" max="100"
                value={draft.zIndex}
                oninput={(e: Event) => updateZIndex(parseInt((e.target as HTMLInputElement).value))}
                class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer" />
            </div>
          </div>
        {/if}
      </div>
      <!-- Appearance -->
      <div class="rounded-lg border border-l-2 {sectionColors.appearance.border} border-gray-700/50 overflow-hidden">
        <button class="flex w-full items-center justify-between px-3 py-2.5 text-xs font-semibold uppercase tracking-wider {sectionColors.appearance.text} {sectionColors.appearance.headerBg} transition-colors"
                onclick={() => toggleSection('appearance')}>
          <span>Appearance</span>
          <IconChevronDown size={14} class="transition-transform duration-200 {openSections.appearance ? 'rotate-180' : ''}" />
        </button>
        {#if openSections.appearance}
          <div class="px-3 pb-3 pt-1 border-t border-gray-700/30 {sectionColors.appearance.bg} space-y-3">
            <div>
              <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-1.5">Color Scheme</p>
              <div class="grid grid-cols-2 gap-1.5">
                {#each colorSchemes as scheme}
                  <button class="rounded px-2 py-1.5 text-xs font-medium transition-colors min-h-[28px]
                    {currentColorScheme === scheme.id
                      ? 'bg-amber-600 text-white'
                      : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
                    onclick={() => updateColorScheme(scheme.id)}
                  >
                    {scheme.label}
                  </button>
                {/each}
              </div>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-xs text-gray-300">Show Value Labels</span>
              <button class="rounded px-3 py-1.5 text-xs font-medium transition-colors min-h-[28px]
                {showLabels ? 'bg-amber-600 text-white' : 'border border-gray-600 text-gray-400 hover:border-gray-500'}"
                onclick={() => updateShowLabels(!showLabels)}
              >
                {showLabels ? 'ON' : 'OFF'}
              </button>
            </div>
            <div class="flex items-center justify-between">
              <span class="text-xs text-gray-300">Show Dimension Badge</span>
              <button class="rounded px-3 py-1.5 text-xs font-medium transition-colors min-h-[28px]
                {showDimensionBadge ? 'bg-amber-600 text-white' : 'border border-gray-600 text-gray-400 hover:border-gray-500'}"
                onclick={() => updateShowDimensionBadge(!showDimensionBadge)}
              >
                {showDimensionBadge ? 'ON' : 'OFF'}
              </button>
            </div>
          </div>
        {/if}
      </div>
    </div>
    <svelte:fragment slot="footer">
      <div class="flex w-full justify-end">
        <Button color="alternative" class="!border-gray-600 !bg-gray-700 !text-gray-300 hover:!bg-gray-600 !text-xs" onclick={handleClose}>Close</Button>
      </div>
    </svelte:fragment>
  </Modal>
{/if}

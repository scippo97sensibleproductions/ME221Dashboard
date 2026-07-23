<script lang="ts">
  import { Modal, Tabs, TabItem } from 'flowbite-svelte';
  import { IconX } from '@tabler/icons-svelte';
  import type { DashboardTableEntry, ColorScheme, DataLinkDefinition } from '../HybridBridgeTypes';
  import { HybridBridge } from '../HybridBridge';

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

  let allDataLinks = $state<DataLinkDefinition[]>([]);

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
  function updateTraceXLink(v: number | null) { if(!draft)return; onchange({...draft,traceXLink:v}); }
  function updateTraceYLink(v: number | null) { if(!draft)return; onchange({...draft,traceYLink:v}); }

  $effect(() => {
    if (open) {
      HybridBridge.getDataLinks().then(result => {
        allDataLinks = result.dataLinks ?? [];
      }).catch(() => {});
    }
  });
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

    <div class="max-h-[70vh] overflow-y-auto px-4 py-3">
      <Tabs tabStyle="full" divider={false}
            classes={{ active: 'bg-cyan-600 text-white', inactive: 'text-gray-400 hover:text-gray-200 hover:bg-gray-700/50' }}>
        <!-- Tab 1: Size -->
        <TabItem key="size" title="Size">
          <div class="space-y-4 pt-3">
            <div>
              <div class="flex items-center justify-between mb-1.5">
                <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Width</p>
                <span class="text-xs font-mono text-cyan-400">{(draft.widthFraction * 100).toFixed(0)}%</span>
              </div>
              <input type="range" step="0.05" min="0.05" max="1.0"
                value={draft.widthFraction}
                oninput={(e: Event) => updateSize('widthFraction', parseFloat((e.target as HTMLInputElement).value))}
                class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
                  [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
                  [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg" />
            </div>
            <div>
              <div class="flex items-center justify-between mb-1.5">
                <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Height</p>
                <span class="text-xs font-mono text-cyan-400">{(draft.heightFraction * 100).toFixed(0)}%</span>
              </div>
              <input type="range" step="0.05" min="0.05" max="1.0"
                value={draft.heightFraction}
                oninput={(e: Event) => updateSize('heightFraction', parseFloat((e.target as HTMLInputElement).value))}
                class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
                  [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
                  [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg" />
            </div>
          </div>
        </TabItem>

        <!-- Tab 2: Position -->
        <TabItem key="position" title="Position">
          <div class="space-y-4 pt-3">
            <div>
              <div class="flex items-center justify-between mb-1.5">
                <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Left</p>
                <span class="text-xs font-mono text-cyan-400">{(draft.fractionX * 100).toFixed(0)}%</span>
              </div>
              <input type="range" step="0.01" min="0" max="{Math.max(0, 1 - draft.widthFraction)}"
                value={draft.fractionX}
                oninput={(e: Event) => updatePos('fractionX', parseFloat((e.target as HTMLInputElement).value))}
                class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
                  [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
                  [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg" />
            </div>
            <div>
              <div class="flex items-center justify-between mb-1.5">
                <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Top</p>
                <span class="text-xs font-mono text-cyan-400">{(draft.fractionY * 100).toFixed(0)}%</span>
              </div>
              <input type="range" step="0.01" min="0" max="{Math.max(0, 1 - draft.heightFraction)}"
                value={draft.fractionY}
                oninput={(e: Event) => updatePos('fractionY', parseFloat((e.target as HTMLInputElement).value))}
                class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
                  [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
                  [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg" />
            </div>
            <div>
              <div class="flex items-center justify-between mb-1.5">
                <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500">Layer</p>
                <span class="text-xs font-mono text-cyan-400">{draft.zIndex}</span>
              </div>
              <input type="range" step="1" min="-10" max="100"
                value={draft.zIndex}
                oninput={(e: Event) => updateZIndex(parseInt((e.target as HTMLInputElement).value))}
                class="w-full h-1.5 rounded-full appearance-none bg-gray-700 accent-cyan-500 cursor-pointer
                  [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4
                  [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-cyan-500 [&::-webkit-slider-thumb]:shadow-lg" />
              <p class="text-[10px] text-gray-500 mt-0.5">Higher = on top</p>
            </div>
          </div>
        </TabItem>

        <!-- Tab 3: Appearance -->
        <TabItem key="appearance" title="Appearance">
          <div class="space-y-4 pt-3">
            <div>
              <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-2">Color Scheme</p>
              <div class="grid grid-cols-2 gap-1.5">
                {#each colorSchemes as scheme}
                  <button class="rounded px-2 py-2 text-xs font-medium transition-colors min-h-[36px]
                    {currentColorScheme === scheme.id
                      ? 'bg-cyan-600 text-white'
                      : 'border border-gray-600 text-gray-400 hover:border-gray-500 hover:text-gray-200'}"
                    onclick={() => updateColorScheme(scheme.id)}
                  >
                    {scheme.label}
                  </button>
                {/each}
              </div>
            </div>

            <div class="border-t border-gray-700/30 pt-4">
              <div class="flex items-center justify-between rounded bg-gray-800/60 px-3 py-2">
                <span class="text-xs text-gray-300">Value Labels</span>
                <button
                  class="relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none"
                  style="background-color: {showLabels ? 'var(--metro-green)' : 'rgb(55,65,81)'}"
                  role="switch"
                  aria-checked={showLabels}
                  aria-label="Toggle value labels"
                  onclick={() => updateShowLabels(!showLabels)}
                >
                  <span
                    class="pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow-lg ring-0 transition duration-200 ease-in-out"
                    style="transform: translateX({showLabels ? '18px' : '0'})"
                  ></span>
                </button>
              </div>
            </div>

            <div>
              <div class="flex items-center justify-between rounded bg-gray-800/60 px-3 py-2">
                <span class="text-xs text-gray-300">Dimension Badge</span>
                <button
                  class="relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none"
                  style="background-color: {showDimensionBadge ? 'var(--metro-green)' : 'rgb(55,65,81)'}"
                  role="switch"
                  aria-checked={showDimensionBadge}
                  aria-label="Toggle dimension badge"
                  onclick={() => updateShowDimensionBadge(!showDimensionBadge)}
                >
                  <span
                    class="pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow-lg ring-0 transition duration-200 ease-in-out"
                    style="transform: translateX({showDimensionBadge ? '18px' : '0'})"
                  ></span>
                </button>
              </div>
            </div>
          </div>
        </TabItem>

        <!-- Tab 4: Trace -->
        <TabItem key="trace" title="Trace">
          <div class="space-y-4 pt-3">
            <div>
              <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-1.5">X Axis</p>
              <select
                class="w-full rounded border border-gray-600 bg-gray-800 px-2 py-2 text-xs text-gray-200 focus:border-cyan-500 focus:outline-none"
                value={draft?.traceXLink ?? ''}
                onchange={(e: Event) => {
                  const val = (e.target as HTMLSelectElement).value;
                  updateTraceXLink(val === '' ? null : parseInt(val));
                }}
              >
                <option value="">Time</option>
                {#each allDataLinks as link}
                  <option value={link.id}>{link.name} ({link.measureUnit})</option>
                {/each}
              </select>
            </div>
            <div>
              <p class="text-[10px] font-semibold uppercase tracking-wider text-gray-500 mb-1.5">Y Axis</p>
              <select
                class="w-full rounded border border-gray-600 bg-gray-800 px-2 py-2 text-xs text-gray-200 focus:border-cyan-500 focus:outline-none"
                value={draft?.traceYLink ?? ''}
                onchange={(e: Event) => {
                  const val = (e.target as HTMLSelectElement).value;
                  updateTraceYLink(val === '' ? null : parseInt(val));
                }}
              >
                <option value="">RPM</option>
                {#each allDataLinks as link}
                  <option value={link.id}>{link.name} ({link.measureUnit})</option>
                {/each}
              </select>
            </div>
          </div>
        </TabItem>
      </Tabs>
    </div>
  </Modal>
{/if}

<script lang="ts">
  import { IconPlugConnected, IconSettings, IconTable, IconMessage, IconDashboard, IconCar, IconCpu, IconSearch, IconX, IconCheck, IconAlertTriangle, IconInfoCircle, IconChevronRight, IconPower, IconRefresh, IconDeviceFloppy, IconUpload, IconDownload, IconTrash, IconPlus, IconMinus, IconEdit, IconCopy, IconFileExport, IconArrowsLeftRight, IconLoader, IconCircleCheck, IconCircleX, IconWifi, IconUsb, IconNetwork } from '@tabler/icons-svelte';

  let activeTab = $state('tiles');
  let inputVal = $state('');
  let numberVal = $state(0);
  let toggleOn = $state(false);
  let checkboxOn = $state(false);
  let selectedOption = $state('option1');

  const tabs = [
    { id: 'tiles', label: 'Tiles' },
    { id: 'colors', label: 'Colors' },
    { id: 'typography', label: 'Type' },
    { id: 'buttons', label: 'Buttons' },
    { id: 'forms', label: 'Forms' },
    { id: 'cards', label: 'Cards' },
    { id: 'headers', label: 'Headers' },
    { id: 'badges', label: 'Badges' },
    { id: 'tables', label: 'Tables' },
  ];
</script>

<div class="h-full overflow-auto" style="background-color: #0A0A0A;">
  <div class="max-w-3xl mx-auto p-4 pb-20">

    <!-- Page Title -->
    <div class="flex items-center gap-3 border-l-4 border-l-[#0078D7] pl-3 mb-6">
      <IconDashboard size="20" class="text-[#0078D7]" />
      <h1 class="text-xl font-extrabold uppercase tracking-[-0.5px] text-white" style="line-height: 1.1;">
        Metro UI <span class="text-[#0078D7]">Style Guide</span>
      </h1>
    </div>

    <p class="text-[13px] text-[#A0A0A0] mb-6" style="line-height: 1.4;">
      Living reference for the Modernized Metro design language. Every component below is a real, interactive example.
    </p>

    <!-- Tab Bar -->
    <div class="flex gap-1 mb-6 p-1 border border-[#333]" style="background-color: #1A1A1A;">
      {#each tabs as tab}
        <button
          class="px-3 py-1.5 text-[11px] font-bold uppercase tracking-wider transition-colors duration-150 {activeTab === tab.id
            ? 'text-white'
            : 'text-[#A0A0A0] hover:text-white'}"
          style={activeTab === tab.id ? 'background-color: #0078D7;' : ''}
          onclick={() => (activeTab = tab.id)}
        >
          {tab.label}
        </button>
      {/each}
    </div>

    <!-- ═══════════════════════════════════════════════════════════════ -->
    <!-- TILES TAB -->
    <!-- ═══════════════════════════════════════════════════════════════ -->
    {#if activeTab === 'tiles'}
      <section class="mb-8">
        <h2 class="text-[13px] font-extrabold uppercase tracking-wider text-white border-l-4 border-l-[#D83B01] pl-3 mb-4" style="line-height: 1.2;">
          Sidebar Tiles
        </h2>
        <p class="text-[13px] text-[#A0A0A0] mb-4" style="line-height: 1.4;">
          Solid color blocks with icon + label. The signature Metro pattern for feature sections.
        </p>

        <div class="grid grid-cols-2 gap-3">
          <!-- Blue Tile -->
          <div class="p-4" style="background-color: #0078D7;">
            <div class="flex items-center gap-2 text-white/90 text-[11px] font-bold uppercase tracking-wider mb-3 pb-2" style="border-bottom: 1px solid rgba(255,255,255,0.2);">
              <IconPlugConnected size="16" /> Connection
            </div>
            <div class="grid grid-cols-2 gap-2">
              <div class="bg-white/15 flex flex-col items-center justify-center aspect-[1.6]">
                <IconWifi size="18" class="text-white mb-1" />
                <span class="text-[10px] font-extrabold uppercase text-white">TCP</span>
              </div>
              <div class="bg-white/15 flex flex-col items-center justify-center aspect-[1.6]">
                <IconUsb size="18" class="text-white mb-1" />
                <span class="text-[10px] font-extrabold uppercase text-white">Serial</span>
              </div>
            </div>
          </div>

          <!-- Teal Tile -->
          <div class="p-4" style="background-color: #00A5A5;">
            <div class="flex items-center gap-2 text-white/90 text-[11px] font-bold uppercase tracking-wider mb-3 pb-2" style="border-bottom: 1px solid rgba(255,255,255,0.2);">
              <IconDashboard size="16" /> Dashboard
            </div>
            <div class="flex flex-wrap gap-1.5">
              <span class="bg-white/20 text-white text-[9px] font-bold uppercase px-2.5 py-1">Arc Gauge</span>
              <span class="bg-white/20 text-white text-[9px] font-bold uppercase px-2.5 py-1">Bar Gauge</span>
              <span class="bg-white/20 text-white text-[9px] font-bold uppercase px-2.5 py-1">Digital</span>
              <span class="bg-white/20 text-white text-[9px] font-bold uppercase px-2.5 py-1">Text</span>
              <span class="bg-white/20 text-white text-[9px] font-bold uppercase px-2.5 py-1">Chart</span>
            </div>
          </div>

          <!-- Purple Tile -->
          <div class="p-4" style="background-color: #6B2C91;">
            <div class="flex items-center gap-2 text-white/90 text-[11px] font-bold uppercase tracking-wider mb-3 pb-2" style="border-bottom: 1px solid rgba(255,255,255,0.2);">
              <IconSettings size="16" /> Calibration
            </div>
            <div class="text-[11px]" style="color: rgba(255,255,255,0.8);">
              <div class="mb-1">FW: ME221-V2B-PNP</div>
              <div class="text-white font-bold">v62</div>
            </div>
          </div>

          <!-- Orange Tile -->
          <div class="p-4" style="background-color: #D83B01;">
            <div class="flex items-center gap-2 text-white/90 text-[11px] font-bold uppercase tracking-wider mb-3 pb-2" style="border-bottom: 1px solid rgba(255,255,255,0.2);">
              <IconTable size="16" /> Tables
            </div>
            <div class="flex items-baseline gap-1">
              <span class="text-[24px] font-extrabold text-white font-mono" style="line-height: 1;">88</span>
              <span class="text-[10px] uppercase" style="color: rgba(255,255,255,0.7);">tables</span>
            </div>
          </div>

          <!-- Green Tile -->
          <div class="p-4" style="background-color: #107C10;">
            <div class="flex items-center gap-2 text-white/90 text-[11px] font-bold uppercase tracking-wider mb-3 pb-2" style="border-bottom: 1px solid rgba(255,255,255,0.2);">
              <IconCar size="16" /> Drivers
            </div>
            <div class="flex items-baseline gap-1">
              <span class="text-[24px] font-extrabold text-white font-mono" style="line-height: 1;">48</span>
              <span class="text-[10px] uppercase" style="color: rgba(255,255,255,0.7);">configured</span>
            </div>
          </div>

          <!-- Red Tile -->
          <div class="p-4" style="background-color: #E81123;">
            <div class="flex items-center gap-2 text-white/90 text-[11px] font-bold uppercase tracking-wider mb-3 pb-2" style="border-bottom: 1px solid rgba(255,255,255,0.2);">
              <IconMessage size="16" /> Logs
            </div>
            <div class="font-mono text-[10px]" style="color: rgba(255,255,255,0.8); line-height: 1.4;">
              <div>[12:34:56] INFO Connected</div>
              <div>[12:34:57] WARN Timeout</div>
              <div>[12:34:58] ERR Failed</div>
            </div>
          </div>
        </div>
      </section>

    <!-- ═══════════════════════════════════════════════════════════════ -->
    <!-- COLORS TAB -->
    <!-- ═══════════════════════════════════════════════════════════════ -->
    {:else if activeTab === 'colors'}
      <section class="mb-8">
        <h2 class="text-[13px] font-extrabold uppercase tracking-wider text-white border-l-4 border-l-[#D83B01] pl-3 mb-4" style="line-height: 1.2;">
          Accent Palette
        </h2>

        <div class="grid grid-cols-2 gap-3">
          <div class="p-4 flex items-center justify-between" style="background-color: #0078D7;">
            <div>
              <div class="text-white text-[11px] font-bold uppercase tracking-wider">Metro Blue</div>
              <div class="text-white/60 text-[10px] font-mono">#0078D7</div>
            </div>
            <span class="text-white/40 text-[9px] uppercase">Primary</span>
          </div>
          <div class="p-4 flex items-center justify-between" style="background-color: #00A5A5;">
            <div>
              <div class="text-white text-[11px] font-bold uppercase tracking-wider">Metro Teal</div>
              <div class="text-white/60 text-[10px] font-mono">#00A5A5</div>
            </div>
            <span class="text-white/40 text-[9px] uppercase">Info</span>
          </div>
          <div class="p-4 flex items-center justify-between" style="background-color: #6B2C91;">
            <div>
              <div class="text-white text-[11px] font-bold uppercase tracking-wider">Metro Purple</div>
              <div class="text-white/60 text-[10px] font-mono">#6B2C91</div>
            </div>
            <span class="text-white/40 text-[9px] uppercase">Config</span>
          </div>
          <div class="p-4 flex items-center justify-between" style="background-color: #D83B01;">
            <div>
              <div class="text-white text-[11px] font-bold uppercase tracking-wider">Metro Orange</div>
              <div class="text-white/60 text-[10px] font-mono">#D83B01</div>
            </div>
            <span class="text-white/40 text-[9px] uppercase">Warning</span>
          </div>
          <div class="p-4 flex items-center justify-between" style="background-color: #107C10;">
            <div>
              <div class="text-white text-[11px] font-bold uppercase tracking-wider">Metro Green</div>
              <div class="text-white/60 text-[10px] font-mono">#107C10</div>
            </div>
            <span class="text-white/40 text-[9px] uppercase">Success</span>
          </div>
          <div class="p-4 flex items-center justify-between" style="background-color: #E81123;">
            <div>
              <div class="text-white text-[11px] font-bold uppercase tracking-wider">Metro Red</div>
              <div class="text-white/60 text-[10px] font-mono">#E81123</div>
            </div>
            <span class="text-white/40 text-[9px] uppercase">Danger</span>
          </div>
        </div>
      </section>

      <section class="mb-8">
        <h2 class="text-[13px] font-extrabold uppercase tracking-wider text-white border-l-4 border-l-[#D83B01] pl-3 mb-4" style="line-height: 1.2;">
          Surface Colors
        </h2>

        <div class="grid grid-cols-4 gap-2">
          <div class="border border-[#333] p-3 text-center" style="background-color: #0A0A0A;">
            <div class="text-white text-[10px] font-bold uppercase mb-1">Background</div>
            <div class="text-[#666] text-[9px] font-mono">#0A0A0A</div>
          </div>
          <div class="border border-[#333] p-3 text-center" style="background-color: #1A1A1A;">
            <div class="text-white text-[10px] font-bold uppercase mb-1">Surface</div>
            <div class="text-[#666] text-[9px] font-mono">#1A1A1A</div>
          </div>
          <div class="border border-[#333] p-3 text-center" style="background-color: #222222;">
            <div class="text-white text-[10px] font-bold uppercase mb-1">Card</div>
            <div class="text-[#666] text-[9px] font-mono">#222222</div>
          </div>
          <div class="border p-3 text-center" style="background-color: #333333; border-color: #444444;">
            <div class="text-white text-[10px] font-bold uppercase mb-1">Border</div>
            <div class="text-[#666] text-[9px] font-mono">#333333</div>
          </div>
        </div>
      </section>

      <section class="mb-8">
        <h2 class="text-[13px] font-extrabold uppercase tracking-wider text-white border-l-4 border-l-[#D83B01] pl-3 mb-4" style="line-height: 1.2;">
          Text Colors
        </h2>

        <div class="space-y-2">
          <div class="border border-[#333] p-3 flex items-center justify-between" style="background-color: #222222;">
            <span class="text-[13px] text-white">Primary Text — The quick brown fox</span>
            <span class="text-[9px] font-mono text-[#666]">#FFFFFF</span>
          </div>
          <div class="border border-[#333] p-3 flex items-center justify-between" style="background-color: #222222;">
            <span class="text-[13px]" style="color: #A0A0A0;">Secondary Text — The quick brown fox</span>
            <span class="text-[9px] font-mono text-[#666]">#A0A0A0</span>
          </div>
          <div class="border border-[#333] p-3 flex items-center justify-between" style="background-color: #222222;">
            <span class="text-[13px]" style="color: #666666;">Disabled Text — The quick brown fox</span>
            <span class="text-[9px] font-mono text-[#666]">#666666</span>
          </div>
        </div>
      </section>

    <!-- ═══════════════════════════════════════════════════════════════ -->
    <!-- TYPOGRAPHY TAB -->
    <!-- ═══════════════════════════════════════════════════════════════ -->
    {:else if activeTab === 'typography'}
      <section class="mb-8">
        <h2 class="text-[13px] font-extrabold uppercase tracking-wider text-white border-l-4 border-l-[#D83B01] pl-3 mb-4" style="line-height: 1.2;">
          Type Scale
        </h2>

        <div class="space-y-2">
          <div class="border border-[#333] p-3 flex items-baseline justify-between" style="background-color: #222222;">
            <span class="font-extrabold uppercase tracking-[-0.5px] text-white" style="font-size: 20px; line-height: 1.1;">Page Title</span>
            <span class="text-[9px] font-mono text-[#666]">20px / 800 / UP / -0.5px</span>
          </div>
          <div class="border border-[#333] p-3 flex items-baseline justify-between" style="background-color: #222222;">
            <span class="font-extrabold uppercase tracking-wider text-white" style="font-size: 13px; line-height: 1.2;">Section Header</span>
            <span class="text-[9px] font-mono text-[#666]">13px / 800 / UP / 0.5px</span>
          </div>
          <div class="border border-[#333] p-3 flex items-baseline justify-between" style="background-color: #222222;">
            <span class="font-bold uppercase text-white" style="font-size: 12px; letter-spacing: 0.3px; line-height: 1.2;">Card Title</span>
            <span class="text-[9px] font-mono text-[#666]">12px / 700 / UP / 0.3px</span>
          </div>
          <div class="border border-[#333] p-3 flex items-baseline justify-between" style="background-color: #222222;">
            <span class="text-white" style="font-size: 13px; line-height: 1.4;">Body Text</span>
            <span class="text-[9px] font-mono text-[#666]">13px / 400 / normal / 0</span>
          </div>
          <div class="border border-[#333] p-3 flex items-baseline justify-between" style="background-color: #222222;">
            <span class="font-semibold text-white" style="font-size: 13px; line-height: 1.4;">Body Bold</span>
            <span class="text-[9px] font-mono text-[#666]">13px / 600 / normal / 0</span>
          </div>
          <div class="border border-[#333] p-3 flex items-baseline justify-between" style="background-color: #222222;">
            <span style="font-size: 11px; font-weight: 500; color: #A0A0A0; line-height: 1.3;">Caption Text</span>
            <span class="text-[9px] font-mono text-[#666]">11px / 500 / normal / 0</span>
          </div>
          <div class="border border-[#333] p-3 flex items-baseline justify-between" style="background-color: #222222;">
            <span class="font-semibold uppercase text-[#A0A0A0]" style="font-size: 10px; letter-spacing: 0.5px; line-height: 1.2;">Micro Label</span>
            <span class="text-[9px] font-mono text-[#666]">10px / 600 / UP / 0.5px</span>
          </div>
          <div class="border border-[#333] p-3 flex items-baseline justify-between" style="background-color: #222222;">
            <span class="font-mono font-bold text-[#0078D7]" style="font-size: 14px; line-height: 1;">3,247</span>
            <span class="text-[9px] font-mono text-[#666]">14px / 700 / mono / Data Value</span>
          </div>
          <div class="border border-[#333] p-3 flex items-baseline justify-between" style="background-color: #222222;">
            <span class="bg-[#107C10] text-white font-bold uppercase" style="font-size: 9px; letter-spacing: 0.5px; padding: 2px 8px; line-height: 1;">Badge</span>
            <span class="text-[9px] font-mono text-[#666]">9px / 700 / UP / 0.5px</span>
          </div>
        </div>
      </section>

    <!-- ═══════════════════════════════════════════════════════════════ -->
    <!-- BUTTONS TAB -->
    <!-- ═══════════════════════════════════════════════════════════════ -->
    {:else if activeTab === 'buttons'}
      <section class="mb-8">
        <h2 class="text-[13px] font-extrabold uppercase tracking-wider text-white border-l-4 border-l-[#D83B01] pl-3 mb-4" style="line-height: 1.2;">
          Buttons
        </h2>

        <div class="space-y-4">
          <!-- Primary (with vertical gradient) -->
          <div>
            <div class="text-[10px] font-bold uppercase text-[#A0A0A0] tracking-wider mb-2">Primary</div>
            <div class="flex flex-wrap gap-2">
              <button
                class="text-white px-4 py-2 text-[13px] font-bold uppercase tracking-wider transition-colors duration-150"
                style="background: linear-gradient(180deg, #0078D7 0%, #005A9E 100%);"
                onmouseenter={(e) => (e.currentTarget.style.background = 'linear-gradient(180deg, #1a8ae0 0%, #0068b8 100%)')}
                onmouseleave={(e) => (e.currentTarget.style.background = 'linear-gradient(180deg, #0078D7 0%, #005A9E 100%)')}
              >
                Connect
              </button>
              <button
                class="text-white px-4 py-2 text-[13px] font-bold uppercase tracking-wider transition-colors duration-150 flex items-center gap-2"
                style="background: linear-gradient(180deg, #0078D7 0%, #005A9E 100%);"
                onmouseenter={(e) => (e.currentTarget.style.background = 'linear-gradient(180deg, #1a8ae0 0%, #0068b8 100%)')}
                onmouseleave={(e) => (e.currentTarget.style.background = 'linear-gradient(180deg, #0078D7 0%, #005A9E 100%)')}
              >
                <IconPlugConnected size="16" /> Connect
              </button>
              <button
                class="text-white px-4 py-2 text-[13px] font-bold uppercase tracking-wider transition-colors duration-150 flex items-center gap-2"
                style="background: linear-gradient(180deg, #0078D7 0%, #005A9E 100%);"
                onmouseenter={(e) => (e.currentTarget.style.background = 'linear-gradient(180deg, #1a8ae0 0%, #0068b8 100%)')}
                onmouseleave={(e) => (e.currentTarget.style.background = 'linear-gradient(180deg, #0078D7 0%, #005A9E 100%)')}
              >
                <IconDeviceFloppy size="16" /> Save
              </button>
            </div>
          </div>

          <!-- Secondary -->
          <div>
            <div class="text-[10px] font-bold uppercase text-[#A0A0A0] tracking-wider mb-2">Secondary</div>
            <div class="flex flex-wrap gap-2">
              <button
                class="bg-transparent text-[#A0A0A0] px-4 py-2 text-[13px] font-bold uppercase tracking-wider transition-colors duration-150 hover:text-white"
                style="border: 1px solid #444;"
                onmouseenter={(e) => (e.currentTarget.style.backgroundColor = '#2A2A2A')}
                onmouseleave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
              >
                Cancel
              </button>
              <button
                class="bg-transparent text-[#A0A0A0] px-4 py-2 text-[13px] font-bold uppercase tracking-wider transition-colors duration-150 hover:text-white flex items-center gap-2"
                style="border: 1px solid #444;"
                onmouseenter={(e) => (e.currentTarget.style.backgroundColor = '#2A2A2A')}
                onmouseleave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
              >
                <IconUpload size="16" /> Import
              </button>
            </div>
          </div>

          <!-- Danger -->
          <div>
            <div class="text-[10px] font-bold uppercase text-[#A0A0A0] tracking-wider mb-2">Danger</div>
            <div class="flex flex-wrap gap-2">
              <button class="text-white px-4 py-2 text-[13px] font-bold uppercase tracking-wider transition-colors duration-150" style="background-color: #E81123;" onmouseenter={(e) => (e.currentTarget.style.backgroundColor = '#C50F1F')} onmouseleave={(e) => (e.currentTarget.style.backgroundColor = '#E81123')}>
                Disconnect
              </button>
              <button class="text-white px-4 py-2 text-[13px] font-bold uppercase tracking-wider transition-colors duration-150 flex items-center gap-2" style="background-color: #E81123;" onmouseenter={(e) => (e.currentTarget.style.backgroundColor = '#C50F1F')} onmouseleave={(e) => (e.currentTarget.style.backgroundColor = '#E81123')}>
                <IconTrash size="16" /> Delete
              </button>
            </div>
          </div>

          <!-- Warning -->
          <div>
            <div class="text-[10px] font-bold uppercase text-[#A0A0A0] tracking-wider mb-2">Warning</div>
            <div class="flex flex-wrap gap-2">
              <button class="text-white px-4 py-2 text-[13px] font-bold uppercase tracking-wider transition-colors duration-150" style="background-color: #D83B01;" onmouseenter={(e) => (e.currentTarget.style.backgroundColor = '#B83201')} onmouseleave={(e) => (e.currentTarget.style.backgroundColor = '#D83B01')}>
                Write to ECU
              </button>
            </div>
          </div>

          <!-- Success -->
          <div>
            <div class="text-[10px] font-bold uppercase text-[#A0A0A0] tracking-wider mb-2">Success</div>
            <div class="flex flex-wrap gap-2">
              <button class="text-white px-4 py-2 text-[13px] font-bold uppercase tracking-wider transition-colors duration-150" style="background-color: #107C10;" onmouseenter={(e) => (e.currentTarget.style.backgroundColor = '#0E6D0E')} onmouseleave={(e) => (e.currentTarget.style.backgroundColor = '#107C10')}>
                Store to NVM
              </button>
            </div>
          </div>

          <!-- Icon Only -->
          <div>
            <div class="text-[10px] font-bold uppercase text-[#A0A0A0] tracking-wider mb-2">Icon Buttons</div>
            <div class="flex flex-wrap gap-2">
              {#each [IconSettings, IconRefresh, IconCopy, IconTrash, IconX] as Icon}
                <button class="w-8 h-8 flex items-center justify-center text-[#A0A0A0] transition-colors duration-150 hover:text-white" style="border-radius: 2px;" onmouseenter={(e) => (e.currentTarget.style.backgroundColor = '#2A2A2A')} onmouseleave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}>
                  <Icon size="18" />
                </button>
              {/each}
            </div>
          </div>

          <!-- Disabled -->
          <div>
            <div class="text-[10px] font-bold uppercase text-[#A0A0A0] tracking-wider mb-2">Disabled</div>
            <div class="flex flex-wrap gap-2">
              <button class="text-[#666] px-4 py-2 text-[13px] font-bold uppercase tracking-wider cursor-not-allowed" style="background-color: #333;" disabled>
                Connect
              </button>
              <button class="text-[#666] px-4 py-2 text-[13px] font-bold uppercase tracking-wider cursor-not-allowed flex items-center gap-2" style="background-color: #333;" disabled>
                <IconLoader size="16" /> Loading...
              </button>
            </div>
          </div>

          <!-- Small -->
          <div>
            <div class="text-[10px] font-bold uppercase text-[#A0A0A0] tracking-wider mb-2">Small</div>
            <div class="flex flex-wrap gap-2">
              <button
                class="text-white px-3 py-1 text-[11px] font-bold uppercase tracking-wider transition-colors duration-150"
                style="background: linear-gradient(180deg, #0078D7 0%, #005A9E 100%);"
                onmouseenter={(e) => (e.currentTarget.style.background = 'linear-gradient(180deg, #1a8ae0 0%, #0068b8 100%)')}
                onmouseleave={(e) => (e.currentTarget.style.background = 'linear-gradient(180deg, #0078D7 0%, #005A9E 100%)')}
              >
                Apply
              </button>
              <button
                class="bg-transparent text-[#A0A0A0] px-3 py-1 text-[11px] font-bold uppercase tracking-wider transition-colors duration-150 hover:text-white"
                style="border: 1px solid #444;"
                onmouseenter={(e) => (e.currentTarget.style.backgroundColor = '#2A2A2A')}
                onmouseleave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
              >
                Reset
              </button>
            </div>
          </div>
        </div>
      </section>

    <!-- ═══════════════════════════════════════════════════════════════ -->
    <!-- FORMS TAB -->
    <!-- ═══════════════════════════════════════════════════════════════ -->
    {:else if activeTab === 'forms'}
      <section class="mb-8">
        <h2 class="text-[13px] font-extrabold uppercase tracking-wider text-white border-l-4 border-l-[#D83B01] pl-3 mb-4" style="line-height: 1.2;">
          Form Inputs
        </h2>

        <div class="space-y-4">
          <!-- Text Input -->
          <div>
            <label class="block text-[11px] font-bold uppercase tracking-wider mb-1.5" style="color: #A0A0A0;">Text Input</label>
            <input
              type="text"
              bind:value={inputVal}
              placeholder="Enter host address..."
              class="w-full text-white px-3 py-2 text-[13px] placeholder:text-[#666] focus:outline-none transition-colors duration-150"
              style="background-color: #1A1A1A; border: 1px solid #444; border-radius: 2px;"
              onfocus={(e) => (e.currentTarget.style.borderColor = '#0078D7')}
              onblur={(e) => (e.currentTarget.style.borderColor = '#444')}
            />
          </div>

          <!-- Number Input -->
          <div>
            <label class="block text-[11px] font-bold uppercase tracking-wider mb-1.5" style="color: #A0A0A0;">Number Input</label>
            <div class="flex items-center gap-2">
              <button
                class="w-8 h-8 flex items-center justify-center text-[#A0A0A0] transition-colors duration-150 hover:text-white"
                style="background-color: #1A1A1A; border: 1px solid #444; border-radius: 2px;"
                onmouseenter={(e) => (e.currentTarget.style.backgroundColor = '#2A2A2A')}
                onmouseleave={(e) => (e.currentTarget.style.backgroundColor = '#1A1A1A')}
                onclick={() => numberVal = Math.max(0, numberVal - 1)}
              >
                <IconMinus size="14" />
              </button>
              <input
                type="number"
                bind:value={numberVal}
                class="w-20 text-white text-center px-3 py-2 text-[13px] font-mono focus:outline-none"
                style="background-color: #1A1A1A; border: 1px solid #444; border-radius: 2px;"
                onfocus={(e) => (e.currentTarget.style.borderColor = '#0078D7')}
                onblur={(e) => (e.currentTarget.style.borderColor = '#444')}
              />
              <button
                class="w-8 h-8 flex items-center justify-center text-[#A0A0A0] transition-colors duration-150 hover:text-white"
                style="background-color: #1A1A1A; border: 1px solid #444; border-radius: 2px;"
                onmouseenter={(e) => (e.currentTarget.style.backgroundColor = '#2A2A2A')}
                onmouseleave={(e) => (e.currentTarget.style.backgroundColor = '#1A1A1A')}
                onclick={() => numberVal++}
              >
                <IconPlus size="14" />
              </button>
            </div>
          </div>

          <!-- Select -->
          <div>
            <label class="block text-[11px] font-bold uppercase tracking-wider mb-1.5" style="color: #A0A0A0;">Select</label>
            <select
              bind:value={selectedOption}
              class="w-full text-white px-3 py-2 text-[13px] focus:outline-none appearance-none"
              style="background-color: #1A1A1A; border: 1px solid #444; border-radius: 2px;"
              onfocus={(e) => (e.currentTarget.style.borderColor = '#0078D7')}
              onblur={(e) => (e.currentTarget.style.borderColor = '#444')}
            >
              <option value="option1">Option 1 — TCP Connection</option>
              <option value="option2">Option 2 — Serial Connection</option>
              <option value="option3">Option 3 — USB Direct</option>
            </select>
          </div>

          <!-- Checkbox -->
          <div>
            <label class="block text-[11px] font-bold uppercase tracking-wider mb-1.5" style="color: #A0A0A0;">Checkbox</label>
            <div class="flex items-center gap-3">
              <button
                class="w-4 h-4 flex items-center justify-center transition-colors duration-150"
                style="border-radius: 0; {checkboxOn ? 'background-color: #0078D7; border: 1px solid #0078D7;' : 'background-color: transparent; border: 1px solid #444;'}"
                onclick={() => checkboxOn = !checkboxOn}
              >
                {#if checkboxOn}
                  <IconCheck size="12" class="text-white" />
                {/if}
              </button>
              <span class="text-[13px] text-white">Enable logging</span>
            </div>
          </div>

          <!-- Toggle (pill-shaped, green active) -->
          <div>
            <label class="block text-[11px] font-bold uppercase tracking-wider mb-1.5" style="color: #A0A0A0;">Toggle</label>
            <button
              class="w-12 h-6 rounded-full transition-colors duration-150 relative"
              style="background-color: {toggleOn ? '#107C10' : '#333'};"
              onclick={() => toggleOn = !toggleOn}
            >
              <div
                class="w-5 h-5 bg-white absolute top-0.5 transition-transform duration-150"
                style="border-radius: 50%; transform: translateX({toggleOn ? '26px' : '2px'});"
              ></div>
            </button>
          </div>

          <!-- Search Input -->
          <div>
            <label class="block text-[11px] font-bold uppercase tracking-wider mb-1.5" style="color: #A0A0A0;">Search</label>
            <div class="relative">
              <IconSearch size="16" class="absolute left-3 top-1/2 -translate-y-1/2 text-[#666]" />
              <input
                type="text"
                placeholder="Search sensors..."
                class="w-full text-white pl-9 pr-8 py-2 text-[13px] placeholder:text-[#666] focus:outline-none"
                style="background-color: #1A1A1A; border: 1px solid #444; border-radius: 2px;"
                onfocus={(e) => (e.currentTarget.style.borderColor = '#0078D7')}
                onblur={(e) => (e.currentTarget.style.borderColor = '#444')}
              />
              <button class="absolute right-2 top-1/2 -translate-y-1/2 text-[#666] hover:text-white transition-colors duration-150">
                <IconX size="14" />
              </button>
            </div>
          </div>

          <!-- Textarea -->
          <div>
            <label class="block text-[11px] font-bold uppercase tracking-wider mb-1.5" style="color: #A0A0A0;">Textarea</label>
            <textarea
              rows="3"
              placeholder="Notes..."
              class="w-full text-white px-3 py-2 text-[13px] placeholder:text-[#666] focus:outline-none resize-none"
              style="background-color: #1A1A1A; border: 1px solid #444; border-radius: 2px;"
              onfocus={(e) => (e.currentTarget.style.borderColor = '#0078D7')}
              onblur={(e) => (e.currentTarget.style.borderColor = '#444')}
            ></textarea>
          </div>
        </div>
      </section>

    <!-- ═══════════════════════════════════════════════════════════════ -->
    <!-- CARDS TAB -->
    <!-- ═══════════════════════════════════════════════════════════════ -->
    {:else if activeTab === 'cards'}
      <section class="mb-8">
        <h2 class="text-[13px] font-extrabold uppercase tracking-wider text-white border-l-4 border-l-[#D83B01] pl-3 mb-4" style="line-height: 1.2;">
          Cards & Panels
        </h2>

        <div class="space-y-3">
          <!-- Standard Card -->
          <div class="border border-[#333] p-4" style="background-color: #222222;">
            <div class="text-[12px] font-bold uppercase tracking-wider text-white mb-2">Standard Card</div>
            <div class="text-[13px]" style="color: #A0A0A0; line-height: 1.4;">Flat rectangle with 1px border. No shadow, no radius. Content is king.</div>
          </div>

          <!-- Card with Left Accent -->
          <div class="border border-[#333] p-4" style="background-color: #222222; border-left: 4px solid #0078D7;">
            <div class="text-[12px] font-bold uppercase tracking-wider mb-2" style="color: #0078D7;">Accent Left Border</div>
            <div class="text-[13px]" style="color: #A0A0A0; line-height: 1.4;">Used for informational panels and status cards.</div>
          </div>

          <!-- Warning Card -->
          <div class="border border-[#333] p-4" style="background-color: #222222; border-left: 4px solid #D83B01;">
            <div class="flex items-center gap-2 text-[12px] font-bold uppercase tracking-wider mb-2" style="color: #D83B01;">
              <IconAlertTriangle size="16" /> Warning Card
            </div>
            <div class="text-[13px]" style="color: #A0A0A0; line-height: 1.4;">Caution state. Orange accent signals attention needed.</div>
          </div>

          <!-- Error Card -->
          <div class="border border-[#333] p-4" style="background-color: #222222; border-left: 4px solid #E81123;">
            <div class="flex items-center gap-2 text-[12px] font-bold uppercase tracking-wider mb-2" style="color: #E81123;">
              <IconCircleX size="16" /> Error Card
            </div>
            <div class="text-[13px]" style="color: #A0A0A0; line-height: 1.4;">Something went wrong. Red accent signals error.</div>
          </div>

          <!-- Success Card -->
          <div class="border border-[#333] p-4" style="background-color: #222222; border-left: 4px solid #107C10;">
            <div class="flex items-center gap-2 text-[12px] font-bold uppercase tracking-wider mb-2" style="color: #107C10;">
              <IconCircleCheck size="16" /> Success Card
            </div>
            <div class="text-[13px]" style="color: #A0A0A0; line-height: 1.4;">Operation completed. Green accent signals success.</div>
          </div>

          <!-- ECU Info Card -->
          <div class="border border-[#333] p-4" style="background-color: #222222;">
            <div class="flex items-center gap-2 text-[12px] font-bold uppercase tracking-wider mb-3" style="color: #107C10;">
              <IconCpu size="16" /> ECU Information
            </div>
            <div class="grid grid-cols-2 gap-x-6 gap-y-2">
              <div class="flex justify-between text-[12px]">
                <span style="color: #A0A0A0;">Product</span>
                <span class="text-white font-mono font-bold">ME221-V2B-PNP</span>
              </div>
              <div class="flex justify-between text-[12px]">
                <span style="color: #A0A0A0;">Firmware</span>
                <span class="text-white font-mono font-bold">v62</span>
              </div>
              <div class="flex justify-between text-[12px]">
                <span style="color: #A0A0A0;">Serial</span>
                <span class="text-white font-mono font-bold">ME-2024-001</span>
              </div>
              <div class="flex justify-between text-[12px]">
                <span style="color: #A0A0A0;">Tables</span>
                <span class="text-white font-mono font-bold">88</span>
              </div>
            </div>
          </div>
        </div>
      </section>

    <!-- ═══════════════════════════════════════════════════════════════ -->
    <!-- HEADERS TAB -->
    <!-- ═══════════════════════════════════════════════════════════════ -->
    {:else if activeTab === 'headers'}
      <section class="mb-8">
        <h2 class="text-[13px] font-extrabold uppercase tracking-wider text-white border-l-4 border-l-[#D83B01] pl-3 mb-4" style="line-height: 1.2;">
          Section Headers
        </h2>
        <p class="text-[13px] mb-4" style="color: #A0A0A0; line-height: 1.4;">
          The Metro signature: colored left border + uppercase text. Each domain has its own accent color.
        </p>

        <div class="space-y-4">
          <div>
            <div class="flex items-center gap-3 pl-3 mb-2" style="border-left: 4px solid #0078D7;">
              <IconPlugConnected size="16" class="text-[#0078D7]" />
              <h3 class="text-[13px] font-extrabold uppercase tracking-wider text-white" style="line-height: 1.2;">Connection</h3>
            </div>
            <div class="text-[13px] pl-6" style="color: #A0A0A0;">Blue accent — brand, connection, primary actions.</div>
          </div>

          <div>
            <div class="flex items-center gap-3 pl-3 mb-2" style="border-left: 4px solid #00A5A5;">
              <IconDashboard size="16" class="text-[#00A5A5]" />
              <h3 class="text-[13px] font-extrabold uppercase tracking-wider text-white" style="line-height: 1.2;">Dashboard</h3>
            </div>
            <div class="text-[13px] pl-6" style="color: #A0A0A0;">Teal accent — dashboard, gauges, live data.</div>
          </div>

          <div>
            <div class="flex items-center gap-3 pl-3 mb-2" style="border-left: 4px solid #6B2C91;">
              <IconSettings size="16" class="text-[#6B2C91]" />
              <h3 class="text-[13px] font-extrabold uppercase tracking-wider text-white" style="line-height: 1.2;">Calibration</h3>
            </div>
            <div class="text-[13px] pl-6" style="color: #A0A0A0;">Purple accent — calibration, config, advanced.</div>
          </div>

          <div>
            <div class="flex items-center gap-3 pl-3 mb-2" style="border-left: 4px solid #D83B01;">
              <IconTable size="16" class="text-[#D83B01]" />
              <h3 class="text-[13px] font-extrabold uppercase tracking-wider text-white" style="line-height: 1.2;">Tables</h3>
            </div>
            <div class="text-[13px] pl-6" style="color: #A0A0A0;">Orange accent — tables, data, warnings.</div>
          </div>

          <div>
            <div class="flex items-center gap-3 pl-3 mb-2" style="border-left: 4px solid #107C10;">
              <IconCar size="16" class="text-[#107C10]" />
              <h3 class="text-[13px] font-extrabold uppercase tracking-wider text-white" style="line-height: 1.2;">Drivers</h3>
            </div>
            <div class="text-[13px] pl-6" style="color: #A0A0A0;">Green accent — drivers, hardware, success.</div>
          </div>

          <div>
            <div class="flex items-center gap-3 pl-3 mb-2" style="border-left: 4px solid #E81123;">
              <IconMessage size="16" class="text-[#E81123]" />
              <h3 class="text-[13px] font-extrabold uppercase tracking-wider text-white" style="line-height: 1.2;">Logs</h3>
            </div>
            <div class="text-[13px] pl-6" style="color: #A0A0A0;">Red accent — logs, errors, danger states.</div>
          </div>
        </div>
      </section>

    <!-- ═══════════════════════════════════════════════════════════════ -->
    <!-- BADGES TAB -->
    <!-- ═══════════════════════════════════════════════════════════════ -->
    {:else if activeTab === 'badges'}
      <section class="mb-8">
        <h2 class="text-[13px] font-extrabold uppercase tracking-wider text-white border-l-4 border-l-[#D83B01] pl-3 mb-4" style="line-height: 1.2;">
          Badges & Status
        </h2>

        <div class="space-y-4">
          <div>
            <div class="text-[10px] font-bold uppercase tracking-wider mb-2" style="color: #A0A0A0;">Status Badges</div>
            <div class="flex flex-wrap gap-2">
              <span class="text-white font-bold uppercase" style="background-color: #107C10; font-size: 9px; letter-spacing: 0.5px; padding: 2px 8px; line-height: 1;">Connected</span>
              <span class="text-white font-bold uppercase" style="background-color: #D83B01; font-size: 9px; letter-spacing: 0.5px; padding: 2px 8px; line-height: 1;">Connecting</span>
              <span class="text-white font-bold uppercase" style="background-color: #E81123; font-size: 9px; letter-spacing: 0.5px; padding: 2px 8px; line-height: 1;">Error</span>
              <span class="font-bold uppercase" style="background-color: #333; color: #A0A0A0; font-size: 9px; letter-spacing: 0.5px; padding: 2px 8px; line-height: 1;">Disconnected</span>
              <span class="text-white font-bold uppercase" style="background-color: #0078D7; font-size: 9px; letter-spacing: 0.5px; padding: 2px 8px; line-height: 1;">Active</span>
              <span class="font-bold uppercase" style="background-color: #FFB900; color: #0A0A0A; font-size: 9px; letter-spacing: 0.5px; padding: 2px 8px; line-height: 1;">Pending</span>
            </div>
          </div>

          <div>
            <div class="text-[10px] font-bold uppercase tracking-wider mb-2" style="color: #A0A0A0;">Pill Badges (Contact Style)</div>
            <div class="flex flex-wrap gap-2">
              <span class="text-[#A0A0A0] font-bold inline-flex items-center gap-1.5" style="background-color: #222; border: 1px solid #333; font-size: 9px; padding: 4px 8px;">
                <IconPlugConnected size="12" class="text-[#0078D7]" /> TCP
              </span>
              <span class="text-[#A0A0A0] font-bold inline-flex items-center gap-1.5" style="background-color: #222; border: 1px solid #333; font-size: 9px; padding: 4px 8px;">
                <IconUsb size="12" class="text-[#6B2C91]" /> Serial
              </span>
              <span class="text-[#A0A0A0] font-bold inline-flex items-center gap-1.5" style="background-color: #222; border: 1px solid #333; font-size: 9px; padding: 4px 8px;">
                <IconNetwork size="12" class="text-[#107C10]" /> USB
              </span>
            </div>
          </div>

          <div>
            <div class="text-[10px] font-bold uppercase tracking-wider mb-2" style="color: #A0A0A0;">Data Values (14px / 700 / Mono)</div>
            <div class="space-y-2">
              <div class="border border-[#333] p-3 flex items-center justify-between" style="background-color: #222222;">
                <span class="text-[13px] text-white">Engine RPM</span>
                <span class="font-mono font-bold" style="font-size: 14px; line-height: 1; color: #0078D7;">3,247</span>
              </div>
              <div class="border border-[#333] p-3 flex items-center justify-between" style="background-color: #222222;">
                <span class="text-[13px] text-white">Coolant Temp</span>
                <span class="font-mono font-bold" style="font-size: 14px; line-height: 1; color: #107C10;">82.50</span>
              </div>
              <div class="border border-[#333] p-3 flex items-center justify-between" style="background-color: #222222;">
                <span class="text-[13px] text-white">Lambda</span>
                <span class="font-mono font-bold" style="font-size: 14px; line-height: 1; color: #E81123;">0.89</span>
              </div>
              <div class="border border-[#333] p-3 flex items-center justify-between" style="background-color: #222222;">
                <span class="text-[13px] text-white">Boost Target</span>
                <span class="font-mono font-bold" style="font-size: 14px; line-height: 1; color: #FFB900;">1.20</span>
              </div>
            </div>
          </div>
        </div>
      </section>

    <!-- ═══════════════════════════════════════════════════════════════ -->
    <!-- TABLES TAB -->
    <!-- ═══════════════════════════════════════════════════════════════ -->
    {:else if activeTab === 'tables'}
      <section class="mb-8">
        <h2 class="text-[13px] font-extrabold uppercase tracking-wider text-white border-l-4 border-l-[#D83B01] pl-3 mb-4" style="line-height: 1.2;">
          Data Tables
        </h2>

        <div class="border border-[#333] overflow-hidden mb-4" style="background-color: #222222;">
          <table class="w-full text-[13px]">
            <thead>
              <tr style="background-color: #1A1A1A;">
                <th class="text-left text-[10px] font-bold uppercase tracking-wider px-3 py-2" style="color: #A0A0A0; border-bottom: 1px solid #333;">Parameter</th>
                <th class="text-right text-[10px] font-bold uppercase tracking-wider px-3 py-2" style="color: #A0A0A0; border-bottom: 1px solid #333;">Value</th>
                <th class="text-right text-[10px] font-bold uppercase tracking-wider px-3 py-2" style="color: #A0A0A0; border-bottom: 1px solid #333;">Unit</th>
                <th class="text-center text-[10px] font-bold uppercase tracking-wider px-3 py-2" style="color: #A0A0A0; border-bottom: 1px solid #333;">Status</th>
              </tr>
            </thead>
            <tbody>
              <tr style="border-bottom: 1px solid #1F1F1F;">
                <td class="px-3 py-2 text-white">Fuel Injector</td>
                <td class="px-3 py-2 text-white text-right font-mono">4.20</td>
                <td class="px-3 py-2 text-right font-mono" style="color: #A0A0A0;">ms</td>
                <td class="px-3 py-2 text-center">
                  <span class="text-white font-bold uppercase" style="background-color: #107C10; font-size: 8px; padding: 1px 6px;">Active</span>
                </td>
              </tr>
              <tr style="border-bottom: 1px solid #1F1F1F;">
                <td class="px-3 py-2 text-white">Ignition Timing</td>
                <td class="px-3 py-2 text-white text-right font-mono">-12.50</td>
                <td class="px-3 py-2 text-right font-mono" style="color: #A0A0A0;">deg</td>
                <td class="px-3 py-2 text-center">
                  <span class="text-white font-bold uppercase" style="background-color: #0078D7; font-size: 8px; padding: 1px 6px;">Active</span>
                </td>
              </tr>
              <tr style="border-bottom: 1px solid #1F1F1F;">
                <td class="px-3 py-2 text-white">Boost Target</td>
                <td class="px-3 py-2 text-white text-right font-mono">1.20</td>
                <td class="px-3 py-2 text-right font-mono" style="color: #A0A0A0;">bar</td>
                <td class="px-3 py-2 text-center">
                  <span class="text-white font-bold uppercase" style="background-color: #D83B01; font-size: 8px; padding: 1px 6px;">Warning</span>
                </td>
              </tr>
              <tr>
                <td class="px-3 py-2 text-white">Idle Speed</td>
                <td class="px-3 py-2 text-white text-right font-mono">850.00</td>
                <td class="px-3 py-2 text-right font-mono" style="color: #A0A0A0;">rpm</td>
                <td class="px-3 py-2 text-center">
                  <span class="text-white font-bold uppercase" style="background-color: #107C10; font-size: 8px; padding: 1px 6px;">Active</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <h3 class="text-[11px] font-bold uppercase tracking-wider mb-2" style="color: #A0A0A0;">Heat Map Preview (16x16 Table)</h3>
        <div class="border border-[#333] p-3 overflow-auto" style="background-color: #222222;">
          <div class="grid grid-cols-16 gap-px w-fit">
            {#each Array(256) as _, i}
              {@const row = Math.floor(i / 16)}
              {@const col = i % 16}
              {@const val = Math.sin(row * 0.4) * Math.cos(col * 0.3) * 50 + 50}
              <div
                class="w-5 h-5 flex items-center justify-center text-[7px] font-mono"
                style="background-color: hsl({val * 2.4}, 70%, {30 + val * 0.3}%); color: rgba(255,255,255,0.8);"
              >
                {Math.round(val)}
              </div>
            {/each}
          </div>
        </div>
      </section>
    {/if}

    <!-- Footer -->
    <div class="pt-3 mt-8 text-center uppercase tracking-wider" style="border-top: 1px solid #333; font-size: 9px; color: #666;">
      ME221 Dashboard — Modernized Metro UI Style Guide — v1.1
    </div>
  </div>
</div>

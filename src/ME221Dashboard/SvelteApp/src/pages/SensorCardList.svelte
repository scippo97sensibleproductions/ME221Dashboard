<script lang="ts">
  import { type AvailableSensor, type SensorCustomization } from '../lib/HybridBridge';
  import { IconCheck } from '@tabler/icons-svelte';

  let { sensors, expandCustomizationId, edits, toggleSensor, toggleCustomization, saveCustomization, clearCustomization }: {
    sensors: AvailableSensor[];
    expandCustomizationId: number | null;
    edits: Record<number, {
      customName: string;
      customUnit: string;
      minRange: string;
      maxRange: string;
    }>;
    toggleSensor: (id: number) => void;
    toggleCustomization: (id: number) => void;
    saveCustomization: (id: number) => void;
    clearCustomization: (id: number) => void;
  } = $props();
</script>

<div class="flex-1 overflow-y-auto lg:min-h-0">
  {#if sensors.length === 0}
    <div class="flex h-full items-center justify-center">
      <p class="text-[13px]" style="color: var(--metro-text-muted);">No sensors match your filter</p>
    </div>
  {:else}
    <div class="space-y-2">
      {#each sensors as sensor (sensor.id)}
        {@const isExpanded = expandCustomizationId === sensor.id}
        <div
          style={sensor.isSelected
            ? 'background-color: var(--metro-card); border: 1px solid var(--metro-purple);'
            : 'background-color: var(--metro-card); border: 1px solid var(--metro-border);'}
        >
          <!-- Sensor row -->
          <div class="flex flex-col gap-2 px-3 py-2.5 sm:flex-row sm:items-center sm:gap-3">
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-1.5">
                <span class="truncate text-[13px] font-semibold" style="color: var(--metro-text);">
                  {sensor.customization?.customName || sensor.name}
                </span>
                {#if sensor.inEntityMap}
                  <span class="shrink-0" style="color: var(--metro-green);" title="Active in ECU"><IconCheck size={12} /></span>
                {/if}
              </div>
              <div class="mt-0.5 flex flex-wrap items-center gap-1.5">
                <span class="px-2 py-0.5 text-[9px] font-bold uppercase tracking-wider"
                      style="background-color: var(--metro-green); color: var(--metro-text-on-accent);">
                  {sensor.customization?.customUnit || sensor.unit || '—'}
                </span>
                <span class="text-[11px]" style="color: var(--metro-text-muted);">{sensor.category}</span>
                {#if sensor.customization}
                  <span class="px-2 py-0.5 text-[9px] font-bold uppercase tracking-wider"
                        style="background-color: var(--metro-purple); color: var(--metro-text-on-accent);">
                    Customized
                  </span>
                {/if}
              </div>
            </div>
            <div class="flex shrink-0 items-center gap-2">
              {#if sensor.isSelected}
                <button
                        class="flex-1 px-3 py-2 text-[12px] font-medium transition-colors duration-150 sm:flex-none sm:px-2 sm:py-1"
                        style="background-color: var(--metro-purple); color: var(--metro-text-on-accent);"
                        onclick={() => toggleSensor(sensor.id)}
                >
                  Selected
                </button>
              {:else}
                <button
                        class="metro-hover-border flex-1 px-3 py-2 text-[12px] font-medium transition-colors duration-150 sm:flex-none sm:px-2 sm:py-1"
                        style="background-color: transparent; border: 1px solid var(--metro-input-border); color: var(--metro-text-secondary);"
                        onclick={() => toggleSensor(sensor.id)}
                >
                  Select
                </button>
              {/if}
              <button
                      class="metro-hover-border flex-1 px-3 py-2 text-[12px] font-medium transition-colors duration-150 sm:flex-none sm:px-2 sm:py-1"
                      style="background-color: transparent; border: 1px solid var(--metro-input-border); color: var(--metro-text-secondary);"
                      onclick={() => toggleCustomization(sensor.id)}
              >
                Customize
              </button>
            </div>
          </div>

          <!-- Expanded customization panel -->
          {#if isExpanded}
            {@const ed = edits[sensor.id] || { customName: '', customUnit: '', minRange: '', maxRange: '' }}
            <div class="px-3 pb-3 pt-2" style="border-top: 1px solid var(--metro-border-subtle);">
              <div class="grid grid-cols-1 gap-2 sm:grid-cols-2">
                <div>
                  <label for="cust-name-{sensor.id}" class="mb-1 block text-[11px] font-extrabold uppercase tracking-wider" style="color: var(--metro-text-secondary);">Custom Name</label>
                  <input
                          id="cust-name-{sensor.id}"
                          type="text"
                          placeholder="Leave blank for default"
                          value={ed.customName}
                          oninput={(e) => { const v = (e.target as HTMLInputElement).value; edits[sensor.id] = { ...edits[sensor.id], customName: v }; }}
                          class="w-full px-3 py-2 text-[12px] outline-none transition-colors duration-150 sm:px-2 sm:py-1.5"
                          style="background-color: var(--metro-input-bg); border: 1px solid var(--metro-input-border); color: var(--metro-text);"
                          onfocus={(e) => { e.currentTarget.style.borderColor = 'var(--metro-purple)'; }}
                          onblur={(e) => { e.currentTarget.style.borderColor = 'var(--metro-input-border)'; }}
                  />
                </div>
                <div>
                  <label for="cust-unit-{sensor.id}" class="mb-1 block text-[11px] font-extrabold uppercase tracking-wider" style="color: var(--metro-text-secondary);">Custom Unit</label>
                  <input
                          id="cust-unit-{sensor.id}"
                          type="text"
                          placeholder="Leave blank for default"
                          value={ed.customUnit}
                          oninput={(e) => { const v = (e.target as HTMLInputElement).value; edits[sensor.id] = { ...edits[sensor.id], customUnit: v }; }}
                          class="w-full px-3 py-2 text-[12px] outline-none transition-colors duration-150 sm:px-2 sm:py-1.5"
                          style="background-color: var(--metro-input-bg); border: 1px solid var(--metro-input-border); color: var(--metro-text);"
                          onfocus={(e) => { e.currentTarget.style.borderColor = 'var(--metro-purple)'; }}
                          onblur={(e) => { e.currentTarget.style.borderColor = 'var(--metro-input-border)'; }}
                  />
                </div>
                <div>
                  <label for="cust-min-{sensor.id}" class="mb-1 block text-[11px] font-extrabold uppercase tracking-wider" style="color: var(--metro-text-secondary);">Min Range</label>
                  <input
                          id="cust-min-{sensor.id}"
                          type="text"
                          placeholder="Auto"
                          value={ed.minRange}
                          oninput={(e) => { const v = (e.target as HTMLInputElement).value; edits[sensor.id] = { ...edits[sensor.id], minRange: v }; }}
                          class="w-full px-3 py-2 text-[12px] outline-none transition-colors duration-150 sm:px-2 sm:py-1.5"
                          style="background-color: var(--metro-input-bg); border: 1px solid var(--metro-input-border); color: var(--metro-text);"
                          onfocus={(e) => { e.currentTarget.style.borderColor = 'var(--metro-purple)'; }}
                          onblur={(e) => { e.currentTarget.style.borderColor = 'var(--metro-input-border)'; }}
                  />
                </div>
                <div>
                  <label for="cust-max-{sensor.id}" class="mb-1 block text-[11px] font-extrabold uppercase tracking-wider" style="color: var(--metro-text-secondary);">Max Range</label>
                  <input
                          id="cust-max-{sensor.id}"
                          type="text"
                          placeholder="Auto"
                          value={ed.maxRange}
                          oninput={(e) => { const v = (e.target as HTMLInputElement).value; edits[sensor.id] = { ...edits[sensor.id], maxRange: v }; }}
                          class="w-full px-3 py-2 text-[12px] outline-none transition-colors duration-150 sm:px-2 sm:py-1.5"
                          style="background-color: var(--metro-input-bg); border: 1px solid var(--metro-input-border); color: var(--metro-text);"
                          onfocus={(e) => { e.currentTarget.style.borderColor = 'var(--metro-purple)'; }}
                          onblur={(e) => { e.currentTarget.style.borderColor = 'var(--metro-input-border)'; }}
                  />
                </div>
              </div>
              <div class="mt-2 flex items-center justify-end gap-1.5">
                <button
                        class="metro-hover-text px-3 py-1.5 text-[12px] transition-colors duration-150"
                        style="color: var(--metro-text-secondary);"
                        onclick={() => clearCustomization(sensor.id)}
                >
                  Clear
                </button>
                <button
                        class="metro-btn-primary px-4 py-1.5 text-[12px] font-medium"
                        onclick={() => saveCustomization(sensor.id)}
                >
                  Apply
                </button>
              </div>
            </div>
          {/if}
        </div>
      {/each}
    </div>
  {/if}
</div>

import { HybridBridge, isBridgeAlive } from '../HybridBridge';
import type { MonitoringPreset } from '../HybridBridge';

class PresetStoreClass {
  #presets: MonitoringPreset[] = $state([]);
  #activePresetId: string | null = $state(null);
  #initialized = false;

  get presets(): MonitoringPreset[] {
    return this.#presets;
  }

  get activePresetId(): string | null {
    return this.#activePresetId;
  }

  get sortedPresets(): MonitoringPreset[] {
    return [...this.#presets].sort((a, b) => a.name.localeCompare(b.name));
  }

  async init(): Promise<void> {
    if (this.#initialized) return;
    if (!isBridgeAlive()) {
      this.#initialized = true;
      return;
    }

    try {
      const result = await HybridBridge.getMonitoringPresets();
      if (result.success) {
        this.#presets = result.presets ?? [];
      }
    } catch (e) {
      console.error('[PresetStore] init failed:', e);
    }
    this.#initialized = true;
  }

  async createPreset(name: string, datalinkIds: number[]): Promise<{ success: boolean; error?: string }> {
    try {
      const result = await HybridBridge.createMonitoringPreset(name, datalinkIds);
      if (result.success && result.preset) {
        this.#presets = [...this.#presets, result.preset];
      }
      return result;
    } catch (e) {
      return { success: false, error: e instanceof Error ? e.message : 'Failed to create preset' };
    }
  }

  async updatePreset(id: string, name: string, datalinkIds: number[]): Promise<{ success: boolean; error?: string }> {
    try {
      const result = await HybridBridge.updateMonitoringPreset(id, name, datalinkIds);
      if (result.success && result.preset) {
        this.#presets = this.#presets.map(p => p.id === id ? result.preset! : p);
      }
      return result;
    } catch (e) {
      return { success: false, error: e instanceof Error ? e.message : 'Failed to update preset' };
    }
  }

  async deletePreset(id: string): Promise<{ success: boolean; error?: string }> {
    try {
      const result = await HybridBridge.deleteMonitoringPreset(id);
      if (result.success) {
        this.#presets = this.#presets.filter(p => p.id !== id);
        if (this.#activePresetId === id) {
          this.#activePresetId = null;
        }
      }
      return result;
    } catch (e) {
      return { success: false, error: e instanceof Error ? e.message : 'Failed to delete preset' };
    }
  }

  loadPreset(id: string): MonitoringPreset | undefined {
    const preset = this.#presets.find(p => p.id === id);
    if (preset) {
      this.#activePresetId = id;
    }
    return preset;
  }

  getPresetById(id: string): MonitoringPreset | undefined {
    return this.#presets.find(p => p.id === id);
  }

  clearActivePreset(): void {
    this.#activePresetId = null;
  }
}

export const presetStore = new PresetStoreClass();

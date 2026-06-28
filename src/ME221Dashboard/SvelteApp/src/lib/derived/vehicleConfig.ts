import { defaultDerivedConfig } from './types';
import type { VehicleConfig } from './types';
import { HybridBridge } from '../HybridBridge';

export async function loadDerivedConfig(): Promise<VehicleConfig> {
  try {
    const vc = await HybridBridge.getVehicleConfig();
    return { ...defaultDerivedConfig(), ...vc };
  } catch (err) {
    console.error('[VEHCFG] loadDerivedConfig FAILED:', err);
    return defaultDerivedConfig();
  }
}

export async function saveDerivedConfig(config: VehicleConfig): Promise<void> {
  try {
    console.log('[VEHCFG] saveDerivedConfig', JSON.parse(JSON.stringify(config)));
    await HybridBridge.setVehicleConfig(config);
  } catch (err) {
    console.error('[VEHCFG] saveDerivedConfig FAILED:', err);
  }
}

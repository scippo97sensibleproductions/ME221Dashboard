import { defaultDerivedConfig } from './types';
import type { VehicleConfig } from './types';
import { HybridBridge } from '../HybridBridge';

/** Save payload: the vehicle fields plus an optional auto-detect flag (R8) —
 *  never the shifter block (the modal strips it; the section sends it). */
export interface VehicleConfigSavePayload extends Omit<VehicleConfig, 'shifter'> {
  shifter?: never;
  autoDetect?: boolean;
}

export async function loadDerivedConfig(): Promise<VehicleConfig> {
  try {
    const vc = await HybridBridge.getVehicleConfig();
    return { ...defaultDerivedConfig(), ...vc };
  } catch (err) {
    console.error('[VEHCFG] loadDerivedConfig FAILED:', err);
    return defaultDerivedConfig();
  }
}

export async function saveDerivedConfig(config: VehicleConfigSavePayload): Promise<void> {
  try {
    await HybridBridge.setVehicleConfig(config);
  } catch (err) {
    console.error('[VEHCFG] saveDerivedConfig FAILED:', err);
  }
}

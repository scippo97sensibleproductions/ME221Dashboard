import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { HybridBridge } from '../HybridBridge';
import { defaultDerivedConfig } from '../derived/types';
import type { VehicleConfig } from '../derived/types';

type InvokeDotNet = (method: string, params?: unknown[]) => Promise<string>;

function installWebViewStub(handler: InvokeDotNet): void {
  vi.stubGlobal('window', {
    HybridWebView: {
      InvokeDotNet: vi.fn(handler),
      SendRawMessage: vi.fn(),
    },
  });
}

function successJson(payload: unknown): string {
  return JSON.stringify({ success: true, ...payload });
}

describe('vehicle config cache (per-dashboard flip)', () => {
  beforeEach(() => {
    HybridBridge._configCache = null;
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('invalidates _configCache on dashboard switch', async () => {
    const seeded: VehicleConfig = {
      ...defaultDerivedConfig(),
      finalDriveRatio: 4.1,
      shifter: { shiftPointRpm: 7000, downshiftFloorRpm: 5000 },
    };
    HybridBridge._configCache = seeded;
    installWebViewStub(async (method) => {
      if (method === 'SetActiveDashboard') return successJson({});
      return successJson({});
    });

    const result = await HybridBridge.setActiveDashboard('track');
    expect(result.success).toBe(true);
    expect(HybridBridge._configCache).toBeNull();
  });

  it('a partial (vehicle-only) save preserves the cached shifter block', async () => {
    HybridBridge._configCache = {
      ...defaultDerivedConfig(),
      shifter: { shiftPointRpm: 7000, downshiftFloorRpm: 5000 },
    };
    installWebViewStub(async (method) => {
      expect(method).toBe('SetVehicleConfig');
      return successJson({});
    });

    // A genuine vehicle-only save never carries the shifter block (U6 strips the
    // keys); the merge must preserve the cached shifter block.
    const full = { ...defaultDerivedConfig(), finalDriveRatio: 4.6 };
    const { shifter: _dropped, ...vehicleOnly } = full;
    void _dropped;
    const result = await HybridBridge.setVehicleConfig(vehicleOnly);
    expect(result.success).toBe(true);
    expect(HybridBridge._configCache?.shifter).toEqual({ shiftPointRpm: 7000, downshiftFloorRpm: 5000 });
    expect(HybridBridge._configCache?.finalDriveRatio).toBe(4.6);
  });

  it('a section save with an explicit shifter block replaces it', async () => {
    HybridBridge._configCache = {
      ...defaultDerivedConfig(),
      shifter: { shiftPointRpm: 7000, downshiftFloorRpm: 5000 },
    };
    installWebViewStub(async () => successJson({}));

    await HybridBridge.setVehicleConfig({
      ...defaultDerivedConfig(),
      shifter: { shiftPointRpm: 6500, downshiftFloorRpm: 4800 },
    });
    expect(HybridBridge._configCache?.shifter).toEqual({ shiftPointRpm: 6500, downshiftFloorRpm: 4800 });
  });

  it('getVehicleConfig returns a copy — mutating the result does not poison the cache', async () => {
    HybridBridge._configCache = {
      ...defaultDerivedConfig(),
      shifter: { shiftPointRpm: 7000, downshiftFloorRpm: 5000 },
    };
    installWebViewStub(async () => successJson({}));

    const returned = await HybridBridge.getVehicleConfig();
    returned.finalDriveRatio = 99;
    returned.shifter!.shiftPointRpm = 1;

    expect(HybridBridge._configCache?.finalDriveRatio).not.toBe(99);
    expect(HybridBridge._configCache?.shifter?.shiftPointRpm).toBe(7000);
  });

  it('getVehicleConfig fetches from C# when the cache is empty', async () => {
    installWebViewStub(async (method) => {
      expect(method).toBe('GetVehicleConfig');
      return JSON.stringify({
        enabled: true,
        tireDiameterInches: 23,
        finalDriveRatio: 4.1,
        gearRatios: [3.6, 2.2, 1.5, 1.1, 0.85, 0.7],
        wheelSlipPercent: 3,
        rpmEntityId: 940,
        vssSpeedEntityId: null,
        mapEntityId: null,
        baroEntityId: null,
        gearEntityId: null,
        shifter: { shiftPointRpm: 7000, downshiftFloorRpm: 0 },
      });
    });

    const config = await HybridBridge.getVehicleConfig();
    expect(config.finalDriveRatio).toBe(4.1);
    expect(config.shifter).toEqual({ shiftPointRpm: 7000, downshiftFloorRpm: 0 });
    expect(HybridBridge._configCache).not.toBeNull();
  });

  it('the transport-only autoDetect flag never round-trips through the cache', async () => {
    HybridBridge._configCache = { ...defaultDerivedConfig() };
    installWebViewStub(async () => successJson({}));

    await HybridBridge.setVehicleConfig({ ...defaultDerivedConfig(), autoDetect: true });
    expect(HybridBridge._configCache).not.toHaveProperty('autoDetect');

    // A later getVehicleConfig (the merge source for the shifter-section save)
    // must not carry the flag either — otherwise the R8 seed gate would be
    // silently disabled on every subsequent save.
    const returned = await HybridBridge.getVehicleConfig();
    expect(returned).not.toHaveProperty('autoDetect');
  });

  it('gearRatios is deep-copied: mutating the returned array does not poison the cache', async () => {
    HybridBridge._configCache = {
      ...defaultDerivedConfig(),
      gearRatios: [3.6, 2.2, 1.5, 1.1, 0.85, 0.7],
    };
    installWebViewStub(async () => successJson({}));

    const returned = await HybridBridge.getVehicleConfig();
    returned.gearRatios.push(99);
    returned.gearRatios[0] = 0;

    expect(HybridBridge._configCache?.gearRatios).toEqual([3.6, 2.2, 1.5, 1.1, 0.85, 0.7]);
  });

  it('setVehicleConfig clones the caller array so later caller mutation cannot poison the cache', async () => {
    HybridBridge._configCache = { ...defaultDerivedConfig() };
    installWebViewStub(async () => successJson({}));

    const payload = { ...defaultDerivedConfig(), gearRatios: [3.6, 2.2, 1.5] };
    await HybridBridge.setVehicleConfig(payload);
    payload.gearRatios.push(99);

    expect(HybridBridge._configCache?.gearRatios).toEqual([3.6, 2.2, 1.5]);
  });

  it('deleteDashboard success invalidates the cache (deleted dashboard may have been active)', async () => {
    HybridBridge._configCache = {
      ...defaultDerivedConfig(),
      finalDriveRatio: 4.1,
      shifter: { shiftPointRpm: 7000, downshiftFloorRpm: 5000 },
    };
    installWebViewStub(async (method) => {
      expect(method).toBe('DeleteDashboard');
      return successJson({ activeDashboard: 'track' });
    });

    const result = await HybridBridge.deleteDashboard('default');
    expect(result.success).toBe(true);
    expect(HybridBridge._configCache).toBeNull();
  });

  it('renameDashboard success invalidates the cache (renamed dashboard may have been active)', async () => {
    HybridBridge._configCache = { ...defaultDerivedConfig() };
    installWebViewStub(async (method) => {
      expect(method).toBe('RenameDashboard');
      return successJson({ activeDashboard: 'street' });
    });

    const result = await HybridBridge.renameDashboard('default', 'street');
    expect(result.success).toBe(true);
    expect(HybridBridge._configCache).toBeNull();
  });
});

import type { VehicleConfig } from './types';

const RPM_PATTERNS = [/^rpm$/i, /engine\s*speed/i, /engine\s*rpm/i, /^engine$/i];
const VSS_PATTERNS = [/vss\s*speed/i, /vehicle\s*speed/i, /^speed$/i, /road\s*speed/i];
const MAP_PATTERNS = [/^map$/i, /manifold\s*abs/i, /boost\s*pressure/i, /manifold\s*pressure/i];
const BARO_PATTERNS = [/^baro$/i, /barometric/i, /ambient\s*press/i, /atmospheric/i];
const GEAR_PATTERNS = [/gear$/i, /vss\s*gear/i, /transmission\s*gear/i, /selected\s*gear/i];

function matchFirst(
  patterns: RegExp[],
  sensors: { id: number; name: string }[]
): number | null {
  for (const pat of patterns) {
    const match = sensors.find(s => pat.test(s.name.trim()));
    if (match) return match.id;
  }
  return null;
}

export function autoDetectMapping(
  sensors: { id: number; name: string }[]
): Partial<VehicleConfig> {
  return {
    rpmEntityId: matchFirst(RPM_PATTERNS, sensors),
    vssSpeedEntityId: matchFirst(VSS_PATTERNS, sensors),
    mapEntityId: matchFirst(MAP_PATTERNS, sensors),
    baroEntityId: matchFirst(BARO_PATTERNS, sensors),
    gearEntityId: matchFirst(GEAR_PATTERNS, sensors),
  };
}

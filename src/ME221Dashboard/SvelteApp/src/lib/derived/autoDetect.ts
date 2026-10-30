import type { RtcConfig, VehicleConfig } from './types';

const RPM_PATTERNS = [/^rpm$/i, /engine\s*speed/i, /engine\s*rpm/i, /^engine$/i];
const VSS_PATTERNS = [/vss\s*speed/i, /vehicle\s*speed/i, /^speed$/i, /road\s*speed/i];
const MAP_PATTERNS = [/^map$/i, /manifold\s*abs/i, /boost\s*pressure/i, /manifold\s*pressure/i];
const BARO_PATTERNS = [/^baro$/i, /barometric/i, /ambient\s*press/i, /atmospheric/i];
const GEAR_PATTERNS = [/gear$/i, /vss\s*gear/i, /transmission\s*gear/i, /selected\s*gear/i];
// ME221 firmware spells its flow channel "Fuelfow Est." (no space) — cover both.
const FUELFLOW_PATTERNS = [/fuel\s*flow/i, /fuelfow/i, /fuel\s*consumption/i, /fuel\s*rate/i];
const INJDUTY_PATTERNS = [/injector\s*duty/i, /inj\.?\s*duty/i];

// The firmware exposes the clock as "RTC Status", "RTC Hour", "RTC Minutes" …
const RTC_STATUS_PATTERNS = [/^rtc\s*status/i];
const RTC_DAY_PATTERNS = [/^rtc\s*day/i];
const RTC_MONTH_PATTERNS = [/^rtc\s*month/i];
const RTC_YEAR_PATTERNS = [/^rtc\s*year/i];
const RTC_HOUR_PATTERNS = [/^rtc\s*hour/i];
const RTC_MINUTE_PATTERNS = [/^rtc\s*minute/i];
const RTC_SECOND_PATTERNS = [/^rtc\s*second/i];

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

/** Detected clock channels; the display options stay with the user's config. */
export type DetectedRtcChannels = Pick<
  RtcConfig,
  | 'statusEntityId'
  | 'dayEntityId'
  | 'monthEntityId'
  | 'yearEntityId'
  | 'hourEntityId'
  | 'minuteEntityId'
  | 'secondEntityId'
>;

export interface AutoDetectResult
  extends Pick<
    VehicleConfig,
    | 'rpmEntityId'
    | 'vssSpeedEntityId'
    | 'mapEntityId'
    | 'baroEntityId'
    | 'gearEntityId'
    | 'fuelFlowEntityId'
    | 'injectorDutyEntityId'
  > {
  rtc: DetectedRtcChannels;
}

export function autoDetectMapping(
  sensors: { id: number; name: string }[]
): AutoDetectResult {
  return {
    rpmEntityId: matchFirst(RPM_PATTERNS, sensors),
    vssSpeedEntityId: matchFirst(VSS_PATTERNS, sensors),
    mapEntityId: matchFirst(MAP_PATTERNS, sensors),
    baroEntityId: matchFirst(BARO_PATTERNS, sensors),
    gearEntityId: matchFirst(GEAR_PATTERNS, sensors),
    fuelFlowEntityId: matchFirst(FUELFLOW_PATTERNS, sensors),
    injectorDutyEntityId: matchFirst(INJDUTY_PATTERNS, sensors),
    rtc: {
      statusEntityId: matchFirst(RTC_STATUS_PATTERNS, sensors),
      dayEntityId: matchFirst(RTC_DAY_PATTERNS, sensors),
      monthEntityId: matchFirst(RTC_MONTH_PATTERNS, sensors),
      yearEntityId: matchFirst(RTC_YEAR_PATTERNS, sensors),
      hourEntityId: matchFirst(RTC_HOUR_PATTERNS, sensors),
      minuteEntityId: matchFirst(RTC_MINUTE_PATTERNS, sensors),
      secondEntityId: matchFirst(RTC_SECOND_PATTERNS, sensors),
    },
  };
}

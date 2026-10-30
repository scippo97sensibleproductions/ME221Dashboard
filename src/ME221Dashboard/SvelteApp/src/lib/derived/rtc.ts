import type { RtcConfig } from './types';

/**
 * ECU real-time clock.
 *
 * The firmware exposes the clock as separate datalinks (status, day, month,
 * year, hour, minutes, seconds) rather than one timestamp. These helpers fold
 * those samples into the two derived entities the dashboard can display:
 * seconds since midnight (a clock) and a `YYYYMMDD` stamp (a date).
 */

const SECONDS_PER_DAY = 86400;

/** Read a required field as a whole number inside `[min, max]`, else null. */
function intInRange(
  values: Record<string, number | null>,
  entityId: number | null,
  min: number,
  max: number,
): number | null {
  if (entityId === null) return null;
  const raw = values[String(entityId)];
  if (raw === null || raw === undefined || !Number.isFinite(raw)) return null;
  const rounded = Math.round(raw);
  return rounded >= min && rounded <= max ? rounded : null;
}

/**
 * Seconds since midnight, or null when the clock is not readable.
 *
 * A mapped status channel gates the result: the firmware reports 0 for
 * "Err - Not Init", so a non-positive status withholds the value instead of
 * showing a plausible-looking 00:00:00. Second is optional (defaults to 0);
 * hour and minute are required.
 */
export function rtcClockSeconds(
  values: Record<string, number | null>,
  config: RtcConfig,
): number | null {
  const hour = intInRange(values, config.hourEntityId, 0, 23);
  const minute = intInRange(values, config.minuteEntityId, 0, 59);
  if (hour === null || minute === null) return null;

  const second = config.secondEntityId === null
    ? 0
    : intInRange(values, config.secondEntityId, 0, 59);
  if (second === null) return null;

  if (config.statusEntityId !== null) {
    const status = values[String(config.statusEntityId)];
    if (status === null || status === undefined || status <= 0) return null;
  }

  return hour * 3600 + minute * 60 + second;
}

/**
 * Date as `YYYYMMDD`, or null when it is not readable. A two-digit year is
 * centred on 2000 (the firmware sends the year either way depending on build).
 */
export function rtcDateStamp(
  values: Record<string, number | null>,
  config: RtcConfig,
): number | null {
  const day = intInRange(values, config.dayEntityId, 1, 31);
  const month = intInRange(values, config.monthEntityId, 1, 12);
  const rawYear = intInRange(values, config.yearEntityId, 0, 9999);
  if (day === null || month === null || rawYear === null) return null;

  const year = rawYear < 100 ? 2000 + rawYear : rawYear;
  return year * 10000 + month * 100 + day;
}

/** `HH:MM` or `HH:MM:SS`, 24-hour or 12-hour with an `AM`/`PM` suffix. */
export function formatClock(secondsOfDay: number, hour12: boolean, showSeconds: boolean): string {
  const total = ((Math.round(secondsOfDay) % SECONDS_PER_DAY) + SECONDS_PER_DAY) % SECONDS_PER_DAY;
  const hour24 = Math.floor(total / 3600);
  const minute = Math.floor((total % 3600) / 60);
  const second = total % 60;

  const hour = hour12 ? (hour24 % 12 === 0 ? 12 : hour24 % 12) : hour24;
  const hh = String(hour).padStart(2, '0');
  const mm = String(minute).padStart(2, '0');
  const time = showSeconds ? `${hh}:${mm}:${String(second).padStart(2, '0')}` : `${hh}:${mm}`;
  if (!hour12) return time;
  return `${time} ${hour24 < 12 ? 'AM' : 'PM'}`;
}

/**
 * `DD/MM/YYYY` from a `YYYYMMDD` stamp. Returns `---` for a stamp that cannot
 * be a real date, so a partially-read clock never renders `00/00/0000`.
 */
export function formatDate(stamp: number | null): string {
  if (stamp === null || !Number.isFinite(stamp)) return '---';
  const year = Math.floor(stamp / 10000);
  const month = Math.floor((stamp % 10000) / 100);
  const day = stamp % 100;
  if (year < 1970 || month < 1 || month > 12 || day < 1 || day > 31) return '---';
  return `${String(day).padStart(2, '0')}/${String(month).padStart(2, '0')}/${year}`;
}

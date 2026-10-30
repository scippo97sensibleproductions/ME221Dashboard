import { describe, it, expect } from 'vitest';
import { formatClock, formatDate, rtcClockSeconds, rtcDateStamp } from '../rtc';
import { defaultRtcConfig } from '../types';
import type { RtcConfig } from '../types';

/** Firmware ids as published by the calibration (RTC Status … RTC Seconds). */
const ID = {
  status: 2220,
  day: 2221,
  month: 2222,
  year: 2223,
  hour: 2224,
  minute: 2225,
  second: 2226,
} as const;

function mapped(overrides: Partial<RtcConfig> = {}): RtcConfig {
  return {
    ...defaultRtcConfig(),
    statusEntityId: ID.status,
    dayEntityId: ID.day,
    monthEntityId: ID.month,
    yearEntityId: ID.year,
    hourEntityId: ID.hour,
    minuteEntityId: ID.minute,
    secondEntityId: ID.second,
    ...overrides,
  };
}

function values(overrides: Record<string, number | null> = {}): Record<string, number | null> {
  return {
    [ID.status]: 1,
    [ID.day]: 30,
    [ID.month]: 10,
    [ID.year]: 2026,
    [ID.hour]: 14,
    [ID.minute]: 32,
    [ID.second]: 9,
    ...overrides,
  };
}

describe('rtcClockSeconds', () => {
  it('folds the clock channels into seconds since midnight', () => {
    expect(rtcClockSeconds(values(), mapped())).toBe(14 * 3600 + 32 * 60 + 9);
  });

  it('treats the seconds channel as optional', () => {
    expect(rtcClockSeconds(values(), mapped({ secondEntityId: null }))).toBe(14 * 3600 + 32 * 60);
  });

  it('withholds the clock when the firmware reports "Err - Not Init"', () => {
    expect(rtcClockSeconds(values({ [ID.status]: 0 }), mapped())).toBeNull();
    expect(rtcClockSeconds(values({ [ID.status]: null }), mapped())).toBeNull();
  });

  it('does not gate on status when no status channel is mapped', () => {
    expect(rtcClockSeconds(values({ [ID.status]: 0 }), mapped({ statusEntityId: null })))
      .toBe(14 * 3600 + 32 * 60 + 9);
  });

  it('returns null when hour or minute is unmapped or missing', () => {
    expect(rtcClockSeconds(values(), mapped({ hourEntityId: null }))).toBeNull();
    expect(rtcClockSeconds(values({ [ID.minute]: null }), mapped())).toBeNull();
    expect(rtcClockSeconds({}, mapped())).toBeNull();
  });

  it('rejects out-of-range time components', () => {
    expect(rtcClockSeconds(values({ [ID.hour]: 24 }), mapped())).toBeNull();
    expect(rtcClockSeconds(values({ [ID.minute]: 60 }), mapped())).toBeNull();
    expect(rtcClockSeconds(values({ [ID.second]: 60 }), mapped())).toBeNull();
  });

  it('rounds fractional samples instead of dropping them', () => {
    expect(rtcClockSeconds(values({ [ID.hour]: 7.4, [ID.minute]: 5.6 }), mapped()))
      .toBe(7 * 3600 + 6 * 60 + 9);
  });
});

describe('rtcDateStamp', () => {
  it('encodes the date as YYYYMMDD', () => {
    expect(rtcDateStamp(values(), mapped())).toBe(20261030);
  });

  it('centres a two-digit year on 2000', () => {
    expect(rtcDateStamp(values({ [ID.year]: 26 }), mapped())).toBe(20261030);
  });

  it('returns null when a part is missing or out of range', () => {
    expect(rtcDateStamp(values(), mapped({ yearEntityId: null }))).toBeNull();
    expect(rtcDateStamp(values({ [ID.month]: 13 }), mapped())).toBeNull();
    expect(rtcDateStamp(values({ [ID.day]: 0 }), mapped())).toBeNull();
  });
});

describe('formatClock', () => {
  it('renders 24-hour text with and without seconds', () => {
    expect(formatClock(14 * 3600 + 5 * 60 + 3, false, true)).toBe('14:05:03');
    expect(formatClock(14 * 3600 + 5 * 60 + 3, false, false)).toBe('14:05');
  });

  it('renders 12-hour text with an AM/PM suffix', () => {
    expect(formatClock(14 * 3600 + 5 * 60, true, false)).toBe('02:05 PM');
    expect(formatClock(9 * 3600, true, true)).toBe('09:00:00 AM');
  });

  it('maps midnight and noon to 12', () => {
    expect(formatClock(0, true, false)).toBe('12:00 AM');
    expect(formatClock(12 * 3600, true, false)).toBe('12:00 PM');
  });

  it('wraps out-of-day values instead of producing nonsense', () => {
    expect(formatClock(86400 + 60, false, false)).toBe('00:01');
  });
});

describe('formatDate', () => {
  it('renders European day-first text', () => {
    expect(formatDate(20261030)).toBe('30/10/2026');
  });

  it('refuses impossible or empty stamps', () => {
    expect(formatDate(null)).toBe('---');
    expect(formatDate(20261330)).toBe('---');
    expect(formatDate(0)).toBe('---');
  });
});

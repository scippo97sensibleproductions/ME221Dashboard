import { formatValue } from './gaugeUtils';
import { DerivedEntityId } from '../derived/types';
import { formatClock, formatDate } from '../derived/rtc';
import { formatDerivedValue } from '../derived/compute';

/** Clock text options; the ECU clock entity stores seconds since midnight. */
export interface ClockDisplayOptions {
  hour12: boolean;
  showSeconds: boolean;
}

export interface EntityFormatOptions {
  lambdaMode?: boolean;
  stoichAfr?: number;
  lowerName?: string;
  lowerUnit?: string;
  clock?: ClockDisplayOptions;
}

const DEFAULT_CLOCK: ClockDisplayOptions = { hour12: false, showSeconds: true };

/**
 * Times, dates, and word-valued derived entities (gear, shift state) need a
 * rendering of their own; everything else falls through to the shared numeric
 * format. One entry point keeps the three gauge-update paths in agreement.
 */
export function formatEntityValue(
  entityId: number | null,
  value: number | null,
  name: string,
  unit: string,
  options: EntityFormatOptions = {},
): string {
  if (value === null || !Number.isFinite(value)) {
    return entityId !== null && entityId <= -3000 ? '---' : '0';
  }

  switch (entityId) {
    case DerivedEntityId.EcuClock: {
      const clock = options.clock ?? DEFAULT_CLOCK;
      return formatClock(value, clock.hour12, clock.showSeconds);
    }
    case DerivedEntityId.EcuDate:
      return formatDate(value);
    case DerivedEntityId.Gear:
    case DerivedEntityId.Boost:
    case DerivedEntityId.ShiftState:
    case DerivedEntityId.FuelEfficiency:
    case DerivedEntityId.AverageFuelEfficiency:
      return formatDerivedValue(entityId, value);
    default:
      return formatValue(
        value,
        name,
        unit,
        options.lambdaMode ?? false,
        options.stoichAfr ?? 14.7,
        options.lowerName,
        options.lowerUnit,
      );
  }
}

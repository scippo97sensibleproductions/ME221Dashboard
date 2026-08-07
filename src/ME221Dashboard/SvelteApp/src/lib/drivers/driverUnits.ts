/**
 * Driver parameter unit helpers.
 *
 * MEITE DEFs carry per-param <MeasurementUnitTypes> lists (Raw, Volt, Ohm,
 * KPa, PSI, Celsius, Fahrenheit). The ECU stores the value in the first
 * (native) unit; the list enables future preferred-unit display conversion.
 */

const UNIT_ABBREVIATIONS: Record<string, string> = {
  Raw: '',
  Volt: 'V',
  Ohm: 'Ω',
  KPa: 'kPa',
  PSI: 'PSI',
  Celsius: '°C',
  Fahrenheit: '°F',
};

/** Abbreviation of the param's native (first) unit, or '' when unspecified. */
export function unitAbbreviation(measurementUnitTypes: string[] | null | undefined): string {
  const first = measurementUnitTypes?.[0];
  return first ? UNIT_ABBREVIATIONS[first] ?? first : '';
}

/** Parenthesized unit suffix for labels, e.g. "Inj Max Duty (%)" → " (%)". */
export function unitSuffix(measurementUnitTypes: string[] | null | undefined): string {
  const abbr = unitAbbreviation(measurementUnitTypes);
  return abbr ? ` (${abbr})` : '';
}

/** Compact value formatting: up to 3 decimals, trailing zeros trimmed. */
export function formatDriverValue(value: number): string {
  if (!Number.isFinite(value)) return '—';
  if (Number.isInteger(value)) return value.toString();
  return parseFloat(value.toFixed(3)).toString();
}

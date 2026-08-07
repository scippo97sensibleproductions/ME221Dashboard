import { describe, it, expect } from 'vitest';
import { unitAbbreviation, unitSuffix, formatDriverValue } from '../driverUnits';

describe('driverUnits', () => {
  describe('unitAbbreviation', () => {
    it('maps MEITE unit type names to abbreviations', () => {
      expect(unitAbbreviation(['KPa', 'PSI'])).toBe('kPa');
      expect(unitAbbreviation(['Celsius', 'Fahrenheit'])).toBe('°C');
      expect(unitAbbreviation(['Volt'])).toBe('V');
      expect(unitAbbreviation(['Ohm'])).toBe('Ω');
      expect(unitAbbreviation(['PSI'])).toBe('PSI');
    });

    it('returns empty string for Raw, empty, or missing unit lists', () => {
      expect(unitAbbreviation(['Raw'])).toBe('');
      expect(unitAbbreviation([])).toBe('');
      expect(unitAbbreviation(null)).toBe('');
      expect(unitAbbreviation(undefined)).toBe('');
    });

    it('falls back to the raw name for unknown unit types', () => {
      expect(unitAbbreviation(['SomethingNew'])).toBe('SomethingNew');
    });

    it('uses the first (native) unit', () => {
      expect(unitAbbreviation(['Fahrenheit', 'Celsius'])).toBe('°F');
    });
  });

  describe('unitSuffix', () => {
    it('parenthesizes the abbreviation', () => {
      expect(unitSuffix(['KPa'])).toBe(' (kPa)');
      expect(unitSuffix(['Volt'])).toBe(' (V)');
    });

    it('returns empty when there is no displayable unit', () => {
      expect(unitSuffix([])).toBe('');
      expect(unitSuffix(['Raw'])).toBe('');
      expect(unitSuffix(null)).toBe('');
    });
  });

  describe('formatDriverValue', () => {
    it('formats integers without decimals', () => {
      expect(formatDriverValue(0)).toBe('0');
      expect(formatDriverValue(100)).toBe('100');
      expect(formatDriverValue(-42)).toBe('-42');
    });

    it('trims trailing zeros up to 3 decimals', () => {
      expect(formatDriverValue(12.5)).toBe('12.5');
      expect(formatDriverValue(12.34)).toBe('12.34');
      expect(formatDriverValue(12.3456)).toBe('12.346');
      expect(formatDriverValue(0.1)).toBe('0.1');
      expect(formatDriverValue(2.0)).toBe('2');
    });

    it('handles non-finite values', () => {
      expect(formatDriverValue(NaN)).toBe('—');
      expect(formatDriverValue(Infinity)).toBe('—');
      expect(formatDriverValue(-Infinity)).toBe('—');
    });
  });
});

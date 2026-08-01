import { describe, it, expect } from 'vitest';
import { formatDigits } from '../digitUtils';

describe('formatDigits', () => {
  describe('auto decimals', () => {
    it('keeps the value as-is when decimals is -1', () => {
      expect(formatDigits(42, -1, false, 0)).toBe('42');
      expect(formatDigits(96.4, -1, false, 0)).toBe('96.4');
      expect(formatDigits(14.7, -1, false, 0)).toBe('14.7');
    });

    it('treats any negative decimals as auto', () => {
      expect(formatDigits(96.4, -2, false, 0)).toBe('96.4');
    });
  });

  describe('fixed decimals', () => {
    it('0 decimals rounds to an integer', () => {
      expect(formatDigits(96.4, 0, false, 0)).toBe('96');
      expect(formatDigits(7500, 0, false, 0)).toBe('7500');
    });

    it('1-3 decimals pads the fractional part', () => {
      expect(formatDigits(96.4, 1, false, 0)).toBe('96.4');
      expect(formatDigits(96.4, 2, false, 0)).toBe('96.40');
      expect(formatDigits(14.7, 3, false, 0)).toBe('14.700');
    });

    it('rounds to the nearest value', () => {
      expect(formatDigits(96.456, 2, false, 0)).toBe('96.46');
      expect(formatDigits(-3.14159, 2, false, 0)).toBe('-3.14');
    });
  });

  describe('zero padding and min digits', () => {
    it('zeroPad pads with leading zeros to minDigits', () => {
      expect(formatDigits(42, 0, true, 6)).toBe('000042');
      expect(formatDigits(0, 0, true, 6)).toBe('000000');
    });

    it('minDigits alone forces at least N digits', () => {
      expect(formatDigits(42, 0, false, 6)).toBe('000042');
    });

    it('never removes existing digits', () => {
      expect(formatDigits(123456, 0, true, 6)).toBe('123456');
      expect(formatDigits(1234567, 0, true, 6)).toBe('1234567');
    });

    it('places padding after the minus sign', () => {
      expect(formatDigits(-42, 0, false, 6)).toBe('-000042');
    });

    it('counts fractional characters toward the target', () => {
      expect(formatDigits(96.4, 1, false, 6)).toBe('0096.4');
    });

    it('pads auto-formatted values', () => {
      expect(formatDigits(96.4, -1, true, 6)).toBe('0096.4');
    });

    it('is a no-op when minDigits is 0 and zeroPad is false', () => {
      expect(formatDigits(42, 0, false, 0)).toBe('42');
    });

    it('is a no-op when zeroPad is true but minDigits is 0', () => {
      expect(formatDigits(42, 0, true, 0)).toBe('42');
    });
  });

  describe('non-finite values', () => {
    it('NaN passes through without padding', () => {
      expect(formatDigits(NaN, 0, true, 6)).toBe('NaN');
    });

    it('Infinity passes through without padding', () => {
      expect(formatDigits(Infinity, 1, true, 6)).toBe('Infinity');
      expect(formatDigits(-Infinity, 1, true, 6)).toBe('-Infinity');
    });
  });
});

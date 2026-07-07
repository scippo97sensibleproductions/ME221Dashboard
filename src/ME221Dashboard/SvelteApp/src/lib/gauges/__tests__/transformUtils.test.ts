import { describe, it, expect } from 'vitest';
import {
  ValueTransformOperation,
  applyTransform,
  stepError,
  isTransformable,
  type ValueTransformStep,
} from '../transformUtils';

const op = ValueTransformOperation;

function step(operation: ValueTransformOperation, operand = 0): ValueTransformStep {
  return { operation, operand };
}

describe('applyTransform', () => {
  it('returns value unchanged for null/empty steps', () => {
    expect(applyTransform(100, null)).toBe(100);
    expect(applyTransform(100, undefined)).toBe(100);
    expect(applyTransform(100, [])).toBe(100);
  });

  it('returns input unchanged for NaN/Infinity input', () => {
    expect(applyTransform(NaN, [step(op.Multiply, 2)])).toBe(NaN);
    expect(applyTransform(Infinity, [step(op.Add, 1)])).toBe(Infinity);
    expect(applyTransform(-Infinity, [step(op.Add, 1)])).toBe(-Infinity);
  });

  describe('individual operations', () => {
    it('Multiply', () => {
      expect(applyTransform(100, [step(op.Multiply, 0.621371)])).toBeCloseTo(62.1371);
    });

    it('Multiply by zero produces 0', () => {
      expect(applyTransform(100, [step(op.Multiply, 0)])).toBe(0);
    });

    it('Add', () => {
      expect(applyTransform(100, [step(op.Add, -5)])).toBe(95);
    });

    it('Divide', () => {
      expect(applyTransform(100, [step(op.Divide, 2)])).toBe(50);
    });

    it('Divide by zero skips step (continues chain)', () => {
      expect(applyTransform(100, [step(op.Divide, 0), step(op.Add, 5)])).toBe(105);
    });

    it('MinClamp', () => {
      expect(applyTransform(-10, [step(op.MinClamp, 0)])).toBe(0);
      expect(applyTransform(50, [step(op.MinClamp, 0)])).toBe(50);
    });

    it('MaxClamp', () => {
      expect(applyTransform(110, [step(op.MaxClamp, 100)])).toBe(100);
      expect(applyTransform(50, [step(op.MaxClamp, 100)])).toBe(50);
    });

    it('InvertSign ignores operand', () => {
      expect(applyTransform(42, [step(op.InvertSign, 99)])).toBe(-42);
      expect(applyTransform(-7, [step(op.InvertSign, 0)])).toBe(7);
    });
  });

  describe('chained operations', () => {
    it('Multiply then Add', () => {
      expect(applyTransform(100, [step(op.Multiply, 0.621371), step(op.Add, 0)])).toBeCloseTo(62.1371);
    });

    it('order matters: Multiply(2) then Divide(2) = identity', () => {
      expect(applyTransform(3, [step(op.Multiply, 2), step(op.Divide, 2)])).toBe(3);
    });

    it('order matters: Divide(2) then Multiply(2) = identity (IEEE 754)', () => {
      expect(applyTransform(3, [step(op.Divide, 2), step(op.Multiply, 2)])).toBe(3);
    });

    it('Multiply(0) then Add(5) produces 5', () => {
      expect(applyTransform(100, [step(op.Multiply, 0), step(op.Add, 5)])).toBe(5);
    });

    it('Add(5) then Multiply(0) produces 0', () => {
      expect(applyTransform(100, [step(op.Add, 5), step(op.Multiply, 0)])).toBe(0);
    });

    it('Clamp range via MinClamp + MaxClamp', () => {
      expect(applyTransform(150, [step(op.MinClamp, 0), step(op.MaxClamp, 100)])).toBe(100);
      expect(applyTransform(-10, [step(op.MinClamp, 0), step(op.MaxClamp, 100)])).toBe(0);
      expect(applyTransform(50, [step(op.MinClamp, 0), step(op.MaxClamp, 100)])).toBe(50);
    });

    it('complex chain: kPa to psi conversion', () => {
      // 200 kPa * 0.145038 = 29.0076 psi
      const steps = [step(op.Multiply, 0.145038)];
      expect(applyTransform(200, steps)).toBeCloseTo(29.0076);
    });
  });

  it('unknown operation is silently skipped', () => {
    expect(applyTransform(100, [{ operation: 99 as ValueTransformOperation, operand: 5 }])).toBe(100);
  });
});

describe('stepError', () => {
  it('Multiply by zero returns error', () => {
    expect(stepError(step(op.Multiply, 0))).toBe('Factor must be non-zero');
  });

  it('Multiply by non-zero returns null', () => {
    expect(stepError(step(op.Multiply, 5))).toBeNull();
  });

  it('Divide by zero returns error', () => {
    expect(stepError(step(op.Divide, 0))).toBe('Divisor must be non-zero');
  });

  it('Divide by non-zero returns null', () => {
    expect(stepError(step(op.Divide, 5))).toBeNull();
  });

  it('Add always returns null', () => {
    expect(stepError(step(op.Add, 0))).toBeNull();
    expect(stepError(step(op.Add, -999))).toBeNull();
  });

  it('MinClamp always returns null', () => {
    expect(stepError(step(op.MinClamp, 0))).toBeNull();
  });

  it('MaxClamp always returns null', () => {
    expect(stepError(step(op.MaxClamp, 0))).toBeNull();
  });

  it('InvertSign always returns null', () => {
    expect(stepError(step(op.InvertSign, 0))).toBeNull();
  });
});

describe('isTransformable', () => {
  it('returns true for positive entity IDs', () => {
    expect(isTransformable(1)).toBe(true);
    expect(isTransformable(123)).toBe(true);
    expect(isTransformable(99999)).toBe(true);
  });

  it('returns false for zero', () => {
    expect(isTransformable(0)).toBe(false);
  });

  it('returns false for negative entity IDs (GPS, odometer, derived)', () => {
    expect(isTransformable(-1)).toBe(false);
    expect(isTransformable(-1001)).toBe(false);
    expect(isTransformable(-2001)).toBe(false);
  });
});

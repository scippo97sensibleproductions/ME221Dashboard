/**
 * Value transform engine — chains math operations on a numeric value.
 * Transforms are scoped per-gauge and applied client-side after smoothing,
 * before formatValue(). Raw ECU values flow unmodified through the bridge.
 */

export enum ValueTransformOperation {
  Multiply = 0,
  Add = 1,
  Divide = 2,
  MinClamp = 3,
  MaxClamp = 4,
  InvertSign = 5,
}

export interface ValueTransformStep {
  operation: ValueTransformOperation;
  operand: number;
}

/**
 * Apply a chain of transform operations to a raw value.
 * Returns the value unchanged if steps is null/empty.
 * Divide by 0 → step is skipped (continues with remaining steps).
 * NaN/Infinity input → returns input unchanged.
 */
export function applyTransform(value: number, steps: ValueTransformStep[] | null | undefined): number {
  if (!steps || steps.length === 0) return value;
  if (!Number.isFinite(value)) return value;

  let result = value;
  for (const step of steps) {
    switch (step.operation) {
      case ValueTransformOperation.Multiply:
        result *= step.operand;
        break;
      case ValueTransformOperation.Add:
        result += step.operand;
        break;
      case ValueTransformOperation.Divide:
        if (step.operand === 0) break;
        result /= step.operand;
        break;
      case ValueTransformOperation.MinClamp:
        if (result < step.operand) result = step.operand;
        break;
      case ValueTransformOperation.MaxClamp:
        if (result > step.operand) result = step.operand;
        break;
      case ValueTransformOperation.InvertSign:
        result = -result;
        break;
      default:
        break;
    }
  }
  return result;
}

/**
 * Get a validation error message for a step, or null if valid.
 */
export function stepError(step: ValueTransformStep): string | null {
  switch (step.operation) {
    case ValueTransformOperation.Multiply:
      return step.operand === 0 ? 'Factor must be non-zero' : null;
    case ValueTransformOperation.Divide:
      return step.operand === 0 ? 'Divisor must be non-zero' : null;
    default:
      return null;
  }
}

/**
 * Check if an entity ID is eligible for transforms.
 * Only ECU-sourced datalinks (positive IDs) are transformable.
 * GPS (negative IDs), odometer (-2001), and derived entities are excluded.
 */
export function isTransformable(entityId: number): boolean {
  return entityId > 0;
}

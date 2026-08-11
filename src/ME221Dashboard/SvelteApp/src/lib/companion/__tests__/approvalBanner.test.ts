import { describe, it, expect } from 'vitest';
import { approvalCountdown } from '../ApprovalBanner.svelte';

const TIMEOUT_MS = 60000;
const STARTED_AT = 1_000_000;

describe('approvalCountdown', () => {
  it('returns 60 at the start', () => {
    expect(approvalCountdown(STARTED_AT, STARTED_AT, TIMEOUT_MS)).toBe(60);
  });

  it('returns 30 midway through the window', () => {
    expect(approvalCountdown(STARTED_AT, STARTED_AT + 30_000, TIMEOUT_MS)).toBe(30);
  });

  it('returns 0 exactly at the timeout', () => {
    expect(approvalCountdown(STARTED_AT, STARTED_AT + TIMEOUT_MS, TIMEOUT_MS)).toBe(0);
  });

  it('returns 0 after the timeout', () => {
    expect(approvalCountdown(STARTED_AT, STARTED_AT + TIMEOUT_MS + 5_000, TIMEOUT_MS)).toBe(0);
  });

  it('never returns negative values for long-elapsed clocks', () => {
    expect(approvalCountdown(STARTED_AT, STARTED_AT + 2 * TIMEOUT_MS, TIMEOUT_MS)).toBe(0);
  });

  it('rounds partial seconds up so the window does not expire early', () => {
    expect(approvalCountdown(STARTED_AT, STARTED_AT + 500, TIMEOUT_MS)).toBe(60);
    expect(approvalCountdown(STARTED_AT, STARTED_AT + 59_500, TIMEOUT_MS)).toBe(1);
  });
});

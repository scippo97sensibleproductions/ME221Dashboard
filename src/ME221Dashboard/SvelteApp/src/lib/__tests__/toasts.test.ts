import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { toast, getToasts, clearToasts } from '../toasts.svelte';

describe('toasts', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    clearToasts();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('starts empty', () => {
    expect(getToasts()).toEqual([]);
  });

  it('adds a toast with default info type', () => {
    toast('hello');
    const list = getToasts();
    expect(list).toHaveLength(1);
    expect(list[0]?.message).toBe('hello');
    expect(list[0]?.type).toBe('info');
    expect(list[0]?.dismissing).toBe(false);
  });

  it('adds toasts with explicit types and unique ids', () => {
    toast('a', 'success');
    toast('b', 'error');
    const list = getToasts();
    expect(list.map(t => t.type)).toEqual(['success', 'error']);
    expect(list[0]?.id).not.toBe(list[1]?.id);
  });

  it('marks a toast as dismissing after the duration', () => {
    toast('bye', 'warning', 3000);
    expect(getToasts()[0]?.dismissing).toBe(false);

    vi.advanceTimersByTime(3000);
    expect(getToasts()[0]?.dismissing).toBe(true);

    vi.advanceTimersByTime(300);
    expect(getToasts()).toEqual([]);
  });

  it('dismisses multiple toasts independently', () => {
    toast('short', 'info', 1000);
    toast('long', 'info', 5000);

    vi.advanceTimersByTime(1000);
    vi.advanceTimersByTime(300);
    const list = getToasts();
    expect(list).toHaveLength(1);
    expect(list[0]?.message).toBe('long');

    vi.advanceTimersByTime(4000);
    vi.advanceTimersByTime(300);
    expect(getToasts()).toEqual([]);
  });
});

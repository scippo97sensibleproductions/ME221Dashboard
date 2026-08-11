import { describe, it, expect, beforeEach } from 'vitest';
import { navigationGate } from '../navigationGate.svelte';

describe('navigationGate', () => {
  beforeEach(() => {
    navigationGate.setBlocked('modal-sheet', false);
    navigationGate.setBlocked('retained-draft', false);
    navigationGate.setBlocked('batch-in-flight', false);
    navigationGate.setBlocked('dirty-form', false);
    navigationGate.clearPendingNavigation();
  });

  it('starts unblocked', () => {
    expect(navigationGate.isNavigationBlocked()).toBe(false);
    expect(navigationGate.blockedReason).toBeNull();
  });

  it('setBlocked toggles the reason', () => {
    navigationGate.setBlocked('retained-draft', true);
    expect(navigationGate.isNavigationBlocked()).toBe(true);
    expect(navigationGate.blockedReason).toBe('retained-draft');

    navigationGate.setBlocked('retained-draft', false);
    expect(navigationGate.isNavigationBlocked()).toBe(false);
  });

  it('keeps the first reason while a different one is active', () => {
    navigationGate.setBlocked('retained-draft', true);
    navigationGate.setBlocked('modal-sheet', true);
    expect(navigationGate.blockedReason).toBe('retained-draft');

    navigationGate.setBlocked('retained-draft', false);
    expect(navigationGate.blockedReason).toBe('modal-sheet');
  });

  it('registerModal blocks while open and clears when closed', () => {
    const modal = navigationGate.registerModal('test-modal');
    modal.open();
    expect(navigationGate.blockedReason).toBe('modal-sheet');
    modal.close();
    expect(navigationGate.blockedReason).toBeNull();
  });

  it('closing one of two open modals keeps the reason', () => {
    const a = navigationGate.registerModal('a');
    const b = navigationGate.registerModal('b');
    a.open();
    b.open();
    expect(navigationGate.blockedReason).toBe('modal-sheet');
    a.close();
    expect(navigationGate.blockedReason).toBe('modal-sheet');
    b.close();
    expect(navigationGate.blockedReason).toBeNull();
  });

  it('isReasonActive sees reasons that are not the surfaced blockedReason', () => {
    navigationGate.setBlocked('dirty-form', true);
    navigationGate.setBlocked('modal-sheet', true);
    expect(navigationGate.blockedReason).toBe('dirty-form');
    expect(navigationGate.isReasonActive('modal-sheet')).toBe(true);
    expect(navigationGate.isReasonActive('dirty-form')).toBe(true);
    expect(navigationGate.isReasonActive('retained-draft')).toBe(false);
  });

  it('stash/take/clear scopes the pending navigation to the dirty-form gate', () => {
    navigationGate.stashNavigation({ page: 'dashboard' });
    expect(navigationGate.takePendingNavigation()).toEqual({ page: 'dashboard' });
    expect(navigationGate.takePendingNavigation()).toBeNull();

    navigationGate.stashNavigation({ page: 'tableEditor', params: { tableId: 7 } });
    navigationGate.clearPendingNavigation();
    expect(navigationGate.takePendingNavigation()).toBeNull();
  });

  it('the stash carries the delete-dashboard variant', () => {
    navigationGate.stashNavigation({ page: 'dashboard', deleteName: 'track' });
    expect(navigationGate.takePendingNavigation()).toEqual({ page: 'dashboard', deleteName: 'track' });
  });

  it("'dirty-form' and 'modal-sheet' coexist without masking each other", () => {
    navigationGate.setBlocked('dirty-form', true);
    navigationGate.setBlocked('modal-sheet', true);
    navigationGate.setBlocked('modal-sheet', false);
    expect(navigationGate.isReasonActive('dirty-form')).toBe(true);
    expect(navigationGate.blockedReason).toBe('dirty-form');
    navigationGate.setBlocked('dirty-form', false);
    expect(navigationGate.isNavigationBlocked()).toBe(false);
  });
});

export type BlockReason = 'retained-draft' | 'batch-in-flight' | 'modal-sheet' | 'dirty-form' | null;

export interface PendingNavigation {
  page: string;
  params?: Record<string, unknown>;
  /** Delete-dashboard variant: the gate resolves to the delete confirm. */
  deleteName?: string;
  /** Create-dashboard variant: the gate resolves to the create confirm. */
  createName?: string;
  /** Disconnect variant: the gate resolves to the full disconnect routine. */
  disconnect?: boolean;
}

interface ModalRegistration {
  open: () => void;
  close: () => void;
}

class NavigationGate {
  blockedReason = $state<BlockReason>(null);

  private activeReasons = new Set<Exclude<BlockReason, null>>();
  private openModals = new Set<string>();
  private pendingNav: PendingNavigation | null = null;

  private recompute(): void {
    this.blockedReason = this.activeReasons.values().next().value ?? null;
  }

  isNavigationBlocked(): boolean {
    return this.blockedReason !== null;
  }

  /** Set-membership accessor: a reason can be active even when another reason
   *  surfaces as `blockedReason` (the first-active wins). */
  isReasonActive(reason: Exclude<BlockReason, null>): boolean {
    return this.activeReasons.has(reason);
  }

  setBlocked(reason: Exclude<BlockReason, null>, blocked: boolean): void {
    if (blocked) {
      this.activeReasons.add(reason);
    } else {
      this.activeReasons.delete(reason);
    }
    this.recompute();
  }

  /**
   * Stashes the pending navigation for the 'dirty-form' gate. Scoped to the
   * dirty-form reason only: all other blocked navigations keep today's silent
   * early-return and never touch the stash.
   */
  stashNavigation(pending: PendingNavigation): void {
    this.pendingNav = pending;
  }

  /** Takes (and clears) the stashed pending navigation after the gate resolves. */
  takePendingNavigation(): PendingNavigation | null {
    const p = this.pendingNav;
    this.pendingNav = null;
    return p;
  }

  /** Clears the stash — "Stay" on the dirty dialog. */
  clearPendingNavigation(): void {
    this.pendingNav = null;
  }

  /**
   * Registers a pre-existing modal surface so an open modal suppresses toast
   * taps and navigation from day one (KTD8). Closing clears the reason only
   * when no other registered modal is still open.
   */
  registerModal(name: string): ModalRegistration {
    const modal: ModalRegistration = {
      open: () => {
        this.openModals.add(name);
        this.setBlocked('modal-sheet', true);
      },
      close: () => {
        this.openModals.delete(name);
        if (this.openModals.size === 0) {
          this.setBlocked('modal-sheet', false);
        }
      },
    };
    return modal;
  }
}

export const navigationGate = new NavigationGate();

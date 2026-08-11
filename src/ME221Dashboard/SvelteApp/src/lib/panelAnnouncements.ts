export interface LedgerEntryLike {
  dataId: number;
  levelId: string;
}

export interface PanelRow {
  dataId: number;
  levelId: string;
  levelName: string;
  name: string;
}

export interface PanelAnnouncementsDeps {
  getLedger: () => LedgerEntryLike[];
  hasVisibleToast: (dataId: number) => boolean;
  datalinkName: (dataId: number) => string;
}

export interface PanelAnnouncements {
  onOpen(rows: PanelRow[]): string[];
  onActivation(activation: PanelRow, panelOpen: boolean): string[];
  onDrop(drop: { dataId: number; fromLevelId: string; toLevelId: string | null }, panelOpen: boolean): string[];
  onCountChange(count: number, panelOpen: boolean): string[];
  reset(): void;
}

function key(dataId: number, levelId: string): string {
  return `${dataId}:${levelId}`;
}

/**
 * Exactly-once announcement ordering for the active-warning panel and badge.
 * The shared commitment ledger (owned by the toast controller) is the
 * cross-channel key: an activation announced through the toast live region or
 * the hidden live region is never re-announced by the panel. The panel keeps
 * its own announced set so activations it covered (e.g. hidden-region skips
 * while the panel is open) are not repeated by the next open-contents pass.
 */
export function createPanelAnnouncements(deps: PanelAnnouncementsDeps): PanelAnnouncements {
  let announced = new Set<string>();
  let lastCount: number | null = null;

  function ledgerHas(row: PanelRow): boolean {
    return deps.getLedger().some(e => e.dataId === row.dataId && e.levelId === row.levelId);
  }

  function announceRows(rows: PanelRow[]): string[] {
    const out: string[] = [];
    for (const row of rows) {
      const k = key(row.dataId, row.levelId);
      if (ledgerHas(row) || announced.has(k)) continue;
      announced.add(k);
      out.push(`${row.name}: ${row.levelName}`);
    }
    return out;
  }

  return {
    onOpen(rows: PanelRow[]): string[] {
      return announceRows(rows);
    },

    onActivation(activation: PanelRow, panelOpen: boolean): string[] {
      if (!panelOpen) return [];
      const k = key(activation.dataId, activation.levelId);
      if (ledgerHas(activation) || announced.has(k)) return [];
      announced.add(k);
      return [`${activation.name}: ${activation.levelName}`];
    },

    onDrop(drop: { dataId: number; fromLevelId: string; toLevelId: string | null }, panelOpen: boolean): string[] {
      if (!panelOpen) return [];
      if (deps.hasVisibleToast(drop.dataId)) return [];
      if (drop.toLevelId === null) {
        return [`${deps.datalinkName(drop.dataId)}: cleared`];
      }
      return [];
    },

    onCountChange(count: number, panelOpen: boolean): string[] {
      if (panelOpen) return [];
      if (lastCount !== null && lastCount === count) return [];
      lastCount = count;
      return [`${count} active warning${count === 1 ? '' : 's'}`];
    },

    reset(): void {
      announced = new Set();
      lastCount = null;
    },
  };
}

// ── Session-scoped ephemeral dashboard store (R7) ────────────────────────────
// B's companion mirror session keeps its own dashboard list in memory: seeded
// from A's applied snapshot, editable for the session only, discarded on
// disconnect. A new session starts from A's dashboards again.

interface SessionDashboard {
  name: string;
  gauges: { id: number }[];
  layoutLocked: boolean;
}

class SessionDashboardsStore {
  dashboards = $state<Record<string, SessionDashboard>>({});
  activeDashboard = $state<string>('default');
  /** Editing is default-locked during live mirroring (R7). */
  isEditing = $state(false);

  loadFromSnapshot(names: string[], active: string): void {
    const next: Record<string, SessionDashboard> = {};
    for (const name of names) {
      next[name] = { name, gauges: [], layoutLocked: true };
    }
    this.dashboards = next;
    this.activeDashboard = names.includes(active) ? active : (names[0] ?? 'default');
    this.isEditing = false;
  }

  createDashboard(name: string): void {
    const trimmed = name.trim();
    if (!trimmed || this.dashboards[trimmed]) return;
    this.dashboards = {
      ...this.dashboards,
      [trimmed]: { name: trimmed, gauges: [], layoutLocked: true },
    };
    this.activeDashboard = trimmed;
  }

  deleteDashboard(name: string): void {
    const next = { ...this.dashboards };
    delete next[name];
    this.dashboards = next;
    if (this.activeDashboard === name) {
      const remaining = Object.keys(next);
      this.activeDashboard = remaining[0] ?? 'default';
    }
  }

  renameDashboard(oldName: string, newName: string): void {
    const trimmed = newName.trim();
    const entry = this.dashboards[oldName];
    if (!entry || !trimmed || this.dashboards[trimmed]) return;
    const next = { ...this.dashboards };
    delete next[oldName];
    next[trimmed] = { ...entry, name: trimmed };
    this.dashboards = next;
    if (this.activeDashboard === oldName) this.activeDashboard = trimmed;
  }

  setActiveDashboard(name: string): void {
    if (this.dashboards[name]) this.activeDashboard = name;
  }

  setEditing(on: boolean): void {
    this.isEditing = on;
  }

  reset(): void {
    this.dashboards = {};
    this.activeDashboard = 'default';
    this.isEditing = false;
  }
}

export const sessionDashboards = new SessionDashboardsStore();

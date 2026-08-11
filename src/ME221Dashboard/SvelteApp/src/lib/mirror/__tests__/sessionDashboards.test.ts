import { describe, it, expect, beforeEach } from 'vitest';
import { sessionDashboards } from '../sessionDashboards.svelte';

describe('session dashboards (R7 ephemeral store)', () => {
  beforeEach(() => {
    sessionDashboards.reset();
  });

  it('loadFromSnapshot seeds names and the active dashboard', () => {
    sessionDashboards.loadFromSnapshot(['default', 'track'], 'track');

    expect(Object.keys(sessionDashboards.dashboards)).toEqual(['default', 'track']);
    expect(sessionDashboards.activeDashboard).toBe('track');
    expect(sessionDashboards.dashboards['track']).toEqual({ name: 'track', gauges: [], layoutLocked: true });
  });

  it('createDashboard adds and activates', () => {
    sessionDashboards.loadFromSnapshot(['default'], 'default');

    sessionDashboards.createDashboard('map');

    expect(sessionDashboards.dashboards['map']).toBeDefined();
    expect(sessionDashboards.activeDashboard).toBe('map');
  });

  it('deleteDashboard removes and re-activates the first remaining', () => {
    sessionDashboards.loadFromSnapshot(['default', 'track'], 'track');

    sessionDashboards.deleteDashboard('track');

    expect(sessionDashboards.dashboards['track']).toBeUndefined();
    expect(sessionDashboards.activeDashboard).toBe('default');
  });

  it('renameDashboard renames and keeps the active name', () => {
    sessionDashboards.loadFromSnapshot(['default', 'track'], 'track');

    sessionDashboards.renameDashboard('track', 'race');

    expect(sessionDashboards.dashboards['race']).toBeDefined();
    expect(sessionDashboards.dashboards['track']).toBeUndefined();
    expect(sessionDashboards.activeDashboard).toBe('race');
  });

  it('reset clears everything (discarded on disconnect)', () => {
    sessionDashboards.loadFromSnapshot(['default', 'track'], 'track');
    sessionDashboards.setEditing(true);

    sessionDashboards.reset();

    expect(sessionDashboards.dashboards).toEqual({});
    expect(sessionDashboards.activeDashboard).toBe('default');
    expect(sessionDashboards.isEditing).toBe(false);
  });

  it('setEditing toggles', () => {
    expect(sessionDashboards.isEditing).toBe(false);

    sessionDashboards.setEditing(true);
    expect(sessionDashboards.isEditing).toBe(true);

    sessionDashboards.setEditing(false);
    expect(sessionDashboards.isEditing).toBe(false);
  });

  it('re-seeding replaces the previous session content — a new session starts from the host dashboards', () => {
    sessionDashboards.loadFromSnapshot(['default'], 'default');
    sessionDashboards.createDashboard('local-only');

    sessionDashboards.loadFromSnapshot(['default', 'track'], 'track');

    expect(Object.keys(sessionDashboards.dashboards)).toEqual(['default', 'track']);
    expect(sessionDashboards.dashboards['local-only']).toBeUndefined();
    expect(sessionDashboards.activeDashboard).toBe('track');
    expect(sessionDashboards.isEditing).toBe(false);
  });
});

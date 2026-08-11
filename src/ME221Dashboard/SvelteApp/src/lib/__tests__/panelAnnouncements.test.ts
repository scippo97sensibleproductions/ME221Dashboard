import { describe, it, expect, beforeEach } from 'vitest';
import { createPanelAnnouncements, type PanelAnnouncements, type PanelRow } from '../panelAnnouncements';

let ledger: { dataId: number; levelId: string }[];
let visibleToasts: number[];
let announcements: PanelAnnouncements;

const row = (dataId: number, levelId: string, name = `DL ${dataId}`): PanelRow => ({
  dataId,
  levelId,
  levelName: levelId,
  name,
});

beforeEach(() => {
  ledger = [];
  visibleToasts = [];
  announcements = createPanelAnnouncements({
    getLedger: () => ledger,
    hasVisibleToast: (dataId) => visibleToasts.includes(dataId),
    datalinkName: (dataId) => `DL ${dataId}`,
  });
});

describe('panelAnnouncements — open contents', () => {
  it('announces the active rows on open', () => {
    const texts = announcements.onOpen([row(1, 'warning'), row(2, 'critical')]);
    expect(texts).toEqual(['DL 1: warning', 'DL 2: critical']);
  });

  it('excludes activations already announced through the toast or hidden channels (ledger)', () => {
    ledger.push({ dataId: 1, levelId: 'warning' });
    const texts = announcements.onOpen([row(1, 'warning'), row(2, 'critical')]);
    expect(texts).toEqual(['DL 2: critical']);
  });

  it('does not re-announce rows the panel itself covered before', () => {
    announcements.onOpen([row(1, 'warning')]);
    const second = announcements.onOpen([row(1, 'warning')]);
    expect(second).toEqual([]);
  });
});

describe('panelAnnouncements — mid-open activations', () => {
  it('announces a newly added row while open when nothing announced it', () => {
    const texts = announcements.onActivation(row(5, 'critical'), true);
    expect(texts).toEqual(['DL 5: critical']);
  });

  it('does not announce when the activation already has a ledger entry', () => {
    ledger.push({ dataId: 5, levelId: 'critical' });
    expect(announcements.onActivation(row(5, 'critical'), true)).toEqual([]);
  });

  it('stays silent while the panel is closed (toast channel owns it)', () => {
    expect(announcements.onActivation(row(5, 'critical'), false)).toEqual([]);
  });

  it('does not re-announce the same row on a later open', () => {
    announcements.onActivation(row(5, 'critical'), true);
    expect(announcements.onOpen([row(5, 'critical')])).toEqual([]);
  });
});

describe('panelAnnouncements — drops and removals', () => {
  it('announces a row removal when no toast is visible for the datalink', () => {
    const texts = announcements.onDrop({ dataId: 1, fromLevelId: 'warning', toLevelId: null }, true);
    expect(texts).toEqual(['DL 1: cleared']);
  });

  it('does not announce a removal that also dismisses/demotes a visible toast', () => {
    visibleToasts.push(1);
    expect(announcements.onDrop({ dataId: 1, fromLevelId: 'warning', toLevelId: null }, true)).toEqual([]);
  });

  it('does not announce demotions while a visible toast covers the change', () => {
    visibleToasts.push(1);
    expect(announcements.onDrop({ dataId: 1, fromLevelId: 'critical', toLevelId: 'warning' }, true)).toEqual([]);
  });

  it('stays silent on drops while the panel is closed', () => {
    expect(announcements.onDrop({ dataId: 1, fromLevelId: 'warning', toLevelId: null }, false)).toEqual([]);
  });
});

describe('panelAnnouncements — badge count changes', () => {
  it('announces count changes while the panel is closed', () => {
    expect(announcements.onCountChange(1, false)).toEqual(['1 active warning']);
    expect(announcements.onCountChange(2, false)).toEqual(['2 active warnings']);
  });

  it('does not repeat the same count', () => {
    announcements.onCountChange(1, false);
    expect(announcements.onCountChange(1, false)).toEqual([]);
  });

  it('stays silent while the panel is open (row list live region owns it)', () => {
    expect(announcements.onCountChange(1, true)).toEqual([]);
  });

  it('announces again after a change away and back', () => {
    announcements.onCountChange(1, false);
    announcements.onCountChange(2, false);
    expect(announcements.onCountChange(1, false)).toEqual(['1 active warning']);
  });
});

describe('panelAnnouncements — reset', () => {
  it('clears the panel-announced set so a later open announces again', () => {
    announcements.onOpen([row(1, 'warning')]);
    announcements.reset();
    expect(announcements.onOpen([row(1, 'warning')])).toEqual(['DL 1: warning']);
  });
});

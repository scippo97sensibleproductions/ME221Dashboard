import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { SessionRecorder } from '../SessionRecorder';

const storeMock = vi.hoisted(() => ({
  liveDataStore: { values: {} as Record<string, number | null> },
}));

vi.mock('../../stores/LiveDataStore.svelte', () => storeMock);

let rafCb: ((time: number) => void) | null = null;
let nowMs = 0;

function tick(timeMs = nowMs): void {
  const cb = rafCb;
  rafCb = null;
  cb?.(timeMs);
}

beforeEach(() => {
  nowMs = 0;
  rafCb = null;
  storeMock.liveDataStore.values = {};
  vi.stubGlobal('performance', { now: () => nowMs });
  vi.stubGlobal('requestAnimationFrame', (cb: (time: number) => void) => {
    rafCb = cb;
    return 1;
  });
  vi.stubGlobal('cancelAnimationFrame', vi.fn());
  SessionRecorder.reset();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('SessionRecorder lifecycle', () => {
  it('starts idle and transitions to recording', () => {
    expect(SessionRecorder.state).toBe('idle');
    SessionRecorder.start([1], new Map([[1, 'RPM']]));
    expect(SessionRecorder.state).toBe('recording');
    expect(SessionRecorder.sensorIds).toEqual([1]);
  });

  it('durationMs is 0 while idle and grows while recording', () => {
    expect(SessionRecorder.durationMs).toBe(0);
    SessionRecorder.start([1]);
    nowMs = 5000;
    expect(SessionRecorder.durationMs).toBe(5000);
  });

  it('start while already recording is a no-op', () => {
    SessionRecorder.start([1], new Map([[1, 'A']]));
    const buffer = SessionRecorder.getBuffer();
    SessionRecorder.start([2], new Map([[2, 'B']]));
    expect(SessionRecorder.sensorIds).toEqual([1]);
    expect(buffer.has(2)).toBe(false);
  });

  it('stop cancels further ticks and keeps recorded data', () => {
    storeMock.liveDataStore.values = { 1: 100 };
    SessionRecorder.start([1]);
    nowMs = 100;
    tick(100);
    SessionRecorder.stop();
    expect(SessionRecorder.state).toBe('stopped');
    expect(SessionRecorder.getRecordedData().data[1]).toHaveLength(2);
    const staleCb = rafCb;
    nowMs = 200;
    staleCb?.(200); // manually driving a stale scheduled tick must do nothing
    expect(SessionRecorder.getRecordedData().data[1]).toHaveLength(2);
  });

  it('stop while not recording is a no-op', () => {
    SessionRecorder.stop();
    expect(SessionRecorder.state).toBe('idle');
  });

  it('reset returns to idle and clears everything', () => {
    SessionRecorder.start([1]);
    nowMs = 100;
    tick(100);
    SessionRecorder.reset();
    expect(SessionRecorder.state).toBe('idle');
    expect(SessionRecorder.sensorIds).toEqual([]);
    expect(SessionRecorder.getRecordedData().data).toEqual({});
  });
});

describe('SessionRecorder sampling', () => {
  it('captures live values immediately on start and on each tick', () => {
    storeMock.liveDataStore.values = { 1: 100, 2: 200 };
    SessionRecorder.start([1, 2]);
    let recorded = SessionRecorder.getRecordedData();
    expect(recorded.data[1]).toEqual([{ t: 0, v: 100 }]);
    expect(recorded.data[2]).toEqual([{ t: 0, v: 200 }]);

    nowMs = 16.7;
    storeMock.liveDataStore.values = { 1: 150 };
    tick(nowMs);
    recorded = SessionRecorder.getRecordedData();
    expect(recorded.data[1]).toEqual([{ t: 0, v: 100 }, { t: 16.7, v: 150 }]);
    expect(recorded.data[2]).toHaveLength(1); // value was null → skipped
  });

  it('does not schedule another tick after stop', () => {
    SessionRecorder.start([1]);
    SessionRecorder.stop();
    const before = SessionRecorder.getRecordedData().data[1].length;
    nowMs = 100;
    tick(100);
    expect(SessionRecorder.getRecordedData().data[1]).toHaveLength(before);
  });

  it('getRecordedData returns defensive copies', () => {
    storeMock.liveDataStore.values = { 1: 50 };
    SessionRecorder.start([1], new Map([[1, 'RPM']]));
    nowMs = 50;
    tick(50);
    const data = SessionRecorder.getRecordedData();
    data.data[1].push({ t: 999, v: 999 });
    data.sensorIds.push(99);
    data.sensorNames[99] = 'X';
    expect(SessionRecorder.getRecordedData().data[1]).toHaveLength(2);
    expect(SessionRecorder.getRecordedData().sensorIds).toEqual([1]);
    expect(SessionRecorder.getRecordedData().sensorNames[99]).toBeUndefined();
  });
});

describe('SessionRecorder freeze frames', () => {
  it('records frames with default labels only while recording', () => {
    SessionRecorder.start([1]);
    nowMs = 1000;
    SessionRecorder.freezeFrame();
    SessionRecorder.freezeFrame('WOT pull');
    const data = SessionRecorder.getRecordedData();
    expect(data.freezeFrames).toHaveLength(2);
    expect(data.freezeFrames[0].label).toBe('Frame 1');
    expect(data.freezeFrames[1].label).toBe('WOT pull');
    expect(data.freezeFrames[1].timeMs).toBe(1000);
  });

  it('ignores freezeFrame when not recording', () => {
    SessionRecorder.freezeFrame();
    expect(SessionRecorder.getRecordedData().freezeFrames).toEqual([]);
  });
});

describe('SessionRecorder exports', () => {
  it('toCsv emits headers, rows and CSV-escaping', () => {
    storeMock.liveDataStore.values = { 1: 10, 2: 20 };
    SessionRecorder.start([1, 2], new Map([[1, 'RPM, speed'], [2, 'Bad"Name']]));
    nowMs = 100;
    tick(100);
    const csv = SessionRecorder.toCsv();
    const lines = csv.split('\n');
    expect(lines[0]).toBe('time_ms,"RPM, speed","Bad""Name"');
    expect(lines[1]).toContain('0.0,10,20');
  });

  it('toCsv returns empty string when nothing recorded', () => {
    expect(SessionRecorder.toCsv()).toBe('');
  });

  it('toVirtualDynoCsv emits Time plus one value per sensor per row', () => {
    storeMock.liveDataStore.values = { 1: 100, 2: 5 };
    SessionRecorder.start([1, 2], new Map([
      [1, 'engine speed'],
      [2, 'coolant temperature'],
    ]));
    nowMs = 1000;
    tick(1000);
    const csv = SessionRecorder.toVirtualDynoCsv();
    const lines = csv.split('\n');
    expect(lines[0]).toBe('ME221');
    expect(lines[1]).toBe('Time,RPM,Coolant Temp');
    expect(lines[2]).toBe('0.000,100,5');
    expect(lines[3]).toBe('1.000,100,5');
  });

  it('toVirtualDynoCsv keeps column alignment when a sensor misses samples', () => {
    storeMock.liveDataStore.values = { 1: 100, 2: 5 };
    SessionRecorder.start([1, 2], new Map([[1, 'RPM'], [2, 'TPS']]));
    nowMs = 100;
    tick(100);
    storeMock.liveDataStore.values = { 1: 150 }; // sensor 2 drops out
    nowMs = 200;
    tick(200);
    const lines = SessionRecorder.toVirtualDynoCsv().split('\n');
    expect(lines[1]).toBe('Time,RPM,Throttle Position');
    expect(lines[2]).toBe('0.000,100,5');
    expect(lines[3]).toBe('0.100,100,5');
    expect(lines[4]).toBe('0.200,150,');
  });

  it('toVirtualDynoCsv passes through unknown names and maps substrings', () => {
    storeMock.liveDataStore.values = { 1: 5 };
    SessionRecorder.start([1], new Map([[1, 'Fuel Rail Pressure']]));
    nowMs = 100;
    tick(100);
    const lines = SessionRecorder.toVirtualDynoCsv().split('\n');
    expect(lines[1]).toBe('Time,Fuel Pressure');
    expect(lines[2]).toBe('0.000,5');
  });

  it('toYaml emits duration and per-sensor samples', () => {
    storeMock.liveDataStore.values = { 1: 25 };
    SessionRecorder.start([1]);
    nowMs = 2000;
    tick(2000);
    const yaml = SessionRecorder.toYaml();
    expect(yaml).toContain('duration_ms: 2000');
    expect(yaml).toContain('    1:');
    expect(yaml).toContain('- t: 0.0');
    expect(yaml).toContain('v: 25');
  });
});

describe('SessionRecorder companion feed (U10)', () => {
  it('startCompanion records feed frames with A timestamps and no trimming', () => {
    SessionRecorder.startCompanion([1]);
    for (let seq = 1; seq <= 108_000; seq++) {
      SessionRecorder.pushFeedFrame(seq, seq * 100, { '1': seq });
    }
    const data = SessionRecorder.getRecordedData();
    expect(data.data[1]).toHaveLength(108_000);
    expect(data.data[1][0]).toEqual({ t: 100, v: 1 });
    expect(data.data[1][107_999]).toEqual({ t: 10_800_000, v: 108_000 });
  });

  it('records every key present in the feed when started with no sensor ids', () => {
    SessionRecorder.startCompanion([]);
    SessionRecorder.pushFeedFrame(1, 1000, { '1': 10, '2': 20 });
    SessionRecorder.pushFeedFrame(2, 2000, { '1': 11, '5': 55 });
    const data = SessionRecorder.getRecordedData();
    expect(data.sensorIds).toEqual([1, 2, 5]);
    expect(data.data[1]).toEqual([{ t: 1000, v: 10 }, { t: 2000, v: 11 }]);
    expect(data.data[2]).toEqual([{ t: 1000, v: 20 }]);
    expect(data.data[5]).toEqual([{ t: 2000, v: 55 }]);
  });

  it('skips null values and sensors that never produced a value', () => {
    SessionRecorder.startCompanion([]);
    SessionRecorder.pushFeedFrame(1, 1000, { '1': 10, '2': null });
    const data = SessionRecorder.getRecordedData();
    expect(data.sensorIds).toEqual([1]);
    expect(data.data[1]).toEqual([{ t: 1000, v: 10 }]);
    expect(data.data[2]).toBeUndefined();
  });

  it('marks missing sequence numbers as gap rows in data and exports', () => {
    SessionRecorder.startCompanion([1, 2]);
    SessionRecorder.pushFeedFrame(1, 1000, { '1': 10, '2': 20 });
    SessionRecorder.pushFeedFrame(2, 2000, { '1': 11, '2': 21 });
    SessionRecorder.pushFeedFrame(4, 4000, { '1': 13, '2': 23 });
    const data = SessionRecorder.getRecordedData();
    expect(data.data[1]).toHaveLength(4);
    expect(Number.isNaN(data.data[1][2].v)).toBe(true);
    expect(data.data[1][2].t).toBe(4000);
    expect(data.data[1][3]).toEqual({ t: 4000, v: 13 });
    expect(data.data[2]).toHaveLength(4);
    const lines = SessionRecorder.toCsv().split('\n');
    expect(lines[3]).toBe('4000.0,GAP,GAP');
    expect(SessionRecorder.toYaml()).toContain('v: GAP');
    expect(SessionRecorder.toVirtualDynoCsv()).toContain('GAP');
  });

  it('ignores duplicate and out-of-order frames without marking gaps', () => {
    SessionRecorder.startCompanion([1]);
    SessionRecorder.pushFeedFrame(1, 1000, { '1': 10 });
    SessionRecorder.pushFeedFrame(1, 1000, { '1': 99 });
    SessionRecorder.pushFeedFrame(2, 2000, { '1': 20 });
    SessionRecorder.pushFeedFrame(2, 2000, { '1': 88 });
    expect(SessionRecorder.getRecordedData().data[1])
      .toEqual([{ t: 1000, v: 10 }, { t: 2000, v: 20 }]);
  });

  it('finalize stops recording, sets a completion notice and clears it on the next start', () => {
    SessionRecorder.startCompanion([1]);
    SessionRecorder.pushFeedFrame(1, 1000, { '1': 10 });
    SessionRecorder.finalize('Session ended');
    expect(SessionRecorder.state).toBe('stopped');
    expect(SessionRecorder.completionNotice).toBe('Session ended');
    SessionRecorder.startCompanion([1]);
    expect(SessionRecorder.completionNotice).toBeNull();
  });

  it('finalize without a reason uses the default notice', () => {
    SessionRecorder.startCompanion([]);
    SessionRecorder.finalize();
    expect(SessionRecorder.completionNotice).toBe('Recording finalized');
  });

  it('newSegment finalizes and restarts with a fresh buffer (reconnect continuation)', () => {
    SessionRecorder.startCompanion([1]);
    SessionRecorder.pushFeedFrame(1, 1000, { '1': 10 });
    SessionRecorder.pushFeedFrame(2, 2000, { '1': 11 });
    SessionRecorder.newSegment();
    expect(SessionRecorder.state).toBe('recording');
    expect(SessionRecorder.segmentCount).toBe(1);
    SessionRecorder.pushFeedFrame(3, 3000, { '1': 12 });
    expect(SessionRecorder.getRecordedData().data[1]).toEqual([{ t: 3000, v: 12 }]);
  });

  it('finalize followed by startCompanion behaves like newSegment', () => {
    SessionRecorder.startCompanion([]);
    SessionRecorder.pushFeedFrame(1, 1000, { '1': 10 });
    SessionRecorder.finalize('Reconnect');
    SessionRecorder.startCompanion([]);
    SessionRecorder.pushFeedFrame(2, 2000, { '1': 20 });
    expect(SessionRecorder.getRecordedData().data[1]).toEqual([{ t: 2000, v: 20 }]);
  });

  it('start after companion recording resets companion state', () => {
    SessionRecorder.startCompanion([1]);
    SessionRecorder.pushFeedFrame(1, 1000, { '1': 10 });
    SessionRecorder.pushFeedFrame(3, 3000, { '1': 30 });
    expect(SessionRecorder.getRecordedData().data[1]).toHaveLength(3);
    SessionRecorder.stop();
    storeMock.liveDataStore.values = { 1: 50 };
    SessionRecorder.start([1]);
    expect(SessionRecorder.getRecordedData().data[1]).toEqual([{ t: 0, v: 50 }]);
    SessionRecorder.pushFeedFrame(4, 4000, { '1': 40 });
    expect(SessionRecorder.getRecordedData().data[1]).toHaveLength(1);
  });

  it('idle periods produce no further gap markers', () => {
    SessionRecorder.startCompanion([1]);
    SessionRecorder.pushFeedFrame(1, 1000, { '1': 10 });
    SessionRecorder.pushFeedFrame(3, 3000, { '1': 30 });
    const gapCount = () => SessionRecorder.getRecordedData().data[1]
      .filter(s => Number.isNaN(s.v)).length;
    expect(gapCount()).toBe(1);
    expect(SessionRecorder.getRecordedData().data[1]).toHaveLength(3);
    SessionRecorder.pushFeedFrame(4, 4000, { '1': 40 });
    expect(gapCount()).toBe(1);
    expect(SessionRecorder.getRecordedData().data[1]).toHaveLength(4);
  });
});

describe('SessionRecorder ECU disconnect finalization (U12, R21)', () => {  it('an ECU disconnect while recording finalizes the file with a completion notice', () => {
    SessionRecorder.startCompanion([1]);
    SessionRecorder.pushFeedFrame(1, 1000, { '1': 10 });

    // App.svelte's connectionStateChanged hook: disconnected → finalize.
    SessionRecorder.finalize('ECU disconnected — session finalized');

    expect(SessionRecorder.state).toBe('stopped');
    expect(SessionRecorder.completionNotice).toBe('ECU disconnected — session finalized');
    expect(SessionRecorder.getRecordedData().data[1]).toEqual([{ t: 1000, v: 10 }]);
  });

  it('a reconnect while recording starts a new segment', () => {
    SessionRecorder.startCompanion([1]);
    SessionRecorder.pushFeedFrame(1, 1000, { '1': 10 });
    SessionRecorder.finalize('ECU disconnected — session finalized');

    // App.svelte's connectionStateChanged hook: connected → new segment.
    SessionRecorder.newSegment();
    SessionRecorder.pushFeedFrame(2, 2000, { '1': 20 });

    expect(SessionRecorder.state).toBe('recording');
    expect(SessionRecorder.segmentCount).toBe(1);
    expect(SessionRecorder.getRecordedData().data[1]).toEqual([{ t: 2000, v: 20 }]);
  });

  it('a disconnect while not recording is a no-op', () => {
    SessionRecorder.reset();
    // The hook guards on state === 'recording'; even if finalize were called it
    // must not fabricate a notice for an idle recorder.
    SessionRecorder.finalize('ECU disconnected — session finalized');
    expect(SessionRecorder.completionNotice).toBeNull();
    expect(SessionRecorder.state).toBe('idle');
  });

  it('repeated disconnects produce one segment per connected period', () => {
    SessionRecorder.startCompanion([1]);
    SessionRecorder.pushFeedFrame(1, 1000, { '1': 10 });
    SessionRecorder.finalize('ECU disconnected — session finalized');
    SessionRecorder.newSegment();
    SessionRecorder.pushFeedFrame(2, 2000, { '1': 20 });
    SessionRecorder.finalize('ECU disconnected — session finalized');
    SessionRecorder.newSegment();
    SessionRecorder.pushFeedFrame(3, 3000, { '1': 30 });

    expect(SessionRecorder.segmentCount).toBe(2);
    expect(SessionRecorder.getRecordedData().data[1]).toEqual([{ t: 3000, v: 30 }]);
  });

  it('newSegment resumes a normal (non-companion) recording with its rAF tick', () => {
    // Regression (review): newSegment unconditionally switched into companion feed
    // mode, stalling A-side recordings after an ECU reconnect.
    SessionRecorder.start([1]);
    storeMock.liveDataStore.values = { 1: 50 };
    tick(0);
    SessionRecorder.newSegment();
    expect(SessionRecorder.state).toBe('recording');
    expect(SessionRecorder.segmentCount).toBe(1);

    storeMock.liveDataStore.values = { 1: 60 };
    tick(1000);
    const data = SessionRecorder.getRecordedData().data[1];
    expect(data.some(s => s.v === 60)).toBe(true); // rAF tick loop resumed after reconnect
    // A companion feed frame must NOT be consumed in normal mode.
    SessionRecorder.pushFeedFrame(1, 1000, { '1': 99 });
    expect(SessionRecorder.getRecordedData().data[1].some(s => s.v === 99)).toBe(false);
  });
});

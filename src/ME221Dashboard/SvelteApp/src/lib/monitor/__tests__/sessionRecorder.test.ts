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

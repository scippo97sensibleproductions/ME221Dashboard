import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import {
  MirrorChunkReassembler,
  SnapshotAttemptTracker,
  mirrorSequencing,
  type MirrorSnapshot,
  type SnapshotChunkMessage,
} from '../mirrorTypes';

function loadGoldenSnapshot(): MirrorSnapshot {
  const fixturePath = fileURLToPath(
    new URL('../../../../../../../tests/ME221Dashboard.Tests/Mirror/fixtures/snapshot-golden.json', import.meta.url),
  );
  return JSON.parse(readFileSync(fixturePath, 'utf8')) as MirrorSnapshot;
}

describe('mirror protocol (golden fixture agreement)', () => {
  const golden = loadGoldenSnapshot();

  it('parses the C#-generated golden snapshot', () => {
    expect(golden.schemaVersion).toBe(1);
    expect(golden.seq).toBe(42);
    expect(golden.appVersion).toBe('1.2.3');
    expect(golden.ecuConnected).toBe(true);
  });

  it('carries calibration tables and drivers in the C# wire shape', () => {
    expect(golden.calibration.dataLinks).toHaveLength(1);
    expect(golden.calibration.dataLinks[0].name).toBe('RPM');
    expect(golden.calibration.tables).toHaveLength(1);
    expect(golden.calibration.tables[0].name).toBe('Ignition');
    expect(golden.calibration.tables[0].output).toEqual([10, 12, 15, 18]);
    expect(golden.calibration.drivers).toHaveLength(1);
    expect(golden.calibration.drivers[0].configs[0].value).toBe(0.5);
  });

  it('carries the dashboard config with active dashboard and preferences', () => {
    expect(golden.dashboardConfig.activeDashboard).toBe('default');
    expect(golden.dashboardConfig.preferences?.lastConnection?.host).toBe('192.168.1.5');
    expect(golden.dashboardConfig.dashboards['default'].gauges[0].id).toBe(3);
  });

  it('carries embedded image bytes', () => {
    expect(Object.keys(golden.images)).toHaveLength(1);
    expect(golden.images['C:\\me221\\images\\bg.png']).toBe('aGVsbG8=');
  });
});

describe('mirror chunk reassembly (KTD-2)', () => {
  const json = JSON.stringify(loadGoldenSnapshot());

  function toChunks(payload: string, seq: number, chunkSize: number): SnapshotChunkMessage[] {
    const encoder = new TextEncoder();
    const bytes = encoder.encode(payload);
    const chunks: SnapshotChunkMessage[] = [];
    const total = Math.ceil(bytes.length / chunkSize);
    for (let i = 0; i < total; i++) {
      const slice = bytes.slice(i * chunkSize, Math.min(bytes.length, (i + 1) * chunkSize));
      let binary = '';
      slice.forEach((b) => (binary += String.fromCharCode(b)));
      chunks.push({ msg: 'snapshotChunk', seq, index: i, total, payload: btoa(binary) });
    }
    return chunks;
  }

  it('reassembles byte-identical JSON', () => {
    const chunks = toChunks(json, 42, 64);
    expect(chunks.length).toBeGreaterThan(1);

    const reassembler = new MirrorChunkReassembler(42);
    for (const chunk of chunks) expect(reassembler.tryAdd(chunk)).toBe(true);

    expect(reassembler.isComplete).toBe(true);
    expect(reassembler.reassemble()).toBe(json);
  });

  it('rejects out-of-order chunks', () => {
    const chunks = toChunks(json, 3, 64);
    const reassembler = new MirrorChunkReassembler(3);
    expect(reassembler.tryAdd(chunks[0])).toBe(true);
    expect(reassembler.tryAdd(chunks[2])).toBe(false);
    expect(reassembler.tryAdd(chunks[1])).toBe(true);
    expect(reassembler.tryAdd(chunks[1])).toBe(false);
  });

  it('rejects chunks from a different sequence', () => {
    const chunks = toChunks(json, 3, 64);
    const reassembler = new MirrorChunkReassembler(5);
    expect(reassembler.tryAdd(chunks[0])).toBe(false);
  });

  it('handles a single chunk', () => {
    const chunks = toChunks('{"a":1}', 9, 1024);
    expect(chunks).toHaveLength(1);
    const reassembler = new MirrorChunkReassembler(9);
    expect(reassembler.tryAdd(chunks[0])).toBe(true);
    expect(reassembler.isComplete).toBe(true);
    expect(reassembler.reassemble()).toBe('{"a":1}');
  });

  it('exhausts the whole-snapshot retry after three attempts', () => {
    const tracker = new SnapshotAttemptTracker();
    expect(tracker.recordFailure()).toBe(true);
    expect(tracker.recordFailure()).toBe(true);
    expect(tracker.recordFailure()).toBe(false);
    expect(tracker.exhausted).toBe(true);
  });

  it('enforces the snapshot-then-frames sequence rule', () => {
    expect(mirrorSequencing.isFrameAfterSnapshot(42, 42)).toBe(false);
    expect(mirrorSequencing.isFrameAfterSnapshot(41, 42)).toBe(false);
    expect(mirrorSequencing.nextFrameSequence(42)).toBe(43);
    expect(mirrorSequencing.isFrameAfterSnapshot(43, 42)).toBe(true);
  });
});

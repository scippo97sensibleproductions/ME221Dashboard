import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { mirrorStore } from '../MirrorStore.svelte';
import { HybridBridge } from '../../HybridBridge';
import type { MirrorSnapshot, SnapshotChunkMessage } from '../../mirror/mirrorTypes';

function loadGoldenSnapshot(): MirrorSnapshot {
  const fixturePath = fileURLToPath(
    new URL('../../../../../../../tests/ME221Dashboard.Tests/Mirror/fixtures/snapshot-golden.json', import.meta.url),
  );
  return JSON.parse(readFileSync(fixturePath, 'utf8')) as MirrorSnapshot;
}

function toChunks(payload: string, seq: number, chunkSize = 512): SnapshotChunkMessage[] {
  const bytes = new TextEncoder().encode(payload);
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

describe('mirror store (snapshot apply)', () => {
  beforeEach(() => {
    mirrorStore.stop();
    mirrorStore.start();
  });

  it('applies the snapshot and refreshes dashboard names', () => {
    const json = JSON.stringify(loadGoldenSnapshot());
    for (const chunk of toChunks(json, 42)) {
      mirrorStore.dispatchForTest({ event: 'mirrorSnapshotChunk', payload: chunk } as never);
    }
    mirrorStore.dispatchForTest({ event: 'mirrorSnapshotDone', payload: { seq: 42 } } as never);

    expect(mirrorStore.snapshotApplied).toBe(true);
    expect(mirrorStore.snapshotSeq).toBe(42);
    expect(mirrorStore.dashboardNames).toContain('default');
    expect(mirrorStore.activeDashboard).toBe('default');
  });

  it('invalidates the bridge config caches on apply', () => {
    const json = JSON.stringify(loadGoldenSnapshot());
    for (const chunk of toChunks(json, 1)) {
      mirrorStore.dispatchForTest({ event: 'mirrorSnapshotChunk', payload: chunk } as never);
    }
    mirrorStore.dispatchForTest({ event: 'mirrorSnapshotDone', payload: { seq: 1 } } as never);

    expect(HybridBridge._configCache).toBeNull();
  });

  it('keeps the gate closed on a malformed snapshot', () => {
    const json = '{"broken":';
    for (const chunk of toChunks(json, 1)) {
      mirrorStore.dispatchForTest({ event: 'mirrorSnapshotChunk', payload: chunk } as never);
    }
    mirrorStore.dispatchForTest({ event: 'mirrorSnapshotDone', payload: { seq: 1 } } as never);

    expect(mirrorStore.snapshotApplied).toBe(false);
  });

  it('tracks the session state and clears it on stop', () => {
    mirrorStore.dispatchForTest({ event: 'mirrorSessionState', state: 'Connected' } as never);
    expect(mirrorStore.isMirroring).toBe(true);

    mirrorStore.stop();
    expect(mirrorStore.isMirroring).toBe(false);
    expect(mirrorStore.sessionState).toBe('Searching');
  });

  it('clears the snapshot on disconnect', () => {
    const json = JSON.stringify(loadGoldenSnapshot());
    for (const chunk of toChunks(json, 42)) {
      mirrorStore.dispatchForTest({ event: 'mirrorSnapshotChunk', payload: chunk } as never);
    }
    mirrorStore.dispatchForTest({ event: 'mirrorSnapshotDone', payload: { seq: 42 } } as never);
    expect(mirrorStore.snapshotApplied).toBe(true);

    mirrorStore.dispatchForTest({ event: 'mirrorSessionState', state: 'Revoked' } as never);
    expect(mirrorStore.snapshotApplied).toBe(false);
  });

  it('propagates the config-update reconnect error and clears the snapshot', () => {
    // R19: 'update made, reconnect required' ends the session with a notice.
    mirrorStore.dispatchForTest({ event: 'mirrorSessionState', state: 'Connected' } as never);
    mirrorStore.dispatchForTest({
      event: 'mirrorSessionState',
      state: 'Disconnected',
      error: 'update made, reconnect required',
    } as never);

    expect(mirrorStore.sessionState).toBe('Disconnected');
    expect(mirrorStore.sessionError).toBe('update made, reconnect required');
    expect(mirrorStore.snapshotApplied).toBe(false);
    expect(mirrorStore.isMirroring).toBe(false);
  });

  it('marks the feed stale after the 2s window and clears on a fresh frame', () => {
    let now = 0;
    const nowSpy = vi.spyOn(performance, 'now').mockImplementation(() => now);
    vi.useFakeTimers();
    try {
      mirrorStore.dispatchForTest({ event: 'mirrorSessionState', state: 'Connected' } as never);
      mirrorStore.dispatchForTest({ event: 'liveDataUpdate', values: {} } as never);
      expect(mirrorStore.isStale).toBe(false);

      now = 2100;
      vi.advanceTimersByTime(2100);
      expect(mirrorStore.isStale).toBe(true);

      now = 2200;
      mirrorStore.dispatchForTest({ event: 'liveDataUpdate', values: { 1: 100 } } as never);
      expect(mirrorStore.isStale).toBe(false);
      vi.advanceTimersByTime(100);
      expect(mirrorStore.isStale).toBe(false);
    } finally {
      nowSpy.mockRestore();
      vi.useRealTimers();
    }
  });

  it('never arms the stale timer outside Connected', () => {
    let now = 0;
    const nowSpy = vi.spyOn(performance, 'now').mockImplementation(() => now);
    vi.useFakeTimers();
    try {
      mirrorStore.dispatchForTest({ event: 'mirrorSessionState', state: 'AwaitingApproval' } as never);
      mirrorStore.dispatchForTest({ event: 'liveDataUpdate', values: {} } as never);
      now = 2100;
      vi.advanceTimersByTime(2100);
      expect(mirrorStore.isStale).toBe(false);
    } finally {
      nowSpy.mockRestore();
      vi.useRealTimers();
    }
  });
});

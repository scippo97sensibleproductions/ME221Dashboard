/**
 * Shared mirror wire types (mirror of Services/Mirror/*).
 * Mirrors MirrorSnapshot schema, the message envelope shapes from the wire
 * protocol table, chunk reassembly (KTD-2), and the monotonic sequence rule (R11).
 */

export type MirrorMsgType =
  | 'hello'
  | 'helloAck'
  | 'approvalDecision'
  | 'snapshotChunk'
  | 'snapshotDone'
  | 'snapshotAck'
  | 'frame'
  | 'recordFeed'
  | 'event'
  | 'configUpdate'
  | 'revoke'
  | 'close';

export interface MirrorMessage {
  msg: MirrorMsgType;
}

export interface HelloMessage extends MirrorMessage {
  msg: 'hello';
  appVersion: string;
  deviceName: string;
  token?: string | null;
  clientId: string;
}

export type HelloAckStatus = 'approved' | 'pending' | 'refused' | 'versionMismatch' | 'revoked';

export interface HelloAckMessage extends MirrorMessage {
  msg: 'helloAck';
  status: HelloAckStatus;
}

export interface ApprovalDecisionMessage extends MirrorMessage {
  msg: 'approvalDecision';
  approved: boolean;
  token?: string | null;
}

export interface SnapshotChunkMessage extends MirrorMessage {
  msg: 'snapshotChunk';
  seq: number;
  index: number;
  total: number;
  payload: string;
}

export interface SnapshotDoneMessage extends MirrorMessage {
  msg: 'snapshotDone';
  seq: number;
}

export interface SnapshotAckMessage extends MirrorMessage {
  msg: 'snapshotAck';
  index: number;
}

export interface FrameMessage extends MirrorMessage {
  msg: 'frame';
  seq: number;
  ts: number;
  values: Record<string, number | null>;
}

export interface RecordFeedMessage extends MirrorMessage {
  msg: 'recordFeed';
  seq: number;
  ts: number;
  values: Record<string, number | null>;
}

export interface EventMessage extends MirrorMessage {
  msg: 'event';
  channel: 'gps' | 'odometer' | 'log';
  payload: string;
}

export interface ConfigUpdateMessage extends MirrorMessage {
  msg: 'configUpdate';
  reason: string;
}

export interface RevokedMessage extends MirrorMessage {
  msg: 'revoke';
  reason: string;
}

export interface CloseMessage extends MirrorMessage {
  msg: 'close';
  reason: string;
}

/** Full A-to-B snapshot (R6, KTD-4). */
export interface MirrorSnapshot {
  schemaVersion: number;
  seq: number;
  appVersion: string;
  ecuConnected: boolean;
  ecuInfo: { productName: string; modelName: string; version: string } | null;
  calibration: {
    metadata: { productName: string; modelName: string; version: string };
    dataLinks: {
      id: number;
      name: string;
      category: string;
      viewInTree: boolean;
      standardLogging: boolean;
      measureUnit: string;
      minValue: number;
      maxValue: number;
    }[];
    tables: {
      id: number;
      name: string;
      category: string;
      tableType: string;
      cols: number;
      rows: number;
      input0LinkId: number;
      outputLinkId: number;
      input0: number[] | null;
      output: number[] | null;
    }[];
    drivers: {
      id: number;
      name: string;
      category: string;
      numberOfConfigs: number;
      configs: { name: string; value: number; min: number; max: number }[];
    }[];
  };
  dashboardConfig: {
    activeDashboard: string;
    gridRows: number;
    gridColumns: number;
    preferences: { lastConnection: { type: string; host: string; port: number } | null } | null;
    warningSettings: unknown[];
    dashboards: Record<
      string,
      {
        gauges: { id: number; gridRow: number; gridColumn: number; shapeCategory: number }[];
        headerVisible: boolean;
        sidebarVisible: boolean;
        warningHistory: unknown[];
      }
    >;
  };
  images: Record<string, string>;
}

/** Ordered snapshot chunk collector on the JS side (KTD-2, R6). */
export class MirrorChunkReassembler {
  static readonly MAX_SNAPSHOT_BYTES = 64 * 1024 * 1024;

  private readonly _seq: number;
  private _bytes: Uint8Array[] = [];
  private _byteCount = 0;
  private _expectedIndex = 0;
  private _total = 0;
  private _complete = false;

  constructor(seq: number) {
    this._seq = seq;
  }

  get seq(): number {
    return this._seq;
  }

  get isComplete(): boolean {
    return this._complete;
  }

  get expectedIndex(): number {
    return this._expectedIndex;
  }

  get totalChunks(): number {
    return this._total;
  }

  tryAdd(chunk: SnapshotChunkMessage): boolean {
    if (this._complete || chunk.seq !== this._seq || chunk.total <= 0) return false;
    if (chunk.index !== this._expectedIndex) return false;
    if (this._byteCount + chunk.payload.length > MirrorChunkReassembler.MAX_SNAPSHOT_BYTES) return false;
    this._total = chunk.total;
    const payload = Uint8Array.from(atob(chunk.payload), (c) => c.charCodeAt(0));
    this._bytes.push(payload);
    this._byteCount += payload.length;
    this._expectedIndex++;
    this._complete = this._expectedIndex === chunk.total;
    return true;
  }

  reassemble(): string | null {
    if (!this._complete) return null;
    const total = this._bytes.reduce((n, b) => n + b.length, 0);
    const merged = new Uint8Array(total);
    let offset = 0;
    for (const b of this._bytes) {
      merged.set(b, offset);
      offset += b.length;
    }
    return new TextDecoder().decode(merged);
  }
}

/** Whole-snapshot retry budget on the sender side (R6). */
export class SnapshotAttemptTracker {
  private _attempts = 0;
  constructor(private readonly _maxAttempts = 3) {}

  get attempts(): number {
    return this._attempts;
  }

  get exhausted(): boolean {
    return this._attempts >= this._maxAttempts;
  }

  recordFailure(): boolean {
    this._attempts++;
    return this._attempts < this._maxAttempts;
  }

  reset(): void {
    this._attempts = 0;
  }
}

export const mirrorSequencing = {
  nextFrameSequence(snapshotSeq: number): number {
    return snapshotSeq + 1;
  },
  isFrameAfterSnapshot(frameSeq: number, snapshotSeq: number): boolean {
    return frameSeq > snapshotSeq;
  },
};

import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import type { WarningSettingsPayload, DataLinkWarningSetting, AvailableSensorsResult, AvailableSensor, VehicleConfig } from '../HybridBridgeTypes';
import { DERIVED_ENTITIES, defaultDerivedConfig } from '../derived/types';

function loadFixture(): WarningSettingsPayload {
  const fixturePath = fileURLToPath(new URL('./warningBridgeContract.fixture.json', import.meta.url));
  return JSON.parse(readFileSync(fixturePath, 'utf8')) as WarningSettingsPayload;
}

interface VehicleSensorsFixture {
  sensors: AvailableSensor[];
  selectedCount: number;
  totalCount: number;
  gridRows: number;
  gridColumns: number;
  backgroundImagePath: string | null;
  vehicleConfig?: VehicleConfig;
}

// The runtime spread `{ ...defaultDerivedConfig(), ...payload }` (U1) — the
// fixture's null-vs-absent semantics test uses the real defaults.
const DEFAULT_SPREAD = defaultDerivedConfig();

function loadVehicleFixture(): VehicleSensorsFixture {
  const fixturePath = fileURLToPath(new URL('./vehicleBridgeContract.fixture.json', import.meta.url));
  return JSON.parse(readFileSync(fixturePath, 'utf8')) as VehicleSensorsFixture;
}

describe('warning bridge contract (golden fixture)', () => {
  const payload = loadFixture();

  it('parses as a WarningSettingsPayload with the global delay', () => {
    expect(payload).toBeDefined();
    expect(Array.isArray(payload.settings)).toBe(true);
    expect(payload.delayMs).toBe(500);
  });

  it('has five representative records with levels and points arrays', () => {
    expect(payload.settings).toHaveLength(5);
    for (const record of payload.settings) {
      expect(Array.isArray(record.levels)).toBe(true);
      expect(Array.isArray(record.points)).toBe(true);
    }
  });

  it('never emits the legacy minWarning/maxWarning keys post-migration', () => {
    for (const record of payload.settings) {
      const json = JSON.stringify(record);
      expect(json).not.toContain('minWarning');
      expect(json).not.toContain('maxWarning');
    }
  });

  it('pins null-vs-absent: migratedBoundsMarkerLevelId key present with null (record 4, Alarm-only)', () => {
    const alarmOnly = payload.settings.find(r => r.dataId === 9)!;
    expect(alarmOnly).toBeDefined();
    const json = JSON.stringify(alarmOnly);
    expect(json).toContain('"migratedBoundsMarkerLevelId":null');
    expect(alarmOnly.migratedBoundsMarkerLevelId).toBeNull();
    expect(alarmOnly.migratedBoundsMarkerSet).toBe(true);
  });

  it('record 1 (migrated): level id equals the marker reference', () => {
    const migrated = payload.settings.find(r => r.dataId === 1)!;
    expect(migrated.levels).toHaveLength(1);
    expect(migrated.migratedBoundsMarkerSet).toBe(true);
    expect(migrated.migratedBoundsMarkerLevelId).toBe(migrated.levels[0]?.id);
    expect(migrated.points.map(p => [p.direction, p.value])).toEqual([
      ['min', 0.8],
      ['max', 1.5],
    ]);
  });

  it('record 3 (R19 Warning+Alarm): deterministic level ids read-7-warning / read-7-alarm', () => {
    const r19 = payload.settings.find(r => r.dataId === 7)!;
    expect(r19.levels.map(l => l.id)).toEqual(['read-7-warning', 'read-7-alarm']);
    expect(r19.levels.find(l => l.id === 'read-7-warning')?.autolog).toBe(false);
    expect(r19.levels.find(l => l.id === 'read-7-warning')?.flash).toBe(false);
    expect(r19.levels.find(l => l.id === 'read-7-alarm')?.autolog).toBe(true);
    expect(r19.levels.find(l => l.id === 'read-7-alarm')?.flash).toBe(true);
    expect(r19.points.map(p => [p.value, p.direction, p.levelId])).toEqual([
      [105, 'max', 'read-7-warning'],
      [120, 'max', 'read-7-alarm'],
    ]);
    expect(r19.migratedBoundsMarkerSet).toBe(false);
  });

  it('record 2 (zero-bound migrated + R7): marker set with deterministic level id', () => {
    const zeroBound = payload.settings.find(r => r.dataId === 42)!;
    expect(zeroBound.levels.map(l => l.id)).toEqual(['read-42-warning']);
    expect(zeroBound.levels[0]?.autolog).toBe(true);
    expect(zeroBound.migratedBoundsMarkerSet).toBe(true);
    expect(zeroBound.migratedBoundsMarkerLevelId).toBe('read-42-warning');
  });

  it('record 5 (Custom edited): user level/point ids round-trip untouched', () => {
    const custom = payload.settings.find(r => r.dataId === 5)!;
    expect(custom.status).toBe('Custom');
    expect(custom.levels.map(l => l.id)).toEqual(['u1', 'u2']);
    expect(custom.points.map(p => p.id)).toEqual(['p1', 'p2']);
  });

  it('all records match the DataLinkWarningSetting shape (compile-time via cast helper)', () => {
    const asSetting = (r: DataLinkWarningSetting): DataLinkWarningSetting => r;
    for (const record of payload.settings) {
      asSetting(record);
      expect(typeof record.dataId).toBe('number');
      expect(typeof record.enabled).toBe('boolean');
      expect(typeof record.status).toBe('string');
    }
  });
});

describe('vehicle bridge contract (golden fixture — derived entities)', () => {
  const fixture = loadVehicleFixture();

  it('parses as an AvailableSensorsResult-shaped payload', () => {
    const asResult = (r: AvailableSensorsResult): AvailableSensorsResult => r;
    asResult(fixture);
    expect(Array.isArray(fixture.sensors)).toBe(true);
    expect(fixture.selectedCount).toBe(1);
  });

  it('exposes both new entities in the Derived category with picker metadata (R5, AE14)', () => {
    const countdown = fixture.sensors.find(s => s.id === -3005)!;
    const shiftState = fixture.sensors.find(s => s.id === -3006)!;

    expect(countdown).toBeDefined();
    expect(countdown.name).toBe('RPM to Shift');
    expect(countdown.unit).toBe('rpm');
    expect(countdown.category).toBe('Derived');
    expect(countdown.minValue).toBe(0);
    expect(countdown.maxValue).toBe(9000);

    expect(shiftState).toBeDefined();
    expect(shiftState.name).toBe('Shift State');
    expect(shiftState.unit).toBe('');
    expect(shiftState.category).toBe('Derived');
    expect(shiftState.minValue).toBe(-1);
    expect(shiftState.maxValue).toBe(1);
  });

  it('lists all six derived entities (drift guard: TS metadata equals fixture literals)', () => {
    const derivedInFixture = fixture.sensors.filter(s => s.category === 'Derived');
    const fixtureEntries = new Map(derivedInFixture.map(s => [s.id, s]));

    for (const [idStr, info] of Object.entries(DERIVED_ENTITIES)) {
      const id = Number(idStr);
      const entry = fixtureEntries.get(id);
      expect(entry, `fixture must carry derived entity ${id}`).toBeDefined();
      expect(entry!.name).toBe(info.name);
      expect(entry!.unit).toBe(info.unit);
      expect(entry!.minValue).toBe(info.minValue);
      expect(entry!.maxValue).toBe(info.maxValue);
    }
    expect(derivedInFixture.length).toBe(Object.keys(DERIVED_ENTITIES).length);
  });

  it('keeps the countdown picker range independent of any configured shift point (R5)', () => {
    const countdown = fixture.sensors.find(s => s.id === -3005)!;
    expect(countdown.maxValue).toBe(9000);
  });

  it('golden getVehicleConfig payload: exact camelCase keys with the shifter block (U7)', () => {
    const vc = fixture.vehicleConfig;
    expect(vc).toBeDefined();
    expect(Object.keys(vc).sort()).toEqual([
      'baroEntityId', 'enabled', 'finalDriveRatio', 'gearEntityId', 'gearRatios',
      'mapEntityId', 'rpmEntityId', 'shifter', 'tireDiameterInches', 'vssSpeedEntityId',
      'wheelSlipPercent',
    ]);
    expect(vc.shifter).toEqual({ shiftPointRpm: 7000, downshiftFloorRpm: 5000 });
  });

  it('null-vs-absent semantics: the shifter block is always present so the default spread keeps explicit zeros', () => {
    const json = JSON.stringify(fixture.vehicleConfig);
    expect(json).toContain('"shifter"');
    // A payload WITHOUT the block (legacy) must survive the
    // { ...defaultDerivedConfig(), ...payload } spread with explicit zero
    // defaults — the fixture pins the canonical block shape for that spread.
    const { shifter: _s, ...vehicleOnly } = fixture.vehicleConfig;
    void _s;
    const merged = { ...DEFAULT_SPREAD, ...vehicleOnly };
    expect(merged.shifter).toEqual({ shiftPointRpm: 0, downshiftFloorRpm: 0 });
    const withBlock = { ...DEFAULT_SPREAD, ...fixture.vehicleConfig };
    expect(withBlock.shifter).toEqual({ shiftPointRpm: 7000, downshiftFloorRpm: 5000 });
  });
});

import { describe, it, expect } from 'vitest';
import { autoDetectMapping } from '../autoDetect';

const SENSORS = [
  { id: 1, name: 'Engine RPM' },
  { id: 2, name: 'Vehicle Speed' },
  { id: 3, name: 'Manifold Pressure' },
  { id: 4, name: 'Barometric Pressure' },
  { id: 5, name: 'Gear' },
];

describe('autoDetectMapping', () => {
  it('maps every well-named sensor', () => {
    expect(autoDetectMapping(SENSORS)).toEqual({
      rpmEntityId: 1,
      vssSpeedEntityId: 2,
      mapEntityId: 3,
      baroEntityId: 4,
      gearEntityId: 5,
    });
  });

  it('matches the bare rpm/speed/map/baro/gear names', () => {
    const sensors = [
      { id: 10, name: 'rpm' },
      { id: 20, name: 'speed' },
      { id: 30, name: 'map' },
      { id: 40, name: 'baro' },
      { id: 50, name: 'gear' },
    ];
    expect(autoDetectMapping(sensors)).toEqual({
      rpmEntityId: 10,
      vssSpeedEntityId: 20,
      mapEntityId: 30,
      baroEntityId: 40,
      gearEntityId: 50,
    });
  });

  it('is case-insensitive and trims whitespace', () => {
    const sensors = [{ id: 7, name: '  ENGINE SPEED  ' }];
    expect(autoDetectMapping(sensors).rpmEntityId).toBe(7);
  });

  it('matches alternates like boost pressure and road speed', () => {
    const sensors = [
      { id: 8, name: 'Boost Pressure' },
      { id: 9, name: 'Road Speed' },
      { id: 10, name: 'Selected Gear' },
      { id: 11, name: 'Atmospheric Pressure' },
    ];
    const result = autoDetectMapping(sensors);
    expect(result.mapEntityId).toBe(8);
    expect(result.vssSpeedEntityId).toBe(9);
    expect(result.gearEntityId).toBe(10);
    expect(result.baroEntityId).toBe(11);
  });

  it('matches the first sensor for the earliest pattern', () => {
    const sensors = [
      { id: 1, name: 'rpm' },
      { id: 2, name: 'Engine Speed' },
    ];
    expect(autoDetectMapping(sensors).rpmEntityId).toBe(1);
  });

  it('returns null for unmapped signals', () => {
    const sensors = [
      { id: 1, name: 'Coolant Temp' },
      { id: 2, name: 'AFR' },
    ];
    expect(autoDetectMapping(sensors)).toEqual({
      rpmEntityId: null,
      vssSpeedEntityId: null,
      mapEntityId: null,
      baroEntityId: null,
      gearEntityId: null,
    });
  });

  it('returns all-null for an empty sensor list', () => {
    expect(autoDetectMapping([])).toEqual({
      rpmEntityId: null,
      vssSpeedEntityId: null,
      mapEntityId: null,
      baroEntityId: null,
      gearEntityId: null,
    });
  });
});

import { describe, it, expect } from 'vitest';
import { buildExportBundle, generateYamlString, parseImportBundle } from '../tableExport';
import type { TableDefinition, TableData } from '../tables/types';

function makeDef(overrides: Partial<TableDefinition> = {}): TableDefinition {
  return {
    id: 1,
    name: 'VE Table',
    category: 'Fuel',
    tableType: 'T16x16',
    rows: 2,
    cols: 2,
    input0Name: 'RPM',
    input1Name: 'MAP',
    outputName: 'VE',
    input0LinkId: 10,
    input1LinkId: 11,
    outputLinkId: 12,
    ...overrides,
  };
}

function makeData(overrides: Partial<TableData> = {}): TableData {
  return {
    input0: [1000, 2000],
    input1: [20, 40],
    output: [1.2345, 2.5, 3.75, 4.99],
    ...overrides,
  };
}

describe('buildExportBundle', () => {
  it('builds a 2D table with x and y axes', () => {
    const bundle = buildExportBundle([{ def: makeDef(), data: makeData() }], {
      10: { name: 'RPM', unit: 'rpm', category: 'Engine' },
      11: { name: 'MAP', unit: 'kPa', category: 'Intake' },
      12: { name: 'VE', unit: '%', category: 'Fuel' },
    });

    expect(bundle.tables).toHaveLength(1);
    const t = bundle.tables[0];
    expect(t.name).toBe('VE Table');
    expect(t.axes.x.name).toBe('RPM');
    expect(t.axes.x.unit).toBe('rpm');
    expect(t.axes.y?.name).toBe('MAP');
    expect(t.axes.y?.unit).toBe('kPa');
    expect(t.output.name).toBe('VE');
    expect(t.output.unit).toBe('%');
  });

  it('rounds output values to 1 decimal place', () => {
    const bundle = buildExportBundle([{ def: makeDef(), data: makeData() }]);
    expect(bundle.tables[0]?.output.values[0]).toEqual([1.2, 2.5]);
    expect(bundle.tables[0]?.output.values[1]).toEqual([3.8, 5]);
  });

  it('omits the y axis for 1D tables', () => {
    const def = makeDef({ rows: 1, cols: 4, tableType: 'T1x16' });
    const data = makeData({ input1: [], output: [1, 2, 3, 4] });
    const bundle = buildExportBundle([{ def, data }]);
    expect(bundle.tables[0]?.axes.y).toBeUndefined();
    expect(bundle.tables[0]?.output.values).toEqual([[1, 2, 3, 4]]);
  });

  it('falls back to empty unit strings when links are missing', () => {
    const bundle = buildExportBundle([{ def: makeDef(), data: makeData() }]);
    expect(bundle.tables[0]?.axes.x.unit).toBe('');
    expect(bundle.tables[0]?.output.unit).toBe('');
  });

  it('uses default ecu info when none provided', () => {
    const bundle = buildExportBundle([{ def: makeDef(), data: makeData() }]);
    expect(bundle.ecu.product).toBe('ME221');
    expect(bundle.ecu.model).toBe('V2B-PNP');
  });
});

describe('generateYamlString', () => {
  it('emits ecu block, links and tables', () => {
    const bundle = buildExportBundle([{ def: makeDef(), data: makeData() }], {
      10: { name: 'RPM', unit: 'rpm', category: 'Engine' },
    });
    const yaml = generateYamlString(bundle);

    expect(yaml).toContain('# ME221 Calibration Export');
    expect(yaml).toContain('product: ME221');
    expect(yaml).toContain('10:');
    expect(yaml).toContain('name: RPM');
    expect(yaml).toContain('name: VE Table');
    expect(yaml).toContain('values: [1000, 2000]');
    expect(yaml).toContain('- [1.2, 2.5]');
  });

  it('omits the links block when there are none', () => {
    const bundle = buildExportBundle([{ def: makeDef(), data: makeData() }]);
    const yaml = generateYamlString(bundle);
    expect(yaml).not.toContain('links:');
  });
});

describe('parseImportBundle', () => {
  it('round-trips an exported bundle', () => {
    const bundle = buildExportBundle([{ def: makeDef(), data: makeData() }], {
      10: { name: 'RPM', unit: 'rpm', category: 'Engine' },
    });
    const parsed = parseImportBundle(generateYamlString(bundle));

    expect(parsed).not.toBeNull();
    expect(parsed?.ecu.product).toBe('ME221');
    expect(parsed?.links[10]?.name).toBe('RPM');
    expect(parsed?.tables[0]?.name).toBe('VE Table');
    expect(parsed?.tables[0]?.axes.x.values).toEqual([1000, 2000]);
    expect(parsed?.tables[0]?.axes.y?.values).toEqual([20, 40]);
    expect(parsed?.tables[0]?.output.values).toEqual([[1.2, 2.5], [3.8, 5]]);
  });

  it('round-trips 1D tables without a y axis', () => {
    const def = makeDef({ rows: 1, cols: 4, tableType: 'T1x16' });
    const data = makeData({ input1: [], output: [1, 2, 3, 4] });
    const bundle = buildExportBundle([{ def, data }]);
    const parsed = parseImportBundle(generateYamlString(bundle));

    expect(parsed?.tables[0]?.axes.y).toBeUndefined();
  });

  it('returns null for invalid YAML', () => {
    expect(parseImportBundle('not: [valid: yaml')).toBeNull();
  });

  it('returns null when the tables array is missing', () => {
    expect(parseImportBundle('ecu:\n  product: ME221\n')).toBeNull();
  });

  it('returns null for non-object payloads', () => {
    expect(parseImportBundle('42')).toBeNull();
    expect(parseImportBundle('')).toBeNull();
  });

  it('defaults missing ecu fields', () => {
    const parsed = parseImportBundle('tables:\n  - name: T\n    axes:\n      x:\n        name: RPM\n        values: []\n    output:\n      name: VE\n      values: []');
    expect(parsed?.ecu.product).toBe('ME221');
    expect(parsed?.tables[0]?.category).toBe('');
  });
});

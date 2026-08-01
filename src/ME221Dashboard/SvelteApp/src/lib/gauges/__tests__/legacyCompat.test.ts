import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { toGaugeDefinition } from '../gaugeUtils';
import { GaugeShapeCategory } from '../gaugeTypes';
import type { GaugeConfigEntry } from '../../HybridBridgeTypes';

const __dirname = dirname(fileURLToPath(import.meta.url));
const fixture = JSON.parse(
  readFileSync(join(__dirname, 'fixtures', 'legacyDashboard.json'), 'utf-8')
) as { gauges: GaugeConfigEntry[] };

const overrides = {
  name: 'Test',
  unit: 'u',
  value: 50,
  formattedValue: '50',
  minValue: 0,
  maxValue: 100,
};

// Every customization v2 field must hold its current-behavior default when a
// pre-feature config is mapped (AE1). Values below reproduce today's rendering.
const DEFAULT_ASSERTIONS: Array<[keyof ReturnType<typeof toGaugeDefinition>, unknown]> = [
  ['tickCount', 3],
  ['tickLabels', false],
  ['tickLabelEvery', 1],
  ['tickSide', 0],
  ['redlineStart', 0],
  ['redlineWidth', 2],
  ['redlineColor', '#E03131'],
  ['needleShape', 0],
  ['barOrientation', 0],
  ['barThickness', 0],
  ['barTicks', false],
  ['barMinMaxLabels', false],
  ['barRedlineStart', 0],
  ['barRedlineColor', '#E03131'],
  ['colorStopColoring', false],
  ['panelStyle', 0],
  ['flashThreshold', 0],
  ['ledColor', '#ff3333'],
  ['digitBgColor', '#1a1a1a'],
  ['glowStrength', 0],
  ['digitDecimals', -1],
  ['zeroPadding', false],
  ['minDigitCount', 0],
  ['rollAnimation', false],
  ['rollSpeedMs', 300],
  ['segmentCount', 36],
  ['segmentGap', 0],
  ['ringStartAngle', 0],
  ['amberThreshold', 0.7],
  ['redThreshold', 0.85],
  ['ringCount', 5],
  ['ringWidth', 0],
  ['ringGap', 0],
  ['peakHoldEnabled', true],
  ['peakHoldAutoResetSec', 0],
  ['wedgeSegmentCount', 32],
  ['wedgeRedlineStart', 0.8],
  ['overlayPillPosition', 0],
  ['overlayFontScale', 1],
  ['chartLineStyle', 0],
  ['chartBackgroundColor', ''],
];

describe('legacy dashboard fixture (AE1)', () => {
  it('fixture contains one gauge per category', () => {
    const categories = fixture.gauges.map((g) => g.shapeCategory).sort((a, b) => a - b);
    expect(categories).toEqual([0, 1, 2, 3, 4, 5, 6, 7]);
  });

  it('maps every category with current-behavior defaults', () => {
    for (const config of fixture.gauges) {
      const def = toGaugeDefinition(config, overrides);
      expect(def.entityId, `entityId for category ${config.shapeCategory}`).toBe(config.entityId);
      for (const [key, expected] of DEFAULT_ASSERTIONS) {
        expect(def[key], `${String(key)} for category ${config.shapeCategory}`).toEqual(expected);
      }
    }
  });

  it('MultiRing legacy config keeps the 270° dial (AE1)', () => {
    const multi = fixture.gauges.find((g) => g.shapeCategory === GaugeShapeCategory.MultiRing)!;
    expect(toGaugeDefinition(multi, overrides).ringSweepAngle).toBe(270);
  });

  it('LedRing legacy config keeps the 360° ring (AE1)', () => {
    const led = fixture.gauges.find((g) => g.shapeCategory === GaugeShapeCategory.LedRing)!;
    expect(toGaugeDefinition(led, overrides).ringSweepAngle).toBe(360);
  });

  it('legacy linked entities survive mapping (MultiRing)', () => {
    const multi = fixture.gauges.find((g) => g.shapeCategory === GaugeShapeCategory.MultiRing)!;
    const def = toGaugeDefinition(multi, overrides);
    expect(def.linkedEntities).toHaveLength(2);
    expect(def.chartOverlays).toEqual([]);
  });
});

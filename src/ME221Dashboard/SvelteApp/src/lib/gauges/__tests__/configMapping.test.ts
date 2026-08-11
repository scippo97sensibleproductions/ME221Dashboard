import { describe, it, expect } from 'vitest';
import { toGaugeDefinition, toSavePayload, estimateVisualSize } from '../gaugeUtils';
import { GaugeShapeCategory } from '../gaugeTypes';
import type { GaugeConfigEntry } from '../../HybridBridgeTypes';

const overrides = {
  name: 'RPM',
  unit: 'rpm',
  value: 3000,
  formattedValue: '3000',
  minValue: 0,
  maxValue: 10000,
};

// Base config matching a legacy dashboard entry (no customization v2 fields).
function baseConfig(): GaugeConfigEntry {
  return {
    entityId: 42,
    shapeCategory: 0,
    sweepAngle: 220,
    arcPosition: 0,
    digitalStyle: 0,
    wedgeStyle: 0,
    texturePath: null,
    needleStartAngle: 135,
    needleEndAngle: 405,
    needleOffsetX: 0,
    needleOffsetY: 0,
    needleWidth: 2.5,
    needleLength: 1,
    scale: 1,
    fontSizeScale: 1,
    labelVerticalOffset: 0,
    showName: true,
    showUnit: true,
    showValue: true,
    iconName: null,
    iconOffsetX: 0,
    iconOffsetY: 0,
    iconSize: 0.5,
    barValuePosition: 4,
    barUnitPosition: 7,
    barNamePosition: 8,
    colorStops: [],
    colorHysteresis: 0.03,
    smoothingEnabled: false,
    smoothingFactor: 0.3,
    smoothingResponseMs: 0,
    spikeGatePercent: 0,
    fractionX: 0.1,
    fractionY: 0.1,
    widthFraction: 0.22,
    heightFraction: 0.28,
    chartTimeWindowSec: 30,
    chartYMin: null,
    chartYMax: null,
    chartLineColor: '#22c8e6',
    chartLineWidth: 2,
    chartShowGrid: true,
    chartFillUnder: false,
    chartShowLabels: true,
    chartPrecision: 1,
    textColor: '#ffffff',
    zIndex: 0,
  };
}

describe('toGaugeDefinition customization v2 defaults (AE1)', () => {
  it('maps a legacy config to current-behavior defaults', () => {
    const def = toGaugeDefinition(baseConfig(), overrides);
    expect(def.tickCount).toBe(3);
    expect(def.tickLabels).toBe(false);
    expect(def.tickLabelEvery).toBe(1);
    expect(def.tickSide).toBe(0);
    expect(def.redlineStart).toBe(0);
    expect(def.redlineWidth).toBe(2);
    expect(def.redlineColor).toBe('#E03131');
    expect(def.needleShape).toBe(0);
    expect(def.barOrientation).toBe(0);
    expect(def.barThickness).toBe(0);
    expect(def.barTicks).toBe(false);
    expect(def.barMinMaxLabels).toBe(false);
    expect(def.barRedlineStart).toBe(0);
    expect(def.colorStopColoring).toBe(false);
    expect(def.panelStyle).toBe(0);
    expect(def.flashThreshold).toBe(0);
    expect(def.ledColor).toBe('#ff3333');
    expect(def.digitBgColor).toBe('#1a1a1a');
    expect(def.glowStrength).toBe(0);
    expect(def.digitDecimals).toBe(-1);
    expect(def.zeroPadding).toBe(false);
    expect(def.minDigitCount).toBe(0);
    expect(def.rollAnimation).toBe(false);
    expect(def.rollSpeedMs).toBe(300);
    expect(def.segmentCount).toBe(36);
    expect(def.segmentGap).toBe(0);
    expect(def.ringStartAngle).toBe(0);
    expect(def.ringSweepAngle).toBe(360);
    expect(def.amberThreshold).toBe(0.7);
    expect(def.redThreshold).toBe(0.85);
    expect(def.ringCount).toBe(5);
    expect(def.ringWidth).toBe(0);
    expect(def.ringGap).toBe(0);
    expect(def.peakHoldEnabled).toBe(true);
    expect(def.peakHoldAutoResetSec).toBe(0);
    expect(def.wedgeSegmentCount).toBe(32);
    expect(def.wedgeRedlineStart).toBe(0.8);
    expect(def.chartOverlays).toEqual([]);
    expect(def.overlayPillPosition).toBe(0);
    expect(def.overlayFontScale).toBe(1);
    expect(def.chartLineStyle).toBe(0);
    expect(def.chartBackgroundColor).toBe('');
  });

  it('degrades invalid enums to defaults (KTD-2)', () => {
    const cfg = baseConfig();
    cfg.needleShape = 9;
    cfg.panelStyle = 7;
    cfg.chartLineStyle = 3;
    cfg.barOrientation = 5;
    cfg.overlayPillPosition = -2;
    const def = toGaugeDefinition(cfg, overrides);
    expect(def.needleShape).toBe(0);
    expect(def.panelStyle).toBe(0);
    expect(def.chartLineStyle).toBe(0);
    expect(def.barOrientation).toBe(0);
    expect(def.overlayPillPosition).toBe(0);
  });

  it('clamps out-of-range values (KTD-2)', () => {
    const cfg = baseConfig();
    cfg.tickCount = 99;
    cfg.redlineStart = 1.5;
    cfg.amberThreshold = 1.2;
    cfg.redThreshold = -0.5;
    cfg.wedgeRedlineStart = 2;
    cfg.minDigitCount = 99;
    const def = toGaugeDefinition(cfg, overrides);
    expect(def.tickCount).toBe(20);
    expect(def.redlineStart).toBe(1);
    expect(def.wedgeRedlineStart).toBe(1);
    expect(def.minDigitCount).toBe(12);
  });

  it('enforces amber <= red (AE2)', () => {
    const cfg = baseConfig();
    cfg.amberThreshold = 0.9;
    cfg.redThreshold = 0.7;
    const def = toGaugeDefinition(cfg, overrides);
    expect(def.amberThreshold).toBeLessThanOrEqual(def.redThreshold);
    expect(def.redThreshold).toBe(0.7);
    expect(def.amberThreshold).toBe(0.7);
  });

  it('truncates chart overlays to 5', () => {
    const cfg = baseConfig();
    cfg.chartOverlays = [1, 2, 3, 4, 5, 6].map((i) => ({
      entityId: i,
      color: '#ffffff',
      lineWidth: 1,
      lineStyle: 0,
    }));
    const def = toGaugeDefinition(cfg, overrides);
    expect(def.chartOverlays).toHaveLength(5);
  });

  it('defaults ringSweepAngle per category (LedRing 360, MultiRing 270) (AE1)', () => {
    const ledRing = baseConfig();
    ledRing.shapeCategory = 6;
    expect(toGaugeDefinition(ledRing, overrides).ringSweepAngle).toBe(360);

    const multiRing = baseConfig();
    multiRing.shapeCategory = 7;
    expect(toGaugeDefinition(multiRing, overrides).ringSweepAngle).toBe(270);
  });
});

describe('shift-light category (U4/KTD5)', () => {
  function shiftLightConfig(over: Partial<GaugeConfigEntry> = {}): GaugeConfigEntry {
    return { ...baseConfig(), shapeCategory: GaugeShapeCategory.ShiftLight, entityId: -3005, ...over };
  }

  it('toGaugeDefinition defaults rampWidthRpm to 1500 and derives the linked shift-state entity', () => {
    const def = toGaugeDefinition(shiftLightConfig(), overrides);
    expect(def.rampWidthRpm).toBe(1500);
    expect(def.linkedEntities).toEqual([{ entityId: -3006, color: '#E81123' }]);
  });

  it('flips the derived pair when anchored on the shift-state entity (−3006 → −3005)', () => {
    const def = toGaugeDefinition(shiftLightConfig({ entityId: -3006 }), overrides);
    expect(def.linkedEntities).toEqual([{ entityId: -3005, color: '#E81123' }]);
  });

  it('toSavePayload writes rampWidthRpm unconditionally for shift-light (clamp resets survive the whitelist)', () => {
    const payload = toSavePayload(shiftLightConfig());
    const json = JSON.stringify(payload);
    expect(json).toContain('"rampWidthRpm":1500');
    const clamped = toSavePayload(shiftLightConfig({ rampWidthRpm: 1000 }));
    expect(JSON.stringify(clamped)).toContain('"rampWidthRpm":1000');
  });

  it('toSavePayload omits rampWidthRpm for other categories at the default (legacy payloads stay byte-identical)', () => {
    const payload = toSavePayload({ ...baseConfig(), shapeCategory: GaugeShapeCategory.Bar });
    expect(JSON.stringify(payload)).not.toContain('rampWidthRpm');
    const nonDefault = toSavePayload({ ...baseConfig(), shapeCategory: GaugeShapeCategory.Bar, rampWidthRpm: 2000 });
    expect(JSON.stringify(nonDefault)).toContain('"rampWidthRpm":2000');
  });

  it('estimateVisualSize uses the full design box, not the Text fallback', () => {
    const size = estimateVisualSize(GaugeShapeCategory.ShiftLight, 350, 80, {});
    expect(size).toEqual({ w: 350, h: 80 });
  });
});

describe('toSavePayload round-trip', () => {
  it('preserves every customization v2 field set to a value', () => {
    const cfg: GaugeConfigEntry = {
      ...baseConfig(),
      tickCount: 12,
      tickLabels: true,
      tickLabelEvery: 2,
      tickSide: 1,
      redlineStart: 0.8,
      redlineWidth: 4,
      redlineColor: '#ff0000',
      needleShape: 2,
      barOrientation: 1,
      barThickness: 6,
      barTicks: true,
      barMinMaxLabels: true,
      barRedlineStart: 0.9,
      barRedlineColor: '#00ff00',
      colorStopColoring: true,
      panelStyle: 2,
      flashThreshold: 0.05,
      ledColor: '#00ff00',
      digitBgColor: '#222222',
      glowStrength: 0.5,
      digitDecimals: 2,
      zeroPadding: true,
      minDigitCount: 5,
      rollAnimation: true,
      rollSpeedMs: 500,
      segmentCount: 48,
      segmentGap: 0.2,
      ringStartAngle: 135,
      ringSweepAngle: 270,
      amberThreshold: 0.6,
      redThreshold: 0.9,
      ringCount: 3,
      ringWidth: 12,
      ringGap: 2,
      peakHoldEnabled: false,
      peakHoldAutoResetSec: 5,
      wedgeSegmentCount: 24,
      wedgeRedlineStart: 0.7,
      chartOverlays: [
        { entityId: -1001, color: '#ff0000', lineWidth: 2, lineStyle: 1 },
        { entityId: 55, color: '#00ff00', lineWidth: 1, lineStyle: 2 },
      ],
      overlayPillPosition: 2,
      overlayFontScale: 1.5,
      chartLineStyle: 1,
      chartBackgroundColor: '#101010',
    };

    const payload = toSavePayload(cfg);
    const json = JSON.stringify(payload);
    const parsed = JSON.parse(json) as GaugeConfigEntry;

    // Every v2 field survives the JSON round-trip with its value intact.
    expect(parsed.tickCount).toBe(12);
    expect(parsed.tickLabels).toBe(true);
    expect(parsed.tickLabelEvery).toBe(2);
    expect(parsed.tickSide).toBe(1);
    expect(parsed.redlineStart).toBe(0.8);
    expect(parsed.redlineWidth).toBe(4);
    expect(parsed.redlineColor).toBe('#ff0000');
    expect(parsed.needleShape).toBe(2);
    expect(parsed.barOrientation).toBe(1);
    expect(parsed.barThickness).toBe(6);
    expect(parsed.barTicks).toBe(true);
    expect(parsed.barMinMaxLabels).toBe(true);
    expect(parsed.barRedlineStart).toBe(0.9);
    expect(parsed.barRedlineColor).toBe('#00ff00');
    expect(parsed.colorStopColoring).toBe(true);
    expect(parsed.panelStyle).toBe(2);
    expect(parsed.flashThreshold).toBe(0.05);
    expect(parsed.ledColor).toBe('#00ff00');
    expect(parsed.digitBgColor).toBe('#222222');
    expect(parsed.glowStrength).toBe(0.5);
    expect(parsed.digitDecimals).toBe(2);
    expect(parsed.zeroPadding).toBe(true);
    expect(parsed.minDigitCount).toBe(5);
    expect(parsed.rollAnimation).toBe(true);
    expect(parsed.rollSpeedMs).toBe(500);
    expect(parsed.segmentCount).toBe(48);
    expect(parsed.segmentGap).toBe(0.2);
    expect(parsed.ringStartAngle).toBe(135);
    expect(parsed.ringSweepAngle).toBe(270);
    expect(parsed.amberThreshold).toBe(0.6);
    expect(parsed.redThreshold).toBe(0.9);
    expect(parsed.ringCount).toBe(3);
    expect(parsed.ringWidth).toBe(12);
    expect(parsed.ringGap).toBe(2);
    expect(parsed.peakHoldEnabled).toBe(false);
    expect(parsed.peakHoldAutoResetSec).toBe(5);
    expect(parsed.wedgeSegmentCount).toBe(24);
    expect(parsed.wedgeRedlineStart).toBe(0.7);
    expect(parsed.chartOverlays).toEqual([
      { entityId: -1001, color: '#ff0000', lineWidth: 2, lineStyle: 1 },
      { entityId: 55, color: '#00ff00', lineWidth: 1, lineStyle: 2 },
    ]);
    expect(parsed.overlayPillPosition).toBe(2);
    expect(parsed.overlayFontScale).toBe(1.5);
    expect(parsed.chartLineStyle).toBe(1);
    expect(parsed.chartBackgroundColor).toBe('#101010');
  });

  it('explicit reset-to-default values round-trip (absent keys would be dropped by the C# whitelist)', () => {
    const cfg: GaugeConfigEntry = {
      ...baseConfig(),
      redlineStart: 0,
      rollAnimation: false,
      segmentCount: 36,
      chartOverlays: [],
    };
    const payload = toSavePayload(cfg);
    const json = JSON.stringify(payload);
    expect(json).toContain('"redlineStart":0');
    expect(json).toContain('"rollAnimation":false');
    expect(json).toContain('"segmentCount":36');
    expect(json).toContain('"chartOverlays":[]');
  });

  it('omits unset optional fields from the payload (legacy payloads stay lean)', () => {
    const payload = toSavePayload(baseConfig());
    const json = JSON.stringify(payload);
    expect(json).not.toContain('tickCount');
    expect(json).not.toContain('chartOverlays');
    expect(json).not.toContain('ringCount');
  });

  it('preview panel full-config passthrough keeps textures, icons, linked entities and v2 fields (gauge preview regression)', () => {
    // The preview panel builds its gauge from the FULL config entry (spread),
    // not a hand-picked subset. This test pins the fields that must survive:
    // missing texturePath/linkedEntities previously made previews wrong.
    const cfg: GaugeConfigEntry = {
      ...baseConfig(),
      shapeCategory: GaugeShapeCategory.WedgeBar,
      texturePath: 'C:\\gauge-textures\\face.png',
      iconName: 'C:\\gauge-textures\\icon.png',
      linkedEntities: [
        { entityId: 55, color: '#00ff00' },
        { entityId: 56, color: '#ff0000' },
      ],
      needleShape: 2,
      tickCount: 7,
      redlineStart: 0.5,
      wedgeSegmentCount: 40,
      panelStyle: 3,
    };
    const def = toGaugeDefinition({ ...cfg, fractionX: 0, fractionY: 0 }, overrides);
    expect(def.texturePath).toBe('C:\\gauge-textures\\face.png');
    expect(def.iconName).toBe('C:\\gauge-textures\\icon.png');
    expect(def.linkedEntities).toEqual([
      { entityId: 55, color: '#00ff00' },
      { entityId: 56, color: '#ff0000' },
    ]);
    expect(def.needleShape).toBe(2);
    expect(def.tickCount).toBe(7);
    expect(def.redlineStart).toBe(0.5);
    expect(def.wedgeSegmentCount).toBe(40);
    expect(def.panelStyle).toBe(3);
    expect(def.category).toBe(GaugeShapeCategory.WedgeBar);
  });
});

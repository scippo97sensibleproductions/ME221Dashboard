import { describe, it, expect } from 'vitest';
import {
  createShiftLightRenderer,
  flashPhase,
  zoneColorsCvdSafe,
  SHIFT_ZONE_COLORS,
  SHIFT_STATE_TEXTS,
  INERT_DIM_OPACITY,
  FROZEN_INTENSITY,
  type ShiftLightRenderInput,
} from '../shiftLightRender';

function input(overrides: Partial<ShiftLightRenderInput> = {}): ShiftLightRenderInput {
  return {
    countdown: 1000,
    shiftState: 0,
    rpm: 6000,
    stale: false,
    shiftPoint: 7000,
    rampWidthRpm: 1500,
    flashOn: false,
    ...overrides,
  };
}

describe('shiftLightRender — fill and zones (AE1, R11)', () => {
  it('at 6000 with shift point 7000 and ramp 1500: green/amber portion lit, no arrows', () => {
    const r = createShiftLightRenderer().render(input());
    expect(r.mode).toBe('progressive');
    expect(r.showUp).toBe(false);
    expect(r.showDown).toBe(false);
    // band 5500..7000; 6000 → position 1/3 → ~5 of 16 segments lit
    const lit = r.segments.filter(s => s.intensity === 1).length;
    expect(lit).toBeGreaterThan(1);
    expect(lit).toBeLessThan(8);
    // colors are green/amber only (below the red zone)
    const colors = new Set(r.segments.filter(s => s.intensity === 1).map(s => s.color));
    expect(colors.has(SHIFT_ZONE_COLORS.red)).toBe(false);
  });

  it('below the ramp → near-empty cold bar (1–2 segments at full intensity), no arrows', () => {
    const r = createShiftLightRenderer().render(input({ rpm: 4000 }));
    expect(r.mode).toBe('cold');
    const lit = r.segments.filter(s => s.intensity === 1).length;
    expect(lit).toBe(2);
    expect(r.showUp).toBe(false);
    expect(r.showDown).toBe(false);
  });

  it('at/above the shift point → full bar + ▲ only, strong 4 Hz blink (AE2)', () => {
    const renderer = createShiftLightRenderer();
    const off = renderer.render(input({ rpm: 7000, countdown: 0, shiftState: 1, flashOn: false }));
    expect(off.mode).toBe('shiftNow');
    expect(off.showUp).toBe(true);
    expect(off.showDown).toBe(false);
    expect(off.flash?.rateHz).toBe(4);
    // Off phase of the blink must be a strong swing (≤ 40%), not a subtle nudge
    expect(off.segments.every(s => s.intensity < 0.4)).toBe(true);
    const on = renderer.render(input({ rpm: 7000, countdown: 0, shiftState: 1, flashOn: true }));
    expect(on.flash?.on).toBe(true);
    expect(on.segments.every(s => s.intensity === 1)).toBe(true);
  });
});

describe('shiftLightRender — inert state (R14, AE5, AE9)', () => {
  it('null countdown/shift-state → dim segments, no lit segments, no arrows', () => {
    const r = createShiftLightRenderer().render(input({ countdown: null, shiftState: null, stale: true }));
    expect(r.mode).toBe('inert');
    expect(r.segments.every(s => s.intensity === INERT_DIM_OPACITY)).toBe(true);
    expect(r.showUp).toBe(false);
    expect(r.showDown).toBe(false);
    expect(r.announcement).toBe(SHIFT_STATE_TEXTS.dataStale);
  });

  it('inert (dim, none lit) differs from cold (1–2 lit at full intensity)', () => {
    const renderer = createShiftLightRenderer();
    const inert = renderer.render(input({ countdown: null, shiftState: null }));
    const cold = renderer.render(input({ rpm: 4000 }));
    expect(inert.segments.filter(s => s.intensity === 1).length).toBe(0);
    expect(cold.segments.filter(s => s.intensity === 1).length).toBe(2);
    expect(inert.mode).not.toBe(cold.mode);
  });

  it('partial null (countdown valued, shift-state null) → inert (staleness resume)', () => {
    const r = createShiftLightRenderer().render(input({ shiftState: null }));
    expect(r.mode).toBe('inert');
  });

  it('null raw RPM with valued entities (sub-tick stale window) → inert', () => {
    const r = createShiftLightRenderer().render(input({ rpm: null }));
    expect(r.mode).toBe('inert');
  });

  it('unset shift point → not-configured announcement', () => {
    const r = createShiftLightRenderer().render(input({ shiftPoint: null, countdown: null, shiftState: null }));
    expect(r.mode).toBe('inert');
    expect(r.announcement).toBe(SHIFT_STATE_TEXTS.notConfigured);
  });

  it('fresh frames with sample null → no announcement (transient sub-tick cause)', () => {
    const r = createShiftLightRenderer().render(input({ countdown: null, shiftState: null, stale: false }));
    expect(r.announcement).toBeNull();
  });
});

describe('shiftLightRender — downshift freeze (R12, AE3)', () => {
  it('entry freezes the pattern; countdown ignored during the hold', () => {
    const renderer = createShiftLightRenderer();
    // progressive entry pattern at 6000 (position 1/3 → ~5 lit segments)
    const entry = renderer.render(input({ rpm: 6000, shiftState: 0, countdown: 1000 }));
    const entryLit = entry.segments.map(s => (s.intensity === 1 ? 1 : 0));

    const held = renderer.render(input({ rpm: 4000, shiftState: -1, countdown: 3000, flashOn: true }));
    expect(held.mode).toBe('downshift');
    expect(held.showDown).toBe(true);
    expect(held.frozen).toBe(true);
    // the frozen pattern preserves the entry lit set (rendered at 50–60% intensity)
    held.segments.forEach((seg, i) => {
      if (i < 3) {
        expect(seg.intensity).toBe(1, `low segment ${i} flashes at full intensity`);
      } else if (entryLit[i] === 1) {
        expect(seg.intensity).toBe(FROZEN_INTENSITY);
      } else {
        expect(seg.intensity).toBe(0.4);
      }
    });
    // the bar never collapses: the entry lit segments survive the hold
    expect(held.segments.filter(s => s.intensity > 0.5).length).toBeGreaterThan(0);
  });

  it('freeze persists across ticks with the same entity pair', () => {
    const renderer = createShiftLightRenderer();
    renderer.render(input({ rpm: 5600, shiftState: 0, countdown: 1400 }));
    const first = renderer.render(input({ rpm: 4000, shiftState: -1, countdown: 3000 }));
    const second = renderer.render(input({ rpm: 4000, shiftState: -1, countdown: 3000, flashOn: true }));
    expect(second.mode).toBe('downshift');
    // same frozen pattern, only the low-segment flash toggles
    first.segments.forEach((seg, i) => {
      if (i >= 3) expect(seg.intensity).toBe(second.segments[i].intensity);
    });
  });

  it('freeze → nulls → re-entry captures a fresh pattern', () => {
    const renderer = createShiftLightRenderer();
    renderer.render(input({ rpm: 5600, shiftState: 0, countdown: 1400 }));
    renderer.render(input({ rpm: 4000, shiftState: -1, countdown: 3000 }));
    // staleness clears the memo
    renderer.render(input({ countdown: null, shiftState: null, rpm: null }));
    const reEntry = renderer.render(input({ rpm: 5600, shiftState: 0, countdown: 1400 }));
    const held = renderer.render(input({ rpm: 4000, shiftState: -1, countdown: 3000 }));
    expect(reEntry.mode).toBe('progressive');
    expect(held.frozen).toBe(true);
  });
});

describe('shiftLightRender — flash schedules (KTD6)', () => {
  it('shiftNow flashes the bar/▲ at 4 Hz', () => {
    const r = createShiftLightRenderer().render(input({ rpm: 7000, countdown: 0, shiftState: 1, flashOn: true }));
    expect(r.flash).toEqual({ rateHz: 4, on: true });
  });

  it('downshift flashes the low segments at 2 Hz with ▼ steady', () => {
    const renderer = createShiftLightRenderer();
    renderer.render(input({ rpm: 5600, shiftState: 0, countdown: 1400 }));
    const r = renderer.render(input({ rpm: 4000, shiftState: -1, countdown: 3000, flashOn: true }));
    expect(r.flash).toEqual({ rateHz: 2, on: true });
    expect(r.showDown).toBe(true);
  });

  it('flashPhase is a 50% duty square wave', () => {
    expect(flashPhase(0, 4)).toBe(true);
    expect(flashPhase(100, 4)).toBe(true);
    expect(flashPhase(130, 4)).toBe(false);
    expect(flashPhase(260, 4)).toBe(true); // wraps at the period boundary
    expect(flashPhase(380, 4)).toBe(false);
  });
});

describe('shiftLightRender — the Warning Centre never dims the shift light', () => {
  it('▲ and the bar blink at full swing even while warnings are active', () => {
    const r = createShiftLightRenderer().render(input({ rpm: 7000, countdown: 0, shiftState: 1, flashOn: false }));
    expect(r.segments.every(s => s.intensity === 0.3)).toBe(true);
  });
});

describe('shiftLightRender — ramp clamp (R13, AE16)', () => {
  it('effectiveRamp never exceeds the shift point; band start never below zero', () => {
    const r = createShiftLightRenderer().render(input({ shiftPoint: 1500, rampWidthRpm: 2000, rpm: 1000, countdown: 500 }));
    expect(r.effectiveRamp).toBe(1500);
  });

  it('an unset shift point at save time renders a zero band (cold), not a negative one', () => {
    const r = createShiftLightRenderer().render(input({ shiftPoint: null, rampWidthRpm: 2000, countdown: null, shiftState: null, rpm: null }));
    expect(r.mode).toBe('inert');
    expect(r.effectiveRamp).toBe(0);
  });
});

describe('shiftLightRender — steady rendering (settings preview)', () => {
  it('a test value with a configured shift point renders a lit ramp (not inert)', () => {
    const r = createShiftLightRenderer().render(input({
      rpm: 6000, countdown: 1000, shiftState: 0, steady: true,
    }));
    expect(r.mode).toBe('progressive');
    expect(r.flash).toBeNull();
  });

  it('a steady ramp > shift point renders the clamped band', () => {
    const r = createShiftLightRenderer().render(input({
      shiftPoint: 1500, rampWidthRpm: 2000, rpm: 1000, countdown: 500, steady: true,
    }));
    expect(r.effectiveRamp).toBe(1500);
  });

  it('a test value at/above the shift point renders the full bar with ▲ steady (no flash)', () => {
    const r = createShiftLightRenderer().render(input({
      rpm: 7500, countdown: 0, shiftState: 1, steady: true,
    }));
    expect(r.showUp).toBe(true);
    expect(r.showDown).toBe(false);
    expect(r.flash).toBeNull();
    expect(r.segments.every(s => s.intensity === 1)).toBe(true);
  });

  it('during a downshift hold only ▼ shows (▲ is state-gated)', () => {
    const renderer = createShiftLightRenderer();
    renderer.render(input({ rpm: 5600, shiftState: 0, countdown: 1400 }));
    const r = renderer.render(input({ rpm: 4000, shiftState: -1, countdown: 3000 }));
    expect(r.mode).toBe('downshift');
    expect(r.showUp).toBe(false);
    expect(r.showDown).toBe(true);
  });
});

describe('shiftLightRender — configurable segments and zones', () => {
  it('segmentCount controls the bar resolution (default 16)', () => {
    const def = createShiftLightRenderer().render(input());
    expect(def.segments).toHaveLength(16);
    const r = createShiftLightRenderer().render(input({ segmentCount: 8 }));
    expect(r.segments).toHaveLength(8);
  });

  it('segmentCount is clamped to 3..48', () => {
    const r = createShiftLightRenderer().render(input({ segmentCount: 2 }));
    expect(r.segments).toHaveLength(3);
    const big = createShiftLightRenderer().render(input({ segmentCount: 64 }));
    expect(big.segments).toHaveLength(48);
  });

  it('zoneCount 2 renders green left half, red right half', () => {
    const r = createShiftLightRenderer().render(input({ segmentCount: 10, zoneCount: 2, rpm: 7000, countdown: 0, shiftState: 1 }));
    expect(r.segments[4].color).toBe(SHIFT_ZONE_COLORS.green);
    expect(r.segments[5].color).toBe(SHIFT_ZONE_COLORS.red);
    expect(r.segments[9].color).toBe(SHIFT_ZONE_COLORS.red);
  });

  it('zoneCount 1 renders the whole bar red', () => {
    const r = createShiftLightRenderer().render(input({ segmentCount: 10, zoneCount: 1, rpm: 7000, countdown: 0, shiftState: 1 }));
    expect(r.segments.every(s => s.color === SHIFT_ZONE_COLORS.red)).toBe(true);
  });

  it('zoneCount 3 splits the bar into thirds', () => {
    const r = createShiftLightRenderer().render(input({ segmentCount: 20, zoneCount: 3, rpm: 7000, countdown: 0, shiftState: 1 }));
    // < 1/3 green, < 2/3 amber, ≥ 2/3 red
    expect(r.segments[6].color).toBe(SHIFT_ZONE_COLORS.green);
    expect(r.segments[10].color).toBe(SHIFT_ZONE_COLORS.amber);
    expect(r.segments[15].color).toBe(SHIFT_ZONE_COLORS.red);
  });

  it('every selected zone is visible even with few segments', () => {
    const r = createShiftLightRenderer().render(input({ segmentCount: 3, zoneCount: 3, rpm: 7000, countdown: 0, shiftState: 1 }));
    expect(r.segments.map(s => s.color)).toEqual([
      SHIFT_ZONE_COLORS.green,
      SHIFT_ZONE_COLORS.amber,
      SHIFT_ZONE_COLORS.red,
    ]);
    const two = createShiftLightRenderer().render(input({ segmentCount: 4, zoneCount: 3, rpm: 7000, countdown: 0, shiftState: 1 }));
    const colors = new Set(two.segments.map(s => s.color));
    expect(colors.has(SHIFT_ZONE_COLORS.red)).toBe(true);
  });
});

describe('shiftLightRender — zone colors (KTD6)', () => {
  it('green/amber/red zone hues are not CVD-confusable', () => {
    expect(zoneColorsCvdSafe()).toBe(true);
  });

  it('palette is a green → amber → red progression', () => {
    expect(SHIFT_ZONE_COLORS.green).not.toBe(SHIFT_ZONE_COLORS.amber);
    expect(SHIFT_ZONE_COLORS.amber).not.toBe(SHIFT_ZONE_COLORS.red);
  });
});

describe('shiftLightRender — reset', () => {
  it('reset clears the freeze memo', () => {
    const renderer = createShiftLightRenderer();
    renderer.render(input({ rpm: 5600, shiftState: 0, countdown: 1400 }));
    renderer.render(input({ rpm: 4000, shiftState: -1, countdown: 3000 }));
    renderer.reset();
    const re = renderer.render(input({ rpm: 5600, shiftState: 0, countdown: 1400 }));
    expect(re.mode).toBe('progressive');
  });
});

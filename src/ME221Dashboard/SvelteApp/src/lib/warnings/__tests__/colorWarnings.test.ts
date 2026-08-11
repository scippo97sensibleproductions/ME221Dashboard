import { describe, it, expect } from 'vitest';
import {
  checkColorWarnings,
  generateDefaultColor,
  hasCvdConfusablePair,
  isCvdConfusable,
  contrastRatio,
} from '../colorWarnings';

const bg = '#0A0A0A';

describe('colorWarnings — duplicate', () => {
  it('fires on an exact duplicate and keeps the color', () => {
    const levels = [
      { id: 'a', color: '#f59e0b' },
      { id: 'b', color: '#ef4444' },
    ];
    const check = checkColorWarnings({ levels, candidateColor: '#f59e0b', candidateId: 'c', background: bg });
    expect(check.duplicate).toBe(true);
    expect(check.nearDuplicate).toBe(false);
  });

  it('is case-insensitive', () => {
    const levels = [{ id: 'a', color: '#F59E0B' }];
    expect(checkColorWarnings({ levels, candidateColor: '#f59e0b', candidateId: 'c', background: bg }).duplicate).toBe(true);
  });

  it('does not flag the candidate against itself', () => {
    const levels = [{ id: 'a', color: '#f59e0b' }];
    expect(checkColorWarnings({ levels, candidateColor: '#f59e0b', candidateId: 'a', background: bg }).duplicate).toBe(false);
  });
});

describe('colorWarnings — near-duplicate', () => {
  it('fires on same-hue adjacency below the perceptual threshold', () => {
    const levels = [{ id: 'a', color: '#f59e0b' }];
    const check = checkColorWarnings({ levels, candidateColor: '#f49e0c', candidateId: 'c', background: bg });
    expect(check.duplicate).toBe(false);
    expect(check.nearDuplicate).toBe(true);
  });

  it('does not fire for clearly distinct colors', () => {
    const levels = [{ id: 'a', color: '#f59e0b' }];
    const check = checkColorWarnings({ levels, candidateColor: '#3b82f6', candidateId: 'c', background: bg });
    expect(check.nearDuplicate).toBe(false);
  });
});

describe('colorWarnings — CVD simulations', () => {
  it('flags a blue/purple pair as confusable under deuteranopia and protanopia', () => {
    expect(isCvdConfusable('#3b82f6', '#a855f7')).toBe(true);
    expect(isCvdConfusable('#ef4444', '#3b82f6')).toBe(false);
  });

  it('exposes the per-datalink confusable-pair flag', () => {
    expect(hasCvdConfusablePair([
      { id: 'a', color: '#3b82f6' },
      { id: 'b', color: '#a855f7' },
    ])).toBe(true);
    expect(hasCvdConfusablePair([
      { id: 'a', color: '#ef4444' },
      { id: 'b', color: '#3b82f6' },
    ])).toBe(false);
  });
});

describe('colorWarnings — contrast', () => {
  it('fails the minimum contrast check against the dark surface', () => {
    const check = checkColorWarnings({ levels: [], candidateColor: '#1a1a1a', candidateId: 'c', background: bg });
    expect(check.lowContrast).toBe(true);
  });

  it('passes for a bright color on the dark surface', () => {
    const check = checkColorWarnings({ levels: [], candidateColor: '#f59e0b', candidateId: 'c', background: bg });
    expect(check.lowContrast).toBe(false);
  });

  it('contrast ratio math matches WCAG', () => {
    expect(contrastRatio('#ffffff', '#000000')).toBeGreaterThan(20);
  });
});

describe('colorWarnings — default generation', () => {
  it('returns a color not currently used', () => {
    const used = ['#f59e0b', '#ef4444', '#22c55e'];
    const next = generateDefaultColor(used);
    expect(used.map(c => c.toLowerCase())).not.toContain(next.toLowerCase());
  });

  it('never self-triggers the duplicate warning while the palette has room', () => {
    const used = ['#f59e0b', '#ef4444', '#22c55e', '#3b82f6', '#a855f7', '#ec4899', '#14b8a6', '#eab308', '#f97316'];
    const next = generateDefaultColor(used);
    const check = checkColorWarnings({
      levels: used.map(c => ({ id: c, color: c })),
      candidateColor: next,
      candidateId: 'new',
      background: bg,
    });
    expect(check.duplicate).toBe(false);
  });

  it('derives a fresh color when the whole palette is in use', () => {
    const used = ['#f59e0b', '#ef4444', '#22c55e', '#3b82f6', '#a855f7', '#ec4899', '#14b8a6', '#eab308', '#f97316', '#06b6d4'];
    const next = generateDefaultColor(used);
    expect(used.map(c => c.toLowerCase())).not.toContain(next.toLowerCase());
  });
});

export interface WarningColorCheck {
  duplicate: boolean;
  nearDuplicate: boolean;
  cvdConfusable: boolean;
  lowContrast: boolean;
}

export interface Rgb {
  r: number;
  g: number;
  b: number;
}

export function parseHex(color: string): Rgb | null {
  const m = /^#?([0-9a-f]{6})$/i.exec(color.trim());
  if (!m) return null;
  const v = parseInt(m[1], 16);
  return { r: (v >> 16) & 255, g: (v >> 8) & 255, b: v & 255 };
}

export function rgbToHex(c: Rgb): string {
  return `#${((c.r << 16) | (c.g << 8) | c.b).toString(16).padStart(6, '0')}`;
}

function linearize(channel: number): number {
  const s = channel / 255;
  return s <= 0.04045 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
}

/** WCAG relative luminance of a hex color. */
export function luminance(color: string): number {
  const c = parseHex(color);
  if (!c) return 0;
  return 0.2126 * linearize(c.r) + 0.7152 * linearize(c.g) + 0.0722 * linearize(c.b);
}

/** WCAG contrast ratio between two hex colors. */
export function contrastRatio(a: string, b: string): number {
  const la = luminance(a);
  const lb = luminance(b);
  const lighter = Math.max(la, lb);
  const darker = Math.min(la, lb);
  return (lighter + 0.05) / (darker + 0.05);
}

/** Perceptual distance (CIE76 on linearized components, 0..~1). */
export function perceptualDistance(a: string, b: string): number {
  const ca = parseHex(a);
  const cb = parseHex(b);
  if (!ca || !cb) return 1;
  return Math.sqrt(
    Math.pow(linearize(ca.r) - linearize(cb.r), 2) +
    Math.pow(linearize(ca.g) - linearize(cb.g), 2) +
    Math.pow(linearize(ca.b) - linearize(cb.b), 2)
  );
}

const NEAR_DUPLICATE_THRESHOLD = 0.06;
const CVD_CONFUSABLE_THRESHOLD = 0.1;
const MIN_CONTRAST_RATIO = 2.2;

/**
 * CVD simulation matrices (linear-RGB domain). Confusable pairs collapse for
 * deuteranopia/protanopia when their simulated distance is below the threshold.
 */
function simulateCvd(color: string, type: 'deuteranopia' | 'protanopia'): string {
  const c = parseHex(color);
  if (!c) return color;
  const r = linearize(c.r);
  const g = linearize(c.g);
  const b = linearize(c.b);
  const matrix = type === 'deuteranopia'
    ? [0.625, 0.375, 0, 0.7, 0.3, 0, 0, 0.3, 0.7]
    : [0.567, 0.433, 0, 0.558, 0.442, 0, 0, 0.242, 0.758];
  const sr = r * matrix[0] + g * matrix[1] + b * matrix[2];
  const sg = r * matrix[3] + g * matrix[4] + b * matrix[5];
  const sb = r * matrix[6] + g * matrix[7] + b * matrix[8];
  const to8 = (v: number) => Math.round(Math.max(0, Math.min(1, v)) * 255);
  return rgbToHex({ r: to8(sr), g: to8(sg), b: to8(sb) });
}

export function isCvdConfusable(a: string, b: string): boolean {
  return (
    perceptualDistance(simulateCvd(a, 'deuteranopia'), simulateCvd(b, 'deuteranopia')) < CVD_CONFUSABLE_THRESHOLD ||
    perceptualDistance(simulateCvd(a, 'protanopia'), simulateCvd(b, 'protanopia')) < CVD_CONFUSABLE_THRESHOLD
  );
}

export interface ColorWarningsInput {
  levels: { id: string; color: string }[];
  candidateColor: string;
  candidateId: string;
  background: string;
}

/**
 * Full warning check for a candidate color against the datalink's levels:
 * duplicate / near-duplicate (perceptual) / CVD-confusable pair / contrast
 * against the app dark surface. Warnings keep the color (kept-and-warned).
 */
export function checkColorWarnings(input: ColorWarningsInput): WarningColorCheck {
  const others = input.levels.filter(l => l.id !== input.candidateId);
  const duplicate = others.some(l => l.color.toLowerCase() === input.candidateColor.toLowerCase());
  const nearDuplicate = !duplicate && others.some(l =>
    perceptualDistance(l.color, input.candidateColor) < NEAR_DUPLICATE_THRESHOLD
  );
  const cvdConfusable = others.some(l => isCvdConfusable(l.color, input.candidateColor));
  const lowContrast = contrastRatio(input.candidateColor, input.background) < MIN_CONTRAST_RATIO;
  return { duplicate, nearDuplicate, cvdConfusable, lowContrast };
}

/**
 * True when ANY pair of the datalink's levels is CVD-confusable — the gauge
 * keeps the level-name indicator visible at reduced scale in that case.
 */
export function hasCvdConfusablePair(levels: { id: string; color: string }[]): boolean {
  for (let i = 0; i < levels.length; i++) {
    for (let j = i + 1; j < levels.length; j++) {
      if (isCvdConfusable(levels[i].color, levels[j].color)) return true;
    }
  }
  return false;
}

const DEFAULT_PALETTE = [
  '#f59e0b', '#ef4444', '#22c55e', '#3b82f6', '#a855f7',
  '#ec4899', '#14b8a6', '#eab308', '#f97316', '#06b6d4',
];

/**
 * Collision-free default color: the first palette swatch not used by the
 * datalink (unbounded-domain case — never self-triggers warnings); when the
 * whole palette is in use, derives a channel-rotated variant of the
 * most-distant swatch so the add action still produces a fresh color.
 */
export function generateDefaultColor(usedColors: string[]): string {
  const used = new Set(usedColors.map(c => c.toLowerCase()));
  const unused = DEFAULT_PALETTE.filter(c => !used.has(c.toLowerCase()));
  if (unused.length > 0) return unused[0];

  // All palette colors used — derive a fresh color from the most-distant swatch
  let best = DEFAULT_PALETTE[0];
  let bestScore = -1;
  for (const c of DEFAULT_PALETTE) {
    const score = Math.min(...DEFAULT_PALETTE.map(o => perceptualDistance(c, o)));
    if (score > bestScore) {
      bestScore = score;
      best = c;
    }
  }
  const rgb = parseHex(best)!;
  const derived = rgbToHex({ r: rgb.g, g: rgb.b, b: rgb.r });
  return used.has(derived.toLowerCase()) ? rgbToHex({ r: rgb.b, g: rgb.r, b: rgb.g }) : derived;
}

const PALETTE = [
  '#0ea5e9', // sky
  '#f97316', // orange
  '#22c55e', // green
  '#ef4444', // red
  '#a855f7', // purple
  '#eab308', // yellow
  '#06b6d4', // cyan
  '#f43f5e', // rose
  '#14b8a6', // teal
  '#8b5cf6', // violet
  '#f59e0b', // amber
  '#10b981', // emerald
  '#6366f1', // indigo
  '#ec4899', // pink
  '#84cc16', // lime
  '#64748b', // slate
];

const STORAGE_KEY = 'monitor_sensor_colors';

function loadColorMap(): Record<number, string> {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '{}');
  } catch {
    return {};
  }
}

function saveColorMap(map: Record<number, string>): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(map));
}

let colorMap = loadColorMap();
let nextIndex = Object.keys(colorMap).length;

export function getSensorColor(dataLinkId: number): string {
  if (colorMap[dataLinkId]) return colorMap[dataLinkId];
  const color = PALETTE[nextIndex % PALETTE.length];
  colorMap[dataLinkId] = color;
  nextIndex++;
  saveColorMap(colorMap);
  return color;
}

export function resetColors(): void {
  colorMap = {};
  nextIndex = 0;
  localStorage.removeItem(STORAGE_KEY);
}

import type { AvailableSensor } from './HybridBridgeTypes';

/**
 * Ranked sensor search shared by the add-gauge popup and the chart-overlay picker.
 *
 * Why ranked + uncapped: the C# `GetAvailableSensors` payload appends the derived
 * entities (−3001..−3006) AFTER every calibration link, so a plain filter capped
 * at N results in array order starves them — searching "rpm" matched dozens of
 * rpm-unit links first and "RPM to Shift"/"Shift State" never surfaced. Name
 * matches rank above category/unit/id matches, and there is no result cap (the
 * lists scroll; capping in array order silently hides tail entries).
 */
export function searchSensors(sensors: readonly AvailableSensor[], query: string): AvailableSensor[] {
  const q = query.trim().toLowerCase();
  if (!q) return [...sensors];

  const ranked: Array<[number, AvailableSensor]> = [];
  for (const s of sensors) {
    const name = (s.customization?.customName || s.name).toLowerCase();
    const category = s.category.toLowerCase();
    const hit = name.includes(q)
      || category.includes(q)
      || s.unit.toLowerCase().includes(q)
      || String(s.id).includes(q);
    if (!hit) continue;

    let rank = 3;
    if (name.startsWith(q)) rank = 0;
    else if (name.includes(q)) rank = 1;
    else if (category.includes(q)) rank = 2;
    ranked.push([rank, s]);
  }

  ranked.sort((a, b) => a[0] - b[0]);
  return ranked.map(([, s]) => s);
}

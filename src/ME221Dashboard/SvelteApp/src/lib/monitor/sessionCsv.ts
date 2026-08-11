// Shared CSV builders for recorded sessions (SessionRecorder + SessionsPage).
// VirtualDyno (and spreadsheets) map columns by position, so rows must contain
// exactly one value per sensor — never interleave (t,v) pairs into one cell,
// or every column after the first shifts and VD reads timestamps as RPM.

export interface CsvSessionSource {
  sensorIds: number[];
  sensorNames: Record<number, string>;
  data: Record<number, Array<{ t: number; v: number }>>;
}

const VD_NAME_MAP: Record<string, string> = {
  'rpm': 'RPM', 'engine speed': 'RPM',
  'throttle position': 'Throttle Position', 'tps': 'Throttle Position',
  'afr': 'AFR', 'wideband': 'AFR', 'lambda': 'AFR',
  'boost': 'Boost', 'map': 'Boost',
  'baro': 'Barometric Pressure',
  'clt': 'Coolant Temp', 'coolant temp': 'Coolant Temp', 'coolant temperature': 'Coolant Temp',
  'iat': 'Intake Air Temp', 'intake air temp': 'Intake Air Temp', 'intake air temperature': 'Intake Air Temp',
  'batt': 'Battery Voltage', 'battery': 'Battery Voltage', 'battery voltage': 'Battery Voltage',
  'vss': 'Vehicle Speed', 'vehicle speed': 'Vehicle Speed', 'speed': 'Vehicle Speed',
  'ignition': 'Ignition Timing', 'ignition timing': 'Ignition Timing', 'ignition advance': 'Ignition Timing',
  'duty': 'Injector Duty', 'injector duty': 'Injector Duty',
  'fuel rail': 'Fuel Pressure', 'fuel pressure': 'Fuel Pressure',
};

const escapeCsv = (s: string): string =>
  s.includes(',') || s.includes('"') ? `"${s.replace(/"/g, '""')}"` : s;

function mapVdName(raw: string): string {
  const lower = raw.toLowerCase().trim();
  if (VD_NAME_MAP[lower]) return VD_NAME_MAP[lower];
  for (const [key, val] of Object.entries(VD_NAME_MAP)) {
    if (lower.includes(key)) return val;
  }
  return raw;
}

/** Plain wide CSV: time_ms column plus one value column per sensor. */
export function buildSessionCsv(session: CsvSessionSource): string {
  const ids = session.sensorIds;
  if (ids.length === 0) return '';
  const headers = ['time_ms', ...ids.map(id => escapeCsv(session.sensorNames[id] ?? String(id)))];
  const lines: string[] = [headers.join(',')];
  const maxLen = Math.max(...ids.map(id => session.data[id]?.length ?? 0));
  for (let i = 0; i < maxLen; i++) {
    const row: string[] = [];
    let timeCell = '';
    for (const id of ids) {
      const s = session.data[id]?.[i];
      if (!s) { row.push(''); continue; }
      if (timeCell === '') timeCell = s.t.toFixed(1);
      row.push(String(s.v));
    }
    if (timeCell === '') continue;
    lines.push([timeCell, ...row].join(','));
  }
  return lines.join('\n');
}

/** VirtualDyno CSV: 'ME221' marker line, Time column plus one value per sensor. */
export function buildSessionVdCsv(session: CsvSessionSource): string {
  const ids = session.sensorIds;
  if (ids.length === 0) return '';
  const mappedNames = ids.map(id => mapVdName(session.sensorNames[id] ?? String(id)));
  const headers = ['Time', ...mappedNames.map(escapeCsv)];
  const lines: string[] = ['ME221', headers.join(',')];
  const maxLen = Math.max(...ids.map(id => session.data[id]?.length ?? 0));
  for (let i = 0; i < maxLen; i++) {
    const row: string[] = [];
    let timeCell = '';
    for (const id of ids) {
      const s = session.data[id]?.[i];
      if (!s) { row.push(''); continue; }
      if (timeCell === '') timeCell = (s.t / 1000).toFixed(3);
      row.push(String(s.v));
    }
    if (timeCell === '') continue;
    lines.push([timeCell, ...row].join(','));
  }
  return lines.join('\n');
}

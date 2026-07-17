import type { LogEntry } from './LogViewerTypes';

export interface ImportResult {
  entries: LogEntry[];
  skippedCount: number;
  totalRows: number;
}

const REQUIRED_HEADERS = ['timestamp', 'level', 'category', 'message', 'exception'];

function parseCsvLine(line: string): string[] {
  const fields: string[] = [];
  let current = '';
  let inQuotes = false;

  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (inQuotes) {
      if (ch === '"') {
        if (i + 1 < line.length && line[i + 1] === '"') {
          current += '"';
          i++;
        } else {
          inQuotes = false;
        }
      } else {
        current += ch;
      }
    } else {
      if (ch === '"') {
        inQuotes = true;
      } else if (ch === ',') {
        fields.push(current);
        current = '';
      } else {
        current += ch;
      }
    }
  }
  fields.push(current);
  return fields;
}

function unquote(field: string): string {
  const trimmed = field.trim();
  if (trimmed.startsWith('"') && trimmed.endsWith('"')) {
    return trimmed.slice(1, -1).replace(/""/g, '"');
  }
  return trimmed;
}

export function parseCsvLog(csvContent: string): ImportResult {
  const lines = csvContent.split(/\r?\n/).filter(l => l.trim().length > 0);

  if (lines.length === 0) {
    return { entries: [], skippedCount: 0, totalRows: 0 };
  }

  const headerFields = parseCsvLine(lines[0]).map(h => h.trim().toLowerCase());
  const headerValid = REQUIRED_HEADERS.every(h => headerFields.includes(h));

  const entries: LogEntry[] = [];
  let skippedCount = 0;
  const totalRows = lines.length - 1;

  const tsIdx = headerFields.indexOf('timestamp');
  const lvlIdx = headerFields.indexOf('level');
  const catIdx = headerFields.indexOf('category');
  const msgIdx = headerFields.indexOf('message');
  const excIdx = headerFields.indexOf('exception');

  if (!headerValid || tsIdx < 0 || lvlIdx < 0 || catIdx < 0 || msgIdx < 0) {
    return { entries: [], skippedCount: totalRows, totalRows };
  }

  for (let i = 1; i < lines.length; i++) {
    const fields = parseCsvLine(lines[i]);
    if (fields.length < 4) {
      skippedCount++;
      continue;
    }

    const timestamp = unquote(fields[tsIdx] ?? '');
    const level = unquote(fields[lvlIdx] ?? '');
    const category = unquote(fields[catIdx] ?? '');
    const message = unquote(fields[msgIdx] ?? '');
    const exception = excIdx >= 0 ? unquote(fields[excIdx] ?? '') : '';

    if (!timestamp || !level || !category) {
      skippedCount++;
      continue;
    }

    entries.push({
      timestamp,
      level,
      category,
      message,
      exception: exception || undefined,
      source: 'import',
    });
  }

  return { entries, skippedCount, totalRows };
}

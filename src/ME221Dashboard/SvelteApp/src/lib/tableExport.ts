import type { TableDefinition, TableData } from './tables/types';
import { getOutputValue } from './tables/types';
import { parse as yamlParse } from 'yaml';

export interface ExportBundle {
  ecu: {
    product: string;
    model: string;
    firmware: string;
    vehicle: string;
  };
  links: Record<number, { name: string; unit: string; category: string }>;
  tables: ExportTable[];
}

export interface ExportTable {
  name: string;
  category: string;
  type: string;
  description: string;
  axes: {
    x: { name: string; unit: string; values: number[] };
    y?: { name: string; unit: string; values: number[] };
  };
  output: {
    name: string;
    unit: string;
    values: number[][];
  };
}

export function buildExportBundle(
  tables: { def: TableDefinition; data: TableData }[],
  links: Record<number, { name: string; unit: string; category: string }> = {},
  ecuInfo: { product: string; model: string; firmware: string; vehicle: string } = {
    product: 'ME221', model: 'V2B-PNP', firmware: '', vehicle: 'Mazda MX-5'
  }
): ExportBundle {
  const exportTables: ExportTable[] = tables.map(({ def, data }) => {
    const is1D = def.rows === 1;
    const outputValues: number[][] = [];
    for (let r = 0; r < def.rows; r++) {
      const row: number[] = [];
      for (let c = 0; c < def.cols; c++) {
        row.push(Math.round(getOutputValue(data, r, c, def.cols) * 10) / 10);
      }
      outputValues.push(row);
    }

    return {
      name: def.name,
      category: def.category,
      type: def.tableType,
      description: `${def.outputName} table — ${def.input0Name}${!is1D ? ` vs ${def.input1Name}` : ''}`,
      axes: {
        x: {
          name: def.input0Name,
          unit: links[def.input0LinkId]?.unit ?? '',
          values: data.input0,
        },
        ...(!is1D ? {
          y: {
            name: def.input1Name,
            unit: links[def.input1LinkId]?.unit ?? '',
            values: data.input1,
          }
        } : {}),
      },
      output: {
        name: def.outputName,
        unit: links[def.outputLinkId]?.unit ?? '',
        values: outputValues,
      },
    };
  });

  return {
    ecu: ecuInfo,
    links,
    tables: exportTables,
  };
}

export function generateYamlString(bundle: ExportBundle): string {
  const lines: string[] = [];
  lines.push('# ME221 Calibration Export');
  lines.push(`# Generated: ${new Date().toISOString()}`);
  lines.push('');
  lines.push('ecu:');
  lines.push(`  product: ${bundle.ecu.product}`);
  lines.push(`  model: ${bundle.ecu.model}`);
  lines.push(`  firmware: "${bundle.ecu.firmware}"`);
  lines.push(`  vehicle: ${bundle.ecu.vehicle}`);
  lines.push('');

  if (Object.keys(bundle.links).length > 0) {
    lines.push('links:');
    for (const [id, link] of Object.entries(bundle.links)) {
      lines.push(`  ${id}:`);
      lines.push(`    name: ${link.name}`);
      lines.push(`    unit: ${link.unit}`);
      lines.push(`    category: ${link.category}`);
    }
    lines.push('');
  }

  lines.push('tables:');
  for (const table of bundle.tables) {
    lines.push(`  - name: ${table.name}`);
    lines.push(`    category: ${table.category}`);
    lines.push(`    type: ${table.type}`);
    lines.push(`    description: "${table.description}"`);
    lines.push('    axes:');
    lines.push(`      x:`);
    lines.push(`        name: ${table.axes.x.name}`);
    lines.push(`        unit: ${table.axes.x.unit}`);
    lines.push(`        values: [${table.axes.x.values.join(', ')}]`);
    if (table.axes.y) {
      lines.push(`      y:`);
      lines.push(`        name: ${table.axes.y.name}`);
      lines.push(`        unit: ${table.axes.y.unit}`);
      lines.push(`        values: [${table.axes.y.values.join(', ')}]`);
    }
    lines.push('    output:');
    lines.push(`      name: ${table.output.name}`);
    lines.push(`      unit: ${table.output.unit}`);
    lines.push('      values:');
    for (const row of table.output.values) {
      lines.push(`        - [${row.join(', ')}]`);
    }
    lines.push('');
  }

  return lines.join('\n');
}

export function parseImportBundle(yamlString: string): ExportBundle | null {
  try {
    const parsed = yamlParse(yamlString);
    if (!parsed || typeof parsed !== 'object') return null;
    if (!parsed.tables || !Array.isArray(parsed.tables)) return null;

    const tables: ExportTable[] = parsed.tables.map((t: Record<string, unknown>) => ({
      name: t.name as string,
      category: (t.category as string) || '',
      type: (t.type as string) || '',
      description: (t.description as string) || '',
      axes: {
        x: {
          name: t.axes?.x?.name as string || '',
          unit: t.axes?.x?.unit as string || '',
          values: Array.isArray(t.axes?.x?.values) ? t.axes.x.values as number[] : [],
        },
        ...(t.axes?.y ? {
          y: {
            name: t.axes.y.name as string || '',
            unit: t.axes.y.unit as string || '',
            values: Array.isArray(t.axes.y.values) ? t.axes.y.values as number[] : [],
          }
        } : {}),
      },
      output: {
        name: t.output?.name as string || '',
        unit: t.output?.unit as string || '',
        values: Array.isArray(t.output?.values) ? t.output.values as number[][] : [],
      },
    }));

    return {
      ecu: {
        product: (parsed.ecu?.product as string) || 'ME221',
        model: (parsed.ecu?.model as string) || '',
        firmware: (parsed.ecu?.firmware as string) || '',
        vehicle: (parsed.ecu?.vehicle as string) || '',
      },
      links: parsed.links || {},
      tables,
    };
  } catch {
    return null;
  }
}

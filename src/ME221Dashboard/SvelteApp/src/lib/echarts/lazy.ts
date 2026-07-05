import type { ECharts } from 'echarts';

let cached: Promise<typeof import('echarts/core')> | null = null;

async function loadEchartsCore() {
  const [core, renderers, charts, components] = await Promise.all([
    import('echarts/core'),
    import('echarts/renderers'),
    import('echarts/charts'),
    import('echarts/components'),
  ]);
  core.use([
    renderers.CanvasRenderer,
    charts.LineChart,
    components.GridComponent,
    components.TooltipComponent,
    components.DataZoomComponent,
    components.DatasetComponent,
    components.LegendComponent,
  ]);
  return core;
}

/**
 * Lazy-load ECharts. First call loads the library (~200KB tree-shaken);
 * subsequent calls return the cached module immediately.
 */
export function getEcharts(): Promise<typeof import('echarts/core')> {
  if (!cached) {
    cached = loadEchartsCore();
  }
  return cached;
}

export type { ECharts };

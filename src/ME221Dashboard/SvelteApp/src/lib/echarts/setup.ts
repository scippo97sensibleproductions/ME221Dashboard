import { use } from 'echarts/core';
import { CanvasRenderer } from 'echarts/renderers';
import { LineChart } from 'echarts/charts';
import {
  GridComponent,
  TooltipComponent,
  DataZoomComponent,
  DatasetComponent,
  LegendComponent,
} from 'echarts/components';

use([
  CanvasRenderer,
  LineChart,
  GridComponent,
  TooltipComponent,
  DataZoomComponent,
  DatasetComponent,
  LegendComponent,
]);

export type { EChartsOption as ECOption } from 'echarts';
export { init as echartsInit } from 'echarts/core';

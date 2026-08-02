// Minimal ambient types for uPlot 1.6.x (the package ships no .d.ts).
// Covers only the API surface used by lib/charts/.

declare module 'uplot' {
  type AlignedData = (number | null)[][];

  interface RangeFunction {
    (u: uPlot, dataMin: number, dataMax: number): [number, number];
  }

  interface SeriesOpts {
    label?: string;
    stroke?: string;
    width?: number;
    dash?: number[];
    spanGaps?: boolean;
    scale?: string;
    points?: { show?: boolean };
  }

  interface AxisOpts {
    stroke?: string;
    font?: string;
    size?: number;
    values?: (u: uPlot, ticks: number[]) => string[];
    grid?: { stroke?: string };
    ticks?: { stroke?: string; size?: number };
  }

  interface CursorOpts {
    show?: boolean;
    x?: boolean;
    y?: boolean;
    stroke?: string;
    width?: number;
    points?: { show?: boolean };
  }

  interface ScaleOpts {
    time?: boolean;
    range?: RangeFunction | [number, number];
  }

  interface Hooks {
    setCursor?: Array<(u: uPlot, dataIdx: number | null) => void>;
    draw?: Array<(u: uPlot) => void>;
  }

  interface Options {
    width?: number;
    height?: number;
    pxRatio?: number;
    legend?: { show?: boolean };
    select?: { show?: boolean };
    focus?: { show?: boolean };
    scales?: Record<string, ScaleOpts>;
    series?: Array<SeriesOpts | Record<string, never>>;
    axes?: AxisOpts[];
    cursor?: CursorOpts;
    hooks?: Hooks;
    plugins?: unknown[];
  }

  interface BBox {
    left: number;
    top: number;
    width: number;
    height: number;
  }

  class uPlot {
    constructor(opts: Options, data: AlignedData | null, target: HTMLElement);

    setData(data: AlignedData | null, redraw?: boolean): void;
    redraw(clear?: boolean): void;
    setSize(size: { width: number; height: number }): void;
    destroy(): void;

    valToPos(val: number, scaleKey: string, seriesIdx?: number): number;

    data: AlignedData;
    cursor: { left: number; top: number; idx: number | null };
    ctx: CanvasRenderingContext2D;
    bbox: BBox;
    root: HTMLElement;
    over: HTMLElement;
    width: number;
    height: number;
  }

  export default uPlot;
}

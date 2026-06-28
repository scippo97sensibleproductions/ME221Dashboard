# ME221 Dashboard Frontend

Svelte 5 app that runs inside the MAUI HybridWebView. This is the entire UI.

## What it does

Renders the dashboard app: connection screen, calibration loader, live gauge dashboard, tuning table editor, sensor selection, logs viewer. Talks to the C# backend through the `HybridBridge` interface.

## Tech stack

- **Svelte 5** with runes (`$state`, `$derived`, `$effect`, `$props`)
- **Vite 8** for build and dev server
- **TypeScript 6**
- **Tailwind CSS 4** for styling
- **Flowbite Svelte** for UI components (buttons, modals, dropdowns, inputs)
- **Tabler Icons** via `@tabler/icons-svelte` (6000+ icons)

## Pages

| Page | File | What it does |
|------|------|-------------|
| Welcome | `WelcomePage.svelte` | Android permission wizard (location, storage, USB) |
| Connection | `ConnectionPage.svelte` | TCP (IP:port) or Serial (port picker) connect |
| Calibration | `CalibrationPage.svelte` | `.mefw` file picker, ECU info matching |
| Dashboard Config | `DashboardConfigPage.svelte` | Sensor selection, paginated, filterable |
| Dashboard | `DashboardPage.svelte` | Live gauge display, drag to reposition |
| Table List | `TableListPage.svelte` | Browse tuning tables by category |
| Table Editor | `TableEditorPage.svelte` | 1D/2D table editor with heatmap, undo, transforms |
| Logs | `LogsPage.svelte` | Real-time log viewer |

Navigation flow:
```
welcome -> connection -> calibration -> config -> dashboard
                                                  -> tableList -> tableEditor
                                                  -> logs
```

## Gauges

Four visual types, each with its own settings panel:

- **Arc** -- sweep-angle gauge with needle, color stops, configurable arc position
- **Bar** -- horizontal/vertical bar with thresholds
- **Text** -- plain value display
- **Digital** -- odometer, large-digit, 7-segment, or cluster style

Gauges are positioned freely on a canvas using percentage-based coordinates (fractionX/fractionY). Layout is saved to the backend on drag release.

## Table editor

Supports 1D (1x16, 1x32) and 2D (16x16) tuning tables.

- **2D tables** -- heat-colored grid (blue to yellow to red), click cells to edit
- **1D tables** -- read-only curve chart on the bottom, interactive data grid on top
- **Transforms** -- scale, offset, set, fill, interpolate, smooth, clamp on range selections
- **Undo/redo** -- full history with Ctrl+Z / Ctrl+Y
- **Copy/paste** -- internal clipboard (no `navigator.clipboard` dependency)
- **Import/export** -- CSV and YAML formats

## The bridge

`HybridBridge.ts` wraps calls to `window.HybridWebView.InvokeDotNet()`. Each method returns a typed Promise. C# events come in through `window.addEventListener('HybridWebViewMessageReceived')`.

```typescript
// Example: connect via TCP
const result = await HybridBridge.connectTcp('192.168.1.100', 22100);

// Example: subscribe to live data
HybridBridge.onMessage('liveDataUpdate', (data) => {
  // update gauge values
});
```

## Development

```powershell
cd src\ME221Dashboard\SvelteApp
npm install
npm run dev       # starts Vite dev server on port 5173
npm run build     # production build to ../wwwroot/
npm run check     # type-check
```

In DEBUG mode, the MAUI app proxies to the Vite dev server automatically. Changes show up without rebuilding the .NET project.

## File structure

```
src/
  App.svelte              Page router + global state
  app.css                 Tailwind imports
  main.ts                 Entry point

  pages/                  8 page components
  lib/
    HybridBridge.ts       JS<->C# bridge wrapper
    HybridBridgeTypes.ts  Bridge interface types
    AppHeader.svelte      Top navigation bar
    Sidebar.svelte        Side navigation + dashboard management
    GaugeSettingsModal.svelte   Gauge configuration dialog
    GaugePreviewPanel.svelte    Live gauge preview
    NewDashboardDialog.svelte   Create/rename dashboard
    VehicleConfigModal.svelte   Vehicle configuration
    NumberInput.svelte    Numeric input with increment/decrement
    ToastContainer.svelte Toast notifications
    toasts.svelte.ts      Toast state management
    tableExport.ts        CSV/YAML export utilities

    gauges/               Gauge components + settings
      GaugeCard.svelte    Container, dispatches to gauge type
      ArcGauge.svelte     Arc gauge visual
      BarGauge.svelte     Bar gauge visual
      TextGauge.svelte    Text gauge visual
      DigitalGauge.svelte Digital gauge visual
      gaugeTypes.ts       Enums + interfaces
      gaugeUtils.ts       Calculation functions
      types.ts            Barrel re-export

    tables/               Table editor components
      TableGrid.svelte    2D heat-colored grid
      CurveEditor.svelte  1D curve + data grid
      CellEditor.svelte   Single cell value editor
      TableToolbar.svelte Undo/redo, commit, import/export
      SelectionToolbar.svelte  Copy/paste/transform bar
      OpsSheet.svelte     Transform operations panel
      tableSelection.ts   Range selection logic
      tableTransforms.ts  Transform operations
      tableUndoRedo.ts    Undo/redo state
      types.ts            Table types + helpers
```

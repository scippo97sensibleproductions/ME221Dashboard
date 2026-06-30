# ME221Dashboard

The main app. .NET MAUI shell on the backend, Svelte 5 on the frontend, talking through a HybridWebView bridge.

## What it does

Connects to a ME221 ECU over TCP or serial (including Android USB serial), loads calibration firmware definitions, displays live sensor data on configurable gauge dashboards, and lets you edit tuning tables. Also handles GPS tracking, odometer, dashboard export/import, and vehicle configuration.

## Platforms

- Windows: TCP and serial connections. Simulated GPS for testing.
- Android: TCP, serial, and USB serial (via UsbSerialForAndroid). Real GPS via MAUI Geolocation API. Min API 26, targets API 37.

iOS and macOS are in the csproj but not actively developed.

## Project structure

```
ME221Dashboard/
  MauiProgram.cs           DI container setup
  App.xaml.cs              App entry point
  MainPage.xaml.cs         HybridWebView host + Vite dev server proxy (DEBUG)

  Services/                C# backend services
    HybridBridgeService.cs Central JS<->C# bridge (~80 methods)
    Bridge/                Partial class files split by domain
      EcuBridgeMethods.cs
      ConnectionBridgeMethods.cs
      DashboardBridgeMethods.cs
      TableBridgeMethods.cs
      GpsBridgeMethods.cs
      VehicleConfigBridgeMethods.cs
      PermissionBridgeMethods.cs
      ImageBridgeMethods.cs
      LogBridgeMethods.cs
      FileBridgeMethods.cs
    EcuConnectionService.cs
    LiveDataService.cs
    CalibrationService.cs
    PersistenceService.cs
    ChannelFactory.cs
    ...

  SvelteApp/               Svelte 5 frontend
    src/
      App.svelte           Page router
      pages/               8 pages
      lib/
        HybridBridge.ts    JS bridge wrapper
        gauges/            4 gauge types + settings panels
        tables/            Table grid, curve editor, transforms

  Platforms/               Android, Windows, iOS, macOS platform code
  Resources/               Icons, fonts, splash screen
  wwwroot/                 Built Svelte output (production)
```

## The bridge

All communication between Svelte and C# goes through `HybridBridgeService`. JS calls C# via `window.HybridWebView.InvokeDotNet(methodName, params)`. C# calls JS via `HybridWebView.SendRawMessage(json)`.

The bridge handles: connection management, calibration loading, live data streaming, dashboard CRUD, table read/write, GPS, logging, permissions, file I/O, and export/import.

In DEBUG mode, `MainPage.xaml.cs` spawns a Vite dev server and proxies requests to it, so you get hot-reload during development.

## Building

Prerequisites: .NET 11 SDK, Node.js 20+, Android SDK (for Android builds).

```powershell
# Build Svelte frontend
cd src\ME221Dashboard\SvelteApp
npm install
npm run build

# Build for Windows
dotnet build src\ME221Dashboard\ME221Dashboard.csproj -f net11.0-windows10.0.19041.0

# Build for Android
dotnet build src\ME221Dashboard\ME221Dashboard.csproj -f net11.0-android37.0

# Or use the build script (interactive)
dotnet build.cs
```

## Running in dev mode

```powershell
cd src\ME221Dashboard\SvelteApp
npm run dev
```

This starts the Vite dev server. If you're running the MAUI app in DEBUG, it automatically proxies to the dev server, so changes show up without rebuilding.

## Running against the emulator

1. Start the emulator:
   ```powershell
   dotnet run --project src\ME221.Emulator -- --calibration calibration.json
   ```
2. In the app, go to Connection, pick TCP, enter `localhost:22100`.

## Key services

| Service | What it does |
|---------|-------------|
| `EcuConnectionService` | Manages TCP/serial connection lifecycle |
| `LiveDataService` | Subscribes to reporting, batches entity updates at ~30fps |
| `CalibrationService` | Loads `.mefw` files, matches calibration to ECU |
| `PersistenceService` | JSON persistence for dashboards, gauge configs, vehicle config |
| `HybridBridgeService` | JS<->C# bridge, all ~80 methods |
| `SimulatedGpsService` | Generates fake GPS data on Windows |
| `GeolocationGpsService` | Real GPS on Android |
| `DashboardPackageService` | `.mez` package export/import |
| `LogCapture` | In-app log viewer |

## Dependencies

- ME221.Comms: protocol library
- ME221.Data: domain models
- ME221Dashboard.Comms: serial channel
- Microsoft.Maui.Controls: UI framework
- UsbSerialForAndroid: Android USB serial (Android only)
- Serilog: logging

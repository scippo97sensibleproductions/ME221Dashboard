# ME221 Dashboard

A tuning and monitoring app for the ME221 engine control unit, found in Mazda MX-5s (and some other cars). Connect over TCP or serial, load your calibration firmware, watch live sensor data on a configurable gauge dashboard, and edit fuel/ignition tables.

Built with .NET MAUI (C# backend) and Svelte 5 (frontend inside a HybridWebView). Targets Windows and Android.

## What's in here

```
src/
  ME221.Comms/              Protocol library. Frames, CRC, request/response, heartbeat.
  ME221.Data/               Domain models. Calibration data, firmware file reader, table serialization.
  ME221Dashboard.Comms/     Serial port channel, zero-allocation report parser.
  ME221.Comms.Cli/          Command-line tool for testing protocol operations.
  ME221.Emulator/           TCP server that pretends to be a real ECU.
  ME221Dashboard/           The actual app. MAUI shell + Svelte frontend.
tests/
  ME221.Comms.Tests/        Unit tests (CRC, framing, protocol, mocks).
  ME221.Comms.IntegrationTests/  Integration tests (skeleton, no tests yet).
  ME221.Benchmarks/         BenchmarkDotNet perf benchmarks.
```

## Building

Prerequisites: .NET 11 SDK, Node.js 20+.

```powershell
# Build everything (Svelte first, then .NET)
dotnet build.cs

# Or manually:
cd src\ME221Dashboard\SvelteApp
npm install
npm run build

dotnet build src\ME221Dashboard\ME221Dashboard.csproj -f net11.0-windows10.0.19041.0
```

For Android, you need the Android SDK installed (API 37). The project targets `net11.0-android37.0`.

## Running the emulator

You need a `calibration.json` file (exported from the app or the CLI tool):

```powershell
dotnet run --project src\ME221.Emulator -- --calibration src\ME221.Emulator\calibration.json --port 22100
```

Then connect from the app using `localhost:22100`.

## Running tests

```powershell
dotnet test ME221.slnx
```

## Architecture

The layering is pretty straightforward:

- ME221.Data is pure domain. No dependencies except Serilog. Models for calibration, tables, sensors, gauges.
- ME221.Comms is the protocol layer. Defines `IChannel` (abstract communication), `ProtocolService` (request/response, heartbeat, correlation), and 25 message types.
- ME221Dashboard.Comms adds the concrete serial channel and a zero-allocation report parser for live data.
- ME221Dashboard is the app. C# services handle ECU connection, calibration, GPS, persistence. The Svelte frontend renders gauges, tables, and connection UI. They talk through a `HybridWebView` bridge (`InvokeDotNet` / `SendRawMessage`).

The frontend uses Svelte 5 runes, Tailwind CSS, and Flowbite components. Gauges are free-form positioned on a canvas (no grid system). Table editors support 1D (curve + data grid) and 2D (heat-colored grid) tables with undo/redo, copy/paste, and batch transforms.

## License

This project is licensed under [CC BY-NC-SA 4.0](LICENSE). Free for non-commercial use. If you use this code, libraries, or any of the protocol implementation, credit the author and link back to this repository. Commercial use requires permission.

## Acknowledgements

This project was built by reverse-engineering the MEITE app and ME221 ECU protocol from Motorsport Electronics. The communication protocol, message formats, calibration file structure (`.mefw`, DEF XML), and live data streaming format were all derived from observing how the MEITE app talks to the ECU.

Motorsport Electronics manufactures the ME221 ECU hardware and develops the MEITE tuning software. Their firmware files, CAN DBC definitions, and install manuals are publicly available at [motorsport-electronics.co.uk](https://motorsport-electronics.co.uk/support/). This project would not exist without their work.

### Permission

Written permission was obtained from Motorsport Electronics to develop and distribute this software for free, non-commercial use. The license is CC BY-NC-SA 4.0. If you use this code, libraries, or protocol implementation, credit the author and link back to this repository.

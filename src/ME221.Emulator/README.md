# ME221.Emulator

TCP server that simulates a ME221 ECU. Connect from the dashboard app (or the CLI tool) without needing real hardware.

## Why this exists

Developing against a real ECU is slow. You have to be in the car, the ECU has to be powered, and if you crash the connection you might need to restart the ignition. The emulator lets you test the full communication stack from your desk.

## What it simulates

- Device info queries (product name, model, version)
- Reporting enable/disable with configurable frequency
- Live sensor data (simulated values that change over time)
- Table read/write operations
- Driver configuration reads
- Status queries
- Heartbeat responses

It does not simulate bootloader/firmware update operations or password protection.

## Usage

```powershell
dotnet run --project src\ME221.Emulator -- --calibration <path-to-calibration.json> --port 22100
```

Arguments:
- `--calibration <path>` -- calibration JSON file (required). Export this from the dashboard app.
- `--port <port>` -- TCP port to listen on (default: 22100)
- `--vehicle-config <path>` -- optional vehicle config file

## Architecture

The emulator uses a clean/hexagonal architecture:

```
Presentation/
  TcpServer.cs           Listens for TCP connections, creates a DI scope per client
  TcpClientSession.cs    Handles a single client connection
  EmulatorConsole.cs     Formatted console output

Application/
  CommandRouter.cs       Routes incoming frames to the correct handler
  ICommandHandler.cs     Handler interface
  Handlers/              SysCommandHandler, ReportingCommandHandler, TableCommandHandler,
                         DriverCommandHandler, DefaultCommandHandler
  ReportingOrchestrator.cs  Manages periodic report generation

Domain/
  EcuState.cs            Reporting state, protocol version, entity map
  EntityStore.cs         In-memory entity values
  SensorSimulator.cs     Generates realistic-ish sensor data

Infrastructure/
  CalibrationLoader.cs   Reads calibration JSON
```

Each TCP client gets its own DI scope, so concurrent connections are isolated from each other.

## Connecting from the dashboard

In the dashboard app, go to Connection, pick TCP, enter `localhost:22100`, and hit Connect. The emulator responds to the same protocol as a real ECU, so the app doesn't know the difference.

## Dependencies

- `ME221.Comms` -- protocol library (frame handling, message types)
- `ME221.Data` -- calibration models
- `Serilog` -- logging

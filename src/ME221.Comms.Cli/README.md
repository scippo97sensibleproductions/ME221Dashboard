# ME221.Comms.Cli

Command-line tool for testing protocol operations against a real ME221 ECU (or the emulator).

## What it does

Connects to an ECU over serial (with auto-discovery), sends read/write commands, and dumps the results. Useful for debugging protocol issues, testing new message types, or just poking around the ECU without writing any code.

## Usage

Basic connection and info dump:

```powershell
dotnet run --project src\ME221.Comms.Cli
```

The tool will:
1. Scan serial ports for ECU devices
2. Let you pick a port
3. Connect (tries primary baud rate, falls back to secondary)
4. Load `calibration.json` if present
5. Query device info, drivers, tables
6. Write results to JSON files

Stream mode (live data):

```powershell
dotnet run --project src\ME221.Comms.Cli -- --stream
```

This enables reporting and displays live ECU values in real-time.

## Connecting to the emulator

```powershell
dotnet run --project src\ME221.Comms.Cli -- --tcp localhost:22100
```

First start the emulator:

```powershell
dotnet run --project src\ME221.Emulator -- --calibration calibration.json --port 22100
```

## Command structure

The CLI uses System.CommandLine for argument parsing. Commands are split into:

- Read commands: device info, drivers, tables, reporting status, logging, firmware info
- Write commands: table writes, driver config writes (executed before reporting to avoid serial bus contention)
- Stream mode: continuous reporting with live value display

## Output

Results are written as JSON files in a `results/` directory. Each file contains a success/failure count and the raw response data.

## Dependencies

- ME221.Comms: protocol library
- ME221.Data: calibration models
- ME221Dashboard.Comms: serial channel
- Microsoft.Extensions.Hosting: DI and configuration
- System.CommandLine: CLI argument parsing
- Serilog: logging (console + rolling file)

## Configuration

`appsettings.json` contains Serilog configuration and default CLI settings. You can override the baud rate, port, and other connection parameters via command-line flags.

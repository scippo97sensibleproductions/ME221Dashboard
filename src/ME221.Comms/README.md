# ME221.Comms

Protocol library for talking to ME221 ECUs over serial, USB, or TCP.

## What it does

Handles the wire-level stuff: framing, CRC-16 checksums, request/response correlation, and a heartbeat timer. You give it a channel (any implementation of `IChannel`), and it gives you typed async message send/receive.

The 25 message types cover everything the ME221 supports: device info, reporting (live data streaming), table read/write, driver configuration, firmware update, logging, and password protection.

## Core concepts

### IChannel

The abstraction for sending and receiving bytes. The library doesn't care how you connect. TCP, serial, USB, a mock for testing, whatever. Implement `IChannel` and pass it to `ProtocolService`.

```csharp
public interface IChannel : IAsyncDisposable
{
    ValueTask OpenAsync(CancellationToken ct = default);
    IAsyncEnumerable<MessageFrame> IncomingFrames { get; }
    ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    DeviceStatus Status { get; }
    ValueTask CloseAsync();
}
```

### ProtocolService

The main entry point. Owns the channel, runs a background receive loop, correlates requests with responses, and manages a heartbeat (1.5 second interval, 3 missed beats = disconnect).

```csharp
var protocol = new ProtocolService(channel, logger);

// Send a request and get a typed response
var response = await protocol.SendAsync<DeviceInformationResponse>(
    new DeviceInformationRequest(), cancellationToken);
```

### RequestCorrelator

Pairs outgoing requests with incoming responses using message IDs. Thread-safe. Used internally by `ProtocolService`.

### Heartbeat

Sends periodic keepalive messages to the ECU. If three consecutive heartbeats go unanswered, the connection is considered dead and `DeviceStatus` transitions to `WaitingToReconnect`. The heartbeat can be paused during Android I/O interruptions (USB permission dialogs, etc.).

## Message types

All in `src/ME221.Comms/Messages/`:

- **Reporting**: V1/V2 live data streaming (enable/disable, parse entity values)
- **Tables**: Read, write, enable/disable tuning tables
- **Drivers**: Read/write ECU driver configuration
- **Bootloader / Firmware**: Firmware update commands
- **Device Info**: Product name, model, version
- **Status**: ECU status queries
- **Hash**: Firmware hash verification
- **Logging**: ECU-side logging control
- **Password**: Password protection commands
- **Trigger Logger**: Trigger logger channel/sync commands

## Building

Targets `net11.0`. Only dependency is Serilog.

```powershell
dotnet build src\ME221.Comms\ME221.Comms.csproj
```

## Testing

```powershell
dotnet test tests\ME221.Comms.Tests\
```

The test project covers CRC computation, frame building/parsing, protocol service behavior, and has mock channels for offline testing.

## Internals visibility

`InternalsVisibleTo` is set for `ME221.Emulator`, `ME221.Comms.Tests`, and `ME221.Benchmarks`. If you're writing a new project that needs internal access, add it to the `InternalsVisibleTo` list in the csproj.

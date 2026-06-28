# ME221Dashboard.Comms

Thin layer on top of `ME221.Comms` that adds the concrete serial port channel and a few dashboard-specific utilities.

## What's in here

### SerialPortChannel

An `IChannel` implementation that talks over `System.IO.Ports.SerialPort`. Handles open/close/send with an async receive loop, extracts frames via `FrameBuffer`, and tracks connection state.

Defaults: 230400 baud, 8 data bits, no parity, 1 stop bit, no handshake. All configurable through `ChannelOptions`.

### ReportParser

Zero-allocation parser for V2 live data reports from the ECU. Reads the status byte, then sequential entity values (float32, int16, uint16, int8, uint8, bool). Fills a pre-allocated `ReportEntity[]` buffer without any heap allocation per parse.

This matters because live data comes in at ~25-30fps. Every allocation per frame adds GC pressure.

### PendingIdTracker

Deduplication tracker for entity IDs that need UI notification. Uses a `HashSet<int>` behind the scenes, exposes a zero-copy `ReadOnlyMemory<int>` for batch dispatch. The `LiveDataService` uses this to batch entity IDs between 40ms throttle intervals.

### ChannelOptions

Simple POCO for serial port configuration: port name, baud rate, read/write timeouts.

## Usage

```csharp
var options = new ChannelOptions
{
    PortName = "COM3",
    BaudRate = 230400,
    ReadTimeout = TimeSpan.FromSeconds(2)
};

var channel = new SerialPortChannel(options, logger);
var protocol = new ProtocolService(channel, logger);

await channel.OpenAsync();
```

## Building

Targets `net11.0`. Depends on `ME221.Comms`, `System.IO.Ports`, and Serilog.

```powershell
dotnet build src\ME221Dashboard.Comms\ME221Dashboard.Comms.csproj
```

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using ME221.Comms;
using ME221.Comms.Channels;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using ME221.Data.Infrastructure;
using ME221.Data.Models;
using ME221Dashboard.Comms;

var logPath = "tests/ME221.Comms.IntegrationTests/ecu-dump.txt";
await using var logFile = new StreamWriter(logPath, false, Encoding.UTF8) { AutoFlush = true };
var origOut = Console.Out;
Console.SetOut(logFile);

static string SpanToHex(ReadOnlySpan<byte> span, int maxBytes = 200)
{
    var sb = new StringBuilder(Math.Min(span.Length, maxBytes) * 3);
    for (var i = 0; i < Math.Min(span.Length, maxBytes); i++)
    {
        if (i > 0) sb.Append(' ');
        sb.Append(span[i].ToString("X2"));
    }
    if (span.Length > maxBytes) sb.Append($"... ({span.Length} total)");
    return sb.ToString();
}

const string ConfigPath = "tests/ME221.Comms.IntegrationTests/TestConfig.json";
const string CalPath = "src/ME221.Emulator/calibration.json";

// Load config
var configJson = await File.ReadAllTextAsync(ConfigPath);
var config = JsonSerializer.Deserialize<JsonElement>(configJson);
var portName = config.GetProperty("PortName").GetString() ?? "COM3";
var baudRate = config.GetProperty("BaudRate").GetInt32();

Console.WriteLine($"Connecting to {portName} at {baudRate} baud...");

// Connect
var serialChannel = new SerialPortChannel(new ChannelOptions
{
    PortName = portName,
    BaudRate = baudRate,
    SendTimeoutMs = 5000,
    ReceiveTimeoutMs = 5000,
});

// Logging wrapper — logs every raw TX frame and every raw RX frame
await using var channel = new LoggingChannel(serialChannel, logFile);
await using var protocol = new ProtocolService(channel);
await protocol.OpenAsync();
Console.WriteLine("Connected.");

// Drain any stale frames from previous sessions
try
{
    var drainCts = new CancellationTokenSource(500);
    var drainCount = 0;
    await foreach (var stale in protocol.UncorrelatedFrames(drainCts.Token))
    {
        drainCount++;
        Console.WriteLine($"Drained stale frame: type=0x{stale.Type:X2} class=0x{stale.Class:X2} cmd=0x{stale.Command:X2} len={stale.Payload.Length}");
    }
    if (drainCount > 0)
        Console.WriteLine($"Drained {drainCount} stale frames.");
    else
        Console.WriteLine("No stale frames.");
}
catch (OperationCanceledException) { Console.WriteLine("Drain: no stale frames (timeout)."); }
catch (Exception ex) { Console.WriteLine($"Drain error: {ex.Message}"); }

// Background logger for uncorrelated frames during testing
var uncorrCts = new CancellationTokenSource();
var uncorrCount = 0;
_ = Task.Run(async () =>
{
    try
    {
        await foreach (var frame in protocol.UncorrelatedFrames(uncorrCts.Token))
        {
            Interlocked.Increment(ref uncorrCount);
            Console.WriteLine($"UNCORRELATED: type=0x{frame.Type:X2} class=0x{frame.Class:X2} cmd=0x{frame.Command:X2} len={frame.Payload.Length}");
        }
    }
    catch (OperationCanceledException) { }
});

// Load calibration
CalibrationData? calibration = null;
if (File.Exists(CalPath))
{
    var calJson = await File.ReadAllTextAsync(CalPath);
    calibration = JsonSerializer.Deserialize<CalibrationData>(calJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    Console.WriteLine($"Calibration loaded: {calibration?.Tables.Count ?? 0} tables, {calibration?.Drivers.Count ?? 0} drivers");
}

// ─── ECU Info ──────────────────────────────────────────────────────────────
Console.WriteLine("\n=== ECU Info ===");
var ecuInfo = await protocol.SendAsync<GetEcuInfoResponse>(new GetEcuInfoRequest());
Console.WriteLine($"Status: {ecuInfo.Status}");
Console.WriteLine($"Product: {ecuInfo.ProductName}");
Console.WriteLine($"Version: {ecuInfo.Version}");

// Disable reporting — fire-and-forget (ECU does not respond to SetState(false))
try
{
    await protocol.SendAsync(new SetStateRequest(false));
    Console.WriteLine("Reporting disable sent (fire-and-forget).");
}
catch (Exception ex)
{
    Console.WriteLine($"Disable reporting: {ex.GetType().Name}: {ex.Message}");
}

// ─── ECU Entity Map (via GetHash DETAILED) ───────────────────────────────
Console.WriteLine("\n=== ECU Entity Map ===");
try
{
    var hashResponse = await protocol.SendAsync<GetHashResponse>(new GetHashRequest(HashRequestMode.Detailed));
    Console.WriteLine($"Status: {hashResponse.Status}");
    Console.WriteLine($"Mode: {hashResponse.Mode}");
    Console.WriteLine($"Overall hash: 0x{hashResponse.OverallHash:X4}");
    Console.WriteLine($"Entity count: {hashResponse.Hashes.Count}");

    // Classify entities by ID range (ME221 convention: 0x1xxx=tables, 0x2xxx=drivers, 0x3xxx=links)
    var ecuTables = hashResponse.Hashes.Where(h => h.Key >= 0x1000 && h.Key < 0x2000).OrderBy(h => h.Key).ToList();
    var ecuDrivers = hashResponse.Hashes.Where(h => h.Key >= 0x2000 && h.Key < 0x3000).OrderBy(h => h.Key).ToList();
    var ecuLinks = hashResponse.Hashes.Where(h => h.Key >= 0x3000 && h.Key < 0x4000).OrderBy(h => h.Key).ToList();
    var other = hashResponse.Hashes.Where(h => h.Key < 0x1000 || h.Key >= 0x4000).OrderBy(h => h.Key).ToList();

    Console.WriteLine($"\n  Tables ({ecuTables.Count}):");
    foreach (var (id, hash) in ecuTables)
    {
        var inCal = calibration?.Tables.Any(t => t.Id == id) == true;
        Console.WriteLine($"    0x{id:X4} hash=0x{hash:X4}{(inCal ? "" : " [NOT IN CAL]")}");
    }

    Console.WriteLine($"\n  Drivers ({ecuDrivers.Count}):");
    foreach (var (id, hash) in ecuDrivers)
    {
        var inCal = calibration?.Drivers.Any(d => d.Id == id) == true;
        Console.WriteLine($"    0x{id:X4} hash=0x{hash:X4}{(inCal ? "" : " [NOT IN CAL]")}");
    }

    if (ecuLinks.Count > 0)
    {
        Console.WriteLine($"\n  Data Links ({ecuLinks.Count}):");
        foreach (var (id, hash) in ecuLinks)
            Console.WriteLine($"    0x{id:X4} hash=0x{hash:X4}");
    }

    if (other.Count > 0)
    {
        Console.WriteLine($"\n  Other ({other.Count}):");
        foreach (var (id, hash) in other)
            Console.WriteLine($"    0x{id:X4} hash=0x{hash:X4}");
    }

    var missingFromCal = hashResponse.Hashes.Keys
        .Where(id => calibration?.Tables.All(t => t.Id != id) == true
                  && calibration?.Drivers.All(d => d.Id != id) == true)
        .OrderBy(id => id).ToList();
    if (missingFromCal.Count > 0)
    {
        Console.WriteLine($"\n  IDs in ECU but NOT in calibration DEF ({missingFromCal.Count}):");
        foreach (var id in missingFromCal)
            Console.WriteLine($"    0x{id:X4}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"GetHash failed: {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException is not null)
        Console.WriteLine($"  Inner: {ex.InnerException.Message}");
}

// ─── Dump Tables ───────────────────────────────────────────────────────────
Console.WriteLine("\n=== Tables ===");
var tables = calibration?.Tables.Where(t => t.ViewInTree).ToList() ?? [];
foreach (var table in tables)
{
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var response = await protocol.SendAsync<GetTableResponse>(new GetTableRequest(table.Id), cts.Token);
        if (response.Status != MessageStatus.Success)
        {
            Console.WriteLine($"FAIL: {table.Name} (0x{table.Id:X4}) — Status: {response.Status}");
            continue;
        }

        var raw = response.SerializedTable.Span;
        Console.WriteLine($"\n--- {table.Name} (0x{table.Id:X4}) ---");
        Console.WriteLine($"  Calibrated as: {table.Rows}×{table.Cols} ({table.TableType})");
        Console.WriteLine($"  SerializedTable.Length = {raw.Length} bytes");
        Console.WriteLine($"  Full response payload ({response.Payload.Length} bytes): {SpanToHex(response.Payload.Span)}");
        Console.WriteLine($"  Raw bytes [{Math.Min(raw.Length, 20)}]: {SpanToHex(raw)}");

        if (raw.Length >= 4)
        {
            var typeByte = raw[0];
            var enabled = raw[1] != 0;
            var rows = raw[2];
            var cols = raw[3];
            Console.WriteLine($"  Wire: type={typeByte}, enabled={enabled}, rows={rows}, cols={cols}");

            if (rows != table.Rows || cols != table.Cols)
                Console.WriteLine($"  *** DIMENSION MISMATCH: wire={rows}×{cols} cal={table.Rows}×{table.Cols} ***");

            // Deserialize if dimensions match
            if (rows == table.Rows && cols == table.Cols)
            {
                var wire = TableSerializer.Deserialize(table, raw);
                Console.WriteLine($"  Input0[0..3]: [{string.Join(", ", wire.Input0.Take(4).Select(v => v.ToString("F2")))}]");
                Console.WriteLine($"  Output[0..3]: [{string.Join(", ", wire.Output.Take(4).Select(v => v.ToString("F2")))}]");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {table.Name} (0x{table.Id:X4}) — {ex.GetType().Name}: {ex.Message}");
        if (ex.InnerException is not null)
            Console.WriteLine($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
    }
}

// ─── Dump Drivers ──────────────────────────────────────────────────────────
Console.WriteLine("\n=== Drivers ===");
var drivers = calibration?.Drivers.Where(d => d.ViewInTree).ToList() ?? [];
foreach (var driver in drivers)
{
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var response = await protocol.SendAsync<GetDriverResponse>(new GetDriverRequest(driver.Id), cts.Token);
        if (response.Status != MessageStatus.Success)
        {
            Console.WriteLine($"FAIL: {driver.Name} (0x{driver.Id:X4}) — Status: {response.Status}");
            continue;
        }

        var raw = response.SerializedDriver.Span;
        var fullPayload = response.Payload.Span;
        Console.WriteLine($"\n--- {driver.Name} (0x{driver.Id:X4}) ---");
        Console.WriteLine($"  Calibrated as: {driver.NumberOfConfigs} configs, {driver.NumberOfOutputs} outputs, {driver.NumberOfInputs} inputs");
        Console.WriteLine($"  Full response payload ({fullPayload.Length} bytes): {SpanToHex(fullPayload)}");
        Console.WriteLine($"  SerializedDriver.Length = {raw.Length} bytes");
        Console.WriteLine($"  Raw bytes: {SpanToHex(raw)}");

        if (raw.Length >= 3)
        {
            var configCount = raw[0];
            var outputCount = raw[1];
            var inputCount = raw[2];
            Console.WriteLine($"  Header: configs={configCount}, outputs={outputCount}, inputs={inputCount}");

            if (configCount != driver.NumberOfConfigs)
                Console.WriteLine($"  *** CONFIG COUNT MISMATCH: wire={configCount} cal={driver.NumberOfConfigs} ***");
            if (outputCount != driver.NumberOfOutputs)
                Console.WriteLine($"  *** OUTPUT COUNT MISMATCH: wire={outputCount} cal={driver.NumberOfOutputs} ***");
            if (inputCount != driver.NumberOfInputs)
                Console.WriteLine($"  *** INPUT COUNT MISMATCH: wire={inputCount} cal={driver.NumberOfInputs} ***");
        }

        try
        {
            var wire = DriverSerializer.Deserialize(raw);
            Console.WriteLine($"  Deserialized: {wire.Configs.Length} configs, {wire.OutputIds.Count} outputs, {wire.InputIds.Count} inputs");
            var calMatch = wire.Configs.Length == driver.NumberOfConfigs
                && wire.OutputIds.Count == driver.NumberOfOutputs
                && wire.InputIds.Count == driver.NumberOfInputs;
            Console.WriteLine($"  Matches calibration: {(calMatch ? "YES" : "NO")}");
            if (wire.Configs.Length > 0)
                Console.WriteLine($"  Configs: [{string.Join(", ", wire.Configs.Select(v => v.ToString("F2")))}]");
            if (wire.OutputIds.Count > 0)
                Console.WriteLine($"  OutputIds: [{string.Join(", ", wire.OutputIds.Select(id => $"0x{id:X4}"))}]");
            if (wire.InputIds.Count > 0)
                Console.WriteLine($"  InputIds: [{string.Join(", ", wire.InputIds.Select(id => $"0x{id:X4}"))}]");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Deserialization FAILED: {ex.Message}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {driver.Name} (0x{driver.Id:X4}) — {ex.GetType().Name}: {ex.Message}");
        if (ex.InnerException is not null)
            Console.WriteLine($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
    }
}

Console.WriteLine("\n=== Done ===");
Console.WriteLine($"Uncorrelated frames during test: {uncorrCount}");
uncorrCts.Cancel();
logFile.Flush();
Console.SetOut(origOut);
Console.WriteLine($"Full dump saved to: {Path.GetFullPath(logPath)}");

// ─── Logging Channel Wrapper ───────────────────────────────────────────────
sealed class LoggingChannel(IChannel inner, StreamWriter log) : IChannel
{
    private int _counter;
    public bool IsOpen => inner.IsOpen;
    public DeviceStatus Status => inner.Status;
    public IAsyncEnumerable<MessageFrame> IncomingFrames => WrapIncoming(inner.IncomingFrames);

    private async IAsyncEnumerable<MessageFrame> WrapIncoming(
        IAsyncEnumerable<MessageFrame> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var frame in source.WithCancellation(ct))
        {
            var seq = Interlocked.Increment(ref _counter);
            var hex = HexDump(frame.Payload.Span);
            await log.WriteLineAsync($"RX #{seq} [{frame.Payload.Length}B] type=0x{frame.Type:X2} class=0x{frame.Class:X2} cmd=0x{frame.Command:X2}: {hex}");
            await log.FlushAsync();
            yield return frame;
        }
    }

    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        var seq = Interlocked.Increment(ref _counter);
        var hex = HexDump(frame.Span);
        await log.WriteLineAsync($"TX #{seq} [{frame.Length}B]: {hex}");
        await log.FlushAsync();
        await inner.SendAsync(frame, cancellationToken);
    }

    public Task OpenAsync(CancellationToken cancellationToken = default)
        => inner.OpenAsync(cancellationToken);

    public Task CloseAsync(CancellationToken cancellationToken = default)
        => inner.CloseAsync(cancellationToken);

    public ValueTask DisposeAsync()
        => inner.DisposeAsync();

    private static string HexDump(ReadOnlySpan<byte> span, int maxBytes = 200)
    {
        var sb = new StringBuilder(Math.Min(span.Length, maxBytes) * 3);
        for (var i = 0; i < Math.Min(span.Length, maxBytes); i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(span[i].ToString("X2"));
        }
        if (span.Length > maxBytes) sb.Append($"... ({span.Length} total)");
        return sb.ToString();
    }
}

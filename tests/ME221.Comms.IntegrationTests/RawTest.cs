using System.Buffers.Binary;
using System.Text;
using ME221.Comms;
using ME221.Comms.Channels;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using ME221Dashboard.Comms;

const string PortName = "COM3";
const int BaudRate = 230400;

static string Hex(ReadOnlySpan<byte> s, int max = 200)
{
    var sb = new StringBuilder(Math.Min(s.Length, max) * 3);
    for (var i = 0; i < Math.Min(s.Length, max); i++)
    {
        if (i > 0) sb.Append(' ');
        sb.Append(s[i].ToString("X2"));
    }
    if (s.Length > max) sb.Append($"...+{s.Length - max}B");
    return sb.ToString();
}

Console.WriteLine($"Connecting to {PortName} at {BaudRate}...");

await using var inner = new SerialPortChannel(new ChannelOptions
{
    PortName = PortName,
    BaudRate = BaudRate,
    SendTimeoutMs = 5000,
    ReceiveTimeoutMs = 5000,
});

await using var channel = new LoggingChannel(inner);
await using var protocol = new ProtocolService(channel);
await protocol.OpenAsync();
Console.WriteLine("Connected.");

// TEST 1: Read 0x1000 (known good) three times
for (var i = 0; i < 3; i++)
{
    Console.Write($"\n[TEST1] Read 0x1000 attempt {i+1}... ");
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var r = await protocol.SendAsync<GetTableResponse>(new GetTableRequest(0x1000), cts.Token);
        Console.WriteLine($"OK id=0x{r.TableId:X4} {r.SerializedTable.Length}B");
    }
    catch (Exception ex) { Console.WriteLine($"FAIL: {ex.GetType().Name}"); }
}

// TEST 2: Read 0x1004 (known flaky) five times alone
for (var i = 0; i < 5; i++)
{
    Console.Write($"\n[TEST2] Read 0x1004 attempt {i+1}... ");
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var r = await protocol.SendAsync<GetTableResponse>(new GetTableRequest(0x1004), cts.Token);
        Console.WriteLine($"OK id=0x{r.TableId:X4} {r.SerializedTable.Length}B");
    }
    catch (Exception ex) { Console.WriteLine($"FAIL: {ex.GetType().Name}"); }
}

// TEST 3: Read 0x1000 then 0x1004 back-to-back, 5 times
for (var i = 0; i < 5; i++)
{
    Console.Write($"\n[TEST3-{i+1}] 0x1000... ");
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var r = await protocol.SendAsync<GetTableResponse>(new GetTableRequest(0x1000), cts.Token);
        Console.Write($"OK ");
    }
    catch (Exception ex) { Console.Write($"FAIL({ex.GetType().Name}) "); }

    Console.Write($"0x1004... ");
    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var r = await protocol.SendAsync<GetTableResponse>(new GetTableRequest(0x1004), cts.Token);
        Console.WriteLine($"OK id=0x{r.TableId:X4} {r.SerializedTable.Length}B");
    }
    catch (Exception ex) { Console.WriteLine($"FAIL({ex.GetType().Name})"); }
}

// TEST 4: Read 0x1000, then 0x1004, then 0x1005 — the full sequence
Console.Write("\n[TEST4] 0x1000... ");
try { using var c = new CancellationTokenSource(TimeSpan.FromSeconds(3)); var r = await protocol.SendAsync<GetTableResponse>(new GetTableRequest(0x1000), c.Token); Console.Write($"OK "); }
catch { Console.Write("FAIL "); }
Console.Write("0x1004... ");
try { using var c = new CancellationTokenSource(TimeSpan.FromSeconds(3)); var r = await protocol.SendAsync<GetTableResponse>(new GetTableRequest(0x1004), c.Token); Console.Write($"OK "); }
catch { Console.Write("FAIL "); }
Console.Write("0x1005... ");
try { using var c = new CancellationTokenSource(TimeSpan.FromSeconds(3)); var r = await protocol.SendAsync<GetTableResponse>(new GetTableRequest(0x1005), c.Token); Console.WriteLine($"OK id=0x{r.TableId:X4}"); }
catch (Exception ex) { Console.WriteLine($"FAIL({ex.GetType().Name})"); }

Console.WriteLine("\nDone.");

// ─── Logging Channel ──────────────────────────────────────────────────────
sealed class LoggingChannel(IChannel inner) : IChannel
{
    private int _seq;
    public bool IsOpen => inner.IsOpen;
    public DeviceStatus Status => inner.Status;
    public IAsyncEnumerable<MessageFrame> IncomingFrames => Wrap(inner.IncomingFrames);

    private async IAsyncEnumerable<MessageFrame> Wrap(IAsyncEnumerable<MessageFrame> src,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var f in src.WithCancellation(ct))
        {
            var s = Interlocked.Increment(ref _seq);
            Console.WriteLine($"  RX #{s} [{f.Payload.Length}B] cls=0x{f.Class:X2} cmd=0x{f.Command:X2}: {Dump(f.Payload.Span)}");
            yield return f;
        }
    }

    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
    {
        var s = Interlocked.Increment(ref _seq);
        Console.WriteLine($"  TX #{s} [{frame.Length}B]: {Dump(frame.Span)}");
        await inner.SendAsync(frame, ct);
    }

    public Task OpenAsync(CancellationToken ct) => inner.OpenAsync(ct);
    public Task CloseAsync(CancellationToken ct) => inner.CloseAsync(ct);
    public ValueTask DisposeAsync() => inner.DisposeAsync();

    static string Dump(ReadOnlySpan<byte> s, int max = 200)
    {
        var sb = new StringBuilder(Math.Min(s.Length, max) * 3);
        for (var i = 0; i < Math.Min(s.Length, max); i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(s[i].ToString("X2"));
        }
        if (s.Length > max) sb.Append($"...+{s.Length - max}B");
        return sb.ToString();
    }
}

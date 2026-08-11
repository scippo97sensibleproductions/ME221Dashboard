using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using FluentAssertions;
using ME221.Comms;
using ME221.Comms.Messages;
using ME221Dashboard.Services;
using ME221Dashboard.Services.Mirror;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ME221Dashboard.Tests.Mirror;

/// <summary>End-to-end streaming tests: real WSS server + stream service + client loopback.</summary>
public sealed class MirrorStreamTests : IAsyncLifetime
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "me221-stream-" + Guid.NewGuid().ToString("N"));
    private readonly ApprovedDeviceStore _store = new();
    private readonly MirrorCertificateProvider _certificates = new();
    private readonly MirrorServerService _server;
    private readonly FakeLiveData _liveData = new();
    private readonly LogCapture _logCapture = new();
    private MirrorStreamService _stream = null!;
    private HybridBridgeService _bridge = null!;
    private int _port;

    public MirrorStreamTests()
    {
        _store.SetBasePath(_tempDir);
        _certificates.SetBasePath(_tempDir);
        _server = new MirrorServerService(_store, _certificates, appVersion: () => "1.2.3",
            logger: NullLogger<MirrorServerService>.Instance);
    }

    public ValueTask InitializeAsync()
    {
        _port = GetFreePort();
        _server.Start(_port);

        var cal = new FakeCalibrationService { Config = new Services.DashboardConfig() };
        _bridge = TestBridge.Create(cal);
        _bridge._activeDashboardName = "default";

        _stream = new MirrorStreamService(
            _server,
            _liveData,
            _logCapture,
            new MirrorSnapshotSerializer(cal, new FakeConnectionService(),
                appVersion: () => "1.2.3", logger: NullLogger<MirrorSnapshotSerializer>.Instance),
            _bridge,
            logger: NullLogger<MirrorStreamService>.Instance);

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _stream.Dispose();
        _server.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        return ValueTask.CompletedTask;
    }

    private static int GetFreePort()
    {
        using var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return port;
    }

    private async Task<ClientWebSocket> ConnectAsync()
    {
        var client = new ClientWebSocket();
        client.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        await client.ConnectAsync(new Uri($"wss://127.0.0.1:{_port}/"), CancellationToken.None);
        return client;
    }

    private static async Task SendJsonAsync(ClientWebSocket client, object message)
    {
        var bytes = Encoding.UTF8.GetBytes(MirrorProtocol.Serialize(message));
        await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
    }

    private static async Task<string> ReceiveJsonAsync(ClientWebSocket client, int timeoutMs = 5000)
    {
        var buffer = new byte[256 * 1024];
        using var cts = new CancellationTokenSource(timeoutMs);
        var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    /// <summary>Approves the client and drains the snapshot handshake (acks every chunk, returns the snapshot seq).</summary>
    private async Task<long> CompleteHandshakeAsync(ClientWebSocket client, string token, string clientId)
    {
        await SendJsonAsync(client, new HelloMessage { AppVersion = "1.2.3", DeviceName = "Tablet", Token = token, ClientId = clientId });
        var first = MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as HelloAckMessage;
        first!.Status.Should().Be("approved");

        long seq = 0;
        var done = false;
        while (!done)
        {
            var msg = MirrorProtocol.Parse(await ReceiveJsonAsync(client));
            switch (msg)
            {
                case SnapshotChunkMessage chunk:
                    seq = chunk.Seq;
                    await SendJsonAsync(client, new SnapshotAckMessage { Index = chunk.Index });
                    break;
                case SnapshotDoneMessage snapshotDone:
                    seq = snapshotDone.Seq;
                    done = true;
                    break;
                default:
                    throw new Xunit.Sdk.XunitException($"Unexpected message during handshake: {msg?.GetType().Name}");
            }
        }
        return seq;
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SequenceAscendsAcrossSnapshotAndFramesWithNoGapsOnRecordingFeed()
    {
        var entry = await _store.AddAsync("client-1", "Tablet");
        using var client = await ConnectAsync();
        var snapshotSeq = await CompleteHandshakeAsync(client, entry.Token, "client-1");

        _liveData.Set(1, 100);
        _liveData.Set(2, 200);
        for (var i = 0; i < 5; i++)
        {
            _liveData.Raise(1, 2);
            await Task.Delay(20);
        }

        var received = new List<object>();
        using var cts = new CancellationTokenSource(5000);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && received.OfType<RecordFeedMessage>().Count() < 5)
        {
            var msg = MirrorProtocol.Parse(await ReceiveJsonAsync(client, 5000));
            received.Add(msg);
        }

        received.OfType<FrameMessage>().Should().NotBeEmpty();
        foreach (var frame in received.OfType<FrameMessage>())
        {
            frame.Seq.Should().BeGreaterThan(snapshotSeq);
            frame.Values["1"].Should().Be(100);
        }

        var records = received.OfType<RecordFeedMessage>().OrderBy(r => r.Seq).ToList();
        records.Should().HaveCount(5);
        records[0].Seq.Should().Be(snapshotSeq + 1);
        // No gaps on the recording feed:
        for (var i = 1; i < records.Count; i++)
            records[i].Seq.Should().Be(records[i - 1].Seq + 1);
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ViewerJoiningMidRecordingGetsItsOwnFullSnapshot()
    {
        var entry1 = await _store.AddAsync("client-1", "Tablet");
        var entry2 = await _store.AddAsync("client-2", "Phone");

        using var first = await ConnectAsync();
        var firstSeq = await CompleteHandshakeAsync(first, entry1.Token, "client-1");

        _liveData.Set(1, 100);
        _liveData.Raise(1);
        await Task.Delay(20);

        using var second = await ConnectAsync();
        var secondSeq = await CompleteHandshakeAsync(second, entry2.Token, "client-2");

        secondSeq.Should().BeGreaterThan(firstSeq, "each viewer's snapshot has its own monotonic base");
        MirrorSequencing.IsFrameAfterSnapshot(secondSeq + 1, secondSeq).Should().BeTrue();
    }

    // ── Error paths ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LostChunkExhaustsRetryThenClosesSession()
    {
        // The client never acks; after 3 attempts the session closes with "snapshot failed".
        var entry = await _store.AddAsync("client-1", "Tablet");
        using var client = await ConnectAsync();
        await SendJsonAsync(client, new HelloMessage { AppVersion = "1.2.3", DeviceName = "Tablet", Token = entry.Token, ClientId = "client-1" });

        var first = MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as HelloAckMessage;
        first!.Status.Should().Be("approved");

        // Drain: expect close within a few ack timeouts (3 attempts × 3s).
        var gotClose = false;
        using var cts = new CancellationTokenSource(15_000);
        try
        {
            while (true)
            {
                var msg = MirrorProtocol.Parse(await ReceiveJsonAsync(client, 15000));
                if (msg is CloseMessage close)
                {
                    close.Reason.Should().Be("snapshot failed");
                    gotClose = true;
                    break;
                }
                if (msg is SnapshotChunkMessage)
                    continue; // deliberately not acking
            }
        }
        catch (OperationCanceledException) { }
        gotClose.Should().BeTrue("the server must close the session after exhausting snapshot retries");
    }

    // ── Integration ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GpsEventsRelayInExactBridgeShape()
    {
        var entry = await _store.AddAsync("client-1", "Tablet");
        using var client = await ConnectAsync();
        await CompleteHandshakeAsync(client, entry.Token, "client-1");

        // Drive the bridge's GPS path; the EventEmitted tap relays the exact payload.
        _bridge.OnGpsLocationUpdated(null!, new GpsLocationEventArgs(1.5, 2.5, 10, 30, 90, 5));

        object? relayed = null;
        using var cts = new CancellationTokenSource(5000);
        while (relayed is null)
        {
            var msg = MirrorProtocol.Parse(await ReceiveJsonAsync(client, 5000));
            if (msg is EventMessage gpsEvt && gpsEvt.Channel == "gps")
                relayed = gpsEvt;
        }

        var evt = (EventMessage)relayed!;
        evt.Payload.Should().Contain("\"event\":\"gpsUpdate\"");
        evt.Payload.Should().Contain("\"latitude\":1.5");
        evt.Payload.Should().Contain("\"speed\":30");
    }

    [Fact]
    public async Task LogEntriesRelayInExactShapeAndCredentialMaterialIsFiltered()
    {
        var entry = await _store.AddAsync("client-1", "Tablet");
        using var client = await ConnectAsync();
        await CompleteHandshakeAsync(client, entry.Token, "client-1");

        var logger = _logCapture.CreateLogger("Mirror.Stream.Tests");
        logger.LogInformation("normal log entry {Value}", 42);
        logger.LogInformation("token leaked: {Token}", "abcdef0123456789");

        var received = new List<EventMessage>();
        using var cts = new CancellationTokenSource(5000);
        try
        {
            while (received.Count < 2)
            {
                var msg = MirrorProtocol.Parse(await ReceiveJsonAsync(client, 5000));
                if (msg is EventMessage evt && evt.Channel == "log")
                    received.Add(evt);
            }
        }
        catch (OperationCanceledException)
        {
            // Receive window expired — the filtered entry is expected to never arrive.
        }

        received.Should().ContainSingle(e => e.Payload.Contains("normal log entry"));
        received.Should().NotContain(e => e.Payload.Contains("token leaked"));
    }

    [Fact]
    public void LogRelayFilterBlocksCredentialMaterial()
    {
        MirrorStreamService.ShouldRelayLog(new LogCapture.LogEntry { Message = "companion-cert.pfx regenerated" }).Should().BeFalse();
        MirrorStreamService.ShouldRelayLog(new LogCapture.LogEntry { Message = "viewer approved", Exception = "companion-viewers.json write failed" }).Should().BeFalse();
        MirrorStreamService.ShouldRelayLog(new LogCapture.LogEntry { Message = "token abc123 issued" }).Should().BeFalse();
        MirrorStreamService.ShouldRelayLog(new LogCapture.LogEntry { Message = "ordinary log line" }).Should().BeTrue();
    }
}

/// <summary>Live-data fake with settable values and a public raise helper.</summary>
public sealed class FakeLiveData : ILiveDataService
{
    private readonly Dictionary<int, float> _values = [];

    public bool IsRunning => true;
    public IReadOnlySet<int> EntityIds => new HashSet<int>(_values.Keys);
    public float? this[int entityId] => _values.TryGetValue(entityId, out var v) ? v : null;
    public event EventHandler<EntitiesUpdatedEventArgs>? EntitiesUpdated;

    public void Set(int id, float value) => _values[id] = value;
    public void Raise(params int[] ids) =>
        EntitiesUpdated?.Invoke(this, new EntitiesUpdatedEventArgs(ids, ids.Length));

    public Task StartAsync(ProtocolService protocolService, int reportingVersion, Dictionary<ushort, (ReportingType Type, int Size)> entityMap) => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public void UpdateExternalValue(int entityId, float value) { }
}

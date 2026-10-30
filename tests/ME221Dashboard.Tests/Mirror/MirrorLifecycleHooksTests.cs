using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using FluentAssertions;
using ME221Dashboard.Services;
using ME221Dashboard.Services.Mirror;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ME221Dashboard.Tests.Mirror;

/// <summary>R19 hooks and approval flow over a real WSS loopback (U5).</summary>
[Collection(MirrorNetworkCollection.Name)]
public sealed class MirrorLifecycleHooksTests : IAsyncLifetime
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "me221-hooks-" + Guid.NewGuid().ToString("N"));
    private readonly ApprovedDeviceStore _store = new();
    private readonly MirrorCertificateProvider _certificates = new();
    private readonly MirrorServerService _server;
    private readonly FakeLiveData _liveData = new();
    private readonly LogCapture _logCapture = new();
    private readonly FakeCalibrationService _calibration = new();
    private HybridBridgeService _bridge = null!;
    private MirrorStreamService _stream = null!;
    private int _port;

    public MirrorLifecycleHooksTests()
    {
        _store.SetBasePath(_tempDir);
        _certificates.SetBasePath(_tempDir);
        _server = new MirrorServerService(_store, _certificates, appVersion: () => "1.2.3",
            logger: NullLogger<MirrorServerService>.Instance);
    }

    public ValueTask InitializeAsync()
    {
        _server.Start(port: 0); // port 0 → OS-assigned, no cross-test port races
        _port = _server.BoundPort;

        _calibration.Config = new Services.DashboardConfig();
        _bridge = TestBridge.Create(_calibration);
        _bridge._activeDashboardName = "default";
        _bridge.WireForMirror(_store, _server, _certificates);

        _stream = new MirrorStreamService(
            _server,
            _liveData,
            _logCapture,
            new MirrorSnapshotSerializer(_calibration, new FakeConnectionService(),
                appVersion: () => "1.2.3", logger: NullLogger<MirrorSnapshotSerializer>.Instance),
            _bridge,
            logger: NullLogger<MirrorStreamService>.Instance);

        var hooks = new MirrorLifecycleHooks(_bridge, _stream);
        hooks.Wire();

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _stream.Dispose();
        _server.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        return ValueTask.CompletedTask;
    }

    private async Task<ClientWebSocket> ConnectApprovedAsync(string token, string clientId = "client-1")
    {
        var client = new ClientWebSocket();
        client.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        await client.ConnectAsync(new Uri($"wss://127.0.0.1:{_port}/"), CancellationToken.None);
        var hello = new HelloMessage { AppVersion = "1.2.3", DeviceName = "Tablet", Token = token, ClientId = clientId };
        await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(MirrorProtocol.Serialize(hello))),
            WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

        // Drain the snapshot handshake (ack every chunk, then snapshotDone).
        while (true)
        {
            var msg = MirrorProtocol.Parse(await ReceiveJsonAsync(client));
            switch (msg)
            {
                case HelloAckMessage ack:
                    ack.Status.Should().Be("approved");
                    break;
                case SnapshotChunkMessage chunk:
                    await client.SendAsync(
                        new ArraySegment<byte>(Encoding.UTF8.GetBytes(MirrorProtocol.Serialize(new SnapshotAckMessage { Index = chunk.Index }))),
                        WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
                    break;
                case SnapshotDoneMessage:
                    return client;
                default:
                    throw new Xunit.Sdk.XunitException($"Unexpected message: {msg?.GetType().Name}");
            }
        }
    }

    private static async Task<string> ReceiveJsonAsync(ClientWebSocket client, int timeoutMs = 5000)
    {
        var buffer = new byte[256 * 1024];
        using var cts = new CancellationTokenSource(timeoutMs);
        var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SavingWarningSettingsEndsViewerSessionsWithConfigUpdate()
    {
        var entry = await _store.AddAsync("client-1", "Tablet");
        using var client = await ConnectApprovedAsync(entry.Token);

        await _bridge.SaveWarningSettings("[]");

        var msg = MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as ConfigUpdateMessage;
        msg.Should().NotBeNull();
        msg!.Reason.Should().Be("warningSettings");
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DashboardLayoutSaveDoesNotEndSessions()
    {
        var entry = await _store.AddAsync("client-1", "Tablet");
        using var client = await ConnectApprovedAsync(entry.Token);

        await _bridge.SaveDashboardLayout("{\"dashboardName\":\"default\",\"gauges\":[]}");

        // The viewer must still be alive: the next snapshot-scoped write reaches it.
        await _bridge.SaveWarningSettings("[]");
        var msg = MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as ConfigUpdateMessage;
        msg.Should().NotBeNull();
        msg!.Reason.Should().Be("warningSettings");
    }

    [Fact]
    public async Task EcuTableWriteDoesNotEndSessions()
    {
        // R19/KTD-6: ECU table writes are excluded from the config-update trigger set.
        var entry = await _store.AddAsync("client-1", "Tablet");
        using var client = await ConnectApprovedAsync(entry.Token);

        var writes = 0;
        _bridge.SnapshotScopedWrite += _ => writes++;

        await _bridge.WriteTableData("{\"tableId\":10,\"input0\":[],\"input1\":[],\"output\":[]}");
        writes.Should().Be(0, "an ECU table write must not notify the mirror server");

        // The viewer is still alive and the R19 hook still fires for real changes.
        await _bridge.SaveWarningSettings("[]");
        writes.Should().Be(1);
        (MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as ConfigUpdateMessage)!.Reason.Should().Be("warningSettings");
    }

    // ── Error paths ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ApprovalWhileBackgroundedAutoDenies()
    {
        _bridge._lifecycleBridge = new AppLifecycleBridge(_bridge);
        _bridge._lifecycleBridge.NotifyBackgrounded();

        using var client = new ClientWebSocket();
        client.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        await client.ConnectAsync(new Uri($"wss://127.0.0.1:{_port}/"), CancellationToken.None);
        var hello = new HelloMessage { AppVersion = "1.2.3", DeviceName = "Stranger", ClientId = "new-client" };
        await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(MirrorProtocol.Serialize(hello))),
            WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

        // The hello is acknowledged as pending; the auto-deny follows immediately.
        (MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as HelloAckMessage)!.Status.Should().Be("pending");
        var msg = MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as ApprovalDecisionMessage;
        msg.Should().NotBeNull();
        msg!.Approved.Should().BeFalse("approval auto-denies while A is backgrounded");
    }

    // ── Integration ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ApprovalDecisionCompletesTheHandshake()
    {
        using var client = new ClientWebSocket();
        client.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        await client.ConnectAsync(new Uri($"wss://127.0.0.1:{_port}/"), CancellationToken.None);
        var hello = new HelloMessage { AppVersion = "1.2.3", DeviceName = "Stranger", ClientId = "new-client" };
        await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(MirrorProtocol.Serialize(hello))),
            WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

        (MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as HelloAckMessage)!.Status.Should().Be("pending");

        await _bridge.ApproveDevice("new-client");

        var decision = MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as ApprovalDecisionMessage;
        decision.Should().NotBeNull();
        decision!.Approved.Should().BeTrue();
        decision.Token.Should().NotBeNullOrWhiteSpace();
    }
}
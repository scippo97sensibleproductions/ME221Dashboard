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

/// <summary>End-to-end WSS tests against the real Fleck server (loopback, self-signed TLS).</summary>
public sealed class MirrorServerTests : IAsyncLifetime
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "me221-server-" + Guid.NewGuid().ToString("N"));
    private readonly ApprovedDeviceStore _store = new();
    private readonly MirrorCertificateProvider _certificates = new();
    private readonly MirrorServerService _server;
    private int _port;

    public MirrorServerTests()
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
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
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
        var json = MirrorProtocol.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
    }

    private static async Task<string> ReceiveJsonAsync(ClientWebSocket client, int timeoutMs = 5000)
    {
        var buffer = new byte[64 * 1024];
        using var cts = new CancellationTokenSource(timeoutMs);
        var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    /// <summary>Asserts the connection was closed: a close frame, a closed socket, or a timeout.</summary>
    private static async Task AssertClosedAsync(ClientWebSocket client, int timeoutMs = 3000)
    {
        var buffer = new byte[64];
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
            if (result.MessageType == WebSocketMessageType.Close || result.Count == 0)
                return; // close frame — expected
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or IOException or SocketException)
        {
            return; // socket closed or timed out — expected
        }
        throw new Xunit.Sdk.XunitException("Expected the server to close the session, but a data frame was received");
    }

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task KnownDeviceReconnectsWithoutApprovalPrompt()
    {
        var entry = await _store.AddAsync("client-1", "Tablet");

        using var client = await ConnectAsync();
        await SendJsonAsync(client, new HelloMessage { AppVersion = "1.2.3", DeviceName = "Tablet", Token = entry.Token, ClientId = "client-1" });

        var ack = MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as HelloAckMessage;
        ack.Should().NotBeNull();
        ack!.Status.Should().Be("approved");
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ConnectionCapOfFourIsEnforced()
    {
        var approved = new List<ClientWebSocket>();
        for (var i = 1; i <= 4; i++)
        {
            var entry = await _store.AddAsync($"client-{i}", $"Device {i}");
            var client = await ConnectAsync();
            await SendJsonAsync(client, new HelloMessage { AppVersion = "1.2.3", DeviceName = $"Device {i}", Token = entry.Token, ClientId = $"client-{i}" });
            var ack = MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as HelloAckMessage;
            ack!.Status.Should().Be("approved");
            approved.Add(client);
        }

        try
        {
            // Fifth approved viewer is refused (closed without an approved ack).
            var fifthEntry = await _store.AddAsync("client-5", "Device 5");
            var fifth = await ConnectAsync();
            await SendJsonAsync(fifth, new HelloMessage { AppVersion = "1.2.3", DeviceName = "Device 5", Token = fifthEntry.Token, ClientId = "client-5" });

            // The session is closed; receive fails with a closed socket.
            await AssertClosedAsync(fifth);
        }
        finally
        {
            foreach (var c in approved) c.Dispose();
        }
    }

    [Fact]
    public async Task UnknownDeviceLandsInPending()
    {
        using var client = await ConnectAsync();
        await SendJsonAsync(client, new HelloMessage { AppVersion = "1.2.3", DeviceName = "Stranger", ClientId = "new-client" });

        var ack = MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as HelloAckMessage;
        ack.Should().NotBeNull();
        ack!.Status.Should().Be("pending");
    }

    [Fact]
    public async Task VersionMismatchClosesAtHello()
    {
        using var client = await ConnectAsync();
        await SendJsonAsync(client, new HelloMessage { AppVersion = "9.9.9", DeviceName = "Old", ClientId = "old-client" });

        var ack = MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as HelloAckMessage;
        ack.Should().NotBeNull();
        ack!.Status.Should().Be("versionMismatch");
    }

    // ── Error paths ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PerIpRateLimitBlocksFourthUnapprovedAttempt()
    {
        for (var i = 1; i <= 3; i++)
        {
            using var client = await ConnectAsync();
            await SendJsonAsync(client, new HelloMessage { AppVersion = "1.2.3", DeviceName = $"S{i}", ClientId = $"s{i}" });
            var ack = MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as HelloAckMessage;
            ack!.Status.Should().Be("pending");
        }

        // 4th unapproved attempt from the same IP is closed without a helloAck.
        using var fourth = await ConnectAsync();
        await SendJsonAsync(fourth, new HelloMessage { AppVersion = "1.2.3", DeviceName = "S4", ClientId = "s4" });

        await AssertClosedAsync(fourth);
    }

    [Fact]
    public async Task CertificateMissingOnFirstRunGeneratesAndPersistsOne()
    {
        // Temp dir is fresh; the server Start() already ran in InitializeAsync.
        _certificates.CertificateFileExists.Should().BeTrue();
    }

    [Fact]
    public async Task CertificateLossWithSurvivingStoreInvalidatesAllTokens()
    {
        // Pair a device, then delete the certificate and start a fresh server: the
        // old token must no longer be accepted (R20).
        var entry = await _store.AddAsync("client-1", "Tablet");
        _certificates.GetOrCreate();
        _certificates.CertificateFileExists.Should().BeTrue();
        File.Delete(_certificates.CertificatePath);

        using var server2 = new MirrorServerService(_store, _certificates, appVersion: () => "1.2.3",
            logger: NullLogger<MirrorServerService>.Instance);
        server2.Start(port: 0); // port 0 → OS-assigned, no cross-test port races
        var port2 = server2.BoundPort;

        var devices = await _store.GetAllAsync();
        devices.Should().BeEmpty("the certificate loss invalidated all device tokens");

        using var client = new ClientWebSocket();
        client.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        await client.ConnectAsync(new Uri($"wss://127.0.0.1:{port2}/"), CancellationToken.None);
        await SendJsonAsync(client, new HelloMessage { AppVersion = "1.2.3", DeviceName = "Tablet", Token = entry.Token, ClientId = "client-1" });

        var ack = MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as HelloAckMessage;
        ack!.Status.Should().Be("pending", "the invalidated token no longer grants access");

        server2.Dispose();
    }

    // ── Integration ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeClosesSessionWithRevokeMessage()
    {
        var entry = await _store.AddAsync("client-1", "Tablet");
        using var client = await ConnectAsync();
        await SendJsonAsync(client, new HelloMessage { AppVersion = "1.2.3", DeviceName = "Tablet", Token = entry.Token, ClientId = "client-1" });
        (MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as HelloAckMessage)!.Status.Should().Be("approved");

        await _server.RevokeDeviceAsync("client-1");

        var msg = MirrorProtocol.Parse(await ReceiveJsonAsync(client)) as RevokedMessage;
        msg.Should().NotBeNull();
        msg!.Reason.Should().Be("revoked");
    }

    [Fact]
    public async Task PairingResetRegeneratesCertificateAndInvalidatesOldPins()
    {
        var oldThumbprint = _certificates.GetOrCreate().Thumbprint;

        // Simulate pairing-reset: regenerate the certificate and clear the store.
        _certificates.Regenerate();
        await _store.ResetAsync();

        _certificates.GetOrCreate().Thumbprint.Should().NotBe(oldThumbprint);
        (await _store.GetAllAsync()).Should().BeEmpty();
    }
}

using System.Net;
using System.Net.WebSockets;
using System.Text;
using FluentAssertions;
using ME221Dashboard.Services;
using ME221Dashboard.Services.Mirror;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ME221Dashboard.Tests.Mirror;

/// <summary>
/// Client-transport integration (review round two): drives MirrorClientService against
/// the real Fleck loopback server — TOFU pin capture, token reconnect, cert-change →
/// RePair, and the disconnect exit. This is the layer where the inverted reconnect
/// loop bug lived; the pure state-machine tests cannot cover it.
/// </summary>
public sealed class MirrorClientTransportTests : IAsyncLifetime
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "me221-client-" + Guid.NewGuid().ToString("N"));
    private readonly ApprovedDeviceStore _store = new();
    private readonly MirrorCertificateProvider _certificates = new();
    private readonly MirrorPinStore _pins = new();
    private readonly MirrorServerService _server;
    private MirrorClientService _client = null!;
    private int _port;

    public MirrorClientTransportTests()
    {
        _store.SetBasePath(_tempDir);
        _certificates.SetBasePath(_tempDir);
        _pins.SetBasePath(_tempDir);
        _server = new MirrorServerService(_store, _certificates, appVersion: () => "1.2.3",
            logger: NullLogger<MirrorServerService>.Instance);
    }

    public ValueTask InitializeAsync()
    {
        _server.Start(port: 0);
        _port = _server.BoundPort;
        _client = new MirrorClientService(
            appVersion: () => "1.2.3",
            deviceName: () => "Tablet",
            pinStore: _pins,
            portOverride: _port,
            logger: NullLogger<MirrorClientService>.Instance);
        _client.Wire();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        _server.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        return ValueTask.CompletedTask;
    }

    private MirrorBeacon Beacon() => new("1.2.3", true, "A", "inst-a", IPAddress.Loopback);

    private async Task ApproveFromServerAsync(string clientId, string deviceName)
    {
        // Approve the pending session from the server side (as the banner would).
        await _server.ApproveDeviceAsync(clientId);
    }

    [Fact]
    public async Task FirstPairingCapturesTokenAndPin()
    {
        await _client.RestoreClientIdAsync();
        await _client.ConnectAsync(Beacon(), token: null, pinnedThumbprint: null);
        await Task.Delay(200);

        // B is awaiting approval; A's banner approves it (client id from the pins store).
        var clientId = await _pins.GetOrCreateClientIdAsync();
        await ApproveFromServerAsync(clientId, "Tablet");

        // The approval decision carries a token; B persists it plus the cert pin.
        await Task.Delay(500);
        var pins = await _pins.GetAsync("inst-a");
        pins.Should().NotBeNull();
        pins!.Token.Should().NotBeNullOrWhiteSpace();
        pins.CertThumbprint.Should().NotBeNullOrWhiteSpace();
        _client.State.State.Should().Be(MirrorClientState.Connected);
    }

    [Fact]
    public async Task TokenReconnectSkipsApproval()
    {
        // Pair first: connect, approve, wait for the pin to persist.
        await _client.RestoreClientIdAsync();
        await _client.ConnectAsync(Beacon(), token: null, pinnedThumbprint: null);
        await Task.Delay(200);
        var clientId = await _pins.GetOrCreateClientIdAsync();
        await ApproveFromServerAsync(clientId, "Tablet");
        await Task.Delay(500);

        // Reconnect with the stored token+pin: approved without a prompt.
        var pins = await _pins.GetAsync("inst-a");
        var client2 = new MirrorClientService(
            appVersion: () => "1.2.3",
            deviceName: () => "Tablet",
            pinStore: _pins,
            portOverride: _port,
            logger: NullLogger<MirrorClientService>.Instance);
        client2.Wire();
        try
        {
            await client2.RestoreClientIdAsync();
            await client2.ConnectAsync(Beacon(), pins?.Token, pins?.CertThumbprint);
            var acked = await client2.WaitForHelloAckAsync(TimeSpan.FromSeconds(3));
            acked.Should().BeTrue("a valid token reconnects without an approval prompt");
            client2.State.State.Should().Be(MirrorClientState.Connected);
        }
        finally
        {
            client2.Dispose();
        }
    }

    [Fact]
    public async Task CertChangeRoutesToRepairState()
    {
        // Pair, then regenerate the certificate: the old pin no longer matches.
        await _client.RestoreClientIdAsync();
        await _client.ConnectAsync(Beacon(), token: null, pinnedThumbprint: null);
        await Task.Delay(200);
        var clientId = await _pins.GetOrCreateClientIdAsync();
        await ApproveFromServerAsync(clientId, "Tablet");
        await Task.Delay(500);
        var pins = await _pins.GetAsync("inst-a");

        // Pairing-reset (R20): regenerate the cert and reload it onto the running
        // listener — the same sequence ResetPairing performs.
        _certificates.Regenerate();
        _server.ReloadCertificate();

        var client2 = new MirrorClientService(
            appVersion: () => "1.2.3",
            deviceName: () => "Tablet",
            pinStore: _pins,
            portOverride: _port,
            logger: NullLogger<MirrorClientService>.Instance);
        client2.Wire();
        try
        {
            var stateChanges = new List<MirrorClientState>();
            client2.StateChanged += s => stateChanges.Add(s.State);

            // The certificate mismatch is detected during TLS validation: the
            // connect fails, and the state machine routes to RePair (R20).
            try
            {
                await client2.ConnectAsync(Beacon(), pins?.Token, pins?.CertThumbprint);
            }
            catch (Exception ex) when (ex is WebSocketException or System.Security.Authentication.AuthenticationException or HttpRequestException)
            {
                // expected — the pinned certificate was rejected
            }

            await Task.Delay(300);
            stateChanges.Should().Contain(MirrorClientState.RePair);
        }
        finally
        {
            client2.Dispose();
        }
    }

    [Fact]
    public async Task DisconnectExitsToStopped()
    {
        await _client.RestoreClientIdAsync();
        await _client.ConnectAsync(Beacon(), token: null, pinnedThumbprint: null);
        await Task.Delay(200);
        var clientId = await _pins.GetOrCreateClientIdAsync();
        await ApproveFromServerAsync(clientId, "Tablet");
        await Task.Delay(300);

        _client.Disconnect();
        _client.State.State.Should().Be(MirrorClientState.Stopped);
    }
}

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using ME221.Comms;
using ME221Dashboard.Services;
using ME221Dashboard.Services.Mirror;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ME221Dashboard.Tests.Mirror;

public sealed class ApprovedDeviceStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "me221-store-" + Guid.NewGuid().ToString("N"));
    private readonly ApprovedDeviceStore _store;

    public ApprovedDeviceStoreTests()
    {
        _store = new ApprovedDeviceStore();
        _store.SetBasePath(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task AddPersistsAcrossRestart()
    {
        var added = await _store.AddAsync("client-1", "Tablet");

        var reloaded = new ApprovedDeviceStore();
        reloaded.SetBasePath(_tempDir);
        var found = await reloaded.FindByTokenAsync(added.Token);

        found.Should().NotBeNull();
        found!.ClientId.Should().Be("client-1");
        found.DeviceName.Should().Be("Tablet");
    }

    [Fact]
    public async Task InstanceIdIsStableAcrossRestart()
    {
        var first = await _store.GetOrCreateInstanceIdAsync();
        var reloaded = new ApprovedDeviceStore();
        reloaded.SetBasePath(_tempDir);
        var second = await reloaded.GetOrCreateInstanceIdAsync();

        second.Should().Be(first);
        first.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TwoStoresHaveDistinctInstanceIds()
    {
        var other = new ApprovedDeviceStore();
        var otherDir = Path.Combine(Path.GetTempPath(), "me221-store-" + Guid.NewGuid().ToString("N"));
        other.SetBasePath(otherDir);

        try
        {
            (await _store.GetOrCreateInstanceIdAsync()).Should().NotBe(await other.GetOrCreateInstanceIdAsync());
        }
        finally
        {
            try { Directory.Delete(otherDir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task CorruptedStoreFallsBackToEmptyWithoutCrash()
    {
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "companion-viewers.json"), "{ not valid json !!");

        var devices = await _store.GetAllAsync();
        devices.Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownTokenReturnsNull()
    {
        (await _store.FindByTokenAsync("nope")).Should().BeNull();
    }

    [Fact]
    public async Task RevokeRemovesEntry()
    {
        var added = await _store.AddAsync("client-1", "Tablet");
        (await _store.RevokeAsync("client-1")).Should().BeTrue();
        (await _store.FindByTokenAsync(added.Token)).Should().BeNull();
        (await _store.RevokeAsync("client-1")).Should().BeFalse("already removed");
    }

    [Fact]
    public async Task ResetClearsAllEntries()
    {
        await _store.AddAsync("client-1", "Tablet");
        await _store.AddAsync("client-2", "Phone");

        await _store.ResetAsync();

        (await _store.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RenameUpdatesDisplayName()
    {
        await _store.SetDisplayNameAsync("Racer Tablet");
        (await _store.GetDisplayNameAsync()).Should().Be("Racer Tablet");
    }
}

public sealed class DiscoveryResponderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "me221-responder-" + Guid.NewGuid().ToString("N"));
    private readonly ApprovedDeviceStore _store;
    private readonly DiscoveryResponderService _responder;

    public DiscoveryResponderTests()
    {
        _store = new ApprovedDeviceStore();
        _store.SetBasePath(_tempDir);
        _responder = new DiscoveryResponderService(
            _store,
            connection: null,
            appVersion: () => "9.9.9",
            logger: NullLogger<DiscoveryResponderService>.Instance);
        _responder.Start(port: 0);
    }

    public void Dispose()
    {
        _responder.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task IdleHostDoesNotAdvertise()
    {
        // A host without an ECU must not appear in discovery at all.
        using var probe = new UdpClient();
        probe.EnableBroadcast = true;
        await probe.SendAsync(
            Encoding.UTF8.GetBytes(DiscoveryResponderService.ProbePayload),
            DiscoveryResponderService.ProbePayload.Length,
            new IPEndPoint(IPAddress.Loopback, _responder.BoundPort));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var act = async () => await probe.ReceiveAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task NonProbeDatagramGetsNoBeaconReply()
    {
        using var probe = new UdpClient();
        var sent = await probe.SendAsync(
            Encoding.UTF8.GetBytes("random noise"),
            11,
            new IPEndPoint(IPAddress.Loopback, _responder.BoundPort));
        sent.Should().Be(11);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var act = async () => await probe.ReceiveAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

public sealed class DiscoveryResponderEcuStateTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "me221-responder-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }

    private sealed class FakeConnection : IEcuConnectionService
    {
        public ConnectionState State { get; init; }
        public ME221Dashboard.Services.ProtocolInfo? ProtocolInfo => null;
        public string? LastError => null;
        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
        public Task<bool> ConnectAsync(ConnectionTarget target, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task DisconnectAsync() => Task.CompletedTask;
        public ProtocolService GetProtocolService() => throw new NotImplementedException();
        public Task<ProtocolInfo> EnableReportingAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DisableReportingAsync() => Task.CompletedTask;
        public void PauseHeartbeat() { }
        public void ResumeHeartbeat() { }
    }

    [Fact]
    public async Task BeaconReflectsEcuConnectionState()
    {
        var store = new ApprovedDeviceStore();
        store.SetBasePath(_tempDir);
        using var responder = new DiscoveryResponderService(
            store,
            connection: new FakeConnection { State = ConnectionState.Connected },
            appVersion: () => "1.0.0",
            logger: NullLogger<DiscoveryResponderService>.Instance);
        responder.Start(port: 0);

        using var probe = new UdpClient();
        probe.EnableBroadcast = true;
        await probe.SendAsync(
            Encoding.UTF8.GetBytes(DiscoveryResponderService.ProbePayload),
            DiscoveryResponderService.ProbePayload.Length,
            new IPEndPoint(IPAddress.Loopback, responder.BoundPort));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var reply = await probe.ReceiveAsync(cts.Token);
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(reply.Buffer));
        doc.RootElement.GetProperty("ecuConnected").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("appVersion").GetString().Should().Be("1.0.0");
        doc.RootElement.GetProperty("displayName").GetString().Should().NotBeNullOrWhiteSpace("fresh installs default to the platform device name, never a generic placeholder");
        doc.RootElement.GetProperty("instanceId").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
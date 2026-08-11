using System.Text;
using System.Text.Json;
using FluentAssertions;
using ME221.Data.Models;
using ME221Dashboard.Services;
using ME221Dashboard.Services.Mirror;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ME221Dashboard.Tests.Mirror;

/// <summary>U8: B-side ingestion — chunk reassembly, snapshot apply, frame gating, event relay.</summary>
public sealed class MirrorEventRelayTests
{
    private readonly FakeCalibrationService _calibration = new();
    private readonly HybridBridgeService _bridge;
    private readonly MirrorSnapshotStore _store = new();
    private readonly MirrorEventRelay _relay;
    private readonly List<string> _emitted = [];

    public MirrorEventRelayTests()
    {
        _calibration.Config = new Services.DashboardConfig();
        _bridge = TestBridge.Create(_calibration);
        _bridge._activeDashboardName = "default";

        _client = new MirrorClientService(appVersion: () => "1.2.3", deviceName: () => "Tablet",
            logger: NullLogger<MirrorClientService>.Instance);
        _relay = new MirrorEventRelay(_client, _store, _bridge,
            logger: NullLogger<MirrorEventRelay>.Instance);
        _relay.SvelteMessageEmitted += json => _emitted.Add(json);
    }

    private readonly MirrorClientService _client;

    // ── Snapshot apply (R6, KTD-2) ───────────────────────────────────────────

    [Fact]
    public async Task SnapshotChunksReassembleApplyAndOpenTheGate()
    {
        var json = MirrorProtocol.Serialize(MirrorTestData.SampleSnapshot());
        var chunks = MirrorChunkFramer.CreateChunks(json, seq: 42, chunkSize: 128);

        foreach (var chunk in chunks)
            _relay.InjectMessage(chunk);
        await _relay.FinalizeForTest(42);

        _relay.SnapshotApplied.Should().BeTrue();
        _relay.SnapshotSeq.Should().Be(42);
        _store.HasSnapshot.Should().BeTrue();
        _store.Calibration!.Tables.Should().ContainSingle(t => t.Name == "Ignition");
        _store.DashboardNames.Should().Contain("default");

        // The snapshot was relayed to Svelte as chunks + done.
        _emitted.Should().Contain(m => m.Contains("mirrorSnapshotChunk"));
        _emitted.Should().Contain(m => m.Contains("mirrorSnapshotDone"));
    }

    [Fact]
    public async Task SnapshotApplyEntersCompanionModeOnTheBridge()
    {
        var json = MirrorProtocol.Serialize(MirrorTestData.SampleSnapshot());
        foreach (var chunk in MirrorChunkFramer.CreateChunks(json, seq: 1, chunkSize: 256))
            _relay.InjectMessage(chunk);
        await _relay.FinalizeForTest(1);

        _bridge.IsMirrorMode.Should().BeTrue("the relay enters companion mode when the snapshot applies");
    }

    [Fact]
    public async Task OutOfOrderChunkIsRejectedAndSnapshotStaysClosed()
    {
        var json = MirrorProtocol.Serialize(MirrorTestData.SampleSnapshot());
        var chunks = MirrorChunkFramer.CreateChunks(json, seq: 7, chunkSize: 128);
        chunks.Should().HaveCountGreaterThan(2);

        _relay.InjectMessage(chunks[0]);
        _relay.InjectMessage(chunks[2]); // out of order → rejected

        await _relay.FinalizeForTest(7);
        _relay.SnapshotApplied.Should().BeFalse("an out-of-order chunk must fail the whole snapshot");
    }

    // ── Frame gating (R11) ───────────────────────────────────────────────────

    [Fact]
    public async Task FramesBeforeSnapshotAreDiscarded()
    {
        _relay.InjectMessage(new FrameMessage { Seq = 99, Ts = 1, Values = new Dictionary<string, float?> { ["1"] = 5 } });
        _emitted.Should().NotContain(m => m.Contains("liveDataUpdate"), "frames before the snapshot gate are discarded");
    }

    [Fact]
    public async Task FramesOlderThanSnapshotAreDiscarded()
    {
        var json = MirrorProtocol.Serialize(MirrorTestData.SampleSnapshot());
        foreach (var chunk in MirrorChunkFramer.CreateChunks(json, seq: 42, chunkSize: 256))
            _relay.InjectMessage(chunk);
        await _relay.FinalizeForTest(42);
        _emitted.Clear();

        _relay.InjectMessage(new FrameMessage { Seq = 42, Ts = 1, Values = new Dictionary<string, float?> { ["1"] = 1 } });
        _relay.InjectMessage(new FrameMessage { Seq = 43, Ts = 2, Values = new Dictionary<string, float?> { ["1"] = 2 } });
        _relay.InjectMessage(new FrameMessage { Seq = 41, Ts = 3, Values = new Dictionary<string, float?> { ["1"] = 3 } });

        var liveFrames = _emitted.Where(m => m.Contains("\"liveDataUpdate\"")).ToList();
        liveFrames.Should().HaveCount(1, "only the newest frame after the snapshot seq is applied");
        using var doc = JsonDocument.Parse(liveFrames[0]);
        doc.RootElement.GetProperty("values").GetProperty("1").GetSingle().Should().Be(2);
    }

    // ── Event relay (R17, KTD-7) ─────────────────────────────────────────────

    [Fact]
    public async Task GpsEventsRelayVerbatimInBridgeShape()
    {
        var gpsJson = "{\"event\":\"gpsUpdate\",\"latitude\":1.5,\"longitude\":2.5,\"speed\":30}";
        _relay.InjectMessage(new EventMessage { Channel = "gps", Payload = gpsJson });

        _emitted.Should().Contain(m => m.Contains("\"event\":\"gpsUpdate\""));
        _emitted.Should().Contain(m => m.Contains("\"latitude\":1.5"));
    }

    [Fact]
    public async Task OdometerEventsUpdateTheStore()
    {
        var odoJson = "{\"event\":\"odometerUpdate\",\"odometer\":123.5,\"odometerUnit\":\"mi\"}";
        _relay.InjectMessage(new EventMessage { Channel = "odometer", Payload = odoJson });

        var (value, km) = _store.Odometer;
        value.Should().Be(123.5);
        km.Should().BeFalse();
    }

    [Fact]
    public async Task LogEventsAppendToTheStoreTail()
    {
        var logJson = "{\"event\":\"logEntry\",\"message\":\"hello\"}";
        _relay.InjectMessage(new EventMessage { Channel = "log", Payload = logJson });

        _store.RecentLogs.Should().ContainSingle();
        _store.RecentLogs[0].Should().Contain("hello");
    }

    // ── Session state (KTD-7) ────────────────────────────────────────────────

    [Fact]
    public async Task SessionStateEmitsAsMirrorSessionState()
    {
        _client.State.StartSearch();
        _client.State.SelectDevice("A");
        _client.State.Approved("tok");

        _emitted.Should().Contain(m => m.Contains("mirrorSessionState"));
        _emitted.Should().Contain(m => m.Contains("\"Connected\""));
    }
}
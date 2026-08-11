using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using ME221Dashboard.Services;
using ME221Dashboard.Services.Mirror;
using Xunit;

namespace ME221Dashboard.Tests.Mirror;

public sealed class MirrorProtocolTests
{
    // ── Snapshot chunking (KTD-2) ────────────────────────────────────────────

    [Fact]
    public void SnapshotSerializesToChunksAndReassemblesByteIdentical()
    {
        var snapshot = MirrorTestData.SampleSnapshot();
        var json = MirrorProtocol.Serialize(snapshot);

        var chunks = MirrorChunkFramer.CreateChunks(json, seq: 42, chunkSize: 1024);
        chunks.Should().HaveCountGreaterThan(1);

        var reassembler = new MirrorChunkReassembler(seq: 42);
        foreach (var chunk in chunks)
            reassembler.TryAdd(chunk).Should().BeTrue();

        reassembler.IsComplete.Should().BeTrue();
        reassembler.Reassemble().Should().Be(json);
    }

    [Fact]
    public void EmptyDashboardListReassemblesWithoutError()
    {
        var snapshot = MirrorTestData.SampleSnapshot();
        snapshot.DashboardConfig.Dashboards.Clear();
        var json = MirrorProtocol.Serialize(snapshot);

        var chunks = MirrorChunkFramer.CreateChunks(json, seq: 1, chunkSize: 64);
        var reassembler = new MirrorChunkReassembler(1);
        foreach (var chunk in chunks)
            reassembler.TryAdd(chunk).Should().BeTrue();

        reassembler.Reassemble().Should().Be(json);
    }

    [Fact]
    public void SingleByteImageChunkReassemblesExact()
    {
        var snapshot = MirrorTestData.SampleSnapshot();
        snapshot.Images = new Dictionary<string, string> { ["x"] = "QQ==" };
        var json = MirrorProtocol.Serialize(snapshot);

        var chunks = MirrorChunkFramer.CreateChunks(json, seq: 9, chunkSize: MirrorProtocol.ChunkSize);
        chunks.Should().ContainSingle();

        var reassembler = new MirrorChunkReassembler(9);
        reassembler.TryAdd(chunks[0]).Should().BeTrue();
        reassembler.IsComplete.Should().BeTrue();
        reassembler.Reassemble().Should().Be(json);
    }

    [Fact]
    public void ChunkSizeBoundarySplitProducesWholeChunks()
    {
        var json = MirrorProtocol.Serialize(MirrorTestData.SampleSnapshot());
        var exactBoundary = MirrorChunkFramer.CreateChunks(json, seq: 7, chunkSize: 8);
        var reassembler = new MirrorChunkReassembler(7);
        foreach (var chunk in exactBoundary)
            reassembler.TryAdd(chunk).Should().BeTrue();
        reassembler.Reassemble().Should().Be(json);
    }

    // ── Error paths ──────────────────────────────────────────────────────────

    [Fact]
    public void OutOfOrderChunkIsRejected()
    {
        var json = MirrorProtocol.Serialize(MirrorTestData.SampleSnapshot());
        var chunks = MirrorChunkFramer.CreateChunks(json, seq: 3, chunkSize: 128);
        chunks.Should().HaveCountGreaterThan(2);

        var reassembler = new MirrorChunkReassembler(3);
        reassembler.TryAdd(chunks[0]).Should().BeTrue();
        reassembler.TryAdd(chunks[2]).Should().BeFalse("chunk 2 is not the expected next index 1");
        reassembler.TryAdd(chunks[1]).Should().BeTrue();
        reassembler.TryAdd(chunks[1]).Should().BeFalse("chunk already consumed");
    }

    [Fact]
    public void WrongSequenceChunkIsRejected()
    {
        var json = MirrorProtocol.Serialize(MirrorTestData.SampleSnapshot());
        var chunks = MirrorChunkFramer.CreateChunks(json, seq: 3, chunkSize: 128);

        var reassembler = new MirrorChunkReassembler(5);
        reassembler.TryAdd(chunks[0]).Should().BeFalse("chunk carries sequence 3, reassembler expects 5");
    }

    [Fact]
    public void LostChunkExhaustsWholeSnapshotRetryAfterThreeAttempts()
    {
        var tracker = new SnapshotAttemptTracker();
        // A lost chunk (no ack within the window) fails one attempt at a time:
        tracker.RecordFailure().Should().BeTrue();  // attempt 1 → retry remains
        tracker.RecordFailure().Should().BeTrue();  // attempt 2 → retry remains
        tracker.RecordFailure().Should().BeFalse(); // attempt 3 → exhausted
        tracker.Exhausted.Should().BeTrue();
    }

    [Fact]
    public void SequenceCollisionIsRejectedBySequenceRule()
    {
        // Snapshot seq N, first frame seq N+1 (R11): a frame at or below the snapshot seq must be discarded.
        MirrorSequencing.IsFrameAfterSnapshot(42, 42).Should().BeFalse();
        MirrorSequencing.IsFrameAfterSnapshot(41, 42).Should().BeFalse();
        MirrorSequencing.NextFrameSequence(42).Should().Be(43);
        MirrorSequencing.IsFrameAfterSnapshot(43, 42).Should().BeTrue();
    }

    // ── Message envelope round-trip ──────────────────────────────────────────

    [Fact]
    public void HelloMessageRoundTripsThroughParser()
    {
        var hello = new HelloMessage { AppVersion = "1.2.3", DeviceName = "Tablet", Token = "tok-1", ClientId = "c1" };
        var parsed = MirrorProtocol.Parse(MirrorProtocol.Serialize(hello)) as HelloMessage;

        parsed.Should().NotBeNull();
        parsed!.AppVersion.Should().Be("1.2.3");
        parsed.DeviceName.Should().Be("Tablet");
        parsed.Token.Should().Be("tok-1");
    }

    // ── Golden fixture (U1 DoD) ──────────────────────────────────────────────

    [Fact]
    public void GoldenFixture_Match_SerializerOutput()
    {
        var fixturePath = FindRepoRoot() + @"tests\ME221Dashboard.Tests\Mirror\fixtures\snapshot-golden.json";
        var fixture = File.ReadAllText(fixturePath);

        var serialized = MirrorProtocol.Serialize(MirrorTestData.SampleSnapshot());

        var fixtureNode = JsonNode.Parse(fixture);
        var serializedNode = JsonNode.Parse(serialized);
        // Semantic deep equality (order-insensitive) — the committed fixture agrees with the serializer.
        JsonNode.DeepEquals(fixtureNode, serializedNode).Should().BeTrue("golden fixture must match current serializer output");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ME221.slnx")))
            dir = dir.Parent;
        return dir?.FullName.TrimEnd('\\', '/') + "\\" ?? throw new InvalidOperationException("repo root not found");
    }
}
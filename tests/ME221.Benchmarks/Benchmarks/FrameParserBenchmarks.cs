using BenchmarkDotNet.Attributes;
using ME221.Comms.Internal;
using ME221.Comms.Messages;

namespace ME221.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for FrameParser — the frame deserialization path.
/// Measures parse performance for various frame sizes.
/// </summary>
[MemoryDiagnoser]
public class FrameParserBenchmarks
{
    private byte[] _smallFrame = null!;   // SendAck response (9 bytes overhead + 0 payload)
    private byte[] _mediumFrame = null!;  // SetState response with entity map
    private byte[] _largeFrame = null!;   // GetTable response (~1KB)
    private byte[] _junkPrefix = null!;   // frame with garbage prefix (worst case FindSyncBytes)

    [GlobalSetup]
    public void Setup()
    {
        _smallFrame = BuildFrame(new SendAckRequest());
        _mediumFrame = BuildFrame(new SetStateRequest(true));
        _largeFrame = BuildFrame(new GetTableRequest(0x0100));

        // Worst case: 200 bytes of junk + valid frame
        _junkPrefix = new byte[200 + _smallFrame.Length];
        Random.Shared.NextBytes(_junkPrefix.AsSpan(0, 200));
        _smallFrame.CopyTo(_junkPrefix, 200);
    }

    [Benchmark(Baseline = true)]
    public bool TryParse_Small() => FrameParser.TryParse(_smallFrame, out _, out _);

    [Benchmark]
    public bool TryParse_Medium() => FrameParser.TryParse(_mediumFrame, out _, out _);

    [Benchmark]
    public bool TryParse_Large() => FrameParser.TryParse(_largeFrame, out _, out _);

    [Benchmark]
    public bool TryParse_JunkPrefix() => FrameParser.TryParse(_junkPrefix, out _, out _);

    private static byte[] BuildFrame(MessageFrame frame)
    {
        using var pooled = FrameBuilder.Build(frame);
        return pooled.Memory.ToArray();
    }
}

using System.Buffers;
using BenchmarkDotNet.Attributes;
using ME221.Comms.Internal;
using ME221.Comms.Messages;

namespace ME221.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for FrameBuilder — the frame serialization path.
/// Measures both pooled (Build) and stack-based (BuildIntoBuffer) approaches.
/// </summary>
[MemoryDiagnoser]
public class FrameBuilderBenchmarks
{
    private SendAckRequest _smallRequest = null!;
    private SetTableRequest _mediumRequest = null!;
    private byte[] _mediumPayload = null!;
    private byte[] _stackBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _smallRequest = new SendAckRequest();

        _mediumPayload = new byte[512];
        Random.Shared.NextBytes(_mediumPayload);
        _mediumRequest = new SetTableRequest(0x1234, _mediumPayload);

        var totalSize = WireFormat.FixedFrameOverhead + _mediumPayload.Length + 2;
        _stackBuffer = new byte[totalSize];
    }

    [Benchmark(Baseline = true)]
    public int Build_Small()
    {
        using var result = FrameBuilder.Build(_smallRequest);
        return result.Memory.Length;
    }

    [Benchmark]
    public int Build_Medium()
    {
        using var result = FrameBuilder.Build(_mediumRequest);
        return result.Memory.Length;
    }

    [Benchmark]
    public bool BuildIntoBuffer_Medium()
    {
        return FrameBuilder.BuildIntoBuffer(_mediumRequest, _stackBuffer, out _);
    }

    [Benchmark]
    public bool BuildIntoBuffer_Small()
    {
        return FrameBuilder.BuildIntoBuffer(_smallRequest, _stackBuffer, out _);
    }
}

using BenchmarkDotNet.Attributes;
using ME221.Comms.Internal;
using ME221.Comms.Messages;

namespace ME221.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for FrameBuffer.TryExtractFrame — the frame recovery path.
/// Measures performance of the SIMD-accelerated sync byte scan on parse failure.
/// </summary>
[MemoryDiagnoser]
public class FrameBufferBenchmarks
{
    private byte[] _cleanFrame = null!;
    private byte[] _junk50 = null!;
    private byte[] _junk200 = null!;
    private byte[] _junk500 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _cleanFrame = BuildFrame(new SendAckRequest());

        _junk50 = BuildJunkPrefix(50, _cleanFrame);
        _junk200 = BuildJunkPrefix(200, _cleanFrame);
        _junk500 = BuildJunkPrefix(500, _cleanFrame);
    }

    /// <summary>
    /// Clean frame — no junk prefix, parse succeeds immediately.
    /// </summary>
    [Benchmark(Baseline = true)]
    public bool TryExtractFrame_Clean()
    {
        using var buffer = new FrameBuffer(8192);
        _cleanFrame.CopyTo(buffer.GetBuffer(), 0);
        buffer.Advance(_cleanFrame.Length);
        return buffer.TryExtractFrame(out _);
    }

    /// <summary>
    /// 50 bytes of junk before a valid frame — tests sync scan performance.
    /// </summary>
    [Benchmark]
    public bool TryExtractFrame_Junk50()
    {
        using var buffer = new FrameBuffer(8192);
        _junk50.CopyTo(buffer.GetBuffer(), 0);
        buffer.Advance(_junk50.Length);
        return buffer.TryExtractFrame(out _);
    }

    /// <summary>
    /// 200 bytes of junk before a valid frame.
    /// </summary>
    [Benchmark]
    public bool TryExtractFrame_Junk200()
    {
        using var buffer = new FrameBuffer(8192);
        _junk200.CopyTo(buffer.GetBuffer(), 0);
        buffer.Advance(_junk200.Length);
        return buffer.TryExtractFrame(out _);
    }

    /// <summary>
    /// 500 bytes of junk before a valid frame.
    /// </summary>
    [Benchmark]
    public bool TryExtractFrame_Junk500()
    {
        using var buffer = new FrameBuffer(8192);
        _junk500.CopyTo(buffer.GetBuffer(), 0);
        buffer.Advance(_junk500.Length);
        return buffer.TryExtractFrame(out _);
    }

    private static byte[] BuildFrame(MessageFrame frame)
    {
        using var pooled = FrameBuilder.Build(frame);
        return pooled.Memory.ToArray();
    }

    private static byte[] BuildJunkPrefix(int junkLength, byte[] validFrame)
    {
        var data = new byte[junkLength + validFrame.Length];
        Random.Shared.NextBytes(data.AsSpan(0, junkLength));
        validFrame.CopyTo(data, junkLength);
        return data;
    }
}

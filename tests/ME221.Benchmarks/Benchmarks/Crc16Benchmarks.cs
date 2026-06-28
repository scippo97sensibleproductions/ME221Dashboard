using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using ME221.Comms.Internal;

namespace ME221.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for Crc16 — runs on every frame in both directions.
/// </summary>
[MemoryDiagnoser]
public class Crc16Benchmarks
{
    private byte[] _data9 = null!;    // minimal frame (no payload)
    private byte[] _data20 = null!;   // typical small command
    private byte[] _data128 = null!;  // medium payload
    private byte[] _data1024 = null!; // large payload (table data)

    [GlobalSetup]
    public void Setup()
    {
        _data9 = new byte[9];
        _data20 = new byte[20];
        _data128 = new byte[128];
        _data1024 = new byte[1024];
        Random.Shared.NextBytes(_data9);
        Random.Shared.NextBytes(_data20);
        Random.Shared.NextBytes(_data128);
        Random.Shared.NextBytes(_data1024);
    }

    [Benchmark]
    public ushort Compute_9B() => Crc16.Compute(_data9);

    [Benchmark]
    public ushort Compute_20B() => Crc16.Compute(_data20);

    [Benchmark]
    public ushort Compute_128B() => Crc16.Compute(_data128);

    [Benchmark]
    public ushort Compute_1024B() => Crc16.Compute(_data1024);

    [Benchmark]
    public bool Verify_20B()
    {
        var crc = Crc16.Compute(_data20.AsSpan(0, 18));
        BinaryPrimitives.WriteUInt16LittleEndian(_data20.AsSpan(18), crc);
        return Crc16.Verify(_data20);
    }
}

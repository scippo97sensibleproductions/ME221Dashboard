using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using ME221Dashboard.Comms;

namespace ME221.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for the reporting hot path — 10 Hz live data parsing.
/// Compares V1 (SendReportResponse constructor — allocates) vs V2 (ReportParser — zero alloc).
/// </summary>
[MemoryDiagnoser]
public class ReportingParseBenchmarks
{
    private byte[] _v1Payload = null!;   // V1: [status][count:2][id:2+value:4] × N entities
    private byte[] _v2Payload = null!;   // V2: [status][value1][value2]... — external map

    private (ushort Id, ReportingType Type, int Size)[] _v2Map = null!;
    private ReportEntity[] _entityBuffer = null!;

    [Params(10, 50)]
    public int EntityCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // V1 format: [status:1][count:2][for each: id:2 LE + value:4 LE float]
        var v1Size = 3 + EntityCount * 6;
        _v1Payload = new byte[v1Size];
        _v1Payload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteUInt16LittleEndian(_v1Payload.AsSpan(1), (ushort)EntityCount);
        var offset = 3;
        for (var i = 0; i < EntityCount; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(_v1Payload.AsSpan(offset), (ushort)(i + 1));
            BinaryPrimitives.WriteSingleLittleEndian(_v1Payload.AsSpan(offset + 2), i * 1.5f);
            offset += 6;
        }

        // V2 external-map format: [status:1][value1][value2]... — no IDs, no type bytes
        _v2Map = new (ushort, ReportingType, int)[EntityCount];
        var v2Size = 1 + EntityCount * 4; // all floats
        _v2Payload = new byte[v2Size];
        _v2Payload[0] = (byte)MessageStatus.Success;
        offset = 1;
        for (var i = 0; i < EntityCount; i++)
        {
            var id = (ushort)(i + 1);
            BinaryPrimitives.WriteSingleLittleEndian(_v2Payload.AsSpan(offset), i * 2.5f);
            _v2Map[i] = (id, ReportingType.Float4B, 4);
            offset += 4;
        }

        _entityBuffer = new ReportEntity[EntityCount];
    }

    /// <summary>
    /// V1 path: creates SendReportResponse (allocates byte[] + ReportEntity[]).
    /// This is the fallback when V2 entity map is not available.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int V1_SendReportResponse()
    {
        var response = new SendReportResponse(_v1Payload);
        return response.Entities.Length;
    }

    /// <summary>
    /// V2 path: ReportParser.ParseV2Report (zero heap alloc, fills pre-allocated buffer).
    /// This is the hot path when reporting is properly configured.
    /// </summary>
    [Benchmark]
    public int V2_ReportParser()
    {
        return ReportParser.ParseV2Report(_v2Payload, _entityBuffer.AsSpan(), _v2Map);
    }
}

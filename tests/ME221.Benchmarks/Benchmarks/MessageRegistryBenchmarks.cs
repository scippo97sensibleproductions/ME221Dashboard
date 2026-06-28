using BenchmarkDotNet.Attributes;
using ME221.Comms.Internal;
using ME221.Comms.Messages;

namespace ME221.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for MessageRegistry.Create — called on every received frame.
/// Maps (type, class, command) to a concrete MessageFrame.
/// </summary>
[MemoryDiagnoser]
public class MessageRegistryBenchmarks
{
    private byte[] _sendAckPayload = [];
    private byte[] _sendReportPayload = null!;
    private byte[] _getTablePayload = null!;

    [GlobalSetup]
    public void Setup()
    {
        // SendAck response: just status byte
        _sendAckPayload = [(byte)MessageStatus.Success];

        // SendReport: status + count(2) + entity(id:2 + value:4)
        _sendReportPayload = new byte[10];
        _sendReportPayload[0] = (byte)MessageStatus.Success;
        _sendReportPayload.AsSpan(1).Fill(0x42);

        // GetTable response: status + tableId(2) + type(1) + enabled(1) + rows(1) + cols(1) + data
        _getTablePayload = new byte[32];
        _getTablePayload[0] = (byte)MessageStatus.Success;
        _getTablePayload.AsSpan(1).Fill(0x10);
    }

    [Benchmark(Baseline = true)]
    public MessageFrame? Create_SendAck()
    {
        return MessageRegistry.Create(WireFormat.ResponseType, WireFormat.ClassReporting, WireFormat.ReportingSendAck, _sendAckPayload);
    }

    [Benchmark]
    public MessageFrame? Create_SendReport()
    {
        return MessageRegistry.Create(WireFormat.ResponseType, WireFormat.ClassReporting, WireFormat.ReportingSendReport, _sendReportPayload);
    }

    [Benchmark]
    public MessageFrame? Create_GetTable()
    {
        return MessageRegistry.Create(WireFormat.ResponseType, WireFormat.ClassTables, WireFormat.TablesGetTable, _getTablePayload);
    }
}

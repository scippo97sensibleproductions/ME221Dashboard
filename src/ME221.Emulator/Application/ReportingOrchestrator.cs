using System.Buffers.Binary;
using ME221.Comms.Messages;
using ME221.Emulator.Domain;
using ME221.Emulator.Presentation;
using Microsoft.Extensions.Logging;

namespace ME221.Emulator.Application;

public sealed class ReportingOrchestrator(
    EntityStore entityStore,
    EcuState state,
    SensorSimulator sensorSimulator,
    ILogger<ReportingOrchestrator> logger,
    EmulatorConsole console,
    string sessionId,
    Func<MessageFrame, ValueTask> sendFrameAsync)
    : IDisposable
{
    private Timer? _timer;
    private readonly object _lock = new();
    private int _tickCount;

    public void Start(byte frequencyHz = 10)
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _tickCount = 0;
            var intervalMs = Math.Max(50, 1000 / frequencyHz);
            _timer = new Timer(OnTick, null, 0, intervalMs);
            logger.LogInformation("ReportingOrchestrator: started at {Freq}Hz (interval={Interval}ms)", frequencyHz, intervalMs);
            console.StateChange(sessionId, $"Reporting: Enabled ({frequencyHz}Hz)");
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            logger.LogInformation("ReportingOrchestrator: stopped (sent {TickCount} reports)", _tickCount);
            console.StateChange(sessionId, "Reporting: Disabled");
        }
    }

    private void OnTick(object? state1)
    {
        if (!state.ReportingEnabled)
        {
            logger.LogWarning("ReportingOrchestrator: tick but reporting disabled — stopping");
            Stop();
            return;
        }

        sensorSimulator.Tick();
        _tickCount++;

        var report = BuildReport();
        if (report is not null)
        {
            logger.LogTrace("ReportingOrchestrator: sending report #{TickCount}", _tickCount);
            try
            {
                sendFrameAsync(report).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                logger.LogInformation(ex, "ReportingOrchestrator: connection lost on tick #{TickCount} — stopping", _tickCount);
                Stop();
            }
        }
        else
        {
            logger.LogWarning("ReportingOrchestrator: BuildReport returned null on tick #{TickCount}", _tickCount);
        }
    }

    private static int GetV2EntitySize(byte reportingType) => reportingType switch
    {
        0 => sizeof(float),   // Float4B
        1 => sizeof(short),   // Int2B
        2 => sizeof(ushort),  // Uint2B
        3 => sizeof(sbyte),   // Int1B
        4 => sizeof(byte),    // Uint1B
        5 => sizeof(byte),    // Bool1B
        _ => sizeof(float),
    };

    private MessageFrame? BuildReport()
    {
        var knownLinks = state.SpecialCfgActive && state.SpecialCfgEntityIds.Count > 0
            ? state.SpecialCfgEntityIds
            : entityStore.Calibration.DataLinks
                .Select(dl => dl.Id)
                .ToList();

        if (knownLinks.Count == 0)
        {
            logger.LogWarning("ReportingOrchestrator: no known links to report");
            return null;
        }

        logger.LogTrace("ReportingOrchestrator: building report with {Count} entities (protocol={Protocol})",
            knownLinks.Count, state.ProtocolVersion);

        if (state.ProtocolVersion == ReportingVersion.V2)
            return BuildV2Report(knownLinks);

        return BuildV1Report(knownLinks);
    }

    private MessageFrame BuildV1Report(List<ushort> knownLinks)
    {
        var payloadSize = 1 + 2 + knownLinks.Count * 6;
        var payload = new byte[payloadSize];
        payload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), (ushort)knownLinks.Count);

        var offset = 3;
        foreach (var id in knownLinks)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset), id);
            offset += 2;
            var value = entityStore.GetDataLinkValue(id);
            BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(offset), value);
            offset += 4;
        }

        logger.LogDebug("ReportingOrchestrator: V1 report built — {Count} entities, {PayloadSize} bytes payload",
            knownLinks.Count, payloadSize);
        return new SendReportResponse(payload.AsSpan());
    }

    private MessageFrame BuildV2Report(List<ushort> knownLinks)
    {
        var valueSizeTotal = 0;
        foreach (var id in knownLinks)
            valueSizeTotal += GetV2EntitySize(entityStore.GetDataLinkReportingType(id));

        var payload = new byte[1 + valueSizeTotal];
        payload[0] = (byte)MessageStatus.Success;

        var offset = 1;
        foreach (var id in knownLinks)
        {
            var rt = entityStore.GetDataLinkReportingType(id);
            var floatValue = entityStore.GetDataLinkValue(id);
            WriteV2Value(payload.AsSpan(offset), floatValue, rt);
            offset += GetV2EntitySize(rt);
        }

        logger.LogDebug("ReportingOrchestrator: V2 report built — {Count} entities, payload={PayloadSize}B, first few link IDs: [{FirstIds}]",
            knownLinks.Count,
            1 + valueSizeTotal,
            string.Join(",", knownLinks.Take(5)));

        return new SendReportResponse(payload.AsSpan());
    }

    private static void WriteV2Value(Span<byte> dest, float value, byte reportingType)
    {
        switch (reportingType)
        {
            case 0:
                BinaryPrimitives.WriteSingleLittleEndian(dest, value);
                break;
            case 1:
                BinaryPrimitives.WriteInt16LittleEndian(dest, (short)Math.Clamp(value, short.MinValue, short.MaxValue));
                break;
            case 2:
                BinaryPrimitives.WriteUInt16LittleEndian(dest, (ushort)Math.Clamp(value, ushort.MinValue, ushort.MaxValue));
                break;
            case 3:
                dest[0] = (byte)(sbyte)Math.Clamp(value, sbyte.MinValue, sbyte.MaxValue);
                break;
            case 4:
                dest[0] = (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);
                break;
            case 5:
                dest[0] = (byte)(value != 0f ? 1 : 0);
                break;
            default:
                BinaryPrimitives.WriteSingleLittleEndian(dest, value);
                break;
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}

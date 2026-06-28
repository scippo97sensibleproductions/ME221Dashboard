using System.Buffers.Binary;
using ME221.Comms.Internal;

namespace ME221.Comms.Messages;

// ─── Logging Requests ───────────────────────────────────────────────────

/// <summary>
/// Request to check if data logging is supported.
/// Command: DataLog / IsSupported (class 0x06, command 0x00).
/// </summary>
public sealed class IsSupportedRequest()
    : Request(WireFormat.ClassDataLog, WireFormat.DataLogIsSupported, Array.Empty<byte>());

/// <summary>
/// Request to start data logging.
/// Command: DataLog / Start (class 0x06, command 0x03).
/// </summary>
public sealed class StartRequest() : Request(WireFormat.ClassDataLog, WireFormat.DataLogStart, Array.Empty<byte>());

/// <summary>
/// Request to stop data logging.
/// Command: DataLog / Stop (class 0x06, command 0x04).
/// </summary>
public sealed class StopRequest() : Request(WireFormat.ClassDataLog, WireFormat.DataLogStop, Array.Empty<byte>());

/// <summary>
/// Request to get logs summary.
/// Command: DataLog / GetLogsSummary (class 0x06, command 0x05).
/// </summary>
public sealed class GetLogsSummaryRequest()
    : Request(WireFormat.ClassDataLog, WireFormat.DataLogGetLogsSummary, Array.Empty<byte>());

/// <summary>
/// Request to get log detail.
/// Command: DataLog / GetLogDetail (class 0x06, command 0x06).
/// Payload: 2-byte entity ID (LE).
/// </summary>
public sealed class GetLogDetailRequest(ushort logId) : Request(WireFormat.ClassDataLog, WireFormat.DataLogGetLogDetail,
    BitConverter.GetBytes(logId))
{
    public ushort LogId { get; } = logId;
}

/// <summary>
/// Request to get a log region.
/// Command: DataLog / GetLogRegion (class 0x06, command 0x07).
/// Payload: [1 byte log index] [4 byte start entry LE] [4 byte num entries LE].
/// </summary>
public sealed class GetLogRegionRequest(byte logIdx, uint startEntry, uint numEntries) : Request(WireFormat.ClassDataLog,
    WireFormat.DataLogGetLogRegion, BuildPayload(logIdx, startEntry, numEntries))
{
    public byte LogIdx { get; } = logIdx;
    public uint StartEntry { get; } = startEntry;
    public uint NumEntries { get; } = numEntries;

    private static byte[] BuildPayload(byte logIdx, uint startEntry, uint numEntries)
    {
        var buf = new byte[9];
        buf[0] = logIdx;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(1), startEntry);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(5), numEntries);
        return buf;
    }
}

/// <summary>
/// Request to erase logs.
/// Command: DataLog / EraseLog (class 0x06, command 0x08).
/// </summary>
public sealed class EraseLogRequest()
    : Request(WireFormat.ClassDataLog, WireFormat.DataLogEraseLog, Array.Empty<byte>());

/// <summary>
/// Request to format memory.
/// Command: DataLog / FormatMemory (class 0x06, command 0x09).
/// </summary>
public sealed class FormatMemoryRequest()
    : Request(WireFormat.ClassDataLog, WireFormat.DataLogFormatMemory, Array.Empty<byte>());

// ─── Logging Responses ──────────────────────────────────────────────────

/// <summary>
/// Response to an IsSupported request.
/// Payload layout (after status byte):
///   [2 byte max frequency LE] [2 byte max number of channels LE]
/// </summary>
public sealed class IsSupportedResponse(ReadOnlySpan<byte> payload) : Response(WireFormat.ClassDataLog,
    WireFormat.DataLogIsSupported,
    payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure,
    payload.ToArray().AsMemory())
{
    public ushort MaxFrequency { get; } = payload.Length > 2
        ? BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1))
        : (ushort)0;

    public ushort MaxNumChannels { get; } = payload.Length > 4
        ? BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(3))
        : (ushort)0;

    public IsSupportedResponse(MessageStatus status) : this(new[] { (byte)status }) { }
}

/// <summary>
/// Response to a Start request.
/// </summary>
public sealed class StartResponse(MessageStatus status)
    : Response(WireFormat.ClassDataLog, WireFormat.DataLogStart, status);

/// <summary>
/// Response to a Stop request.
/// </summary>
public sealed class StopResponse(MessageStatus status)
    : Response(WireFormat.ClassDataLog, WireFormat.DataLogStop, status);

/// <summary>
/// Response to a GetLogsSummary request.
/// </summary>
public sealed class GetLogsSummaryResponse(MessageStatus status)
    : Response(WireFormat.ClassDataLog, WireFormat.DataLogGetLogsSummary, status);

/// <summary>
/// Response to a GetLogDetail request.
/// </summary>
public sealed class GetLogDetailResponse(MessageStatus status)
    : Response(WireFormat.ClassDataLog, WireFormat.DataLogGetLogDetail, status);

/// <summary>
/// Response to a GetLogRegion request.
/// </summary>
public sealed class GetLogRegionResponse(MessageStatus status)
    : Response(WireFormat.ClassDataLog, WireFormat.DataLogGetLogRegion, status);

/// <summary>
/// Response to an EraseLog request.
/// </summary>
public sealed class EraseLogResponse(MessageStatus status)
    : Response(WireFormat.ClassDataLog, WireFormat.DataLogEraseLog, status);

/// <summary>
/// Response to a FormatMemory request.
/// </summary>
public sealed class FormatMemoryResponse(MessageStatus status)
    : Response(WireFormat.ClassDataLog, WireFormat.DataLogFormatMemory, status);

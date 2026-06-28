using System.Buffers.Binary;
using ME221.Comms.Internal;

namespace ME221.Comms.Messages;

// ─── Driver Requests ────────────────────────────────────────────────────

/// <summary>
/// Request to get a driver by its entity ID.
/// Command: Drivers / GetDriver (class 0x02, command 0x01).
/// Payload: 2-byte entity ID (little-endian ushort).
/// </summary>
public sealed class GetDriverRequest(ushort driverId) : Request(WireFormat.ClassDrivers, WireFormat.DriversGetDriver,
    CreatePayload(driverId))
{
    public ushort DriverId { get; } = driverId;

    private static ReadOnlyMemory<byte> CreatePayload(ushort id)
    {
        var buf = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, id);
        return buf;
    }
}

/// <summary>
/// Request to set (write) a driver by its entity ID.
/// Command: Drivers / SetDriver (class 0x02, command 0x00).
/// Wire payload: [entityId:2 LE] [dataSize:2 LE] [serializedDriver:dataSize bytes].
/// </summary>
public sealed class SetDriverRequest : Request
{
    public ushort DriverId { get; }

    public SetDriverRequest(ushort driverId, ReadOnlyMemory<byte> serializedDriver)
        : base(WireFormat.ClassDrivers, WireFormat.DriversSetDriver, BuildPayload(driverId, serializedDriver))
    {
        DriverId = driverId;
    }

    private static ReadOnlyMemory<byte> BuildPayload(ushort driverId, ReadOnlyMemory<byte> data)
    {
        var totalLen = 2 + 2 + data.Length;
        var buf = new byte[totalLen];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, driverId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2), (ushort)data.Length);
        data.Span.CopyTo(buf.AsSpan(4));
        return buf;
    }
}

/// <summary>
/// Request to store a driver in non-volatile memory.
/// Command: Drivers / StoreInNVM (class 0x02, command 0x02).
/// Payload: 2-byte entity ID (little-endian ushort).
/// </summary>
public sealed class StoreDriverRequest(ushort driverId) : Request(WireFormat.ClassDrivers, WireFormat.DriversStoreInNvm,
    CreatePayload(driverId))
{
    public ushort DriverId { get; } = driverId;

    private static ReadOnlyMemory<byte> CreatePayload(ushort id)
    {
        var buf = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, id);
        return buf;
    }
}

// ─── Driver Responses ───────────────────────────────────────────────────

/// <summary>
/// Response to a GetDriver request.
/// Wire: [status:1] [entityId:2 LE] [dataSize:2 LE] [serializedDriver:dataSize bytes].
/// </summary>
public sealed class GetDriverResponse : Response
{
    public ushort DriverId { get; }
    public ReadOnlyMemory<byte> SerializedDriver { get; }

    public GetDriverResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassDrivers, WireFormat.DriversGetDriver,
            payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure,
            CopyPayload(payload))
    {
        if (payload.Length > 4)
        {
            DriverId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1));
            var dataSize = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(3));
            SerializedDriver = Payload.Slice(5, dataSize);
        }
    }
}

/// <summary>
/// Response to a SetDriver request.
/// </summary>
public sealed class SetDriverResponse : Response
{
    public ushort DriverId { get; }

    public SetDriverResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassDrivers, WireFormat.DriversSetDriver,
            payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure,
            CopyPayload(payload))
    {
        if (payload.Length > 1)
            DriverId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1));
    }
}

/// <summary>
/// Response to a StoreDriver request.
/// </summary>
public sealed class StoreDriverResponse : Response
{
    public ushort DriverId { get; }

    public StoreDriverResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassDrivers, WireFormat.DriversStoreInNvm,
            payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure,
            CopyPayload(payload))
    {
        if (payload.Length > 1)
            DriverId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1));
    }
}

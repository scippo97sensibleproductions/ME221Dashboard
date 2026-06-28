using System.Buffers.Binary;
using ME221.Comms.Internal;

namespace ME221.Comms.Messages;

// ─── Table Requests ─────────────────────────────────────────────────────

/// <summary>
/// Request to get a table by its entity ID.
/// Command: Tables / GetTable (class 0x01, command 0x01).
/// Payload: 2-byte entity ID (LE).
/// </summary>
public sealed class GetTableRequest(ushort tableId)
    : Request(WireFormat.ClassTables, WireFormat.TablesGetTable, CreatePayload(tableId))
{
    public ushort TableId { get; } = tableId;

    private static ReadOnlyMemory<byte> CreatePayload(ushort id)
    {
        var buf = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, id);
        return buf;
    }
}

/// <summary>
/// Request to set (write) a table.
/// Command: Tables / SetTable (class 0x01, command 0x00).
/// Wire payload: [entityId:2 LE] [dataSize:2 LE] [serializedTable:dataSize bytes].
/// </summary>
public sealed class SetTableRequest : Request
{
    public ushort TableId { get; }

    public SetTableRequest(ushort tableId, ReadOnlyMemory<byte> serializedTable)
        : base(WireFormat.ClassTables, WireFormat.TablesSetTable, BuildPayload(tableId, serializedTable))
    {
        TableId = tableId;
    }

    private static ReadOnlyMemory<byte> BuildPayload(ushort tableId, ReadOnlyMemory<byte> data)
    {
        var totalLen = 2 + 2 + data.Length;
        var buf = new byte[totalLen];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, tableId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2), (ushort)data.Length);
        data.Span.CopyTo(buf.AsSpan(4));
        return buf;
    }
}

/// <summary>
/// Request to enable a table.
/// Command: Tables / EnableTable (class 0x01, command 0x02).
/// Payload: 2-byte entity ID (LE).
/// </summary>
public sealed class EnableTableRequest(ushort tableId) : Request(WireFormat.ClassTables, WireFormat.TablesEnableTable,
    BitConverter.GetBytes(tableId))
{
    public ushort TableId { get; } = tableId;
}

/// <summary>
/// Request to disable a table.
/// Command: Tables / DisableTable (class 0x01, command 0x03).
/// Payload: 2-byte entity ID (LE).
/// </summary>
public sealed class DisableTableRequest(ushort tableId) : Request(WireFormat.ClassTables, WireFormat.TablesDisableTable,
    BitConverter.GetBytes(tableId))
{
    public ushort TableId { get; } = tableId;
}

/// <summary>
/// Request to set table data at specific offsets.
/// Command: Tables / SetDataAtOffsets (class 0x01, command 0x04).
/// Payload: 2-byte entity ID (LE) + offset/value pairs.
/// </summary>
public sealed class SetTableDataRequest : Request
{
    public ushort TableId { get; }

    public SetTableDataRequest(ushort tableId, byte[] payload)
        : base(WireFormat.ClassTables, WireFormat.TablesSetDataAtOffsets, payload)
    {
        TableId = BinaryPrimitives.ReadUInt16LittleEndian(payload);
    }
}

/// <summary>
/// Request to get table data at specific offsets.
/// Command: Tables / GetDataAtOffset (class 0x01, command 0x05).
/// Payload: 2-byte entity ID (LE) + offset list.
/// </summary>
public sealed class GetTableDataRequest : Request
{
    public ushort TableId { get; }

    public GetTableDataRequest(ushort tableId, byte[] payload)
        : base(WireFormat.ClassTables, WireFormat.TablesGetDataAtOffset, payload)
    {
        TableId = BinaryPrimitives.ReadUInt16LittleEndian(payload);
    }
}

/// <summary>
/// Request to store a table in non-volatile memory.
/// Command: Tables / StoreInNVM (class 0x01, command 0x06).
/// Payload: 2-byte entity ID (LE).
/// </summary>
public sealed class StoreTableRequest(ushort tableId) : Request(WireFormat.ClassTables, WireFormat.TablesStoreInNvm,
    BitConverter.GetBytes(tableId))
{
    public ushort TableId { get; } = tableId;
}

/// <summary>
/// Request to set table reporting configuration.
/// Command: Tables / SetTableReporting (class 0x01, command 0x07).
/// Payload: 2-byte entity ID (LE) + reporting config data.
/// </summary>
public sealed class SetTableReportingRequest : Request
{
    public ushort TableId { get; }

    public SetTableReportingRequest(ushort tableId, byte[] payload)
        : base(WireFormat.ClassTables, WireFormat.TablesSetTableReporting, payload)
    {
        TableId = BinaryPrimitives.ReadUInt16LittleEndian(payload);
    }
}

// ─── Table Responses ────────────────────────────────────────────────────

/// <summary>
/// Response to a SetTable request.
/// </summary>
public sealed class SetTableResponse : Response
{
    public ushort TableId { get; }

    public SetTableResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassTables, WireFormat.TablesSetTable, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        if (payload.Length > 1)
            TableId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1));
    }
}

/// <summary>
/// Response to a GetTable request.
/// Payload (after status + entity ID + dataSize): serialized table data.
/// Wire format: [status:1][tableId:2][dataSize:2][tableData:dataSize bytes]
/// </summary>
public sealed class GetTableResponse : Response
{
    public ushort TableId { get; }

    public ReadOnlyMemory<byte> SerializedTable { get; }

    public GetTableResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassTables, WireFormat.TablesGetTable, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        if (payload.Length > 4)
        {
            TableId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1));
            var dataSize = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(3));
            SerializedTable = Payload.Slice(5, dataSize);
        }
    }
}

/// <summary>
/// Response to an EnableTable request.
/// </summary>
public sealed class EnableTableResponse : Response
{
    public ushort TableId { get; }

    public EnableTableResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassTables, WireFormat.TablesEnableTable, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        if (payload.Length > 1)
            TableId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1));
    }
}

/// <summary>
/// Response to a DisableTable request.
/// </summary>
public sealed class DisableTableResponse : Response
{
    public ushort TableId { get; }

    public DisableTableResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassTables, WireFormat.TablesDisableTable, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        if (payload.Length > 1)
            TableId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1));
    }
}

/// <summary>
/// Response to a SetTableData request.
/// </summary>
public sealed class SetTableDataResponse : Response
{
    public ushort TableId { get; }

    /// <summary>Element type of the table data.</summary>
    public byte ElemType { get; }

    /// <summary>Offsets that were set.</summary>
    public ushort[]? Offsets { get; }

    /// <summary>Values that were set.</summary>
    public byte[]? Values { get; }

    public SetTableDataResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassTables, WireFormat.TablesSetDataAtOffsets, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        if (payload.Length > 2)
        {
            TableId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1));
            var offset = 3;
            if (offset < payload.Length)
            {
                ElemType = payload[offset++];
                Offsets = [];
                Values = payload[offset..].ToArray();
            }
        }
    }
}

/// <summary>
/// Response to a GetTableData request.
/// </summary>
public sealed class GetTableDataResponse : Response
{
    public ushort TableId { get; }

    public GetTableDataResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassTables, WireFormat.TablesGetDataAtOffset, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        if (payload.Length > 2)
            TableId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1));
    }
}

/// <summary>
/// Response to a StoreTable request.
/// </summary>
public sealed class StoreTableResponse : Response
{
    public ushort TableId { get; }

    public StoreTableResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassTables, WireFormat.TablesStoreInNvm, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        if (payload.Length > 1)
            TableId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1));
    }
}

/// <summary>
/// Response to a SetTableReporting request.
/// </summary>
public sealed class SetTableReportingResponse : Response
{
    public ushort TableId { get; }

    public SetTableReportingResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassTables, WireFormat.TablesSetTableReporting, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        if (payload.Length > 1)
            TableId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(1));
    }
}

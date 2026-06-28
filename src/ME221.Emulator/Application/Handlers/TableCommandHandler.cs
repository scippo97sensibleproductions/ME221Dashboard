using System.Buffers.Binary;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using ME221.Data.Models;
using ME221.Emulator.Domain;
using ME221.Emulator.Messages;

namespace ME221.Emulator.Application.Handlers;

public sealed class TableCommandHandler(EntityStore entityStore, EcuState state) : ICommandHandler
{
    private readonly EcuState _state = state;

    public bool CanHandle(byte classId, byte command)
    {
        return classId == WireFormat.ClassTables;
    }

    public ValueTask<MessageFrame> HandleAsync(MessageFrame request)
    {
        var response = request.Command switch
        {
            WireFormat.TablesSetTable => HandleSetTable(request),
            WireFormat.TablesGetTable => HandleGetTable(request),
            WireFormat.TablesEnableTable => HandleEnableTable(request),
            WireFormat.TablesDisableTable => HandleDisableTable(request),
            WireFormat.TablesSetDataAtOffsets => HandleSetDataAtOffsets(request),
            WireFormat.TablesGetDataAtOffset => HandleGetDataAtOffset(request),
            WireFormat.TablesStoreInNvm => HandleStoreInNvm(request),
            WireFormat.TablesSetTableReporting => HandleSetTableReporting(request),
            _ => new StatusResponse(request.Class, request.Command, MessageStatus.UnsupportedRequest),
        };

        return ValueTask.FromResult(response);
    }

    private MessageFrame HandleSetTable(MessageFrame request)
    {
        var payload = request.Payload;
        if (payload.Length < 6) // id:2 + type:1 + enabled:1 + rows:1 + cols:1 = 6
            return new StatusResponse(WireFormat.ClassTables, WireFormat.TablesSetTable, MessageStatus.InvalidParameter);

        var tableId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span);
        var tableDef = entityStore.Calibration.Tables.FirstOrDefault(t => t.Id == tableId);
        if (tableDef is null)
            return new StatusResponse(WireFormat.ClassTables, WireFormat.TablesSetTable, MessageStatus.InvalidParameter);

        var offset = 2;
        offset++; // skip type byte
        var enabled = payload.Span[offset++] != 0;
        var rows = payload.Span[offset++];
        var cols = payload.Span[offset++];

        if (rows != tableDef.Rows || cols != tableDef.Cols)
            return new StatusResponse(WireFormat.ClassTables, WireFormat.TablesSetTable, MessageStatus.InvalidParameter);

        var input1 = new float[rows];
        if (rows > 1)
        {
            for (var i = 0; i < rows; i++)
            {
                input1[i] = BinaryPrimitives.ReadSingleLittleEndian(payload.Span[offset..]);
                offset += 4;
            }
        }

        var input0 = new float[cols];
        for (var i = 0; i < cols; i++)
        {
            input0[i] = BinaryPrimitives.ReadSingleLittleEndian(payload.Span[offset..]);
            offset += 4;
        }

        var output = new float[rows * cols];
        for (var i = 0; i < rows * cols; i++)
        {
            output[i] = BinaryPrimitives.ReadSingleLittleEndian(payload.Span[offset..]);
            offset += 4;
        }

        entityStore.SetTableOutput(tableId, output);
        entityStore.SetTableInput0(tableId, input0);
        entityStore.SetTableInput1(tableId, input1);
        entityStore.SetTableEnabled(tableId, enabled);

        return new SetTableResponse([(byte)MessageStatus.Success]);
    }

    private MessageFrame HandleGetTable(MessageFrame request)
    {
        var payload = request.Payload;
        if (payload.Length < 2)
            return new StatusResponse(WireFormat.ClassTables, WireFormat.TablesGetTable, MessageStatus.InvalidParameter);

        var tableId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span);
        var tableDef = entityStore.Calibration.Tables.FirstOrDefault(t => t.Id == tableId);
        if (tableDef is null)
            return new StatusResponse(WireFormat.ClassTables, WireFormat.TablesGetTable, MessageStatus.Failure);

        entityStore.TryGetTableOutput(tableId, out var output);
        entityStore.TryGetTableInput0(tableId, out var input0);
        entityStore.TryGetTableInput1(tableId, out var input1);

        var serialized = SerializeTable(tableDef, entityStore.IsTableEnabled(tableId), input0, input1, output);
        var responsePayload = new byte[1 + 2 + 2 + serialized.Length];
        responsePayload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteUInt16LittleEndian(responsePayload.AsSpan(1), tableId);
        BinaryPrimitives.WriteUInt16LittleEndian(responsePayload.AsSpan(3), (ushort)serialized.Length);
        serialized.CopyTo(responsePayload.AsSpan(5));

        return new GetTableResponse(responsePayload);
    }

    private static byte[] SerializeTable(TableDefinition tableDef, bool enabled, float[]? input0, float[]? input1, float[]? output)
    {
        var rows = tableDef.Rows;
        var cols = tableDef.Cols;

        input0 ??= new float[cols];
        input1 ??= new float[rows];

        if (output is null || output.Length != rows * cols)
            output = new float[rows * cols];

        var typeByte = tableDef.TableType switch
        {
            "T1x16" => (byte)0,
            "T1x32" => (byte)1,
            "T32x32" => (byte)3,
            _ => (byte)2, // T16x16 default
        };

        var headerSize = 4;
        var input1Size = rows > 1 ? rows * 4 : 0;
        var input0Size = cols * 4;
        var outputSize = rows * cols * 4;
        var buf = new byte[headerSize + input1Size + input0Size + outputSize];
        var offset = 0;

        buf[offset++] = typeByte;
        buf[offset++] = (byte)(enabled ? 1 : 0);
        buf[offset++] = (byte)rows;
        buf[offset++] = (byte)cols;

        if (rows > 1)
        {
            for (var i = 0; i < rows; i++)
            {
                BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(offset), input1[i]);
                offset += 4;
            }
        }

        for (var i = 0; i < cols; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(offset), input0[i]);
            offset += 4;
        }

        for (var i = 0; i < rows * cols; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(buf.AsSpan(offset), output[i]);
            offset += 4;
        }

        return buf;
    }

    private MessageFrame HandleEnableTable(MessageFrame request)
    {
        var payload = request.Payload;
        if (payload.Length < 2)
            return new StatusResponse(WireFormat.ClassTables, WireFormat.TablesEnableTable, MessageStatus.InvalidParameter);

        var tableId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span);
        if (!entityStore.Calibration.Tables.Any(t => t.Id == tableId))
            return new StatusResponse(WireFormat.ClassTables, WireFormat.TablesEnableTable, MessageStatus.Failure);

        entityStore.SetTableEnabled(tableId, true);
        var responsePayload = new byte[3];
        responsePayload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteUInt16LittleEndian(responsePayload.AsSpan(1), tableId);
        return new EnableTableResponse(responsePayload);
    }

    private MessageFrame HandleDisableTable(MessageFrame request)
    {
        var payload = request.Payload;
        if (payload.Length < 2)
            return new StatusResponse(WireFormat.ClassTables, WireFormat.TablesDisableTable, MessageStatus.InvalidParameter);

        var tableId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span);
        if (!entityStore.Calibration.Tables.Any(t => t.Id == tableId))
            return new StatusResponse(WireFormat.ClassTables, WireFormat.TablesDisableTable, MessageStatus.Failure);

        entityStore.SetTableEnabled(tableId, false);
        var responsePayload = new byte[3];
        responsePayload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteUInt16LittleEndian(responsePayload.AsSpan(1), tableId);
        return new DisableTableResponse(responsePayload);
    }

    private MessageFrame HandleSetDataAtOffsets(MessageFrame request)
    {
        var payload = request.Payload;
        if (payload.Length < 5) // id:2 + elemType:1 + count:2 = 5 minimum
            return new StatusResponse(WireFormat.ClassTables, WireFormat.TablesSetDataAtOffsets, MessageStatus.InvalidParameter);

        var tableId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span);
        if (!entityStore.Calibration.Tables.Any(t => t.Id == tableId))
            return new StatusResponse(WireFormat.ClassTables, WireFormat.TablesSetDataAtOffsets, MessageStatus.InvalidParameter);

        var elemType = payload.Span[2];
        var count = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span[3..]);

        entityStore.TryGetTableOutput(tableId, out var output);
        entityStore.TryGetTableInput0(tableId, out var input0);
        entityStore.TryGetTableInput1(tableId, out var input1);

        var offset = 5;
        for (var i = 0; i < count; i++)
        {
            if (offset + 6 > payload.Length) break;
            var cellOffset = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span[offset..]);
            offset += 2;
            var value = BinaryPrimitives.ReadSingleLittleEndian(payload.Span[offset..]);
            offset += 4;

            switch (elemType)
            {
                case 0: // INPUT_0 → column axis
                    if (input0 is not null && cellOffset < (uint)input0.Length)
                        input0[cellOffset] = value;
                    break;
                case 1: // INPUT_1 → row axis
                    if (input1 is not null && cellOffset < (uint)input1.Length)
                        input1[cellOffset] = value;
                    break;
                case 2: // OUTPUT
                    if (output is not null && cellOffset < (uint)output.Length)
                        output[cellOffset] = value;
                    break;
            }
        }

        if (output is not null) entityStore.SetTableOutput(tableId, output);
        if (input0 is not null) entityStore.SetTableInput0(tableId, input0);
        if (input1 is not null) entityStore.SetTableInput1(tableId, input1);

        return new SetTableDataResponse([(byte)MessageStatus.Success]);
    }

    private static MessageFrame HandleGetDataAtOffset(MessageFrame request)
    {
        return new GetTableDataResponse([(byte)MessageStatus.Success]);
    }

    private static MessageFrame HandleStoreInNvm(MessageFrame request)
    {
        return new StoreTableResponse([(byte)MessageStatus.Success]);
    }

    private static MessageFrame HandleSetTableReporting(MessageFrame request)
    {
        return new SetTableReportingResponse([(byte)MessageStatus.Success]);
    }
}

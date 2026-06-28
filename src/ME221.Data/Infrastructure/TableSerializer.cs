using System.Buffers.Binary;
using ME221.Data.Models;

namespace ME221.Data.Infrastructure;

public static class TableSerializer
{
    public static byte[] Serialize(TableDefinition tableDef, bool enabled, float[] input0, float[] input1, float[] output)
    {
        var rows = tableDef.Rows;
        var cols = tableDef.Cols;

        var typeByte = tableDef.TableType switch
        {
            "T1x16" => (byte)0,
            "T1x32" => (byte)1,
            "T32x32" => (byte)3,
            _ => (byte)2,
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

    public static TableWireData Deserialize(TableDefinition tableDef, ReadOnlySpan<byte> data)
    {
        var rows = tableDef.Rows;
        var cols = tableDef.Cols;

        if (data.Length < 4)
            throw new ArgumentException("Table data too short for header", nameof(data));

        var offset = 0;
        var typeByte = data[offset++];
        var enabled = data[offset++] != 0;
        var wireRows = data[offset++];
        var wireCols = data[offset++];

        if (wireRows != rows || wireCols != cols)
            throw new ArgumentException($"Table dimension mismatch: expected {rows}x{cols}, got {wireRows}x{wireCols}");

        var input1 = new float[rows];
        if (rows > 1)
        {
            for (var i = 0; i < rows; i++)
            {
                input1[i] = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
                offset += 4;
            }
        }

        var input0 = new float[cols];
        for (var i = 0; i < cols; i++)
        {
            input0[i] = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
            offset += 4;
        }

        var output = new float[rows * cols];
        for (var i = 0; i < rows * cols; i++)
        {
            output[i] = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
            offset += 4;
        }

        return new TableWireData(enabled, input0, input1, output);
    }
}

public sealed record TableWireData(bool Enabled, float[] Input0, float[] Input1, float[] Output);

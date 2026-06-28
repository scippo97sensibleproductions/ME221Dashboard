using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace ME221.Data.Infrastructure;

public static class DriverSerializer
{
    /// <summary>
    /// Serialize driver data to the wire format.
    /// Wire format: [configCount:1][outputCount:1][inputCount:1][configs:N*4][outputs:M*2][inputs:K*2]
    /// </summary>
    public static byte[] Serialize(ReadOnlySpan<float> configs, ReadOnlySpan<ushort> outputIds, ReadOnlySpan<ushort> inputIds)
    {
        var totalSize = 3 + configs.Length * 4 + outputIds.Length * 2 + inputIds.Length * 2;
        var buf = new byte[totalSize];
        Serialize(configs, outputIds, inputIds, buf);
        return buf;
    }

    public static byte[] Serialize(float[] configs, List<ushort> outputIds, List<ushort> inputIds)
        => Serialize(configs.AsSpan(), CollectionsMarshal.AsSpan(outputIds), CollectionsMarshal.AsSpan(inputIds));

    /// <summary>
    /// Serialize driver data into the provided buffer.
    /// Returns the number of bytes written.
    /// </summary>
    public static int Serialize(ReadOnlySpan<float> configs, ReadOnlySpan<ushort> outputIds, ReadOnlySpan<ushort> inputIds, Span<byte> destination)
    {
        var offset = 0;

        destination[offset++] = (byte)configs.Length;
        destination[offset++] = (byte)outputIds.Length;
        destination[offset++] = (byte)inputIds.Length;

        foreach (var cfg in configs)
        {
            BinaryPrimitives.WriteSingleLittleEndian(destination[offset..], cfg);
            offset += 4;
        }

        foreach (var id in outputIds)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destination[offset..], id);
            offset += 2;
        }

        foreach (var id in inputIds)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(destination[offset..], id);
            offset += 2;
        }

        return offset;
    }

    /// <summary>
    /// Deserialize driver wire data.
    /// Wire format: [configCount:1][outputCount:1][inputCount:1][configs:N*4][outputs:M*2][inputs:K*2]
    /// </summary>
    public static DriverWireData Deserialize(ReadOnlySpan<byte> data)
    {
        if (data.Length < 3)
            throw new ArgumentException("Driver data too short for header (need at least 3 bytes)", nameof(data));

        var offset = 0;
        var configCount = data[offset++];
        var outputCount = data[offset++];
        var inputCount = data[offset++];

        var expectedLength = 3 + configCount * 4 + outputCount * 2 + inputCount * 2;
        if (data.Length < expectedLength)
            throw new ArgumentException($"Driver data too short: expected {expectedLength} bytes, got {data.Length}");

        var configs = new float[configCount];
        for (var i = 0; i < configCount; i++)
        {
            configs[i] = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
            offset += 4;
        }

        var outputIds = new List<ushort>(outputCount);
        for (var i = 0; i < outputCount; i++)
        {
            outputIds.Add(BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]));
            offset += 2;
        }

        var inputIds = new List<ushort>(inputCount);
        for (var i = 0; i < inputCount; i++)
        {
            inputIds.Add(BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]));
            offset += 2;
        }

        return new DriverWireData(configs, outputIds, inputIds);
    }

    /// <summary>
    /// Returns the serialized size for a driver with the given counts, without allocating.
    /// </summary>
    public static int GetSerializedSize(int configCount, int outputCount, int inputCount)
        => 3 + configCount * 4 + outputCount * 2 + inputCount * 2;
}

public sealed record DriverWireData(float[] Configs, List<ushort> OutputIds, List<ushort> InputIds);

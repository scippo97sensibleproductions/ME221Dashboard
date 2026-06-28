using System.Buffers.Binary;
using ME221.Comms;
using ME221.Comms.Messages;

namespace ME221Dashboard.Comms;

/// <summary>
/// Zero-allocation report parsers for the ME221 ECU live data protocol.
/// Extracted from LiveDataService to enable benchmarking and reuse across platforms.
/// </summary>
public static class ReportParser
{
    /// <summary>
    /// Zero-allocation V2 report parser — fills a pre-allocated buffer with ReportEntity values.
    /// Returns the number of entities successfully parsed.
    ///
    /// Payload format: [status:1][value1][value2]... — no IDs, no type bytes.
    /// Order and types come from the <paramref name="map"/> dictionary.
    /// </summary>
    public static int ParseV2Report(
        ReadOnlySpan<byte> payload,
        Span<ReportEntity> buffer,
        ReadOnlySpan<(ushort Id, ReportingType Type, int Size)> map)
    {
        if (payload.Length < 1 || payload[0] != (byte)MessageStatus.Success)
            return 0;

        var offset = 1;
        var count = 0;

        for (var i = 0; i < map.Length; i++)
        {
            var (id, type, size) = map[i];
            if (offset + size > payload.Length)
                break;

            var value = type switch
            {
                ReportingType.Float4B => BinaryPrimitives.ReadSingleLittleEndian(payload[offset..]),
                ReportingType.Int2B => BinaryPrimitives.ReadInt16LittleEndian(payload[offset..]),
                ReportingType.Uint2B => BinaryPrimitives.ReadUInt16LittleEndian(payload[offset..]),
                ReportingType.Int1B => (sbyte)payload[offset],
                ReportingType.Uint1B => payload[offset],
                ReportingType.Bool1B => payload[offset],
                _ => BinaryPrimitives.ReadSingleLittleEndian(payload[offset..]),
            };

            buffer[count++] = new ReportEntity(id, value);
            offset += size;
        }

        return count;
    }
}

using System.Buffers.Binary;

namespace ME221.Comms.Messages;

/// <summary>
/// Parsed log metadata from a <see cref="GetLogsSummaryResponse"/> payload.
/// Matches the 17-byte serialized format from the legacy MEITE reference.
/// </summary>
public readonly struct LogInfo
{
    /// <summary>Serialized size in bytes (17).</summary>
    public const int SerializedSize = 17;

    /// <summary>Log entry ID.</summary>
    public byte Id { get; }

    /// <summary>Log timestamp (day/month are packed; year is stored as ushort offset).</summary>
    public DateTime Timestamp { get; }

    /// <summary>Duration in seconds.</summary>
    public float DurationSeconds { get; }

    /// <summary>Number of log entries.</summary>
    public uint NumEntries { get; }

    /// <summary>Number of channels logged.</summary>
    public ushort NumOfChannels { get; }

    /// <summary>
    /// Parses a <see cref="LogInfo"/> from a 17-byte span.
    /// Layout: [Id:1] [Day:1] [Month:1] [Year:2] [Hour:1] [Minute:1] [Duration:4] [NumEntries:4] [NumOfChannels:2]
    /// </summary>
    public static LogInfo Parse(ReadOnlySpan<byte> span)
    {
        var id = span[0];
        var day = span[1];
        var month = span[2];
        var year = BinaryPrimitives.ReadUInt16LittleEndian(span[3..]);
        var hour = span[5];
        var minute = span[6];
        var duration = BinaryPrimitives.ReadSingleLittleEndian(span[7..]);
        var numEntries = BinaryPrimitives.ReadUInt32LittleEndian(span[11..]);
        var numChannels = BinaryPrimitives.ReadUInt16LittleEndian(span[15..]);

        return new LogInfo(id, day, month, year, hour, minute, duration, numEntries, numChannels);
    }

    private LogInfo(byte id, byte day, byte month, ushort year, byte hour, byte minute,
        float duration, uint numEntries, ushort numChannels)
    {
        Id = id;
        Timestamp = new DateTime(year, month, day, hour, minute, 0);
        DurationSeconds = duration;
        NumEntries = numEntries;
        NumOfChannels = numChannels;
    }
}

using System.Buffers.Binary;

namespace ME221.Comms.Messages;

/// <summary>
/// A single report entity with an ID and a float value.
/// Used when parsing <see cref="SendReportResponse"/> or <see cref="ReportResponse"/> payloads.
/// </summary>
public readonly struct ReportEntity
{
    /// <summary>The entity/report ID.</summary>
    public ushort Id { get; }

    /// <summary>The reported value.</summary>
    public float Value { get; }

    /// <summary>Creates a new report entity.</summary>
    /// <param name="id">Entity/report ID.</param>
    /// <param name="value">Reported float value.</param>
    public ReportEntity(ushort id, float value)
    {
        Id = id;
        Value = value;
    }

    /// <summary>
    /// Parses a <see cref="ReportEntity"/> from a 6-byte span:
    /// [2 bytes ID LE] [4 bytes float LE].
    /// </summary>
    public static ReportEntity Parse(ReadOnlySpan<byte> span)
    {
        var id = BinaryPrimitives.ReadUInt16LittleEndian(span);
        var value = BinaryPrimitives.ReadSingleLittleEndian(span[2..]);
        return new ReportEntity(id, value);
    }
}

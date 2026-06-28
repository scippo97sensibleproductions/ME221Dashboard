namespace ME221.Comms.Messages;

/// <summary>
/// Base class for entity messages (drivers, tables, data links) that carry a 2-byte entity ID.
/// The entity ID is stored as the first 2 bytes of the payload (little-endian ushort).
/// </summary>
public abstract class EntityMessageRequest(byte classId, byte command, ushort entityId, ReadOnlyMemory<byte> payload)
    : Request(classId, command, payload)
{
    /// <summary>The entity ID (driver, table, or data link identifier).</summary>
    public ushort EntityId { get; } = entityId;
}

/// <summary>
/// Base class for entity responses that carry a 2-byte entity ID.
/// </summary>
public abstract class EntityMessageResponse(
    byte classId,
    byte command,
    MessageStatus status,
    ushort entityId,
    ReadOnlyMemory<byte> payload)
    : Response(classId, command, status, payload)
{
    /// <summary>The entity ID (driver, table, or data link identifier).</summary>
    public ushort EntityId { get; } = entityId;
}

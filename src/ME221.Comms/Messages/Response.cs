using ME221.Comms.Internal;

namespace ME221.Comms.Messages;

/// <summary>
/// Base class for all response message frames (type 0x0F).
/// Every response starts with a <see cref="Status"/> byte followed by class-specific data.
/// </summary>
public abstract class Response : MessageFrame
{
    public MessageStatus Status { get; }

    protected Response(byte classId, byte command, MessageStatus status, ReadOnlyMemory<byte> payload)
        : base(WireFormat.ResponseType, classId, command, payload)
    {
        Status = status;
    }

    protected Response(byte classId, byte command, MessageStatus status)
        : base(WireFormat.ResponseType, classId, command, ReadOnlyMemory<byte>.Empty)
    {
        Status = status;
    }

    /// <summary>
    /// Copies a span into a new heap-allocated array and returns it as ReadOnlyMemory.
    /// For the short-lived message objects in this protocol layer, heap allocation
    /// is more appropriate than ArrayPool — pooled arrays are never returned, starving the pool.
    /// </summary>
    protected static ReadOnlyMemory<byte> CopyPayload(ReadOnlySpan<byte> source)
    {
        return source.IsEmpty ? ReadOnlyMemory<byte>.Empty : source.ToArray();
    }
}

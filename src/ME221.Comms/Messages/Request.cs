using ME221.Comms.Internal;

namespace ME221.Comms.Messages;

/// <summary>
/// Base class for all request message frames (type 0x00).
/// </summary>
public abstract class Request(byte classId, byte command, ReadOnlyMemory<byte> payload)
    : MessageFrame(WireFormat.RequestType, classId, command, payload);

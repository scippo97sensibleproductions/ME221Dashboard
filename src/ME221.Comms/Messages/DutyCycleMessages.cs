using ME221.Comms.Internal;

namespace ME221.Comms.Messages;

/// <summary>
/// Request to set the DBW (duty cycle) value.
/// Command: DBW / SetDBWDuty (class 0x08, command 0x00).
/// </summary>
public sealed class SetDbwDutyRequest(byte[] payload) : Request(WireFormat.ClassDbw, WireFormat.DbwSetDbwDuty, payload);

/// <summary>
/// Response to a SetDBWDuty request.
/// </summary>
public sealed class SetDbwDutyResponse(MessageStatus status)
    : Response(WireFormat.ClassDbw, WireFormat.DbwSetDbwDuty, status);

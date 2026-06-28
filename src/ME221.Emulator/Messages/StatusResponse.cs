using ME221.Comms.Messages;

namespace ME221.Emulator.Messages;

public sealed class StatusResponse(byte classId, byte command, MessageStatus status)
    : Response(classId, command, status);

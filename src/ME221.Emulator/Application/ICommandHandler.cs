using ME221.Comms.Messages;

namespace ME221.Emulator.Application;

public interface ICommandHandler
{
    bool CanHandle(byte classId, byte command);
    ValueTask<MessageFrame> HandleAsync(MessageFrame request);
}

namespace ME221.Emulator.Application;

public sealed class CommandRouter
{
    private readonly List<ICommandHandler> _handlers = [];

    public void Register(ICommandHandler handler)
    {
        _handlers.Add(handler);
    }

    public ICommandHandler? TryRoute(byte classId, byte command)
    {
        foreach (var handler in _handlers)
        {
            if (handler.CanHandle(classId, command))
                return handler;
        }

        return null;
    }
}

using Microsoft.Extensions.Logging;

namespace ME221.Comms.Tests;

internal static partial class MockChannelLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "MockChannel: configured send failure")]
    public static partial void LogSendFailure(ILogger logger);

    [LoggerMessage(Level = LogLevel.Trace, Message = "MockChannel: sent {Length} bytes")]
    public static partial void LogSentBytes(ILogger logger, int length);

    [LoggerMessage(Level = LogLevel.Information, Message = "MockChannel: opened")]
    public static partial void LogOpened(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "MockChannel: closed")]
    public static partial void LogClosed(ILogger logger);
}

using Microsoft.Extensions.Logging;

namespace ME221.Comms.Tests;

internal static partial class OfflineChannelLog
{
    [LoggerMessage(Level = LogLevel.Trace, Message = "OfflineChannel: send ignored (offline mode)")]
    public static partial void LogSendIgnored(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "OfflineChannel: opened (still offline)")]
    public static partial void LogOpened(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "OfflineChannel: closed")]
    public static partial void LogClosed(ILogger logger);
}

using Microsoft.Extensions.Logging;

namespace ME221.Comms.Internal;

internal static partial class RequestCorrelatorLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "RequestCorrelator: registered request {Type} {ClassId} {Command}")]
    public static partial void LogRegisteredRequest(ILogger logger, byte type, byte classId, byte command);

    [LoggerMessage(Level = LogLevel.Debug, Message = "RequestCorrelator: correlated response {Type} {ClassId} {Command}")]
    public static partial void LogCorrelatedResponse(ILogger logger, byte type, byte classId, byte command);

    [LoggerMessage(Level = LogLevel.Debug, Message = "RequestCorrelator: no pending request for {Type} {ClassId} {Command}")]
    public static partial void LogNoPendingRequest(ILogger logger, byte type, byte classId, byte command);

    [LoggerMessage(Level = LogLevel.Debug, Message = "RequestCorrelator: TryCorrelate key=({ClassId:X2},{Command:X2}) found={Found} taskCompleted={TaskCompleted}")]
    public static partial void LogTryCorrelate(ILogger logger, byte classId, byte command, bool found, bool taskCompleted);
}

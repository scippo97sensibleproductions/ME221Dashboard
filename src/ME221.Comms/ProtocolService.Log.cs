using Microsoft.Extensions.Logging;

namespace ME221.Comms;

internal static partial class ProtocolServiceLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "ProtocolService: sending {Type} {ClassId} {Command}")]
    public static partial void LogSendingRequest(ILogger logger, byte type, byte classId, byte command);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ProtocolService: received response {Type} {ClassId} {Command}")]
    public static partial void LogReceivedResponse(ILogger logger, byte type, byte classId, byte command);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ProtocolService: timeout waiting for response to {Type} {ClassId} {Command}")]
    public static partial void LogTimeoutWaitingForResponse(ILogger logger, byte type, byte classId, byte command);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ProtocolService: cancelled waiting for response to {Type} {ClassId} {Command}")]
    public static partial void LogCancelledWaitingForResponse(ILogger logger, byte type, byte classId, byte command);

    [LoggerMessage(Level = LogLevel.Trace, Message = "ProtocolService: sent fire-and-forget {Type} {ClassId} {Command}")]
    public static partial void LogSentFireAndForget(ILogger logger, byte type, byte classId, byte command);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ProtocolService: disposing")]
    public static partial void LogDisposing(ILogger logger);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Heartbeat (Ack) failed: {Error}")]
    public static partial void LogHeartbeatFailed(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ProtocolService: receive loop — frame arrived Type={Type} Class={ClassId:X2} Cmd={Command:X2}")]
    public static partial void LogFrameArrived(ILogger logger, byte type, byte classId, byte command);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ProtocolService: receive loop — frame was Response={IsResponse}, correlation succeeded={Correlated}")]
    public static partial void LogFrameCorrelationResult(ILogger logger, bool isResponse, bool correlated);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ProtocolService: receive loop cancelled")]
    public static partial void LogReceiveLoopCancelled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "ProtocolService: receive loop error: {Message}")]
    public static partial void LogReceiveLoopError(ILogger logger, Exception exception, string message);
}

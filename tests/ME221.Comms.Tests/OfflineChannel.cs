using ME221.Comms.Channels;
using ME221.Comms.Messages;
using Microsoft.Extensions.Logging;
using static ME221.Comms.Tests.OfflineChannelLog;

namespace ME221.Comms.Tests;

public sealed class OfflineChannel(ILogger<OfflineChannel>? logger = null) : IChannel
{
    private volatile DeviceStatus _status = DeviceStatus.Closed;

    public bool IsOpen => _status is DeviceStatus.Opened or DeviceStatus.Connected;

    public DeviceStatus Status => _status;

    public IAsyncEnumerable<MessageFrame> IncomingFrames => YieldEmpty();

    private static async IAsyncEnumerable<MessageFrame> YieldEmpty()
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        if (logger is not null)
        {
            LogSendIgnored(logger);
        }
        return Task.CompletedTask;
    }

    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        _status = DeviceStatus.Closed;
        if (logger is not null)
        {
            LogOpened(logger);
        }
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _status = DeviceStatus.Closed;
        if (logger is not null)
        {
            LogClosed(logger);
        }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

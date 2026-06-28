using System.Threading.Channels;
using ME221.Comms.Channels;
using ME221.Comms.Messages;
using Microsoft.Extensions.Logging;
using static ME221.Comms.Tests.MockChannelLog;

namespace ME221.Comms.Tests;

public sealed class MockChannel(ILogger<MockChannel>? logger = null) : IChannel
{
    private readonly Channel<MessageFrame> _incomingChannel = Channel.CreateUnbounded<MessageFrame>();
    private volatile DeviceStatus _status = DeviceStatus.Closed;
    private bool _failOnSend;
    private TimeSpan _sendDelay = TimeSpan.Zero;
    private TimeSpan _receiveDelay = TimeSpan.Zero;

    public void SetFailOnSend(bool fail) => _failOnSend = fail;

    public void SetSendDelay(TimeSpan delay)
    {
        _sendDelay = delay;
    }

    public void SetReceiveDelay(TimeSpan delay)
    {
        _receiveDelay = delay;
    }

    public void InjectFrame(MessageFrame frame)
    {
        if (_receiveDelay > TimeSpan.Zero)
        {
            Thread.Sleep(_receiveDelay);
        }
        _incomingChannel.Writer.TryWrite(frame);
    }

    public bool IsOpen => _status is DeviceStatus.Opened or DeviceStatus.Connected;

    public DeviceStatus Status => _status;

    public IAsyncEnumerable<MessageFrame> IncomingFrames => _incomingChannel.Reader.ReadAllAsync();

    public async Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        if (_failOnSend)
        {
            if (logger is not null)
            {
                LogSendFailure(logger);
            }
            throw new InvalidOperationException("MockChannel: configured send failure");
        }

        if (_sendDelay > TimeSpan.Zero)
        {
            await Task.Delay(_sendDelay, cancellationToken).ConfigureAwait(false);
        }

        if (logger is not null)
        {
            LogSentBytes(logger, frame.Length);
        }
    }

    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        _status = DeviceStatus.Opened;
        if (logger is not null)
        {
            LogOpened(logger);
        }
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _status = DeviceStatus.Closed;
        _incomingChannel.Writer.TryComplete();
        if (logger is not null)
        {
            LogClosed(logger);
        }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return new ValueTask(CloseAsync());
    }
}

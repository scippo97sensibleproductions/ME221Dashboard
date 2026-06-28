using ME221.Comms.Messages;

namespace ME221.Comms.Channels;

/// <summary>
/// Abstract interface for device communication channels.
/// All channels must be fully async and support cancellation.
/// </summary>
public interface IChannel : IAsyncDisposable
{
    /// <summary>
    /// Whether the channel is currently open and ready for communication.
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// The current device status.
    /// </summary>
    DeviceStatus Status { get; }

    /// <summary>
    /// Async enumerable of incoming message frames.
    /// Frames are yielded as they are received and parsed from the device.
    /// </summary>
    IAsyncEnumerable<MessageFrame> IncomingFrames { get; }

    /// <summary>
    /// Sends a raw byte frame to the device.
    /// </summary>
    /// <param name="frame">The frame bytes to send.</param>
    /// <param name="cancellationToken">Token to cancel the send operation.</param>
    /// <returns>A task that completes when the send is done.</returns>
    Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the channel and establishes the connection.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the open operation.</param>
    /// <returns>A task that completes when the channel is open.</returns>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the channel and releases resources.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the close operation.</param>
    /// <returns>A task that completes when the channel is closed.</returns>
    Task CloseAsync(CancellationToken cancellationToken = default);
}

using ME221.Comms.Cli.Config;
using ME221.Comms.Messages;
using ME221Dashboard.Comms;
using Microsoft.Extensions.Logging;

namespace ME221.Comms.Cli.Commands;

/// <summary>
/// Wraps ProtocolService lifecycle for CLI use.
/// Handles connection, baud rate fallback, and cleanup.
/// </summary>
public sealed class CommandExecutor : IAsyncDisposable
{
    private ProtocolService? _protocolService;
    private readonly CliSettings _settings;
    private ChannelOptions _options;
    private bool _isDisposed;

    public bool IsConnected => _protocolService?.IsOpen == true;

    public ProtocolService GetProtocolService() => _protocolService!;

    public CommandExecutor(string portName, int baudRate, CliSettings settings, ILogger<ProtocolService>? protocolLogger = null)
    {
        _settings = settings;
        _options = new ChannelOptions
        {
            PortName = portName,
            BaudRate = baudRate,
            SendTimeoutMs = settings.ConnectionTimeout,
            ReceiveTimeoutMs = settings.ConnectionTimeout
        };
        _protocolService = new ProtocolService(new SerialPortChannel(_options), protocolLogger);
    }

    /// <summary>
    /// Opens the connection to the device.
    /// Returns true if connection succeeded, false otherwise.
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _protocolService!.OpenAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to reconnect at the fallback baud rate.
    /// </summary>
    public async Task<bool> ReconnectAtFallbackAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _protocolService!.CloseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Ignore cleanup errors during reconnect attempt
        }

        var newOptions = new ChannelOptions
        {
            PortName = _options.PortName ?? "COM1",
            BaudRate = _settings.FallbackBaudRate,
            SendTimeoutMs = _settings.ConnectionTimeout,
            ReceiveTimeoutMs = _settings.ConnectionTimeout
        };

        // Dispose old channel and create new one
        await using var oldChannel = new SerialPortChannel(_options);
        await oldChannel.CloseAsync(cancellationToken).ConfigureAwait(false);

        _options = newOptions;
        _protocolService = new ProtocolService(new SerialPortChannel(_options)); // Logger is lost on reconnect — acceptable for edge case

        try
        {
            await _protocolService.OpenAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sends a request and awaits the matching response.
    /// Enforces a per-request timeout from settings.
    /// </summary>
    public async Task<TResponse> SendAsync<TResponse>(Request request, CancellationToken cancellationToken = default)
        where TResponse : Response
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_settings.ConnectionTimeout);
        return await _protocolService!.SendAsync<TResponse>(request, timeoutCts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a frame without waiting for a response (fire-and-forget).
    /// </summary>
    public async Task SendAsync(MessageFrame frame, CancellationToken cancellationToken = default)
    {
        await _protocolService!.SendAsync(frame, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends multiple frames sequentially.
    /// </summary>
    public async Task SendBatchAsync(IEnumerable<MessageFrame> frames, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frames);
        foreach (var frame in frames)
        {
            await _protocolService!.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            await _protocolService!.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Ignore disposal errors
        }
    }
}

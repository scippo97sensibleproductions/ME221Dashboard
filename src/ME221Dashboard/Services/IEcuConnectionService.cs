using ME221.Comms;

namespace ME221Dashboard.Services;

public class ConnectionStateChangedEventArgs(ConnectionState oldState, ConnectionState newState, string? error) : EventArgs
{
    public ConnectionState OldState { get; } = oldState;
    public ConnectionState NewState { get; } = newState;
    public string? Error { get; } = error;
}

public interface IEcuConnectionService
{
    ConnectionState State { get; }
    ProtocolInfo? ProtocolInfo { get; }
    string? LastError { get; }

    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    Task<bool> ConnectAsync(ConnectionTarget target, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    ProtocolService GetProtocolService();
    Task<ProtocolInfo> EnableReportingAsync(CancellationToken cancellationToken = default);
    Task DisableReportingAsync();
    void PauseHeartbeat();
    void ResumeHeartbeat();
}

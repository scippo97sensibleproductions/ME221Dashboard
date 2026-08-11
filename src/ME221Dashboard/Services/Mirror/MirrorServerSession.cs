namespace ME221Dashboard.Services.Mirror;

public enum MirrorSessionStatus
{
    AwaitingHello,
    PendingApproval,
    Approved,
    Closed,
}

/// <summary>
/// One viewer session on A (U2). Wraps the transport connection and the handshake
/// state; all mirror traffic out is text frames of the JSON wire protocol (KTD-2).
/// </summary>
public sealed class MirrorServerSession
{
    private readonly Fleck.IWebSocketConnection _socket;

    public MirrorServerSession(Fleck.IWebSocketConnection socket, string clientIp)
    {
        _socket = socket;
        ClientIp = clientIp;
    }

    public string ClientId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string? Token { get; set; }
    public string ClientIp { get; }
    public MirrorSessionStatus Status { get; set; } = MirrorSessionStatus.AwaitingHello;

    public Task SendAsync(object message) => _socket.Send(MirrorProtocol.Serialize(message));

    public void Close() => _socket.Close();
}
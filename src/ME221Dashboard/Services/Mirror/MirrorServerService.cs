using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services.Mirror;

/// <summary>
/// A's WSS mirror server (U2, KTD-1). Accepts viewer sessions, runs the hello →
/// version → token accept flow, enforces the per-A connection cap and the
/// pre-approval per-source-IP rate limit, and hosts the pending-approval flow with
/// the 60 s auto-deny (R2). Listens whenever the app runs.
/// </summary>
public sealed class MirrorServerService : IDisposable
{
    private readonly ApprovedDeviceStore _store;
    private readonly MirrorCertificateProvider _certificates;
    private readonly Func<string> _appVersion;
    private readonly ILogger<MirrorServerService> _logger;
    private Fleck.WebSocketServer? _server;
    private readonly Dictionary<string, MirrorServerSession> _sessions = new();
    private readonly Dictionary<string, List<DateTime>> _unapprovedAttempts = new();
    private readonly object _gate = new();

    public MirrorServerService(
        ApprovedDeviceStore store,
        MirrorCertificateProvider certificates,
        Func<string>? appVersion = null,
        ILogger<MirrorServerService>? logger = null)
    {
        _store = store;
        _certificates = certificates;
        _appVersion = appVersion ?? (() => AppInfo.Current.VersionString);
        _logger = logger ?? NullLogger<MirrorServerService>.Instance;
    }

    /// <summary>Raised when a viewer needs approval (banner, U11).</summary>
    public event Action<MirrorServerSession>? ApprovalRequested;

    /// <summary>Raised when a session transitions to approved (streaming hooks, U3).</summary>
    public event Action<MirrorServerSession>? SessionApproved;

    /// <summary>Raised when a session closes for any reason.</summary>
    public event Action<MirrorServerSession>? SessionClosed;

    /// <summary>Raised for every parsed non-hello message (ack routing, U3).</summary>
    public event Action<MirrorServerSession, object>? MessageReceived;

    public bool IsRunning => _server is not null;
    public int BoundPort { get; private set; }

    private int CountByStatus(MirrorSessionStatus status)
    {
        lock (_gate)
            return _sessions.Values.Count(s => s.Status == status);
    }

    public int ViewerCount => CountByStatus(MirrorSessionStatus.Approved);
    public int PendingCount => CountByStatus(MirrorSessionStatus.PendingApproval);
    public int AwaitingHelloCount => CountByStatus(MirrorSessionStatus.AwaitingHello);

    /// <summary>All live sessions (viewer list for the bridge, U5).</summary>
    public IReadOnlyList<MirrorServerSession> Viewers()
    {
        lock (_gate)
            return _sessions.Values.ToList();
    }

    /// <summary>Close every session (pairing reset, shutdown).</summary>
    public async Task DisconnectAllAsync(string reason)
    {
        List<MirrorServerSession> sessions;
        lock (_gate)
            sessions = _sessions.Values.ToList();

        foreach (var session in sessions)
        {
            try
            {
                await session.SendAsync(new CloseMessage { Reason = reason }).ConfigureAwait(false);
                session.Close();
            }
            catch { /* best effort */ }
        }
    }

    public void Start(int? port = null)
    {
        if (_server is not null)
            return;

        // Certificate loss with a surviving store: the tokens were bound to the old
        // certificate, so invalidate them before generating a fresh one (R20).
        if (_storeHasDevices() && !_certificates.CertificateFileExists)
            _store.ResetAsync().GetAwaiter().GetResult();

        var cert = _certificates.GetOrCreate();
        _server = new Fleck.WebSocketServer($"wss://0.0.0.0:{port ?? MirrorProtocol.WssPort}");
        _server.Certificate = cert;
        _server.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;
        _server.Start(OnClientConnected);
        BoundPort = _server.Port;
        _logger.LogInformation("Mirror server listening on wss://0.0.0.0:{Port}", BoundPort);
    }

    public void Stop()
    {
        if (_server is null)
            return;

        lock (_gate)
        {
            foreach (var session in _sessions.Values)
            {
                try { session.Close(); } catch { /* best effort */ }
            }
            _sessions.Clear();
        }

        _server.Dispose();
        _server = null;
        _logger.LogInformation("Mirror server stopped");
    }

    /// <summary>Reload the TLS certificate after a pairing-reset regenerated it (R20).</summary>
    public void ReloadCertificate()
    {
        if (_server is null)
            return;
        _server.Certificate = _certificates.GetOrCreate();
        _logger.LogInformation("Mirror server certificate reloaded");
    }

    public void Dispose() => Stop();

    // ── Approval control (called from the bridge, U5) ────────────────────────

    /// <summary>Approve the pending session: issue a token and notify the viewer (R2, R16).</summary>
    public async Task ApproveDeviceAsync(string clientId)
    {
        MirrorServerSession? session;
        lock (_gate)
        {
            session = _sessions.Values.FirstOrDefault(s => s.ClientId == clientId && s.Status == MirrorSessionStatus.PendingApproval);
            if (session is null)
                return;

            // Cap check and status mutation under the same lock — no TOCTOU window.
            if (CountByStatusLocked(MirrorSessionStatus.Approved) >= MirrorProtocol.ConnectionCap)
            {
                session.Status = MirrorSessionStatus.Closed;
                SendSafely(session, new ApprovalDecisionMessage { Approved = false });
                session.Close();
                return;
            }
            session.Status = MirrorSessionStatus.Approved;
        }

        var entry = await _store.AddAsync(clientId, session.DeviceName).ConfigureAwait(false);
        lock (_gate)
        {
            session.Token = entry.Token;
        }

        await SendSafelyAsync(session, new ApprovalDecisionMessage { Approved = true, Token = entry.Token }).ConfigureAwait(false);
        _logger.LogInformation("Viewer '{Device}' approved", session.DeviceName);
        SessionApproved?.Invoke(session);
    }

    private int CountByStatusLocked(MirrorSessionStatus status) =>
        _sessions.Values.Count(s => s.Status == status);

    /// <summary>Deny the pending session (R2).</summary>
    public async Task DenyDeviceAsync(string clientId)
    {
        MirrorServerSession? session;
        lock (_gate)
        {
            session = _sessions.Values.FirstOrDefault(s => s.ClientId == clientId && s.Status == MirrorSessionStatus.PendingApproval);
            if (session is null)
                return;
            // Mark closed under the lock so the 60s auto-deny task and the banner
            // deny cannot both send on the same session.
            session.Status = MirrorSessionStatus.Closed;
        }

        await SendSafelyAsync(session, new ApprovalDecisionMessage { Approved = false }).ConfigureAwait(false);
        await CloseGracefullyAsync(session).ConfigureAwait(false);
    }

    /// <summary>Revoke an approved viewer mid-session (R3, AE4).</summary>
    public async Task RevokeDeviceAsync(string clientId)
    {
        MirrorServerSession? session;
        lock (_gate)
        {
            session = _sessions.Values.FirstOrDefault(s => s.ClientId == clientId && s.Status == MirrorSessionStatus.Approved);
            if (session is null)
                return;
            session.Status = MirrorSessionStatus.Closed;
        }

        await SendSafelyAsync(session, new RevokedMessage { Reason = "revoked" }).ConfigureAwait(false);
        await CloseGracefullyAsync(session).ConfigureAwait(false);
    }

    /// <summary>Snapshot-scoped config changed: end every viewer session (R19, KTD-6).</summary>
    public async Task NotifyConfigUpdateAsync(string reason)
    {
        List<MirrorServerSession> sessions;
        lock (_gate)
            sessions = _sessions.Values.Where(s => s.Status == MirrorSessionStatus.Approved).ToList();

        foreach (var session in sessions)
        {
            await SendSafelyAsync(session, new ConfigUpdateMessage { Reason = reason }).ConfigureAwait(false);
            await CloseGracefullyAsync(session).ConfigureAwait(false);
        }
        _logger.LogInformation("Config update '{Reason}': ended {Count} viewer sessions", reason, sessions.Count);
    }

    /// <summary>Send with no unobserved exceptions; closed/dead sockets are tolerated.</summary>
    private static async Task SendSafelyAsync(MirrorServerSession session, object message)
    {
        try { await session.SendAsync(message).ConfigureAwait(false); }
        catch (Exception) { /* session is closing */ }
    }

    private static void SendSafely(MirrorServerSession session, object message)
    {
        try { session.SendAsync(message).ConfigureAwait(false); }
        catch (Exception) { /* session is closing */ }
    }

    /// <summary>
    /// Close after the final frame: Fleck's Close() right after SendAsync can abort the
    /// in-flight write (RST), so the last message never reaches the viewer. A short
    /// grace lets the frame flush before the socket closes.
    /// </summary>
    private static async Task CloseGracefullyAsync(MirrorServerSession session)
    {
        try
        {
            await Task.Delay(100).ConfigureAwait(false);
            session.Close();
        }
        catch { /* best effort */ }
    }

    // ── Accept flow ──────────────────────────────────────────────────────────

    private void OnClientConnected(Fleck.IWebSocketConnection socket)
    {
        var ip = socket.ConnectionInfo.ClientIpAddress ?? "unknown";
        var session = new MirrorServerSession(socket, ip);

        // Unauthenticated pre-hello connections are capped and expire: without this,
        // raw connections that never send a hello grow _sessions without bound and
        // never time out (the per-IP rate limit only runs after a hello parses).
        lock (_gate)
        {
            if (CountByStatusLocked(MirrorSessionStatus.AwaitingHello) >= MirrorProtocol.PreHelloCap)
            {
                _logger.LogWarning("Pre-hello connection cap reached; rejecting {Ip}", ip);
                socket.Close();
                return;
            }
            _sessions[socket.ConnectionInfo.Id.ToString()] = session;
        }

        socket.OnMessage = text => _ = HandleMessageAsync(session, text);
        socket.OnClose = () => OnSessionClosed(session);

        // A socket that never sends a hello is closed after a short window.
        _ = Task.Run(async () =>
        {
            await Task.Delay(MirrorProtocol.PreHelloTimeout).ConfigureAwait(false);
            lock (_gate)
            {
                if (session.Status != MirrorSessionStatus.AwaitingHello)
                    return;
                _sessions.Remove(socket.ConnectionInfo.Id.ToString());
            }
            try { socket.Close(); } catch { /* best effort */ }
        });
    }

    private async Task HandleMessageAsync(MirrorServerSession session, string text)
    {
        var parsed = MirrorProtocol.Parse(text);

        // Messages other than hello (snapshot ack etc.) route to the stream service.
        if (parsed is not HelloMessage)
        {
            if (parsed is not null)
                MessageReceived?.Invoke(session, parsed);
            return;
        }

        var hello = (HelloMessage)parsed;

        session.ClientId = hello.ClientId;
        session.DeviceName = hello.DeviceName;

        // Version gate (R4, AE3): identical app version only.
        if (hello.AppVersion != _appVersion())
        {
            await session.SendAsync(new HelloAckMessage { Status = "versionMismatch" }).ConfigureAwait(false);
            session.Close();
            return;
        }

        // Token check (R16, KTD-3): an approved device reconnects without a prompt.
        var approved = !string.IsNullOrEmpty(hello.Token)
            ? await _store.FindByTokenAsync(hello.Token).ConfigureAwait(false)
            : null;
        if (approved is not null && approved.ClientId == hello.ClientId)
        {
            lock (_gate)
            {
                if (CountByStatusLocked(MirrorSessionStatus.Approved) >= MirrorProtocol.ConnectionCap)
                {
                    session.Close();
                    return;
                }
                session.Status = MirrorSessionStatus.Approved;
                session.Token = approved.Token;
            }
            await SendSafelyAsync(session, new HelloAckMessage { Status = "approved" }).ConfigureAwait(false);
            _logger.LogInformation("Viewer '{Device}' reconnected", session.DeviceName);
            SessionApproved?.Invoke(session);
            return;
        }

        // Pre-approval rate limit (R2): 3 unapproved attempts per IP per 60 s.
        if (!RecordUnapprovedAttempt(session.ClientIp))
        {
            session.Close();
            return;
        }

        lock (_gate)
        {
            if (CountByStatusLocked(MirrorSessionStatus.PendingApproval) >= MirrorProtocol.PendingCap)
            {
                session.Close();
                return;
            }
            session.Status = MirrorSessionStatus.PendingApproval;
        }

        await SendSafelyAsync(session, new HelloAckMessage { Status = "pending" }).ConfigureAwait(false);
        _logger.LogInformation("Viewer '{Device}' ({Ip}) requests approval", session.DeviceName, session.ClientIp);
        ApprovalRequested?.Invoke(session);

        // Auto-deny after 60 s (R2). DenyDeviceAsync marks the session closed under
        // the lock, so a racing banner deny cannot double-send.
        _ = Task.Run(async () =>
        {
            await Task.Delay(MirrorProtocol.ApprovalTimeout).ConfigureAwait(false);
            MirrorServerSession? still;
            lock (_gate)
                still = _sessions.Values.FirstOrDefault(s => ReferenceEquals(s, session) && s.Status == MirrorSessionStatus.PendingApproval);
            if (still is not null)
                await DenyDeviceAsync(still.ClientId).ConfigureAwait(false);
        });
    }

    private bool RecordUnapprovedAttempt(string ip)
    {
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            // Prune stale entries so the dictionary cannot grow without bound.
            var stale = _unapprovedAttempts.Where(kvp => kvp.Value.Count == 0 || now - kvp.Value[^1] > MirrorProtocol.PerIpWindow).Select(kvp => kvp.Key).ToList();
            foreach (var key in stale)
                _unapprovedAttempts.Remove(key);

            if (!_unapprovedAttempts.TryGetValue(ip, out var attempts))
            {
                _unapprovedAttempts[ip] = [now];
                return true;
            }

            attempts.RemoveAll(t => now - t > MirrorProtocol.PerIpWindow);
            if (attempts.Count >= MirrorProtocol.PerIpAttemptLimit)
            {
                _logger.LogWarning("Rate limit hit for {Ip}: {Count} unapproved attempts", ip, attempts.Count);
                return false;
            }

            attempts.Add(now);
            return true;
        }
    }

    private void OnSessionClosed(MirrorServerSession session)
    {
        lock (_gate)
        {
            var key = _sessions.FirstOrDefault(kv => kv.Value == session).Key;
            if (key is not null)
                _sessions.Remove(key);
        }
        SessionClosed?.Invoke(session);
    }

    private bool _storeHasDevices()
    {
        try
        {
            return _store.GetAllAsync().GetAwaiter().GetResult().Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inspect the approved-device store");
            return false;
        }
    }
}
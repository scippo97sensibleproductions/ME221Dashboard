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

    public bool IsRunning => _server is not null;
    public int BoundPort { get; private set; }
    public int ViewerCount => _sessions.Values.Count(s => s.Status == MirrorSessionStatus.Approved);
    public int PendingCount => _sessions.Values.Count(s => s.Status == MirrorSessionStatus.PendingApproval);

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

    public void Dispose() => Stop();

    // ── Approval control (called from the bridge, U5) ────────────────────────

    /// <summary>Approve the pending session: issue a token and notify the viewer (R2, R16).</summary>
    public async Task ApproveDeviceAsync(string clientId)
    {
        MirrorServerSession? session;
        lock (_gate)
            session = _sessions.Values.FirstOrDefault(s => s.ClientId == clientId && s.Status == MirrorSessionStatus.PendingApproval);

        if (session is null)
            return;

        if (ViewerCount >= MirrorProtocol.ConnectionCap)
        {
            await session.SendAsync(new ApprovalDecisionMessage { Approved = false }).ConfigureAwait(false);
            session.Close();
            return;
        }

        var entry = await _store.AddAsync(clientId, session.DeviceName).ConfigureAwait(false);
        lock (_gate)
        {
            session.Status = MirrorSessionStatus.Approved;
            session.Token = entry.Token;
        }

        await session.SendAsync(new ApprovalDecisionMessage { Approved = true, Token = entry.Token }).ConfigureAwait(false);
        _logger.LogInformation("Viewer '{Device}' approved", session.DeviceName);
        SessionApproved?.Invoke(session);
    }

    /// <summary>Deny the pending session (R2).</summary>
    public async Task DenyDeviceAsync(string clientId)
    {
        MirrorServerSession? session;
        lock (_gate)
            session = _sessions.Values.FirstOrDefault(s => s.ClientId == clientId && s.Status == MirrorSessionStatus.PendingApproval);

        if (session is null)
            return;

        await session.SendAsync(new ApprovalDecisionMessage { Approved = false }).ConfigureAwait(false);
        session.Close();
    }

    /// <summary>Revoke an approved viewer mid-session (R3, AE4).</summary>
    public async Task RevokeDeviceAsync(string clientId)
    {
        MirrorServerSession? session;
        lock (_gate)
            session = _sessions.Values.FirstOrDefault(s => s.ClientId == clientId && s.Status == MirrorSessionStatus.Approved);

        if (session is null)
            return;

        await session.SendAsync(new RevokedMessage { Reason = "revoked" }).ConfigureAwait(false);
        session.Close();
    }

    /// <summary>Snapshot-scoped config changed: end every viewer session (R19, KTD-6).</summary>
    public async Task NotifyConfigUpdateAsync(string reason)
    {
        List<MirrorServerSession> sessions;
        lock (_gate)
            sessions = _sessions.Values.Where(s => s.Status == MirrorSessionStatus.Approved).ToList();

        foreach (var session in sessions)
        {
            await session.SendAsync(new ConfigUpdateMessage { Reason = reason }).ConfigureAwait(false);
            session.Close();
        }
        _logger.LogInformation("Config update '{Reason}': ended {Count} viewer sessions", reason, sessions.Count);
    }

    // ── Accept flow ──────────────────────────────────────────────────────────

    private void OnClientConnected(Fleck.IWebSocketConnection socket)
    {
        var ip = socket.ConnectionInfo.ClientIpAddress ?? "unknown";
        var session = new MirrorServerSession(socket, ip);

        socket.OnMessage = text => _ = HandleMessageAsync(session, text);
        socket.OnClose = () => OnSessionClosed(session);

        lock (_gate)
            _sessions[socket.ConnectionInfo.Id.ToString()] = session;
    }

    private async Task HandleMessageAsync(MirrorServerSession session, string text)
    {
        if (MirrorProtocol.Parse(text) is not HelloMessage hello)
        {
            await session.SendAsync(new CloseMessage { Reason = "expected hello" }).ConfigureAwait(false);
            session.Close();
            return;
        }

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
                if (ViewerCount >= MirrorProtocol.ConnectionCap)
                {
                    session.Close();
                    return;
                }
                session.Status = MirrorSessionStatus.Approved;
                session.Token = approved.Token;
            }
            await session.SendAsync(new HelloAckMessage { Status = "approved" }).ConfigureAwait(false);
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
            if (PendingCount >= MirrorProtocol.PendingCap)
            {
                session.Close();
                return;
            }
            session.Status = MirrorSessionStatus.PendingApproval;
        }

        await session.SendAsync(new HelloAckMessage { Status = "pending" }).ConfigureAwait(false);
        _logger.LogInformation("Viewer '{Device}' ({Ip}) requests approval", session.DeviceName, session.ClientIp);
        ApprovalRequested?.Invoke(session);

        // Auto-deny after 60 s (R2).
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
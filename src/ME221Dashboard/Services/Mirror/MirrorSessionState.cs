namespace ME221Dashboard.Services.Mirror;

public enum MirrorClientState
{
    Searching,
    NoA,
    Incompatible,
    AwaitingApproval,
    Refused,
    Connected,
    Disconnected,
    Revoked,
    RePair,
    Stopped,
}

/// <summary>
/// Pure B-side session state machine (U6, F1/F3). Mirrors the plan's state diagram:
/// searching → noA / incompatible / awaitingApproval → refused (re-requestable) →
/// connected; connected → disconnected (exhausted retries, manual retry) / revoked /
/// rePair (certificate changed, forget-and-request-again) / stopped (disconnect
/// button). Revocation suppresses auto-reconnect until the user acts; denial is
/// per-session retryable.
/// </summary>
public sealed class MirrorSessionState
{
    public const int MaxReconnectAttempts = 3;

    public MirrorClientState State { get; private set; } = MirrorClientState.Searching;
    public string? Error { get; private set; }
    public string? Token { get; private set; }
    public string? ServerDisplayName { get; private set; }
    public int ReconnectAttempts { get; private set; }

    public event Action<MirrorClientState, MirrorClientState>? Transitioned;

    // ── Discovery (F1) ──────────────────────────────────────────────────────

    public void StartSearch()
    {
        if (State is MirrorClientState.Searching or MirrorClientState.NoA or MirrorClientState.Incompatible or MirrorClientState.Stopped)
            Transition(MirrorClientState.Searching);
    }

    public void NoBeaconFound() => Transition(MirrorClientState.NoA);

    /// <summary>Version-mismatched A found (R4, AE3): shown, never connectable.</summary>
    public void BeaconMismatched(string? displayName = null)
    {
        ServerDisplayName = displayName;
        Transition(MirrorClientState.Incompatible);
    }

    /// <summary>User selected an A (R1).</summary>
    public void SelectDevice(string? displayName = null)
    {
        ServerDisplayName = displayName;
        Transition(MirrorClientState.AwaitingApproval);
    }

    // ── Approval (R2, AE1) ───────────────────────────────────────────────────

    public void ApprovalDeniedOrTimeout() => Transition(MirrorClientState.Refused);

    /// <summary>Explicit re-request from the refused state (AE1).</summary>
    public void ReRequest() => Transition(MirrorClientState.AwaitingApproval);

    /// <summary>Approved; the per-device token becomes the reconnect credential (R16).</summary>
    public void Approved(string token)
    {
        Token = token;
        Transition(MirrorClientState.Connected);
    }

    // ── Session (F3) ─────────────────────────────────────────────────────────

    /// <summary>Link drop: auto-reconnect while attempts remain (R13). Counts from
    /// Connected (live session) and AwaitingApproval (host died mid-approval) so
    /// both exhaust to Disconnected instead of stranding the session.</summary>
    public bool LinkDropped()
    {
        if (State is not (MirrorClientState.Connected or MirrorClientState.AwaitingApproval))
            return false;

        ReconnectAttempts++;
        if (ReconnectAttempts < MaxReconnectAttempts)
        {
            Error = "Connection lost; reconnecting";
            return true; // keep Connected; the client reconnects in place
        }

        Transition(MirrorClientState.Disconnected);
        return false;
    }

    public void ReconnectSucceeded()
    {
        ReconnectAttempts = 0;
        Error = null;
        if (State is not MirrorClientState.Connected)
            Transition(MirrorClientState.Connected);
    }

    /// <summary>Manual retry from the exhausted-disconnected screen (R13).</summary>
    public void ManualRetry()
    {
        ReconnectAttempts = 0;
        Error = null;
        Transition(MirrorClientState.Searching);
    }

    /// <summary>Return to discovery (R13).</summary>
    public void ReturnToDiscovery()
    {
        Error = null;
        Transition(MirrorClientState.Searching);
    }

    /// <summary>Revoked mid-session (R3, AE4): suppress auto-reconnect until the user acts.</summary>
    public void Revoked()
    {
        Error = "This device was revoked by the host";
        Transition(MirrorClientState.Revoked);
    }

    /// <summary>A's certificate changed after pairing (R15, R20).</summary>
    public void CertificateChanged()
    {
        Error = "The host's certificate changed — re-pair required";
        Transition(MirrorClientState.RePair);
    }

    /// <summary>Forget the pairing and request again (R20).</summary>
    public void ForgetAndReRequest()
    {
        Token = null;
        Error = null;
        Transition(MirrorClientState.Searching);
    }

    /// <summary>Disconnect button: exit companion mode to the connection page (F3).</summary>
    public void DisconnectRequested() => Transition(MirrorClientState.Stopped);

    /// <summary>
    /// R19: A changed snapshot-scoped config mid-session; B shows the reconnect
    /// notice and does NOT auto-reconnect — the user acts (U9's ConfigUpdateNotice).
    /// </summary>
    public void ConfigUpdateRequired()
    {
        Error = "update made, reconnect required";
        Transition(MirrorClientState.Disconnected);
    }

    private void Transition(MirrorClientState next)
    {
        if (State == next)
            return;
        var prev = State;
        State = next;
        Transitioned?.Invoke(prev, next);
    }
}
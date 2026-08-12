using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services.Mirror;

/// <summary>One discovered A device (UDP beacon, KTD-8); the address is the probe's source.</summary>
public sealed record MirrorBeacon(string AppVersion, bool EcuConnected, string DisplayName, string InstanceId, IPAddress Address);

/// <summary>
/// B-side mirror client (U6): UDP discovery scan, WSS connection with hello/token
/// presentation, certificate pinning (R15), and the reconnect state machine (R13).
/// Incoming wire messages surface on <see cref="MessageReceived"/> for the event
/// relay (U8); session state surfaces on <see cref="StateChanged"/> for Svelte via
/// the dedicated mirrorSessionState event (KTD-7 — never the ECU connection event).
/// </summary>
public sealed class MirrorClientService : IDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    private readonly Func<string> _appVersion;
    private readonly Func<string> _deviceName;
    private readonly Func<string?> _localInstanceId;
    private readonly ILogger<MirrorClientService> _logger;
    private string _clientId;
    private readonly MirrorSessionState _state = new();
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private string? _serverHost;
    private string? _pinnedThumbprint;
    private string? _capturedThumbprint;
    private MirrorBeacon? _target;
    private MirrorPinStore? _pinStore;
    private bool _reconnecting;
    private TaskCompletionSource<bool>? _helloAck;

    public MirrorClientService(
        Func<string>? appVersion = null,
        Func<string>? deviceName = null,
        MirrorPinStore? pinStore = null,
        Func<string?>? localInstanceId = null,
        ILogger<MirrorClientService>? logger = null)
    {
        _appVersion = appVersion ?? (() => AppInfo.Current.VersionString);
        _deviceName = deviceName ?? (() => DeviceInfo.Current.Name);
        _pinStore = pinStore;
        _localInstanceId = localInstanceId;
        _logger = logger ?? NullLogger<MirrorClientService>.Instance;
        _clientId = Guid.NewGuid().ToString("N");
    }

    /// <summary>Session state transitions, surfaced to Svelte as mirrorSessionState (KTD-7).</summary>
    public event Action<MirrorSessionState>? StateChanged;

    /// <summary>Every parsed wire message from A; the relay (U8) consumes frames/events.</summary>
    public event Action<object>? MessageReceived;

    public MirrorSessionState State => _state;
    public bool IsConnected => _state.State == MirrorClientState.Connected;

    /// <summary>Bind the state machine to the subscriber once (wire in MauiProgram).</summary>
    public void Wire() => _state.Transitioned += (_, next) => StateChanged?.Invoke(_state);

    /// <summary>Restore B's stable identity across restarts (R16): the client id must survive
    /// app restarts or the approved token never matches again (server requires
    /// ClientId equality) and every restart re-prompts approval.
    /// </summary>
    public bool NeedsClientIdRestore => _pinStore is not null;

    public async Task RestoreClientIdAsync()
    {
        if (_pinStore is null) return;
        try
        {
            _clientId = await _pinStore.GetOrCreateClientIdAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore the mirror client id");
        }
    }

    // ── Discovery (R1, R4) ───────────────────────────────────────────────────

    /// <summary>Broadcast a probe and collect beacons for ~<paramref name="timeoutMs"/>.</summary>
    public async Task<List<MirrorBeacon>> ScanAsync(int timeoutMs = 2000)
    {
        var found = new Dictionary<string, MirrorBeacon>(StringComparer.Ordinal);
        var localId = _localInstanceId?.Invoke();
        using var udp = new UdpClient();
        udp.EnableBroadcast = true;

        var probe = Encoding.UTF8.GetBytes(DiscoveryResponderService.ProbePayload);
        var cts = new CancellationTokenSource(timeoutMs);

        var receive = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var result = await udp.ReceiveAsync(cts.Token).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(result.Buffer));
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("instanceId", out var idProp))
                        continue;
                    var instanceId = idProp.GetString() ?? "";
                    if (string.IsNullOrEmpty(instanceId) || instanceId == localId)
                        continue; // self-discovery: never mirror our own instance
                    var beacon = new MirrorBeacon(
                        root.GetProperty("appVersion").GetString() ?? "",
                        root.GetProperty("ecuConnected").GetBoolean(),
                        root.GetProperty("displayName").GetString() ?? "ME221",
                        instanceId,
                        result.RemoteEndPoint.Address);
                    found[beacon.InstanceId] = beacon;
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) when (ex is not ObjectDisposedException)
                {
                    _logger.LogDebug(ex, "Discovery receive hiccup");
                }
            }
        });

        try
        {
            await udp.SendAsync(probe, probe.Length, new IPEndPoint(IPAddress.Broadcast, MirrorProtocol.DiscoveryPort)).ConfigureAwait(false);
            await Task.Delay(timeoutMs).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Discovery probe failed");
        }
        finally
        {
            cts.Cancel();
        }

        await receive.ConfigureAwait(false);
        return found.Values.ToList();
    }

    // ── Connect (R15, R16) ───────────────────────────────────────────────────

    /// <summary>Connect to a discovered A; if a token is held it is presented (R2 reconnect).</summary>
    public async Task ConnectAsync(MirrorBeacon target, string? token, string? pinnedThumbprint)
    {
        _target = target;
        _serverHost = target.InstanceId; // pins and tokens bind to the instance, not the IP
        _pinnedThumbprint = pinnedThumbprint;
        _state.SelectDevice(target.DisplayName);

        // Replace the previous socket/CTS (leak-safe on repeated reconnects).
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        try { _socket?.Dispose(); } catch { /* best effort */ }
        _socket = new ClientWebSocket();
        _socket.Options.RemoteCertificateValidationCallback = ValidateCertificate;

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        connectCts.CancelAfter(ConnectTimeout);
        try
        {
            await _socket.ConnectAsync(new Uri($"wss://{target.Address}:{MirrorProtocol.WssPort}/"), connectCts.Token).ConfigureAwait(false);
            _helloAck = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await SendHelloAsync(token).ConfigureAwait(false);
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        }
        catch
        {
            try { _socket.Dispose(); } catch { /* best effort */ }
            _socket = null;
            throw;
        }
    }

    /// <summary>Await the hello acknowledgment; false when refused/timed out (reconnect path).</summary>
    public async Task<bool> WaitForHelloAckAsync(TimeSpan timeout)
    {
        var tcs = _helloAck;
        if (tcs is null) return false;
        try
        {
            return await tcs.Task.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private bool ValidateCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (certificate is null)
            return false;

        var thumbprint = certificate.GetCertHashString();
        _capturedThumbprint = thumbprint;

        if (_pinnedThumbprint is null)
            return true; // first pairing: trust-on-first-use; the pin is captured at approval

        if (thumbprint != _pinnedThumbprint)
        {
            _logger.LogWarning("Certificate changed for {Host}: expected {Expected}, got {Actual}", _serverHost, _pinnedThumbprint, thumbprint);
            _state.CertificateChanged();
            return false;
        }
        return true;
    }

    private async Task SendHelloAsync(string? token)
    {
        var hello = new HelloMessage
        {
            AppVersion = _appVersion(),
            DeviceName = _deviceName(),
            Token = token,
            ClientId = _clientId,
        };
        var bytes = Encoding.UTF8.GetBytes(MirrorProtocol.Serialize(hello));
        await _socket!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[512 * 1024];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var segment = new ArraySegment<byte>(buffer);
                var result = await _socket!.ReceiveAsync(segment, ct).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    HandleServerClose();
                    return;
                }

                // A single malformed frame must never kill the loop: parse and
                // dispatch per-message, log and continue.
                try
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (MirrorProtocol.Parse(json) is { } message)
                    {
                        HandleControlMessage(message);
                        MessageReceived?.Invoke(message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Malformed mirror message discarded");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "Mirror connection dropped");
            if (_state.State is MirrorClientState.AwaitingApproval or MirrorClientState.Connected)
                _ = ReconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mirror receive loop error");
            if (_state.State is MirrorClientState.AwaitingApproval or MirrorClientState.Connected)
                _ = ReconnectAsync();
        }
    }

    private void HandleServerClose()
    {
        _logger.LogInformation("Mirror server closed the connection");
        // A Close frame is a link drop: reconnect while attempts remain (R13).
        if (_state.State is MirrorClientState.Connected)
            _ = ReconnectAsync();
    }

    /// <summary>Session-control messages drive the state machine (KTD-7).</summary>
    private void HandleControlMessage(object message)
    {
        switch (message)
        {
            case HelloAckMessage ack:
                switch (ack.Status)
                {
                    case "approved":
                        _state.ReconnectSucceeded();
                        _helloAck?.TrySetResult(true);
                        break;
                    case "pending":
                        break; // awaiting approval; banner flow proceeds on A
                    case "refused":
                        _state.ApprovalDeniedOrTimeout();
                        _helloAck?.TrySetResult(false);
                        break;
                    case "versionMismatch":
                        _state.BeaconMismatched();
                        _helloAck?.TrySetResult(false);
                        break;
                    case "revoked":
                        _state.Revoked();
                        _helloAck?.TrySetResult(false);
                        break;
                }
                break;

            case ApprovalDecisionMessage decision when decision.Approved && _target is not null:
                _state.Approved(decision.Token ?? "");
                _ = PersistPinAsync(decision.Token ?? ""); // capture the pin now that pairing is complete (R15)
                break;

            case ApprovalDecisionMessage:
                _state.ApprovalDeniedOrTimeout();
                break;

            case RevokedMessage:
                _state.Revoked();
                break;

            case ConfigUpdateMessage:
                // R19: the host changed snapshot-scoped config; reconnect required.
                _state.ConfigUpdateRequired();
                break;

            case CloseMessage:
                if (_state.State is MirrorClientState.Connected)
                    _state.LinkDropped();
                break;
        }
    }

    private async Task PersistPinAsync(string token)
    {
        if (_pinStore is null || _target is null || _capturedThumbprint is null)
            return;
        try
        {
            await _pinStore.SaveAsync(new MirrorPinEntry
            {
                InstanceId = _target.InstanceId,
                DisplayName = _target.DisplayName,
                Token = token,
                CertThumbprint = _capturedThumbprint,
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist the mirror pairing pin");
        }
    }

    private async Task ReconnectAsync()
    {
        if (_reconnecting || _target is null)
            return;
        _reconnecting = true;
        try
        {
            // R13: retry while attempts remain; revocation/cert-change stop auto-reconnect.
            while (_state.LinkDropped())
            {
                await Task.Delay(2000).ConfigureAwait(false);
                if (_target is null || _cts?.IsCancellationRequested == true)
                    return;
                try
                {
                    await ConnectAsync(_target, _state.Token, _pinnedThumbprint).ConfigureAwait(false);
                    // Only declare success on an approved helloAck; a pending or
                    // refused reply must not flip the state to Connected.
                    if (await WaitForHelloAckAsync(ConnectTimeout).ConfigureAwait(false))
                        return;
                    _logger.LogWarning("Reconnect hello was not approved");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Reconnect attempt failed");
                }
            }
        }
        finally
        {
            _reconnecting = false;
        }
    }

    /// <summary>Send a wire message to A (acks, hello).</summary>
    public async Task SendAsync(object message)
    {
        if (_socket is null) return;
        var bytes = Encoding.UTF8.GetBytes(MirrorProtocol.Serialize(message));
        await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>Disconnect button (F3): stop auto-reconnect and exit companion mode.</summary>
    public void Disconnect()
    {
        _cts?.Cancel();
        try { _socket?.Dispose(); } catch { /* best effort */ }
        _socket = null;
        _cts?.Dispose();
        _cts = null;
        _target = null;
        _state.DisconnectRequested();
    }

    public void Dispose() => Disconnect();
}
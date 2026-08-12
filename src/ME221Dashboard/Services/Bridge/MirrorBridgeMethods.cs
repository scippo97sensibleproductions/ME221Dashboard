using System.Text.Json;
using ME221Dashboard.Services.Mirror;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    private ApprovedDeviceStore? _approvedDeviceStore;
    private DiscoveryResponderService? _discoveryResponder;
    private MirrorServerService? _mirrorServer;
    private MirrorCertificateProvider? _mirrorCertificates;

    /// <summary>Called from JS: window.HybridWebView.InvokeDotNet('GetAppVersion')</summary>
    public string GetAppVersion() =>
        JsonSerializer.Serialize(new { appVersion = AppInfo.Current.VersionString });

    /// <summary>Called from JS: window.HybridWebView.InvokeDotNet('GetApprovedDevices')</summary>
    public async Task<string> GetApprovedDevices()
    {
        if (_approvedDeviceStore is null)
            return JsonSerializer.Serialize(new { success = false, error = "Mirror store not available" });

        var devices = await _approvedDeviceStore.GetAllAsync().ConfigureAwait(false);
        return JsonSerializer.Serialize(new
        {
            success = true,
            devices = devices.Select(d => new
            {
                clientId = d.ClientId,
                deviceName = d.DeviceName,
                approvedAtUtc = d.ApprovedAtUtc,
            }),
            displayName = await _approvedDeviceStore.GetDisplayNameAsync().ConfigureAwait(false),
        });
    }

    /// <summary>Called from JS: window.HybridWebView.InvokeDotNet('RevokeDevice', [clientId])</summary>
    public async Task<string> RevokeDevice(string clientId)
    {
        if (_approvedDeviceStore is null)
            return JsonSerializer.Serialize(new { success = false, error = "Mirror store not available" });

        var removed = await _approvedDeviceStore.RevokeAsync(clientId).ConfigureAwait(false);
        if (_mirrorServer is not null)
            await _mirrorServer.RevokeDeviceAsync(clientId).ConfigureAwait(false);
        return JsonSerializer.Serialize(new { success = removed, error = removed ? null : "Device not found" });
    }

    /// <summary>Called from JS: window.HybridWebView.InvokeDotNet('RenameDevice', [displayName])</summary>
    public async Task<string> RenameDevice(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return JsonSerializer.Serialize(new { success = false, error = "Name required" });

        if (_approvedDeviceStore is null)
            return JsonSerializer.Serialize(new { success = false, error = "Mirror store not available" });

        await _approvedDeviceStore.SetDisplayNameAsync(displayName.Trim()).ConfigureAwait(false);
        return JsonSerializer.Serialize(new { success = true });
    }

    /// <summary>Called from JS: window.HybridWebView.InvokeDotNet('ApproveDevice', [clientId])</summary>
    public async Task<string> ApproveDevice(string clientId)
    {
        if (_mirrorServer is null)
            return JsonSerializer.Serialize(new { success = false, error = "Mirror server not available" });

        await _mirrorServer.ApproveDeviceAsync(clientId).ConfigureAwait(false);
        return JsonSerializer.Serialize(new { success = true });
    }

    /// <summary>Called from JS: window.HybridWebView.InvokeDotNet('DenyDevice', [clientId])</summary>
    public async Task<string> DenyDevice(string clientId)
    {
        if (_mirrorServer is null)
            return JsonSerializer.Serialize(new { success = false, error = "Mirror server not available" });

        await _mirrorServer.DenyDeviceAsync(clientId).ConfigureAwait(false);
        return JsonSerializer.Serialize(new { success = true });
    }

    /// <summary>Called from JS: window.HybridWebView.InvokeDotNet('GetViewerList')</summary>
    public string GetViewerList()
    {
        if (_mirrorServer is null)
            return JsonSerializer.Serialize(new { success = false, error = "Mirror server not available" });

        return JsonSerializer.Serialize(new
        {
            success = true,
            viewers = _mirrorServer.Viewers().Select(s => new
            {
                clientId = s.ClientId,
                deviceName = s.DeviceName,
                status = s.Status.ToString(),
                ip = s.ClientIp,
            }),
        });
    }

    /// <summary>Called from JS: window.HybridWebView.InvokeDotNet('GetMirrorServerState')</summary>
    public string GetMirrorServerState()
    {
        if (_mirrorServer is null)
            return JsonSerializer.Serialize(new { success = false, error = "Mirror server not available" });

        return JsonSerializer.Serialize(new
        {
            success = true,
            running = _mirrorServer.IsRunning,
            port = _mirrorServer.BoundPort,
            viewerCount = _mirrorServer.ViewerCount,
            pendingCount = _mirrorServer.PendingCount,
        });
    }

    // ── B-side surface (U9) ─────────────────────────────────────────────────

    private MirrorClientService? _mirrorClient;

    internal void WireMirrorClient(MirrorClientService client) => _mirrorClient = client;

    /// <summary>Called from JS: window.HybridWebView.InvokeDotNet('MirrorScan') — UDP discovery (R1, R4).</summary>
    public async Task<string> MirrorScan()
    {
        if (_mirrorClient is null)
            return JsonSerializer.Serialize(new { success = false, error = "Mirror client not available" });

        var beacons = await _mirrorClient.ScanAsync().ConfigureAwait(false);
        var myVersion = AppInfo.Current.VersionString;
        return JsonSerializer.Serialize(new
        {
            success = true,
            devices = beacons.Select(b => new
            {
                instanceId = b.InstanceId,
                displayName = b.DisplayName,
                appVersion = b.AppVersion,
                ecuConnected = b.EcuConnected,
                compatible = b.AppVersion == myVersion,
                address = b.Address.ToString(),
            }),
        });
    }

    /// <summary>Called from JS: window.HybridWebView.InvokeDotNet('MirrorConnect', [instanceId]) — hello + token (R2, R16).</summary>
    public async Task<string> MirrorConnect(string instanceId)
    {
        if (_mirrorClient is null)
            return JsonSerializer.Serialize(new { success = false, error = "Mirror client not available" });

        // Restore B's stable identity (R16) before the hello carries the token;
        // GetOrCreate persists on first run and returns the same id thereafter.
        await _mirrorClient.RestoreClientIdAsync().ConfigureAwait(false);

        var pin = _mirrorPinStore is null ? null : await _mirrorPinStore.GetAsync(instanceId).ConfigureAwait(false);
        var beacon = (await _mirrorClient.ScanAsync(1500).ConfigureAwait(false)).FirstOrDefault(b => b.InstanceId == instanceId);
        if (beacon is null)
            return JsonSerializer.Serialize(new { success = false, error = "Host not found" });

        await _mirrorClient.ConnectAsync(beacon, pin?.Token, pin?.CertThumbprint).ConfigureAwait(false);
        return JsonSerializer.Serialize(new { success = true, known = pin is not null });
    }

    /// <summary>Called from JS: window.HybridWebView.InvokeDotNet('MirrorDisconnect') — exit companion mode (F3).</summary>
    public string MirrorDisconnect()
    {
        _mirrorClient?.Disconnect();
        return JsonSerializer.Serialize(new { success = true });
    }

    /// <summary>Called from JS: window.HybridWebView.InvokeDotNet('MirrorForgetDevice', [instanceId]) — re-pair flow (R20): drop the stored token + pin so the next connect re-runs approval.</summary>
    public async Task<string> MirrorForgetDevice(string instanceId)
    {
        if (_mirrorPinStore is null)
            return JsonSerializer.Serialize(new { success = false, error = "Mirror pins not available" });

        await _mirrorPinStore.RemoveAsync(instanceId).ConfigureAwait(false);
        return JsonSerializer.Serialize(new { success = true });
    }

    private MirrorPinStore? _mirrorPinStore;
    internal void WireMirrorPins(MirrorPinStore pins) => _mirrorPinStore = pins;

    /// <summary>Called from JS: window.HybridWebView.InvokeDotNet('ResetPairing') — R20 pairing-reset.</summary>
    public async Task<string> ResetPairing()
    {
        if (_approvedDeviceStore is null || _mirrorServer is null)
            return JsonSerializer.Serialize(new { success = false, error = "Mirror services not available" });

        // Regenerating the certificate invalidates old pins; clearing the store
        // invalidates every token (R20). Sessions end with a close, and the running
        // listener reloads the fresh certificate so new handshakes use it.
        _mirrorCertificates!.Regenerate();
        await _approvedDeviceStore.ResetAsync().ConfigureAwait(false);
        await _mirrorServer.DisconnectAllAsync("pairing reset").ConfigureAwait(false);
        _mirrorServer.ReloadCertificate();
        return JsonSerializer.Serialize(new { success = true });
    }

    /// <summary>Approval requests surface here; auto-deny while A is backgrounded (R2, U5).</summary>
    internal void OnApprovalRequested(MirrorServerSession session)
    {
        if (_lifecycleBridge?.IsBackgrounded == true)
        {
            _logger.LogInformation("Auto-denying approval for '{Device}': app backgrounded", session.DeviceName);
            _ = _mirrorServer?.DenyDeviceAsync(session.ClientId);
            return;
        }

        SendToReact(new
        {
            @event = "approvalRequest",
            clientId = session.ClientId,
            deviceName = session.DeviceName,
        });
    }

    /// <summary>Test seam: attach mirror services to a bridge constructed with fakes.</summary>
    internal void WireForMirror(ApprovedDeviceStore store, MirrorServerService server, MirrorCertificateProvider certificates)
    {
        _approvedDeviceStore = store;
        _mirrorServer = server;
        _mirrorCertificates = certificates;
        server.ApprovalRequested += OnApprovalRequested;
        server.SessionApproved += _ => UpdateForegroundService();
        server.SessionClosed += _ => UpdateForegroundService();
    }
}
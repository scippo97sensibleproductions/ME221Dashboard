using System.Text.Json;
using ME221Dashboard.Services.Mirror;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    private ApprovedDeviceStore? _approvedDeviceStore;
    private DiscoveryResponderService? _discoveryResponder;

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
        OnDeviceRevoked?.Invoke(clientId);
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

    /// <summary>Raised when a viewer is revoked; the mirror server closes the live session (U2/U5).</summary>
    internal event Action<string>? OnDeviceRevoked;
}
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services.Mirror;

/// <summary>
/// R19 notification wiring (U5, KTD-6). Subscribes to the bridge's snapshot-scoped
/// write events — calibration load, dashboard CRUD, sensor selection, warning
/// settings, vehicle config — and ends every viewer session with a configUpdate
/// notice. Layout-position saves and ECU table/driver writes never fire the event,
/// so they never end sessions.
/// </summary>
public sealed class MirrorLifecycleHooks(HybridBridgeService bridge, MirrorStreamService stream,
    ILogger<MirrorLifecycleHooks>? logger = null)
{
    private readonly ILogger<MirrorLifecycleHooks> _logger = logger ?? NullLogger<MirrorLifecycleHooks>.Instance;

    public void Wire()
    {
        bridge.SnapshotScopedWrite += reason => _ = NotifyAsync(reason);
    }

    private async Task NotifyAsync(string reason)
    {
        try
        {
            await stream.NotifyConfigUpdateAsync(reason).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "configUpdate notification failed for '{Reason}'", reason);
        }
    }
}
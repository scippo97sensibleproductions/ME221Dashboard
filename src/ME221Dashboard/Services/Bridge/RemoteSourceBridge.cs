using System.Text.Json;
using ME221Dashboard.Services.Mirror;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

/// <summary>
/// B's companion-mode read branch and single write gate (U7, R7-R9, KTD-5).
/// While <see cref="IsMirrorMode"/> is active every gated write returns the
/// established <c>{ success: false, error }</c> shape and mirror-served reads
/// answer from the applied snapshot store — never B's local data.
/// </summary>
public partial class HybridBridgeService
{
    private MirrorSnapshotStore? _mirrorStore;
    private bool _mirrorMode;
    private MirrorCalibrationSource? _mirrorCalibration;

    internal bool IsMirrorMode => _mirrorMode;
    internal MirrorSnapshotStore? MirrorStore => _mirrorStore;

    /// <summary>Enter companion mode: attach the applied-snapshot store and gate writes.</summary>
    internal void EnterMirrorMode(MirrorSnapshotStore store)
    {
        _mirrorStore = store;
        _mirrorMode = true;
        _mirrorCalibration = new MirrorCalibrationSource(_realCalibration, store, _logger);
        _calibration = _mirrorCalibration;
        _logger.LogInformation("Companion mode active — writes gated, reads mirror-served");
    }

    /// <summary>Exit companion mode (disconnect): restore the normal bridge surface.</summary>
    internal void ExitMirrorMode()
    {
        _mirrorMode = false;
        _calibration = _realCalibration;
        _mirrorCalibration = null;
        _mirrorStore?.Clear();
        _mirrorStore = null;
    }

    /// <summary>The single gate: null when the write may proceed, otherwise the gated error JSON.</summary>
    internal string? GateWrite(string feature)
    {
        if (!_mirrorMode)
            return null;
        _logger.LogDebug("Write gated in companion mode: {Feature}", feature);
        return JsonSerializer.Serialize(new { success = false, error = "Read-only in companion mode" });
    }
}
#if ANDROID
using Android.Content;
using Android.OS;
using Microsoft.Extensions.Logging;

using Application = global::Android.App.Application;

namespace ME221Dashboard.Platforms.Android.Services;

/// <summary>
/// Manages a partial WakeLock that keeps the CPU and USB host controller awake
/// during active ECU communication. Without this, Android doze mode can suspend
/// USB bulk transfers when the screen dims — causing the connection to drop.
/// </summary>
public sealed class UsbPowerManager : IDisposable
{
    private readonly PowerManager.WakeLock _wakeLock;
    private readonly ILogger? _logger;
    private int _refCount;

    public UsbPowerManager(ILogger? logger = null)
    {
        _logger = logger;
        var pm = (PowerManager?)Application.Context.GetSystemService(Context.PowerService);
        if (pm is null)
            throw new InvalidOperationException("PowerManager not available");

        _wakeLock = pm.NewWakeLock(WakeLockFlags.Partial, "ME221Dashboard::EcuUsbLink")
            ?? throw new InvalidOperationException("Failed to create WakeLock");
        _wakeLock.SetReferenceCounted(false);
    }

    public void Acquire()
    {
        if (Interlocked.CompareExchange(ref _refCount, 1, 0) == 0)
        {
            _wakeLock.Acquire();
            _logger?.LogInformation("UsbPowerManager: WakeLock ACQUIRED (PARTIAL_WAKE_LOCK)");
        }
    }

    public void Release()
    {
        if (Interlocked.CompareExchange(ref _refCount, 0, 1) == 1)
        {
            try
            {
                if (_wakeLock.IsHeld)
                    _wakeLock.Release();
                _logger?.LogInformation("UsbPowerManager: WakeLock RELEASED");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "UsbPowerManager: failed to release WakeLock");
            }
        }
    }

    public void Dispose()
    {
        Release();
        _wakeLock?.Dispose();
        GC.SuppressFinalize(this);
    }
}
#endif

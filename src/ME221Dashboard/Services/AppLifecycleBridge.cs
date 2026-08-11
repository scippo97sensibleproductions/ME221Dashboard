using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.Controls;

#if WINDOWS
using Microsoft.UI.Windowing;
using WinUIWindow = Microsoft.UI.Xaml.Window;
#endif

namespace ME221Dashboard.Services;

/// <summary>
/// Emits app lifecycle bridge events (appBackgrounded / appForegrounded / calibrationLoaded)
/// to the Svelte app through the HybridBridgeService.
/// </summary>
public sealed class AppLifecycleBridge
{
    private readonly HybridBridgeService _bridge;
    private readonly ILogger<AppLifecycleBridge> _logger;
    private string? _lastLifecycleState;
#if WINDOWS
    private bool _windowsAttached;
#endif

    public AppLifecycleBridge(HybridBridgeService bridge, ILogger<AppLifecycleBridge>? logger = null)
    {
        _bridge = bridge;
        _logger = logger ?? NullLogger<AppLifecycleBridge>.Instance;
        bridge._lifecycleBridge = this;
    }

    /// <summary>
    /// Hook the app Window lifecycle so background/foreground transitions are reported to the Svelte app.
    /// </summary>
    public void Attach(Window window)
    {
#if ANDROID
        window.Stopped += (_, _) => EmitLifecycle("appBackgrounded");
        window.Resumed += (_, _) => EmitLifecycle("appForegrounded");
#elif WINDOWS
        AttachWindowsWindow(window);
        window.HandlerChanged += (_, _) => AttachWindowsWindow(window);
#endif
    }

    /// <summary>
    /// Notify the Svelte app that a calibration was loaded (evaluator reset + settings re-fetch).
    /// </summary>
    public void NotifyCalibrationLoaded()
    {
        _bridge.SendLifecycleEvent("calibrationLoaded");
    }

    private void EmitLifecycle(string eventName)
    {
        if (_lastLifecycleState == eventName) return;
        _lastLifecycleState = eventName;
        _bridge.SendLifecycleEvent(eventName);
    }

#if WINDOWS
    private void AttachWindowsWindow(Window window)
    {
        if (_windowsAttached) return;
        if (window.Handler?.PlatformView is not WinUIWindow platformWindow) return;
        if (platformWindow.AppWindow is not { } appWindow) return;
        appWindow.Changed += OnAppWindowChanged;
        _windowsAttached = true;
        _logger.LogDebug("AppLifecycleBridge: attached to Windows AppWindow");
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidPresenterChange) return;
        if (sender.Presenter is not OverlappedPresenter presenter) return;

        switch (presenter.State)
        {
            case OverlappedPresenterState.Minimized:
                EmitLifecycle("appBackgrounded");
                break;
            case OverlappedPresenterState.Restored:
            case OverlappedPresenterState.Maximized:
                EmitLifecycle("appForegrounded");
                break;
        }
    }
#endif
}

using ME221Dashboard.Services;

namespace ME221Dashboard;

public partial class App
{
    private AppLifecycleBridge? _lifecycleBridge;

#if WINDOWS
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);
#endif

    public App()
    {
        InitializeComponent();
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            System.Diagnostics.Trace.WriteLine("[SHUTDOWN] ProcessExit event fired");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var services = activationState!.Context.Services;
        var mainPage = services.GetRequiredService<MainPage>();
        _lifecycleBridge ??= services.GetRequiredService<AppLifecycleBridge>();
        var window = new Window(mainPage) { Title = "ME221 Dashboard" };
        _lifecycleBridge.Attach(window);
#if WINDOWS
        // Keep the display on while the app window is active (dashboard use).
        // DisplayRequest isn't projected in MAUI's reduced Windows SDK ref pack,
        // so drive the underlying Win32 mechanism (SetThreadExecutionState) directly.
        const uint ES_CONTINUOUS = 0x80000000;
        const uint ES_SYSTEM_REQUIRED = 0x00000001;
        const uint ES_DISPLAY_REQUIRED = 0x00000002;
        var awake = false;
        window.Activated += (_, _) =>
        {
            if (awake) return;
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
            awake = true;
        };
        window.Deactivated += (_, _) =>
        {
            if (!awake) return;
            SetThreadExecutionState(ES_CONTINUOUS);
            awake = false;
        };
#endif
        return window;
    }
}

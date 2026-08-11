using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Usb;
using Android.OS;
using Android.Views;
using AndroidX.Activity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using ME221Dashboard.Services;

namespace ME221Dashboard;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(new[] { UsbManager.ActionUsbDeviceAttached })]
[MetaData(UsbManager.ActionUsbDeviceAttached, Resource = "@xml/device_filter")]
public class MainActivity : MauiAppCompatActivity
{
    public static MainActivity? Instance { get; private set; }

    private static AndroidBackCallback? s_backCallback;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Instance = this;

        // Keep the display on while the dashboard is visible — the flag is
        // automatically ignored when the window loses visibility, so the
        // screen can still sleep normally when the app is backgrounded.
        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);

        // Dirty-form gate leg (U8): forward Android back presses to the Svelte
        // app, which decides between the dirty dialog and the router's back
        // navigation. The legacy OnBackPressed override is deprecated and a
        // no-op on API 35+ targets, so the dispatcher path is required.
        // Disabled by default: the Svelte side enables it only while the dirty
        // gate is armed or a back-capable sub-page is mounted (the bridge
        // method SetBackInterceptionEnabled), so back at the root exits the
        // app normally and pre-mount presses are never swallowed.
        s_backCallback = new AndroidBackCallback();
        OnBackPressedDispatcher.AddCallback(this, s_backCallback);
    }

    /// <summary>Enable/disable back interception (called from the JS bridge).</summary>
    public static void SetBackInterceptionEnabled(bool enabled)
    {
        if (s_backCallback != null)
            s_backCallback.Enabled = enabled;
    }

    private sealed class AndroidBackCallback : OnBackPressedCallback
    {
        public AndroidBackCallback() : base(false) { }

        public override void HandleOnBackPressed()
        {
            var bridge = IPlatformApplication.Current?.Services?.GetService<HybridBridgeService>();
            bridge?.SendAndroidBack();
        }
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        // Handle USB device attached when app is already running
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus)
            EnterFullScreen();
    }

    private void EnterFullScreen()
    {
        if (Window?.DecorView is null) return;

#pragma warning disable CS0618
        Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
            (int)SystemUiFlags.Fullscreen |
            (int)SystemUiFlags.HideNavigation |
            (int)SystemUiFlags.ImmersiveSticky |
            (int)SystemUiFlags.LayoutFullscreen |
            (int)SystemUiFlags.LayoutHideNavigation |
            (int)SystemUiFlags.LayoutStable);
#pragma warning restore CS0618
    }
}

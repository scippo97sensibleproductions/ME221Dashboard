using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Usb;
using Android.OS;
using Android.Views;

namespace ME221Dashboard;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter(new[] { UsbManager.ActionUsbDeviceAttached })]
[MetaData(UsbManager.ActionUsbDeviceAttached, Resource = "@xml/device_filter")]
public class MainActivity : MauiAppCompatActivity
{
    public static MainActivity? Instance { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Instance = this;
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

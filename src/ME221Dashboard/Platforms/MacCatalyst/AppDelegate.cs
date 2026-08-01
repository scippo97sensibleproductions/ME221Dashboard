using Foundation;
using UIKit;

namespace ME221Dashboard;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        // Keep the display on while the dashboard is in the foreground.
        UIApplication.SharedApplication.IdleTimerDisabled = true;
        return base.FinishedLaunching(application, launchOptions);
    }
}
namespace ME221Dashboard;

public partial class App
{
    public App()
    {
        InitializeComponent();
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            System.Diagnostics.Trace.WriteLine("[SHUTDOWN] ProcessExit event fired");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var mainPage = activationState!.Context.Services.GetRequiredService<MainPage>();
        return new Window(mainPage) { Title = "ME221 Dashboard" };
    }
}

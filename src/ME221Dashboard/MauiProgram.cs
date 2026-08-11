using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ME221Dashboard;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var logCapture = new LogCapture();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Debug(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(FileSystem.AppDataDirectory, "logs", "me221-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        Log.Information("=== ME221 Dashboard starting ===");

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddSingleton<IChannelFactory, ChannelFactory>();
        builder.Services.AddSingleton<IEcuConnectionService, EcuConnectionService>();
        builder.Services.AddSingleton<ILiveDataService, LiveDataService>();
        builder.Services.AddSingleton<IPersistenceService, PersistenceService>();
        builder.Services.AddSingleton<ICalibrationService, CalibrationService>();
        builder.Services.AddSingleton<LogCapture>(logCapture);
        builder.Services.AddSingleton<DashboardPackageService>();
        builder.Services.AddSingleton<IUpdateCheckerService, UpdateCheckerService>();
        builder.Services.AddSingleton<HybridBridgeService>();
        builder.Services.AddSingleton<AppLifecycleBridge>();
        builder.Services.AddSingleton<ME221Dashboard.Services.Mirror.ApprovedDeviceStore>();
        builder.Services.AddSingleton<ME221Dashboard.Services.Mirror.MirrorCertificateProvider>();
        builder.Services.AddSingleton<ME221Dashboard.Services.Mirror.MirrorServerService>();
        builder.Services.AddSingleton<ME221Dashboard.Services.Mirror.MirrorSnapshotSerializer>();
        builder.Services.AddSingleton<ME221Dashboard.Services.Mirror.MirrorStreamService>();
        builder.Services.AddSingleton<ME221Dashboard.Services.Mirror.MirrorLifecycleHooks>();
        builder.Services.AddSingleton<ME221Dashboard.Services.Mirror.DiscoveryResponderService>();

#if WINDOWS
        builder.Services.AddSingleton<IGpsService>(sp =>
        {
            var liveData = sp.GetService<ILiveDataService>();
            var logger = sp.GetService<ILogger<SimulatedGpsService>>();
            return new SimulatedGpsService(liveData, logger);
        });
        Log.Information("DI: Registered SimulatedGpsService with ILiveDataService (WINDOWS)");
#elif ANDROID
        builder.Services.AddSingleton<IGpsService, GeolocationGpsService>();
        Log.Information("DI: Registered GeolocationGpsService (ANDROID)");
#elif MACCATALYST
        builder.Services.AddSingleton<IGpsService>(sp =>
        {
            var liveData = sp.GetService<ILiveDataService>();
            var logger = sp.GetService<ILogger<SimulatedGpsService>>();
            return new SimulatedGpsService(liveData, logger);
        });
        Log.Information("DI: Registered SimulatedGpsService with ILiveDataService (MACCATALYST)");
#else
        Log.Warning("DI: NO GPS service registered — target platform undefined");
#endif

        builder.Services.AddTransient<MainPage>();

        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logCapture);
        builder.Logging.AddSerilog(dispose: true);

#if DEBUG
        builder.Services.AddHybridWebViewDeveloperTools();
#endif

        var app = builder.Build();

        // Idle discovery + mirror server: A responds to probes and listens for viewers
        // whenever the app runs (Product Contract).
        try
        {
            app.Services.GetRequiredService<ME221Dashboard.Services.Mirror.DiscoveryResponderService>().Start();
            Log.Information("DI: Discovery responder started");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Discovery responder failed to start");
        }

        try
        {
            app.Services.GetRequiredService<ME221Dashboard.Services.Mirror.MirrorServerService>().Start();
            Log.Information("DI: Mirror server started");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Mirror server failed to start");
        }

        // R19 hooks: snapshot-scoped writes end viewer sessions (KTD-6).
        try
        {
            app.Services.GetRequiredService<ME221Dashboard.Services.Mirror.MirrorLifecycleHooks>().Wire();
            Log.Information("DI: Mirror lifecycle hooks wired");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Mirror lifecycle hooks failed to wire");
        }

        return app;
    }
}

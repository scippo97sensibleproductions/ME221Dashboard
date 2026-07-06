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
            .WriteTo.File(
                Path.Combine(FileSystem.AppDataDirectory, "logs", "me221-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

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
#else
        Log.Warning("DI: NO GPS service registered — ANDROID and WINDOWS both undefined");
#endif

        builder.Services.AddTransient<MainPage>();

        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logCapture);
        builder.Logging.AddSerilog(dispose: true);

#if DEBUG
        builder.Services.AddHybridWebViewDeveloperTools();
#endif

        return builder.Build();
    }
}

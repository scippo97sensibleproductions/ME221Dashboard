using ME221.Data.Models;
using ME221.Emulator.Application;
using ME221.Emulator.Application.Handlers;
using ME221.Emulator.Domain;
using ME221.Emulator.Infrastructure;
using ME221.Emulator.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

var serilogLogger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ME221", "logs", "emulator-.log"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        shared: true)
    .CreateLogger();

try
{
    var calibrationPath = "calibration.json";
    var vehicleConfigPath = (string?)null;
    var port = 22100;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--calibration" when i + 1 < args.Length:
                calibrationPath = args[i + 1];
                break;
            case "--vehicle-config" when i + 1 < args.Length:
                vehicleConfigPath = args[i + 1];
                break;
            case "--port" when i + 1 < args.Length:
                port = int.Parse(args[i + 1]);
                break;
        }
    }

    var services = new ServiceCollection();

    services.AddLogging(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Trace);
        builder.AddSerilog(serilogLogger);
    });

    services.AddSingleton<EmulatorConsole>();
    services.AddSingleton<CalibrationData>(sp =>
    {
        var loader = sp.GetRequiredService<CalibrationLoader>();
        var cal = loader.Load(calibrationPath);
        if (cal is null)
        {
            var fallbackPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ME221", "calibration.json");
            serilogLogger.Information("CalibrationLoader: trying fallback at '{FallbackPath}'", fallbackPath);
            cal = loader.Load(fallbackPath);
        }
        if (cal is null)
            throw new InvalidOperationException($"Failed to load calibration from '{calibrationPath}'");
        return cal;
    });
    services.AddSingleton<CalibrationLoader>();

    // VehicleConfig: try --vehicle-config arg, then well-known path, then calibration.json fallback
    services.AddSingleton<VehicleConfigData>(sp =>
    {
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("VehicleConfig");
        var cal = sp.GetRequiredService<CalibrationData>();

        // Check well-known path if no explicit arg
        var wellKnownPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".me221", "vehicle-config.json");

        var configPath = vehicleConfigPath ?? (File.Exists(wellKnownPath) ? wellKnownPath : null);
        return VehicleConfigLoader.Load(configPath, cal, logger);
    });

    services.AddScoped<EcuState>();
    services.AddScoped<EntityStore>();
    services.AddScoped<SensorSimulator>();
    services.AddScoped<SysCommandHandler>();
    services.AddScoped<ReportingCommandHandler>();
    services.AddScoped<TableCommandHandler>();
    services.AddScoped<DriverCommandHandler>();
    services.AddScoped<DefaultCommandHandler>();
    services.AddScoped<CommandRouter>(sp =>
    {
        var router = new CommandRouter();
        router.Register(sp.GetRequiredService<SysCommandHandler>());
        router.Register(sp.GetRequiredService<ReportingCommandHandler>());
        router.Register(sp.GetRequiredService<TableCommandHandler>());
        router.Register(sp.GetRequiredService<DriverCommandHandler>());
        router.Register(sp.GetRequiredService<DefaultCommandHandler>());
        return router;
    });

    services.AddSingleton(sp =>
    {
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        return new TcpServer(
            port,
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<EmulatorConsole>(),
            loggerFactory);
    });

    var serviceProvider = services.BuildServiceProvider();

    var calibration = serviceProvider.GetRequiredService<CalibrationData>();
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Loaded calibration: {Product} {Model} v{Version} — {Links} links, {Tables} tables, {Drivers} drivers",
        calibration.Metadata.ProductName,
        calibration.Metadata.ModelName,
        calibration.Metadata.Version,
        calibration.DataLinks.Count,
        calibration.Tables.Count,
        calibration.Drivers.Count);

    var server = serviceProvider.GetRequiredService<TcpServer>();

    logger.LogInformation("ECU Emulator starting on 127.0.0.1:{Port}", port);
    await server.StartAsync();

    return 0;
}
catch (Exception ex)
{
    serilogLogger.Fatal(ex, "Emulator terminated unexpectedly");
    return 1;
}
finally
{
    serilogLogger?.Dispose();
}

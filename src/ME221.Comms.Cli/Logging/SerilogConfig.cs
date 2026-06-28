using System.Globalization;
using Serilog;
using Serilog.Events;

namespace ME221.Comms.Cli.Logging;

/// <summary>
/// Initializes Serilog with File + Console sinks.
/// CLI-only logging - ME221.Comms uses Microsoft.Extensions.Logging.Abstractions (passed as null here).
/// </summary>
public static class SerilogConfig
{
    public static ILogger CreateLogger(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("ME221.Comms", LogEventLevel.Verbose)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.File(
                path: Path.Combine(logDirectory, "irl-test-{Date}.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();
    }
}

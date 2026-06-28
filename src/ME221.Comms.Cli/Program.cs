using System.Globalization;
using System.Text.Json;
using ME221.Comms.Cli.Commands;
using ME221.Comms.Cli.Config;
using ME221.Comms.Cli.Discovery;
using ME221.Comms.Cli.Results;
using ME221.Comms.Cli.ui;
using ME221.Data.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace ME221.Comms.Cli;

sealed class Program
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    static async Task<int> Main(string[] args)
    {
        var streamMode = args.Contains("--stream", StringComparer.OrdinalIgnoreCase);
        var dumpDrivers = args.Contains("--dump-drivers", StringComparer.OrdinalIgnoreCase);

        // Read settings first to determine log directory
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();
        var settings = new CliSettings();
        config.GetSection(nameof(CliSettings)).Bind(settings);

        // Build host with Serilog wired as the MEL logging provider
        using var host = Host.CreateDefaultBuilder()
            .UseSerilog((_, _, configuration) => configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("ME221.Comms", LogEventLevel.Verbose)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    formatProvider: CultureInfo.InvariantCulture)
                .WriteTo.File(
                    path: Path.Combine(settings.LogDirectory, "irl-test-{Date}.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    formatProvider: CultureInfo.InvariantCulture))
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("ME221 IRL Communication Test starting");

        try
        {
            ConsoleDisplay.WriteHeader("ME221 IRL Communication Test");
            ConsoleDisplay.WriteLine("Testing read-only protocol operations against a real ECU");
            if (streamMode)
                ConsoleDisplay.WriteLine("Mode: Live data stream (use --stream flag)");
            ConsoleDisplay.WriteLine();

            // Step 1: Start device polling
            ConsoleDisplay.WriteSection("Device Discovery");
            using var poller = new DevicePoller(settings.PollIntervalMs);
            poller.Start();
            logger.LogInformation("Device polling started (interval: {PollIntervalMs}ms)", settings.PollIntervalMs);

            // Step 2: Poll until device found or timeout
            ConsoleDisplay.WriteLine("Scanning for devices...");
            var deadline = DateTime.UtcNow.AddSeconds(settings.DeviceSelectionTimeout);
            while (poller.DiscoveredPorts.Count == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(settings.PollIntervalMs);
            }
            logger.LogInformation("Device scan completed, found {Count} devices", poller.DiscoveredPorts.Count);

            // Step 3: Device selection
            var selectedPort = ConsoleDisplay.PromptForDeviceSelection(poller.DiscoveredPorts);
            if (string.IsNullOrEmpty(selectedPort))
            {
                ConsoleDisplay.WriteError("No device selected. Exiting.");
                return 1;
            }

            logger.LogInformation("Device selected: {PortName}", selectedPort);

            // Step 4: Connect to device
            ConsoleDisplay.WriteSection("Connection");
            ConsoleDisplay.WriteLine($"Attempting to connect to {selectedPort} at {settings.BaudRate} baud...");

            var protocolLogger = host.Services.GetRequiredService<ILogger<ProtocolService>>();
            await using var executor = new CommandExecutor(selectedPort, settings.BaudRate, settings, protocolLogger);

            var connected = await executor.ConnectAsync();
            if (!connected)
            {
                ConsoleDisplay.WriteLine($"Connection failed at {settings.BaudRate} baud, trying fallback at {settings.FallbackBaudRate}...");
                connected = await executor.ReconnectAtFallbackAsync();
                if (!connected)
                {
                    ConsoleDisplay.WriteError("Connection failed at both baud rates. Exiting.");
                    logger.LogError("Connection failed at both {DefaultBaud} and {FallbackBaud} baud", settings.BaudRate, settings.FallbackBaudRate);
                    return 1;
                }
                ConsoleDisplay.WriteInfo($"Connected at fallback baud rate: {settings.FallbackBaudRate}");
            }
            else
            {
                ConsoleDisplay.WriteSuccess($"Connected to {selectedPort} at {settings.BaudRate} baud");
            }

            // Step 5: Load calibration data
            CalibrationData? calibration = null;
            var calPath = "calibration.json";
            if (File.Exists(calPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(calPath);
                    calibration = JsonSerializer.Deserialize<CalibrationData>(json, _jsonOptions);
                    logger.LogInformation("Calibration loaded: {DataLinks} data links, {Tables} tables, {Drivers} drivers",
                        calibration?.DataLinks.Count ?? 0,
                        calibration?.Tables.Count ?? 0,
                        calibration?.Drivers.Count ?? 0);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to load calibration.json");
                }
            }
            else
            {
                logger.LogWarning("calibration.json not found at {Path}", Path.GetFullPath(calPath));
            }

            // Dump drivers mode: read all drivers and save full state
            if (dumpDrivers)
            {
                ConsoleDisplay.WriteSection("Driver State Dump");
                var dumpResults = await DriverCommands.ReadAllDriversAsync(executor, calibration).ConfigureAwait(false);

                var dumpFile = Path.Combine(settings.ResultOutputDirectory, $"driver-dump-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                var dumpJson = System.Text.Json.JsonSerializer.Serialize(dumpResults.Select(r => new
                {
                    r.CommandName,
                    r.Status,
                    r.DataSize,
                    r.Summary,
                }), new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(dumpFile, dumpJson).ConfigureAwait(false);

                ConsoleDisplay.WriteInfo($"Driver state saved to: {Path.GetFullPath(dumpFile)}");
                poller.Stop();
                return 0;
            }

            // Stream mode: enable reporting and display live values
            if (streamMode)
            {
                var dataLinkLookup = calibration?.DataLinks.ToDictionary(dl => dl.Id);
                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                await StreamCommands.ExecuteAsync(executor, dataLinkLookup, settings.ResultOutputDirectory, cts.Token).ConfigureAwait(false);
                poller.Stop();
                return 0;
            }

            // Step 6: Execute pre-reporting read commands (device info, drivers, tables)
            ConsoleDisplay.WriteSection("Read-Only Commands");
            var results = (await ReadCommands.ExecutePreReportingAsync(executor, calibration)).ToList();

            // Step 7: Write operations (before reporting to avoid serial interference)
            ConsoleDisplay.WriteSection("Write Operations");
            results.AddRange(await WriteCommands.ExecuteAsync(executor, calibration));

            // Step 8: Post-reporting commands (reporting, logging, firmware)
            ConsoleDisplay.WriteSection("Reporting & Remaining Commands");
            results.AddRange(await ReadCommands.ExecutePostReportingAsync(executor, calibration));

            // Step 9: Write results
            ConsoleDisplay.WriteSection("Results");
            var resultFile = ResultWriter.Write(
                ResultWriter.Create(selectedPort, settings.BaudRate, results),
                settings.ResultOutputDirectory);

            // Display results
            var successCount = results.Count(r => r.Status is "success" or "sent");
            var failureCount = results.Count - successCount;
            ConsoleDisplay.WriteSummary(results.Count, successCount, failureCount);
            ConsoleDisplay.WriteLine($"Results saved to: {Path.GetFullPath(resultFile)}");

            // Cleanup
            poller.Stop();
            logger.LogInformation("Test completed. {Success}/{Total} commands succeeded", successCount, results.Count);

            return 0;
        }
        catch (OperationCanceledException)
        {
            ConsoleDisplay.WriteInfo("Operation cancelled by user.");
            return 130;
        }
        catch (Exception ex)
        {
            ConsoleDisplay.WriteError($"Unexpected error: {ex.Message}");
            logger.LogError(ex, "Unexpected error during test execution");
            return 1;
        }
    }
}

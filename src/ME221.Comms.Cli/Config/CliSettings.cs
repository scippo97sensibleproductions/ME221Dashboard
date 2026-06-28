namespace ME221.Comms.Cli.Config;

/// <summary>
/// Strongly-typed configuration for the CLI tool.
/// Bound to appsettings.json via Microsoft.Extensions.Configuration.
/// </summary>
public sealed class CliSettings
{
    /// <summary>Default baud rate for ECU connection.</summary>
    public int BaudRate { get; set; } = 230400;

    /// <summary>Fallback baud rate if default fails.</summary>
    public int FallbackBaudRate { get; set; } = 115200;

    /// <summary>Connection timeout in milliseconds.</summary>
    public int ConnectionTimeout { get; set; } = 5000;

    /// <summary>Device polling interval in milliseconds.</summary>
    public int PollIntervalMs { get; set; } = 500;

    /// <summary>Timeout for device selection in seconds.</summary>
    public int DeviceSelectionTimeout { get; set; } = 30;

    /// <summary>Directory for writing result files.</summary>
    public string ResultOutputDirectory { get; set; } = "results";

    /// <summary>Directory for Serilog log files.</summary>
    public string LogDirectory { get; set; } = "logs";
}

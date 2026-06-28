using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ME221.Comms.Cli.Results;

/// <summary>
/// Represents the result of a single command execution.
/// </summary>
public sealed class CommandResult
{
    [JsonPropertyName("name")]
    public string CommandName { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("responseType")]
    public string ResponseType { get; init; } = string.Empty;

    [JsonPropertyName("dataSize")]
    public int DataSize { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>
/// Represents the device information used for the test.
/// </summary>
public sealed class DeviceInfo
{
    [JsonPropertyName("portName")]
    public string PortName { get; init; } = string.Empty;

    [JsonPropertyName("baudRate")]
    public int BaudRate { get; init; }
}

/// <summary>
/// Represents the overall test summary.
/// </summary>
public sealed class TestSummary
{
    [JsonPropertyName("totalCommands")]
    public int TotalCommands { get; init; }

    [JsonPropertyName("successCount")]
    public int SuccessCount { get; init; }

    [JsonPropertyName("failureCount")]
    public int FailureCount { get; init; }

    [JsonPropertyName("overallStatus")]
    public string OverallStatus { get; init; } = string.Empty;
}

/// <summary>
/// Represents the complete test result file.
/// </summary>
public sealed class TestResult
{
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;

    [JsonPropertyName("device")]
    public DeviceInfo Device { get; init; } = new();

    [JsonPropertyName("commands")]
    public IReadOnlyList<CommandResult> Commands { get; init; } = [];

    [JsonPropertyName("summary")]
    public TestSummary Summary { get; init; } = new();
}

/// <summary>
/// Writes test results to a timestamped JSON file.
/// </summary>
public static class ResultWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>
    /// Writes the test results to a JSON file in the specified output directory.
    /// Returns the file path.
    /// </summary>
    public static string Write(TestResult result, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var fileName = $"test-result-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        var filePath = Path.Combine(outputDirectory, fileName);

        var json = JsonSerializer.Serialize(result, JsonOptions);
        File.WriteAllText(filePath, json);

        return filePath;
    }

    /// <summary>
    /// Creates a TestResult from the given parameters.
    /// </summary>
    public static TestResult Create(string portName, int baudRate, IReadOnlyList<CommandResult> commands)
    {
        var successCount = commands.Count(c => c.Status is "success" or "sent");
        var failureCount = commands.Count - successCount;

        var overallStatus = failureCount == 0
            ? "success"
            : failureCount < commands.Count / 2
                ? "partial-failure"
                : "major-failure";

        return new TestResult
        {
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            Device = new DeviceInfo { PortName = portName, BaudRate = baudRate },
            Commands = commands,
            Summary = new TestSummary
            {
                TotalCommands = commands.Count,
                SuccessCount = successCount,
                FailureCount = failureCount,
                OverallStatus = overallStatus
            }
        };
    }
}

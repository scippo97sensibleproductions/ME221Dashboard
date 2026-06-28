namespace ME221.Comms.Cli.ui;

/// <summary>
/// Terminal output formatting helper for the CLI tool.
/// </summary>
public static class ConsoleDisplay
{
    private const string GreenCheck = "✓";
    private const string RedX = "✗";
    private const string YellowWarning = "⚠";
    private const string BlueInfo = "ℹ";

    public static void ClearScreen()
    {
        Console.Clear();
    }

    public static void WriteHeader(string text)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine($"  {text}");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine();
    }

    public static void WriteSection(string text)
    {
        Console.WriteLine();
        Console.WriteLine($"── {text} ──");
        Console.WriteLine();
    }

    public static void WriteLine(string text)
    {
        Console.WriteLine(text);
    }

    public static void WriteLine()
    {
        Console.WriteLine();
    }

    public static void WriteSuccess(string text)
    {
        Console.WriteLine($"  {GreenCheck} {text}");
    }

    public static void WriteError(string text)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  {RedX} {text}");
        Console.ResetColor();
    }

    public static void WriteWarning(string text)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  {YellowWarning} {text}");
        Console.ResetColor();
    }

    public static void WriteInfo(string text)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  {BlueInfo} {text}");
        Console.ResetColor();
    }

    public static void WriteProgress(string text)
    {
        Console.Write($"  ... {text}");
    }

    public static void WriteDeviceList(IReadOnlyCollection<string> ports)
    {
        Console.WriteLine();
        Console.WriteLine("Discovered devices:");
        Console.WriteLine();

        if (ports.Count == 0)
        {
            WriteWarning("No devices found. Please connect your ECU and try again.");
            return;
        }

        var portList = ports.ToList();
        for (int i = 0; i < portList.Count; i++)
        {
            Console.WriteLine($"  [{i + 1}] {portList[i]}");
        }

        Console.WriteLine();
    }

    public static string? PromptForDeviceSelection(IReadOnlyCollection<string> ports)
    {
        if (ports.Count == 0)
        {
            WriteWarning("No devices found. Please connect your ECU and try again.");
            return null;
        }

        WriteDeviceList(ports);

        if (ports.Count == 1)
        {
            var port = ports.First();
            WriteWarning($"Auto-selected {port} (1 device found)");
            return port;
        }

        Console.Write($"Select device (1-{ports.Count}) or press Enter to refresh: ");
        var input = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(input))
            return null; // Refresh

        if (!int.TryParse(input, out var selection) || selection < 1 || selection > ports.Count)
        {
            WriteError($"Invalid selection. Please enter a number between 1 and {ports.Count}.");
            return PromptForDeviceSelection(ports);
        }

        var portList = ports.ToList();
        return portList[selection - 1];
    }

    public static void WriteCommandResult(string commandName, bool success, string? details = null)
    {
        var status = success ? GreenCheck : RedX;
        var color = success ? ConsoleColor.Green : ConsoleColor.Red;

        Console.ForegroundColor = color;
        Console.WriteLine($"  {status} {commandName}");
        Console.ResetColor();

        if (!string.IsNullOrEmpty(details))
        {
            if (success)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
            }
            Console.WriteLine($"      {details}");
            Console.ResetColor();
        }
    }

    public static void WriteSummary(int total, int successCount, int failureCount)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine($"  RESULTS: {successCount}/{total} commands succeeded");
        if (failureCount > 0)
            Console.WriteLine($"  {failureCount} command(s) failed");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine();
    }
}

using System.Diagnostics;
using System.Text.Json;

namespace ME221.Data.Infrastructure;

file static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: ME221.Data <input.mefw> [output.json]");
            Console.Error.WriteLine("  Extracts calibration data from a .mefw firmware file");
            return 1;
        }

        var inputPath = args[0];
        var outputPath = args.Length > 1 ? args[1] : Path.ChangeExtension(inputPath, ".json");

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"File not found: {inputPath}");
            return 1;
        }

        try
        {
            Console.Error.WriteLine($"Reading: {inputPath}");
            var sw = Stopwatch.StartNew();

            var xml = MefwReader.ReadDefXml(inputPath);
            Console.Error.WriteLine($"DEF XML extracted: {xml.Length:N0} chars ({sw.Elapsed.TotalSeconds:F1}s)");

            var calibration = DefXmlParser.Parse(xml);
            Console.Error.WriteLine($"Parsed: {calibration.DataLinks.Count} data links, " +
                $"{calibration.Tables.Count} tables, {calibration.Drivers.Count} drivers");

            var json = JsonSerializer.Serialize(calibration, CalibrationJsonContext.Default.CalibrationData);

            File.WriteAllText(outputPath, json);
            Console.Error.WriteLine($"Written: {outputPath} ({json.Length:N0} chars)");

            sw.Stop();
            Console.Error.WriteLine($"Done in {sw.Elapsed.TotalSeconds:F1}s");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}

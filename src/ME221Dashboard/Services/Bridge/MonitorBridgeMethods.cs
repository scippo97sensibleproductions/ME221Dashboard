using System.Text.Json;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    /// <summary>
    /// Import a session CSV file for historical chart display.
    /// Called from JS: window.HybridWebView.InvokeDotNet('ImportSessionCsv')
    /// Returns: { picked: true, headers: string[], rows: number[][] } or { picked: false }
    /// </summary>
    public async Task<string> ImportSessionCsv()
    {
        _logger.LogInformation("ImportSessionCsv called");
        try
        {
            var isWindows = DeviceInfo.Current.Platform == DevicePlatform.WinUI;
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Session CSV File",
                FileTypes = new FilePickerFileType(isWindows
                    ? new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, [".csv", ".txt"] }
                    }
                    : new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, ["text/csv", "text/plain", "application/octet-stream"] }
                    })
            }).ConfigureAwait(false);

            if (fileResult == null)
                return JsonSerializer.Serialize(new { picked = false });

            var content = await File.ReadAllTextAsync(fileResult.FullPath).ConfigureAwait(false);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return JsonSerializer.Serialize(new { picked = true, headers = Array.Empty<string>(), rows = Array.Empty<object>() });

            var headers = ParseCsvLine(lines[0]);
            var rows = new List<double[]>();
            for (int i = 1; i < lines.Length; i++)
            {
                var vals = ParseCsvLine(lines[i]);
                var doubles = new double[vals.Length];
                for (int j = 0; j < vals.Length; j++)
                {
                    double.TryParse(vals[j], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out doubles[j]);
                }
                rows.Add(doubles);
            }

            return JsonSerializer.Serialize(new
            {
                picked = true,
                headers,
                rows
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportSessionCsv failed");
            return JsonSerializer.Serialize(new { picked = false, error = ex.Message });
        }
    }

    private static string[] ParseCsvLine(string line)
    {
        return line.Split(',').Select(s => s.Trim().Trim('"')).ToArray();
    }
}

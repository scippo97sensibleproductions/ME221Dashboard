using System.Text.Json;
using ME221.Data;
using ME221.Data.Models;
using Microsoft.Extensions.Logging;

namespace ME221.Emulator.Infrastructure;

public sealed class CalibrationLoader(ILogger<CalibrationLoader> logger)
{
    public CalibrationData? Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        logger.LogDebug("CalibrationLoader: resolving path '{Path}' → '{FullPath}'", path, fullPath);

        if (!File.Exists(fullPath))
        {
            logger.LogError("CalibrationLoader: file not found at '{FullPath}'", fullPath);
            return null;
        }

        var json = File.ReadAllText(fullPath);
        var calibration = JsonSerializer.Deserialize(json, CalibrationJsonContext.Default.CalibrationData);

        if (calibration?.DataLinks is not { Count: > 0 }) return calibration;
        var sample = calibration.DataLinks
            .Where(dl => !string.IsNullOrEmpty(dl.MeasureUnit))
            .Take(5)
            .Select(dl => $"{dl.Id}:{dl.Name}='{dl.MeasureUnit}'");
        logger.LogInformation("CalibrationLoader: loaded {Count} links from '{FullPath}' — sample units: [{Sample}]",
            calibration.DataLinks.Count, fullPath, string.Join(", ", sample));

        return calibration;
    }
}

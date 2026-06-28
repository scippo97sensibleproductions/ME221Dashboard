using System.IO.Compression;
using System.Text.Json;
using ME221.Data.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services;

public sealed class DashboardPackageService(ILogger<DashboardPackageService>? logger = null)
{
    private readonly ILogger<DashboardPackageService> _logger = logger ?? NullLogger<DashboardPackageService>.Instance;

    private static string BasePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ME221");
    private static string DashboardImagesPath => Path.Combine(BasePath, "dashboard-images");
    private static string GaugeTexturesPath => Path.Combine(BasePath, "gauge-textures");

    public byte[] CreatePackage(DashboardConfig config, string dashboardName)
    {
        if (!config.Dashboards.TryGetValue(dashboardName, out var dashboard))
            throw new ArgumentException($"Dashboard '{dashboardName}' not found");

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                var manifest = JsonSerializer.Serialize(new PackageManifest { SchemaVersion = 1, DashboardName = dashboardName });
                writer.Write(manifest);
            }

            var exportConfig = new DashboardConfig
            {
                ActiveDashboard = dashboardName,
                GridRows = config.GridRows,
                GridColumns = config.GridColumns,
                Dashboards = new Dictionary<string, DashboardDefinition>
                {
                    [dashboardName] = dashboard
                }
            };

            var configEntry = archive.CreateEntry("dashboard.json");
            using (var writer = new StreamWriter(configEntry.Open()))
            {
                var configJson = JsonSerializer.Serialize(exportConfig, V2JsonContext.Default.DashboardConfig);
                writer.Write(configJson);
            }

            if (!string.IsNullOrEmpty(dashboard.BackgroundImagePath) && File.Exists(dashboard.BackgroundImagePath))
            {
                var bgFileName = Path.GetFileName(dashboard.BackgroundImagePath);
                var bgEntry = archive.CreateEntry($"background/{bgFileName}");
                using var bgEntryStream = bgEntry.Open();
                using var bgFileStream = File.OpenRead(dashboard.BackgroundImagePath);
                bgFileStream.CopyTo(bgEntryStream);
            }

            foreach (var gauge in dashboard.Gauges)
            {
                if (string.IsNullOrEmpty(gauge.TexturePath) || !File.Exists(gauge.TexturePath))
                    continue;

                var texFileName = Path.GetFileName(gauge.TexturePath);
                var texEntry = archive.CreateEntry($"textures/{texFileName}");
                using var texEntryStream = texEntry.Open();
                using var texFileStream = File.OpenRead(gauge.TexturePath);
                texFileStream.CopyTo(texEntryStream);
            }
        }

        var packageBytes = ms.ToArray();
        _logger.LogInformation("Package created for dashboard '{Name}': {Size} bytes", dashboardName, packageBytes.Length);
        return packageBytes;
    }

    public ImportPackageResult ExtractPackage(Stream zipStream)
    {
        try
        {
            if (!zipStream.CanSeek)
            {
                var buffer = new MemoryStream();
                zipStream.CopyTo(buffer);
                buffer.Position = 0;
                zipStream = buffer;
            }

            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            var manifestEntry = archive.GetEntry("manifest.json");
            if (manifestEntry == null)
                return Error("Invalid package: missing manifest.json");

            string manifestJson;
            using (var reader = new StreamReader(manifestEntry.Open()))
                manifestJson = reader.ReadToEnd();

            var manifest = JsonSerializer.Deserialize<PackageManifest>(manifestJson);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.DashboardName))
                return Error("Invalid package: malformed manifest");

            var configEntry = archive.GetEntry("dashboard.json");
            if (configEntry == null)
                return Error("Invalid package: missing dashboard.json");

            string configJson;
            using (var reader = new StreamReader(configEntry.Open()))
                configJson = reader.ReadToEnd();

            var importConfig = JsonSerializer.Deserialize(configJson, V2JsonContext.Default.DashboardConfig);
            if (importConfig == null)
                return Error("Invalid package: malformed dashboard.json");

            var bgEntry = archive.Entries
                .FirstOrDefault(e => e.FullName.StartsWith("background/") && !e.FullName.EndsWith("/"));
            if (bgEntry != null)
            {
                Directory.CreateDirectory(DashboardImagesPath);
                var bgFileName = Path.GetFileName(bgEntry.FullName);
                var destPath = Path.Combine(DashboardImagesPath, bgFileName);
                bgEntry.ExtractToFile(destPath, overwrite: true);
                _logger.LogDebug("Extracted background: {Path}", destPath);

                foreach (var kvp in importConfig.Dashboards)
                    kvp.Value.BackgroundImagePath = destPath;
            }

            var textureEntries = archive.Entries
                .Where(e => e.FullName.StartsWith("textures/") && !e.FullName.EndsWith("/"));
            foreach (var texEntry in textureEntries)
            {
                Directory.CreateDirectory(GaugeTexturesPath);
                var texFileName = Path.GetFileName(texEntry.FullName);
                var destPath = Path.Combine(GaugeTexturesPath, texFileName);
                texEntry.ExtractToFile(destPath, overwrite: true);
                _logger.LogDebug("Extracted texture: {Path}", destPath);

                var nameWithoutExt = Path.GetFileNameWithoutExtension(texFileName);
                var idPart = nameWithoutExt.Contains('_') ? nameWithoutExt[..nameWithoutExt.IndexOf('_')] : nameWithoutExt;
                if (int.TryParse(idPart, out var entityId))
                {
                    foreach (var kvp in importConfig.Dashboards)
                    {
                        foreach (var gauge in kvp.Value.Gauges.Where(g => g.Id == entityId))
                            gauge.TexturePath = destPath;
                    }
                }
            }

            return new ImportPackageResult
            {
                Success = true,
                DashboardName = manifest.DashboardName,
                ImportedConfig = importConfig
            };
        }
        catch (InvalidDataException ex)
        {
            _logger.LogError(ex, "Import package is not a valid ZIP");
            return Error("Invalid package file format");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import package extraction failed");
            return Error(ex.Message);
        }
    }

    private static ImportPackageResult Error(string message) =>
        new() { Success = false, Error = message };
}

public sealed class ImportPackageResult
{
    public bool Success { get; init; }
    public string? DashboardName { get; init; }
    public string? Error { get; init; }
    public DashboardConfig? ImportedConfig { get; init; }
}

internal sealed class PackageManifest
{
    [System.Text.Json.Serialization.JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }
    [System.Text.Json.Serialization.JsonPropertyName("dashboardName")]
    public string? DashboardName { get; init; }
}

using System.Text.Json;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── Image Picker Methods ────────────────────────────────────────────────

    private static readonly PickOptions ImagePickOptions = new()
    {
        PickerTitle = "Select Image",
        FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.Android, ["image/*"] },
            { DevicePlatform.WinUI, [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".svg", ".tiff", ".tif", ".ico"] }
        })
    };

    /// <summary>
    /// Pick a dashboard background image. Called from JS: window.HybridWebView.InvokeDotNet('PickDashboardBackground')
    /// </summary>
    public async Task<string> PickDashboardBackground()
    {
        _logger.LogInformation("PickDashboardBackground called");
        _connection.PauseHeartbeat();
        try
        {
            var fileResult = await FilePicker.Default.PickAsync(ImagePickOptions).ConfigureAwait(false);
            if (fileResult == null)
                return JsonSerializer.Serialize(new { picked = false });

            var destDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ME221", "dashboard-images");
            Directory.CreateDirectory(destDir);

            var destPath = await CopyFileToLocalAsync(fileResult, destDir, "background").ConfigureAwait(false);
            _imageBase64Cache.Remove(destPath);
            return JsonSerializer.Serialize(new { picked = true, path = destPath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PickDashboardBackground failed");
            return JsonSerializer.Serialize(new { picked = false, error = ex.Message });
        }
        finally
        {
            _connection.ResumeHeartbeat();
        }
    }

    /// <summary>
    /// Pick a gauge texture image. Called from JS: window.HybridWebView.InvokeDotNet('PickGaugeTexture', [gaugeId])
    /// </summary>
    public async Task<string> PickGaugeTexture(string gaugeId)
    {
        _logger.LogInformation("PickGaugeTexture called");
        _connection.PauseHeartbeat();
        try
        {
            var fileResult = await FilePicker.Default.PickAsync(ImagePickOptions).ConfigureAwait(false);
            if (fileResult == null)
                return JsonSerializer.Serialize(new { picked = false });

            var destDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ME221", "gauge-textures");
            Directory.CreateDirectory(destDir);

            var safeName = string.Join("_", gaugeId.Split(Path.GetInvalidFileNameChars()));
            var destPath = await CopyFileToLocalAsync(fileResult, destDir, safeName).ConfigureAwait(false);
            _imageBase64Cache.Remove(destPath);
            return JsonSerializer.Serialize(new { picked = true, path = destPath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PickGaugeTexture failed");
            return JsonSerializer.Serialize(new { picked = false, error = ex.Message });
        }
        finally
        {
            _connection.ResumeHeartbeat();
        }
    }

    /// <summary>
    /// Read an image file and return it as a base64 data URL. Results are cached per path.
    /// Called from JS: window.HybridWebView.InvokeDotNet('GetImageBase64', [path])
    /// </summary>
    public async Task<string> GetImageBase64(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return JsonSerializer.Serialize(new { success = false, error = "File not found" });

            if (_imageBase64Cache.TryGetValue(path, out var cached))
                return cached;

            var bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var mime = ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".tiff" or ".tif" => "image/tiff",
                ".ico" => "image/x-icon",
                _ => "image/png"
            };
            var base64 = Convert.ToBase64String(bytes);
            var dataUrl = $"data:{mime};base64,{base64}";
            var result = JsonSerializer.Serialize(new { success = true, dataUrl });
            _imageBase64Cache[path] = result;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetImageBase64 failed for {Path}", path);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private static async Task<string> CopyFileToLocalAsync(
        FileResult fileResult,
        string destDir,
        string fileNameWithoutExt)
    {
        var ext = Path.GetExtension(fileResult.FileName);
        if (string.IsNullOrEmpty(ext))
            ext = ".png";
        var destPath = Path.Combine(destDir, $"{fileNameWithoutExt}_{DateTime.Now.Ticks}{ext}");

        await using var srcStream = await fileResult.OpenReadAsync().ConfigureAwait(false);
        await using var dstStream = File.Create(destPath);
        await srcStream.CopyToAsync(dstStream).ConfigureAwait(false);

        return destPath;
    }
}

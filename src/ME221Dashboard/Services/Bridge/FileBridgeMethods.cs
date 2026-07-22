using System.Text.Json;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── Export / Import Dashboard ────────────────────────────────────────────

    /// <summary>
    /// Export a dashboard as a .mez package file.
    /// Called from JS: window.HybridWebView.InvokeDotNet('ExportDashboard', [dashboardName])
    /// On Android: uses the system share sheet.
    /// On Windows: uses the native FileSavePicker dialog.
    /// </summary>
    public async Task<string> ExportDashboard(string dashboardName)
    {
        _logger.LogInformation("ExportDashboard called: {Name}", dashboardName);
        try
        {
            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false);
            if (config == null || !config.Dashboards.ContainsKey(dashboardName))
                return JsonSerializer.Serialize(new { success = false, error = "Dashboard not found" });

            var packageBytes = _packageService.CreatePackage(config, dashboardName);
            var safeName = string.Join("_", dashboardName.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"ME221-{safeName}.mez";

#if WINDOWS
            var windowsApp = App.Current;
            if (windowsApp?.Windows.Count > 0)
            {
                var savePicker = new Windows.Storage.Pickers.FileSavePicker
                {
                    SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
                };
                savePicker.FileTypeChoices.Add("ME221 Dashboard Package", [".mez"]);
                savePicker.SuggestedFileName = fileName;

                if (windowsApp.Windows[0]?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                    WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
                }

                var file = await savePicker.PickSaveFileAsync();
                if (file == null)
                    return JsonSerializer.Serialize(new { success = false, error = "Save cancelled" });

                using var stream = await file.OpenStreamForWriteAsync().ConfigureAwait(false);
                stream.SetLength(0);
                await stream.WriteAsync(packageBytes).ConfigureAwait(false);
                _logger.LogInformation("Dashboard exported to {Path} ({Size} bytes)", file.Path, packageBytes.Length);
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    path = file.Path,
                    message = $"Saved to {file.Path}"
                });
            }

            return JsonSerializer.Serialize(new { success = false, error = "No window available" });
#else
            var cacheDir = FileSystem.CacheDirectory;
            var filePath = Path.Combine(cacheDir, fileName);
            await File.WriteAllBytesAsync(filePath, packageBytes).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
                Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = $"Export Dashboard: {dashboardName}",
                    File = new ShareFile(filePath)
                })).ConfigureAwait(false);

            _logger.LogInformation("Dashboard '{Name}' exported to {Path}", dashboardName, filePath);
            return JsonSerializer.Serialize(new { success = true });
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExportDashboard failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Import a dashboard from a .mez package file.
    /// Called from JS: window.HybridWebView.InvokeDotNet('ImportDashboard')
    /// </summary>
    public async Task<string> ImportDashboard()
    {
        _logger.LogInformation("ImportDashboard called");
        try
        {
            var isWindows = DeviceInfo.Current.Platform == DevicePlatform.WinUI;
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Dashboard Package (.mez)",
                FileTypes = isWindows
                    ? new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, [".mez"] }
                    })
                    : null // Android: no MIME filter — shows all files
            }).ConfigureAwait(false);

            if (fileResult == null)
                return JsonSerializer.Serialize(new { picked = false });

            await using var stream = await fileResult.OpenReadAsync().ConfigureAwait(false);
            var result = _packageService.ExtractPackage(stream);

            if (!result.Success)
                return JsonSerializer.Serialize(new { picked = true, success = false, error = result.Error });

            var config = await _calibration.GetPersistedDashboardConfigAsync().ConfigureAwait(false) ?? new DashboardConfig();
            var baseName = result.DashboardName!;

            if (config.Dashboards.ContainsKey(baseName))
            {
                baseName = $"{baseName} (imported)";
                var counter = 2;
                while (config.Dashboards.ContainsKey(baseName))
                {
                    baseName = $"{result.DashboardName} (imported {counter})";
                    counter++;
                }
            }

            var importDef = result.ImportedConfig!.Dashboards.Values.First();
            config.Dashboards[baseName] = importDef;
            config.ActiveDashboard = baseName;
            await _calibration.SaveDashboardConfigAsync(config).ConfigureAwait(false);

            _logger.LogInformation("Dashboard '{Name}' imported as '{NewName}'", result.DashboardName, baseName);
            return JsonSerializer.Serialize(new
            {
                picked = true,
                success = true,
                dashboardName = baseName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportDashboard failed");
            return JsonSerializer.Serialize(new { picked = true, success = false, error = ex.Message });
        }
    }

    // ─── File Export ─────────────────────────────────────────────────────────

    /// <summary>
    /// Save a text file to the user's chosen location.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveFile', [jsonPayload])
    /// Payload: { filename, content, fileExtension }
    /// </summary>
    public async Task<string> SaveFile(string jsonPayload)
    {
        _logger.LogInformation("SaveFile called");
        try
        {
            var payload = JsonSerializer.Deserialize<SaveFilePayload>(jsonPayload, SJsonOptions);
            _logger.LogInformation("SaveFile deserialized: Filename={Filename}, FileExtension={FileExtension}", payload?.Filename, payload?.FileExtension);
            if (payload == null)
                return JsonSerializer.Serialize(new { success = false, error = "Invalid payload" });

            // Decode base64 if encoding is specified (InvokeDotNet sends params as HTTP headers
            // which must be ISO-8859-1 — Unicode content breaks without encoding)
            byte[] bytes;
            if (string.Equals(payload.Encoding, "base64", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(payload.Content))
                bytes = Convert.FromBase64String(payload.Content);
            else
                bytes = System.Text.Encoding.UTF8.GetBytes(payload.Content ?? "");

            // Ensure file extension starts with '.' and has no wildcards
            var ext = payload.FileExtension;
            if (string.IsNullOrEmpty(ext) || !ext.StartsWith('.') || ext.Contains('*') || ext.Contains('?'))
                ext = Path.GetExtension(payload.Filename);
            if (string.IsNullOrEmpty(ext))
                ext = ".txt";

            _logger.LogInformation("SaveFile using extension: {Ext}", ext);

#if WINDOWS
            var windowsApp = App.Current;
            if (windowsApp?.Windows.Count > 0)
            {
                var savePicker = new Windows.Storage.Pickers.FileSavePicker
                {
                    SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
                };
                savePicker.FileTypeChoices.Add("Files", [ext]);
                savePicker.SuggestedFileName = payload.Filename;

                if (windowsApp.Windows[0]?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                    WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
                }

                var file = await savePicker.PickSaveFileAsync();
                if (file == null)
                    return JsonSerializer.Serialize(new { success = false, error = "Save cancelled" });

                using var stream = await file.OpenStreamForWriteAsync().ConfigureAwait(false);
                await stream.WriteAsync(bytes).ConfigureAwait(false);
                _logger.LogInformation("File saved to {Path}", file.Path);
                return JsonSerializer.Serialize(new { success = true, path = file.Path });
            }

            return JsonSerializer.Serialize(new { success = false, error = "No window available" });
#else
            var cacheDir = FileSystem.CacheDirectory;
            var filePath = Path.Combine(cacheDir, payload.Filename);
            await File.WriteAllBytesAsync(filePath, bytes).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
                Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = $"Save {payload.Filename}",
                    File = new ShareFile(filePath)
                })).ConfigureAwait(false);

            _logger.LogInformation("File saved to {Path}", filePath);
            return JsonSerializer.Serialize(new { success = true });
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveFile failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private record SaveFilePayload(string Filename, string Content, string FileExtension, string? Encoding = null);

    /// <summary>
    /// Import a YAML table file.
    /// Called from JS: window.HybridWebView.InvokeDotNet('ImportYamlTable')
    /// </summary>
    public async Task<string> ImportYamlTable()
    {
        _logger.LogInformation("ImportYamlTable called");
        try
        {
            var isWindows = DeviceInfo.Current.Platform == DevicePlatform.WinUI;
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select YAML Calibration File",
                FileTypes = isWindows
                    ? new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, [".yaml", ".yml", ".txt"] }
                    })
                    : null // Android: no MIME filter — shows all files
            }).ConfigureAwait(false);

            if (fileResult == null)
                return JsonSerializer.Serialize(new { picked = false });

            var content = await File.ReadAllTextAsync(fileResult.FullPath).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { picked = true, content });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportYamlTable failed");
            return JsonSerializer.Serialize(new { picked = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Called from JS: window.HybridWebView.InvokeDotNet('ImportCsvTable')
    /// </summary>
    public async Task<string> ImportCsvTable()
    {
        _logger.LogInformation("ImportCsvTable called");
        try
        {
            var isWindows = DeviceInfo.Current.Platform == DevicePlatform.WinUI;
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select CSV Calibration File",
                FileTypes = isWindows
                    ? new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, [".csv", ".txt"] }
                    })
                    : null // Android: no MIME filter — text/csv is invalid, shows no files
            }).ConfigureAwait(false);

            if (fileResult == null)
                return JsonSerializer.Serialize(new { picked = false });

            var content = await File.ReadAllTextAsync(fileResult.FullPath).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { picked = true, content });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportCsvTable failed");
            return JsonSerializer.Serialize(new { picked = false, error = ex.Message });
        }
    }
}

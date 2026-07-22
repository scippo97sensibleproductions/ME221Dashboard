using System.IO.Compression;
using System.Text.Json;
using ME221Dashboard.Services;
using ME221Dashboard.Services.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.Controls;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    private SessionStorageService? _sessionStorage;
    private SessionPackageService? _sessionPackage;

    private SessionStorageService SessionStorage =>
        _sessionStorage ??= new SessionStorageService();

    private SessionPackageService SessionPackage =>
        _sessionPackage ??= new SessionPackageService();

    // ── Session CRUD (metadata + summaries) ────────────────────────────────

    /// <summary>
    /// Load all session summaries from C# backend.
    /// Called from JS: window.HybridWebView.InvokeDotNet('LoadSessionList')
    /// Returns: JSON array of SessionSummary
    /// </summary>
    public async Task<string> LoadSessionList()
    {
        try
        {
            var summaries = await SessionStorage.ListSessionsAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(summaries, SessionJsonContext.Default.ListSessionSummary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadSessionList failed");
            return "[]";
        }
    }

    /// <summary>
    /// Load a full session (with data) from C# backend.
    /// Called from JS: window.HybridWebView.InvokeDotNet('LoadSession', [id])
    /// Returns: JSON RecordedSession or null
    /// </summary>
    public async Task<string> LoadSession(string id)
    {
        try
        {
            var session = await SessionStorage.LoadSessionAsync(id).ConfigureAwait(false);
            if (session == null)
                return "null";
            return JsonSerializer.Serialize(session, SessionJsonContext.Default.RecordedSession);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadSession failed for {Id}", id);
            return "null";
        }
    }

    /// <summary>
    /// Save session metadata/summary to C# backend. Full data stays in localStorage.
    /// Called from JS: window.HybridWebView.InvokeDotNet('SaveSessionMetadata', [jsonPayload])
    /// Payload: JSON RecordedSession (from localStorage)
    /// </summary>
    public async Task<string> SaveSessionMetadata(string jsonPayload)
    {
        try
        {
            var session = JsonSerializer.Deserialize(jsonPayload, SessionJsonContext.Default.RecordedSession);
            if (session == null)
                return JsonSerializer.Serialize(new { success = false, error = "Invalid session data" });

            await SessionStorage.SaveSessionAsync(session).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveSessionMetadata failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a session from C# backend.
    /// Called from JS: window.HybridWebView.InvokeDotNet('DeleteSession', [id])
    /// </summary>
    public async Task<string> DeleteSession(string id)
    {
        try
        {
            await SessionStorage.DeleteSessionAsync(id).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteSession failed for {Id}", id);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Rename a session in C# backend.
    /// Called from JS: window.HybridWebView.InvokeDotNet('RenameSession', [jsonPayload])
    /// Payload: { id, name }
    /// </summary>
    public async Task<string> RenameSession(string jsonPayload)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(jsonPayload);
            var root = doc.RootElement;
            var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(id))
                return JsonSerializer.Serialize(new { success = false, error = "Missing session id" });

            await SessionStorage.RenameSessionAsync(id, name).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RenameSession failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Migrate sessions from localStorage to C# backend.
    /// Called from JS: window.HybridWebView.InvokeDotNet('MigrateSessions', [jsonArray])
    /// </summary>
    public async Task<string> MigrateSessions(string jsonArray)
    {
        try
        {
            await SessionStorage.MigrateFromLocalStorageAsync(jsonArray).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MigrateSessions failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Clear all sessions from C# backend.
    /// Called from JS: window.HybridWebView.InvokeDotNet('ClearAllSessions')
    /// </summary>
    public async Task<string> ClearAllSessions()
    {
        try
        {
            await SessionStorage.ClearAllAsync().ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClearAllSessions failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    // ── Session Export/Import (.mes files) ──────────────────────────────────

    /// <summary>
    /// Export a single session to a .mes file. C# loads from disk.
    /// Called from JS: window.HybridWebView.InvokeDotNet('ExportSession', [sessionId])
    /// </summary>
    public async Task<string> ExportSession(string sessionId)
    {
        try
        {
            var session = await SessionStorage.LoadSessionAsync(sessionId).ConfigureAwait(false);
            if (session == null)
                return JsonSerializer.Serialize(new { success = false, error = "Session not found on disk" });

            var packageBytes = SessionPackage.CreatePackage([session]);
            var fileName = $"ME221-{SanitizeFileName(session.Name)}.mes";

            return await SaveFileWithPicker(packageBytes, fileName, "ME221 Session", ".mes").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExportSession failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Export all sessions to a single .mes file. C# loads from disk.
    /// Called from JS: window.HybridWebView.InvokeDotNet('ExportAllSessions')
    /// </summary>
    public async Task<string> ExportAllSessions()
    {
        try
        {
            var summaries = await SessionStorage.ListSessionsAsync().ConfigureAwait(false);
            if (summaries.Count == 0)
                return JsonSerializer.Serialize(new { success = false, error = "No sessions to export" });

            var sessions = new List<RecordedSession>();
            foreach (var summary in summaries)
            {
                var session = await SessionStorage.LoadSessionAsync(summary.Id).ConfigureAwait(false);
                if (session != null) sessions.Add(session);
            }

            var packageBytes = SessionPackage.CreatePackage(sessions);
            var fileName = "ME221-Sessions.mes";

            return await SaveFileWithPicker(packageBytes, fileName, "ME221 Session", ".mes").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExportAllSessions failed");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Import sessions from a .mes file.
    /// Called from JS: window.HybridWebView.InvokeDotNet('ImportSession')
    /// Returns: { picked: true, sessions: RecordedSession[] } or { picked: false }
    /// </summary>
    public async Task<string> ImportSession()
    {
        _logger.LogInformation("ImportSession called");
        try
        {
            var isWindows = DeviceInfo.Current.Platform == DevicePlatform.WinUI;
            var fileResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Session File (.mes)",
                FileTypes = isWindows
                    ? new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, [".mes"] }
                    })
                    : null
            }).ConfigureAwait(false);

            if (fileResult == null)
                return JsonSerializer.Serialize(new { picked = false });

            // Check file size before reading
            var fileInfo = new FileInfo(fileResult.FullPath);
            if (!fileInfo.Exists)
                return JsonSerializer.Serialize(new { picked = false, error = "File not found" });

            const long maxSize = 100L * 1024 * 1024; // 100MB
            if (fileInfo.Length > maxSize)
                return JsonSerializer.Serialize(new { picked = false, error = $"File too large ({fileInfo.Length / (1024 * 1024)}MB, max 100MB)" });

            await using var stream = await fileResult.OpenReadAsync().ConfigureAwait(false);
            var result = SessionPackage.ExtractPackage(stream);

            if (!result.Success)
                return JsonSerializer.Serialize(new { picked = true, success = false, error = result.Error });

            // Save imported sessions to C# backend
            foreach (var session in result.Sessions)
            {
                await SessionStorage.SaveSessionAsync(session).ConfigureAwait(false);
            }

            // Return full sessions to JS so they can be added to localStorage
            return JsonSerializer.Serialize(new ImportSessionResponse
            {
                Picked = true,
                Success = true,
                Sessions = result.Sessions,
            }, SessionJsonContext.Default.ImportSessionResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportSession failed");
            return JsonSerializer.Serialize(new { picked = false, error = ex.Message });
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static async Task<string> SaveFileWithPicker(byte[] data, string fileName, string fileDescription, string extension)
    {
#if WINDOWS
        var windowsApp = App.Current;
        if (windowsApp?.Windows.Count > 0)
        {
            var savePicker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            savePicker.FileTypeChoices.Add(fileDescription, [extension]);
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
            await stream.WriteAsync(data).ConfigureAwait(false);
            return JsonSerializer.Serialize(new { success = true, path = file.Path });
        }

        return JsonSerializer.Serialize(new { success = false, error = "No window available" });
#else
        var cacheDir = FileSystem.CacheDirectory;
        var filePath = Path.Combine(cacheDir, fileName);
        await File.WriteAllBytesAsync(filePath, data).ConfigureAwait(false);

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = $"Export {fileDescription}",
                    File = new ShareFile(filePath)
                })).ConfigureAwait(false);
        }
        finally
        {
            try { File.Delete(filePath); } catch { }
        }

        return JsonSerializer.Serialize(new { success = true });
#endif
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray());
    }

    // ── Legacy CSV Import ──────────────────────────────────────────────────

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
                FileTypes = isWindows
                    ? new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, [".csv", ".txt"] }
                    })
                    : null
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

// Response type for ImportSession (includes sessions list)
internal sealed class ImportSessionResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("picked")]
    public bool Picked { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("success")]
    public bool Success { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("error")]
    public string? Error { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("sessions")]
    public List<ME221Dashboard.Services.Sessions.RecordedSession> Sessions { get; init; } = new();
}

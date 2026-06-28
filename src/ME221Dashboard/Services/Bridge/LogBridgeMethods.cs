using System.Text.Json;
using ME221Dashboard.Services;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

public partial class HybridBridgeService
{
    // ─── Log Streaming ────────────────────────────────────────────────────

    /// <summary>
    /// Start streaming log entries to the WebView. Called from JS: window.HybridWebView.InvokeDotNet('StartLogStreaming')
    /// </summary>
    public string StartLogStreaming()
    {
        _logger.LogInformation("StartLogStreaming called");
        _logCapture.StartStreaming();
        return JsonSerializer.Serialize(new { success = true });
    }

    /// <summary>
    /// Stop streaming log entries to the WebView. Called from JS: window.HybridWebView.InvokeDotNet('StopLogStreaming')
    /// </summary>
    public string StopLogStreaming()
    {
        _logger.LogInformation("StopLogStreaming called");
        _logCapture.StopStreaming();
        return JsonSerializer.Serialize(new { success = true });
    }

    /// <summary>
    /// Get recent log entries. Called from JS: window.HybridWebView.InvokeDotNet('GetRecentLogs', [count])
    /// </summary>
    public string GetRecentLogs(int count = 200)
    {
        var entries = _logCapture.GetRecentEntries(count);
        var json = JsonSerializer.Serialize(new
        {
            entries = entries.Select(e => new
            {
                timestamp = e.Timestamp.ToString("HH:mm:ss.fff"),
                level = e.Level,
                category = e.Category,
                message = e.Message,
                exception = e.Exception,
            })
        });
        return json;
    }

    /// <summary>
    /// Clear all captured log entries. Called from JS: window.HybridWebView.InvokeDotNet('ClearLogs')
    /// </summary>
    public string ClearLogs()
    {
        _logCapture.Clear();
        return JsonSerializer.Serialize(new { success = true });
    }
}

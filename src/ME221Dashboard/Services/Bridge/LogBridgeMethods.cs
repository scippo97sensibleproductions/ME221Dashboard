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
        // Companion mode: the relayed log tail (R17, KTD-7), never B's local logs.
        if (_mirrorMode && _mirrorStore is { } store)
        {
            var entries = store.RecentLogs.TakeLast(count).Select(ParseRelayedLog);
            return JsonSerializer.Serialize(new { entries });
        }

        var localEntries = _logCapture.GetRecentEntries(count);
        var json = JsonSerializer.Serialize(new
        {
            entries = localEntries.Select(e => new
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

    /// <summary>Relayed log entries are stored as ready JSON objects; return them as-is.</summary>
    private static object? ParseRelayedLog(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.Clone();
            return root;
        }
        catch (JsonException)
        {
            return new { message = json };
        }
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

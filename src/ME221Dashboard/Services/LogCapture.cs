using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ME221Dashboard.Services;

/// <summary>
/// Central log capture that stores recent log entries and optionally streams them to the WebView.
/// Registered as ILoggerProvider alongside Serilog so ALL app logs pass through here.
/// </summary>
public sealed class LogCapture : ILoggerProvider
{
    private const int MaxEntries = 500;

    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private int _totalDropped;
    private volatile bool _streaming;
    private Func<string, Task>? _sendToWebView;

    public bool IsStreaming => _streaming;

    public void SetWebViewSender(Func<string, Task>? sender)
    {
        _sendToWebView = sender;
    }

    public void StartStreaming() => _streaming = true;
    public void StopStreaming() => _streaming = false;

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(this, categoryName);

    internal void Enqueue(string category, LogLevel level, string message, Exception? exception)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level.ToString(),
            Category = category,
            Message = message,
            Exception = exception?.ToString()
        };

        _entries.Enqueue(entry);
        while (_entries.Count > MaxEntries)
        {
            _entries.TryDequeue(out _);
            Interlocked.Increment(ref _totalDropped);
        }

        if (_streaming && _sendToWebView is { } send)
        {
            var json = JsonSerializer.Serialize(new
            {
                @event = "logEntry",
                timestamp = entry.Timestamp.ToString("HH:mm:ss.fff"),
                level = entry.Level,
                category = entry.Category,
                message = entry.Message,
                exception = entry.Exception,
            });
            _ = send(json);
        }
    }

    public List<LogEntry> GetRecentEntries(int count = 200)
    {
        var arr = _entries.ToArray();
        var start = Math.Max(0, arr.Length - count);
        return [.. arr.Skip(start)];
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
        _totalDropped = 0;
    }

    public void Dispose() { }

    internal sealed class CaptureLogger(LogCapture capture, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            capture.Enqueue(category, logLevel, message, exception);
        }
    }

    public sealed class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = "";
        public string Category { get; set; } = "";
        public string Message { get; set; } = "";
        public string? Exception { get; set; }
    }
}

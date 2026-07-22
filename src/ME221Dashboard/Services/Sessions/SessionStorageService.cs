using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services.Sessions;

public sealed class SessionStorageService(ILogger<SessionStorageService>? logger = null)
{
    private readonly ILogger<SessionStorageService> _logger = logger ?? NullLogger<SessionStorageService>.Instance;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private static string BasePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ME221", "sessions");

    private const int MaxSessions = 200;
    private const long MaxTotalStorageBytes = 500L * 1024 * 1024; // 500MB

    public async Task<List<SessionSummary>> ListSessionsAsync()
    {
        try
        {
            var dir = BasePath;
            if (!Directory.Exists(dir))
                return [];

            var files = Directory.GetFiles(dir, "*.json");
            var summaries = new List<SessionSummary>(files.Length);

            foreach (var file in files)
            {
                try
                {
                    // Use JsonDocument to extract only summary fields without deserializing full session
                    var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                    var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                    var startTime = root.TryGetProperty("startTime", out var stProp) ? stProp.GetString() ?? "" : "";
                    var durationMs = root.TryGetProperty("durationMs", out var durProp) ? durProp.GetInt64() : 0;
                    var sensorCount = root.TryGetProperty("sensorIds", out var idsProp) ? idsProp.GetArrayLength() : 0;

                    if (!string.IsNullOrEmpty(id))
                    {
                        summaries.Add(new SessionSummary
                        {
                            Id = id,
                            Name = name,
                            StartTime = startTime,
                            DurationMs = durationMs,
                            SensorCount = sensorCount,
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read session file: {File}", file);
                }
            }

            return summaries.OrderByDescending(s => s.StartTime).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListSessionsAsync failed");
            return [];
        }
    }

    public async Task<RecordedSession?> LoadSessionAsync(string id)
    {
        try
        {
            var path = GetSessionPath(id);
            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            return JsonSerializer.Deserialize(json, SessionJsonContext.Default.RecordedSession);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadSessionAsync failed for {Id}", id);
            return null;
        }
    }

    public async Task SaveSessionAsync(RecordedSession session)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(BasePath);

            // Enforce max sessions — delete oldest if over limit
            var files = Directory.GetFiles(BasePath, "*.json");
            if (files.Length >= MaxSessions)
            {
                var toDelete = files
                    .OrderBy(f => File.GetLastWriteTimeUtc(f))
                    .Take(files.Length - MaxSessions + 1)
                    .ToList();
                foreach (var f in toDelete)
                {
                    try { File.Delete(f); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete old session: {File}", f); }
                }
            }

            // Enforce total storage limit
            var totalSize = Directory.GetFiles(BasePath, "*.json").Sum(f =>
            {
                try { return new FileInfo(f).Length; }
                catch { return 0L; }
            });
            var sessionJson = JsonSerializer.Serialize(session, SessionJsonContext.Default.RecordedSession);
            var sessionSize = System.Text.Encoding.UTF8.GetByteCount(sessionJson);

            if (totalSize + sessionSize > MaxTotalStorageBytes)
            {
                _logger.LogWarning("Storage limit exceeded ({Total}+{New} > {Max}), pruning oldest",
                    totalSize, sessionSize, MaxTotalStorageBytes);
                PruneOldestSessions(sessionSize);
            }

            var path = GetSessionPath(session.Id);
            var tmpPath = path + $".tmp.{Environment.ProcessId}.{DateTime.UtcNow.Ticks}";
            await File.WriteAllTextAsync(tmpPath, sessionJson).ConfigureAwait(false);
            File.Move(tmpPath, path, overwrite: true);

            _logger.LogDebug("Saved session {Id} ({Size} bytes)", session.Id, sessionSize);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "SaveSessionAsync IOException for {Id}", session.Id);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "SaveSessionAsync UnauthorizedAccess for {Id}", session.Id);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteSessionAsync(string id)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var path = GetSessionPath(id);
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogDebug("Deleted session {Id}", id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteSessionAsync failed for {Id}", id);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task RenameSessionAsync(string id, string name)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var path = GetSessionPath(id);
            if (!File.Exists(path))
                return;

            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            var session = JsonSerializer.Deserialize(json, SessionJsonContext.Default.RecordedSession);
            if (session == null)
                return;

            var updated = new RecordedSession
            {
                Id = session.Id,
                Name = name,
                StartTime = session.StartTime,
                DurationMs = session.DurationMs,
                SensorIds = session.SensorIds,
                SensorNames = session.SensorNames,
                Data = session.Data,
                FreezeFrames = session.FreezeFrames,
            };

            var updatedJson = JsonSerializer.Serialize(updated, SessionJsonContext.Default.RecordedSession);
            var tmpPath = path + $".tmp.{Environment.ProcessId}.{DateTime.UtcNow.Ticks}";
            await File.WriteAllTextAsync(tmpPath, updatedJson).ConfigureAwait(false);
            File.Move(tmpPath, path, overwrite: true);

            _logger.LogDebug("Renamed session {Id} to '{Name}'", id, name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RenameSessionAsync failed for {Id}", id);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task ClearAllAsync()
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var dir = BasePath;
            if (!Directory.Exists(dir))
                return;

            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try { File.Delete(file); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete session: {File}", file); }
            }
            _logger.LogDebug("Cleared all sessions");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClearAllAsync failed");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task MigrateFromLocalStorageAsync(string jsonArray)
    {
        try
        {
            var sessions = JsonSerializer.Deserialize(jsonArray, SessionJsonContext.Default.ListRecordedSession);
            if (sessions == null || sessions.Count == 0)
                return;

            _logger.LogInformation("Migrating {Count} sessions from localStorage", sessions.Count);
            foreach (var session in sessions)
            {
                await SaveSessionAsync(session).ConfigureAwait(false);
            }
            _logger.LogInformation("Migration complete: {Count} sessions saved to C# backend", sessions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MigrateFromLocalStorageAsync failed");
        }
    }

    private static string GetSessionPath(string id)
    {
        var sanitized = SanitizeId(id);
        return Path.Combine(BasePath, $"{sanitized}.json");
    }

    internal static string SanitizeId(string id)
    {
        var chars = id.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray();
        return chars.Length == 0 ? "unnamed" : new string(chars);
    }

    private void PruneOldestSessions(long neededBytes)
    {
        try
        {
            var files = Directory.GetFiles(BasePath, "*.json")
                .Select(f => new FileInfo(f))
                .OrderBy(fi => fi.LastWriteTimeUtc)
                .ToList();

            long freed = 0;
            foreach (var fi in files)
            {
                if (freed >= neededBytes)
                    break;
                var len = fi.Length;
                try
                {
                    fi.Delete();
                    freed += len;
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to prune session: {File}", fi.FullName); }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PruneOldestSessions failed");
        }
    }
}

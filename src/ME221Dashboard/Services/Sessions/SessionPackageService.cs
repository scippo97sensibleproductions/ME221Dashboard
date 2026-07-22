using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services.Sessions;

public sealed class SessionPackageService(ILogger<SessionPackageService>? logger = null)
{
    private readonly ILogger<SessionPackageService> _logger = logger ?? NullLogger<SessionPackageService>.Instance;

    private const long MaxTotalUncompressedBytes = 100L * 1024 * 1024; // 100MB
    private const int MaxSamplesPerSensor = 2_000_000;
    private const int MaxSensors = 50;
    private const long MaxDurationMs = 24 * 60 * 60 * 1000; // 24 hours

    public byte[] CreatePackage(List<RecordedSession> sessions)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Write manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                var manifest = new SessionManifest
                {
                    SchemaVersion = 1,
                    AppVersion = GetAppVersion(),
                    ExportDate = DateTime.UtcNow.ToString("o"),
                    SessionCount = sessions.Count,
                };
                writer.Write(JsonSerializer.Serialize(manifest, SessionJsonContext.Default.SessionManifest));
            }

            // Write each session
            for (int i = 0; i < sessions.Count; i++)
            {
                var entry = archive.CreateEntry($"session-{i}.json");
                using var writer = new StreamWriter(entry.Open());
                var json = JsonSerializer.Serialize(sessions[i], SessionJsonContext.Default.RecordedSession);
                writer.Write(json);
            }
        }

        var bytes = ms.ToArray();
        _logger.LogInformation("Session package created: {Count} sessions, {Size} bytes", sessions.Count, bytes.Length);
        return bytes;
    }

    public ImportSessionResult ExtractPackage(Stream zipStream)
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

            // Read manifest
            var manifestEntry = archive.GetEntry("manifest.json");
            if (manifestEntry == null)
                return Error("Invalid package: missing manifest.json");

            string manifestJson;
            using (var reader = new StreamReader(manifestEntry.Open()))
                manifestJson = reader.ReadToEnd();

            var manifest = JsonSerializer.Deserialize(manifestJson, SessionJsonContext.Default.SessionManifest);
            if (manifest == null)
                return Error("Invalid package: malformed manifest");

            if (manifest.SchemaVersion > 1)
                return Error($"Unsupported schema version: {manifest.SchemaVersion}");

            // Check total uncompressed size
            long totalSize = archive.Entries.Sum(e => e.Length);
            if (totalSize > MaxTotalUncompressedBytes)
                return Error($"Package too large: {totalSize / (1024 * 1024)}MB (max {MaxTotalUncompressedBytes / (1024 * 1024)}MB)");

            // Read sessions
            var sessions = new List<RecordedSession>();
            for (int i = 0; i < manifest.SessionCount; i++)
            {
                var entry = archive.GetEntry($"session-{i}.json");
                if (entry == null)
                {
                    _logger.LogWarning("Missing session-{I}.json in package, stopping", i);
                    break;
                }

                string sessionJson;
                using (var entryStream = entry.Open())
                {
                    // Use a size-limited reader to prevent zip bombs
                    var limitedStream = new SizeLimitedStream(entryStream, MaxTotalUncompressedBytes);
                    using var limitedReader = new StreamReader(limitedStream);
                    sessionJson = limitedReader.ReadToEnd();
                    if (limitedStream.Exceeded)
                    {
                        _logger.LogWarning("Session entry {I} exceeded size limit during decompression, aborting", i);
                        return Error("Session data too large during decompression");
                    }
                }

                var session = JsonSerializer.Deserialize(sessionJson, SessionJsonContext.Default.RecordedSession);
                if (session == null)
                {
                    _logger.LogWarning("Failed to deserialize session-{I}, skipping", i);
                    continue;
                }

                // Validate session constraints
                if (session.DurationMs > MaxDurationMs)
                {
                    _logger.LogWarning("Session {Id} duration {Duration}ms exceeds max, skipping", session.Id, session.DurationMs);
                    continue;
                }

                if (session.SensorIds == null || session.SensorIds.Length > MaxSensors)
                {
                    _logger.LogWarning("Session {Id} has invalid sensor count, skipping", session.Id);
                    continue;
                }

                bool tooManySamples = session.Data != null &&
                    session.Data.Values.Any(pts => pts != null && pts.Count > MaxSamplesPerSensor);
                if (tooManySamples)
                {
                    _logger.LogWarning("Session {Id} has too many samples per sensor, skipping", session.Id);
                    continue;
                }

                sessions.Add(session);
            }

            _logger.LogInformation("Imported {Count} sessions from package", sessions.Count);
            return new ImportSessionResult { Success = true, Sessions = sessions };
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

    private static ImportSessionResult Error(string message) =>
        new() { Success = false, Error = message };

    private static string GetAppVersion()
    {
        try
        {
            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            return version?.ToString(3) ?? "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }
}

/// <summary>
/// Stream wrapper that tracks total bytes read and signals when a limit is exceeded.
/// Used to prevent zip bombs during .mes import.
/// </summary>
internal sealed class SizeLimitedStream(Stream inner, long limit) : Stream
{
    private long _totalRead;
    public bool Exceeded { get; private set; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);
        _totalRead += read;
        if (_totalRead > limit)
            Exceeded = true;
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        _totalRead += read;
        if (_totalRead > limit)
            Exceeded = true;
        return read;
    }

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

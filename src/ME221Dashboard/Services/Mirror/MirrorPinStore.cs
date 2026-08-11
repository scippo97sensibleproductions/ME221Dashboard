using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services.Mirror;

/// <summary>B-side remembered pairing for one A instance: token (R16) + pinned cert thumbprint (R15).</summary>
public sealed class MirrorPinEntry
{
    public string InstanceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Token { get; set; } = "";
    public string CertThumbprint { get; set; } = "";
}

/// <summary>
/// B's persisted pairing store (<c>companion-pins.json</c>, app data dir): the token
/// issued at approval and A's pinned certificate thumbprint, keyed by A's instance id.
/// Written atomically per the repo convention (PersistenceService).
/// </summary>
public sealed class MirrorPinStore(ILogger<MirrorPinStore>? logger = null)
{
    private readonly ILogger<MirrorPinStore> _logger = logger ?? NullLogger<MirrorPinStore>.Instance;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private static string DefaultBasePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ME221");

    private string _basePath = DefaultBasePath;
    private string StorePath => Path.Combine(_basePath, "companion-pins.json");

    public void SetBasePath(string basePath) => _basePath = basePath;

    public async Task<MirrorPinEntry?> GetAsync(string instanceId)
    {
        var file = await LoadAsync().ConfigureAwait(false);
        return file.Pins.FirstOrDefault(p => p.InstanceId == instanceId);
    }

    /// <summary>Remember the pairing (approval or cert pin update).</summary>
    public async Task SaveAsync(MirrorPinEntry entry)
    {
        await MutateAsync(file =>
        {
            file.Pins.RemoveAll(p => p.InstanceId == entry.InstanceId);
            file.Pins.Add(entry);
        }).ConfigureAwait(false);
    }

    /// <summary>Forget the pairing (re-pair flow, R20).</summary>
    public async Task RemoveAsync(string instanceId)
    {
        await MutateAsync(file => file.Pins.RemoveAll(p => p.InstanceId == instanceId)).ConfigureAwait(false);
    }

    private async Task MutateAsync(Action<CompanionPinsFile> mutate)
    {
        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var file = await LoadAsync().ConfigureAwait(false);
            mutate(file);
            await WriteAsync(file).ConfigureAwait(false);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private async Task<CompanionPinsFile> LoadAsync()
    {
        try
        {
            if (!File.Exists(StorePath))
                return new CompanionPinsFile();

            var json = await File.ReadAllTextAsync(StorePath).ConfigureAwait(false);
            return JsonSerializer.Deserialize(json, V2JsonContext.Default.CompanionPinsFile) ?? new CompanionPinsFile();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Corrupted companion-pins.json; starting with an empty store");
            return new CompanionPinsFile();
        }
    }

    private async Task WriteAsync(CompanionPinsFile file)
    {
        Directory.CreateDirectory(_basePath);
        var json = JsonSerializer.Serialize(file, V2JsonContext.Default.CompanionPinsFile);
        var tmpPath = StorePath + $".tmp.{Environment.ProcessId}.{DateTime.UtcNow.Ticks}";
        await File.WriteAllTextAsync(tmpPath, json).ConfigureAwait(false);
        File.Move(tmpPath, StorePath, overwrite: true);
    }
}

/// <summary>Persisted shape of companion-pins.json.</summary>
public sealed class CompanionPinsFile
{
    public int Version { get; set; } = 1;
    public List<MirrorPinEntry> Pins { get; set; } = [];
}
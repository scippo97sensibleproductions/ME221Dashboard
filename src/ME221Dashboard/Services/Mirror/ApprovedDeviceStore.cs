using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ME221Dashboard.Services.Mirror;

/// <summary>One approved viewer device on A (R3, R16, KTD-3).</summary>
public sealed class ApprovedDeviceEntry
{
    public string ClientId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string Token { get; set; } = "";
    public DateTime ApprovedAtUtc { get; set; }
}

/// <summary>
/// A's approved-viewer store (KTD-3): <c>companion-viewers.json</c> under the app data
/// directory, plain JSON following the PersistenceService write discipline (atomic
/// tmp+move, corruption backup, directory creation). Holds the per-instance identity
/// (instance id + display name) used by the discovery beacon (KTD-8) and the per-device
/// tokens issued at approval.
/// </summary>
public sealed class ApprovedDeviceStore(ILogger<ApprovedDeviceStore>? logger = null)
{
    private readonly ILogger<ApprovedDeviceStore> _logger = logger ?? NullLogger<ApprovedDeviceStore>.Instance;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private static string DefaultBasePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ME221");

    private string _basePath = DefaultBasePath;
    private string StorePath => Path.Combine(_basePath, "companion-viewers.json");

    internal string BasePath => _basePath;

    /// <summary>Test seam: point the store at a temp directory.</summary>
    public void SetBasePath(string basePath) => _basePath = basePath;

    /// <summary>All approved devices, newest first.</summary>
    public async Task<List<ApprovedDeviceEntry>> GetAllAsync()
    {
        var file = await LoadAsync().ConfigureAwait(false);
        return file.Devices.OrderByDescending(d => d.ApprovedAtUtc).ToList();
    }

    /// <summary>Look up an approved device by its credential token (R16).</summary>
    public async Task<ApprovedDeviceEntry?> FindByTokenAsync(string token)
    {
        var file = await LoadAsync().ConfigureAwait(false);
        return file.Devices.FirstOrDefault(d => d.Token == token);
    }

    public async Task<ApprovedDeviceEntry?> FindByClientIdAsync(string clientId)
    {
        var file = await LoadAsync().ConfigureAwait(false);
        return file.Devices.FirstOrDefault(d => d.ClientId == clientId);
    }

    /// <summary>Issue a new credential and remember the device (R2, R16).</summary>
    public async Task<ApprovedDeviceEntry> AddAsync(string clientId, string deviceName)
    {
        var entry = new ApprovedDeviceEntry
        {
            ClientId = clientId,
            DeviceName = deviceName,
            Token = Guid.NewGuid().ToString("N"),
            ApprovedAtUtc = DateTime.UtcNow,
        };
        await MutateAsync(file => file.Devices.Add(entry)).ConfigureAwait(false);
        return entry;
    }

    /// <summary>Forget a device; its token no longer grants access (R3).</summary>
    public async Task<bool> RevokeAsync(string clientId)
    {
        var removed = false;
        await MutateAsync(file =>
        {
            removed = file.Devices.RemoveAll(d => d.ClientId == clientId) > 0;
        }).ConfigureAwait(false);
        return removed;
    }

    /// <summary>Pairing-reset: forget every device (R20). Tokens are invalidated.</summary>
    public async Task ResetAsync()
    {
        await MutateAsync(file => file.Devices.Clear()).ConfigureAwait(false);
    }

    /// <summary>Rename A's own display name as shown in B's discovery (U11, KTD-8).</summary>
    public async Task SetDisplayNameAsync(string displayName)
    {
        await MutateAsync(file => file.DisplayName = displayName).ConfigureAwait(false);
    }

    /// <summary>A's display name as shown in B's discovery (KTD-8).</summary>
    public async Task<string> GetDisplayNameAsync()
    {
        var file = await LoadAsync().ConfigureAwait(false);
        return file.DisplayName;
    }

    /// <summary>Instance identity: stable per A instance, distinct across two A devices on one network.</summary>
    public async Task<string> GetOrCreateInstanceIdAsync()
    {
        var file = await LoadAsync().ConfigureAwait(false);
        return file.InstanceId;
    }

    private async Task MutateAsync(Action<CompanionViewersFile> mutate)
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

    /// <summary>Beacon display name for a fresh install: the platform device name so two
    /// hosts on one network are distinguishable; ME221 is the fallback when the
    /// platform name is unavailable (e.g. unit test hosts).</summary>
    private static string DefaultDisplayName()
    {
        try
        {
            var name = DeviceInfo.Current.Name;
            return string.IsNullOrWhiteSpace(name) || name == "unknown" ? "ME221" : name;
        }
        catch
        {
            return "ME221"; // non-MAUI host (tests) — DeviceInfo has no platform impl
        }
    }

    private async Task<CompanionViewersFile> LoadAsync()
    {
        try
        {
            if (!File.Exists(StorePath))
                return CreateFresh();

            var json = await File.ReadAllTextAsync(StorePath).ConfigureAwait(false);
            var file = JsonSerializer.Deserialize(json, V2JsonContext.Default.CompanionViewersFile);
            if (file == null)
                throw new JsonException("companion-viewers.json deserialized to null");

            file.InstanceId = string.IsNullOrEmpty(file.InstanceId) ? Guid.NewGuid().ToString("N") : file.InstanceId;
            if (string.IsNullOrEmpty(file.DisplayName) || file.DisplayName == "ME221")
            {
                // Migrate legacy installs (which seeded the generic "ME221") to the
                // platform device name so hosts are identifiable in discovery.
                var migrated = DefaultDisplayName();
                file.DisplayName = migrated;
                try
                {
                    await WriteAsync(file).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Failed to persist the migrated display name");
                }
            }
            return file;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Corrupted companion-viewers.json; falling back to an empty store");
            BackupCorrupt();
            return CreateFresh();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to read companion-viewers.json; falling back to an empty store");
            BackupCorrupt();
            return CreateFresh();
        }
    }

    private CompanionViewersFile CreateFresh()
    {
        var file = new CompanionViewersFile { InstanceId = Guid.NewGuid().ToString("N"), DisplayName = DefaultDisplayName() };
        WriteAsync(file).GetAwaiter().GetResult();
        return file;
    }

    private async Task WriteAsync(CompanionViewersFile file)
    {
        Directory.CreateDirectory(_basePath);
        var json = JsonSerializer.Serialize(file, V2JsonContext.Default.CompanionViewersFile);
        var tmpPath = StorePath + $".tmp.{Environment.ProcessId}.{DateTime.UtcNow.Ticks}";
        await File.WriteAllTextAsync(tmpPath, json).ConfigureAwait(false);
        File.Move(tmpPath, StorePath, overwrite: true);
    }

    private void BackupCorrupt()
    {
        try
        {
            if (File.Exists(StorePath))
                File.Move(StorePath, StorePath + $".corrupt.{DateTime.UtcNow:yyyyMMdd-HHmmss}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to back up corrupt companion-viewers.json");
        }
    }
}

/// <summary>Persisted shape of companion-viewers.json.</summary>
public sealed class CompanionViewersFile
{
    public int Version { get; set; } = 1;
    public string InstanceId { get; set; } = "";
    public string DisplayName { get; set; } = "ME221";
    public List<ApprovedDeviceEntry> Devices { get; set; } = [];
}
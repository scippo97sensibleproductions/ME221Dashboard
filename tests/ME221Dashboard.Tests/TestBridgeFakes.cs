using ME221.Comms;
using ME221.Comms.Messages;
using ME221.Data.Models;
using ME221Dashboard.Services;

namespace ME221Dashboard.Tests;

/// <summary>
/// Shared bridge fakes for the MAUI-referencing test host. The service
/// constructor only reads/raises these members; the fakes stay minimal.
/// </summary>
public sealed class FakeCalibrationService : ICalibrationService
{
    public DashboardConfig Config { get; set; } = new();

    public Task<CalibrationResult> GetPersistedCalibrationAsync() =>
        Task.FromResult(new CalibrationResult(CalibrationResultType.NotFound, null));

    public Task<DashboardConfig?> GetPersistedDashboardConfigAsync() => Task.FromResult<DashboardConfig?>(Config);

    public Task SaveDashboardConfigAsync(DashboardConfig config)
    {
        Config = config;
        return Task.CompletedTask;
    }

    public Task<CalibrationData> LoadAndParseAsync(Stream mefwStream) => throw new NotImplementedException();
    public Task SaveCalibrationAsync(CalibrationData calibration) => Task.CompletedTask;
    public Task<(string Product, string Model, string Version)> GetEcuInfoAsync(ProtocolService protocol) =>
        Task.FromResult(("", "", ""));
    public bool MatchesEcu(CalibrationData calibration, string product, string model, string version) => false;
}

public sealed class FakeConnectionService : IEcuConnectionService
{
    public ConnectionState State => ConnectionState.Disconnected;
    public ProtocolInfo? ProtocolInfo => null;
    public string? LastError => null;
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public Task<bool> ConnectAsync(ConnectionTarget target, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task DisconnectAsync() => Task.CompletedTask;
    public ProtocolService GetProtocolService() => throw new NotImplementedException();
    public Task<ProtocolInfo> EnableReportingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProtocolInfo(ReportingVersion.V1, "", "", "", Array.Empty<EntityMapEntry>()));
    public Task DisableReportingAsync() => Task.CompletedTask;
    public void PauseHeartbeat() { }
    public void ResumeHeartbeat() { }
}

public sealed class FakeLiveDataService : ILiveDataService
{
    public bool IsRunning => false;
    public float? this[int entityId] => null;
    public IReadOnlySet<int> EntityIds => new HashSet<int>();
    public event EventHandler<EntitiesUpdatedEventArgs>? EntitiesUpdated;
    public Task StartAsync(ProtocolService protocolService, int reportingVersion, Dictionary<ushort, (ReportingType Type, int Size)> entityMap) => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public void UpdateExternalValue(int entityId, float value) { }
}

/// <summary>Convenience: a fully-wired bridge over in-memory fakes.</summary>
public static class TestBridge
{
    public static HybridBridgeService Create(FakeCalibrationService cal)
    {
        return new HybridBridgeService(
            new FakeConnectionService(),
            new FakeLiveDataService(),
            cal,
            new DashboardPackageService(),
            new LogCapture(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HybridBridgeService>.Instance);
    }
}

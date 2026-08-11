using FluentAssertions;
using ME221.Comms;
using ME221.Comms.Messages;
using ME221.Data.Models;
using ME221Dashboard.Services;
using ME221Dashboard.Services.Mirror;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ME221Dashboard.Tests.Mirror;

public sealed class MirrorSnapshotSerializerTests
{
    private sealed class FakeCalibration : ICalibrationService
    {
        public CalibrationData? Calibration { get; set; }
        public DashboardConfig? Config { get; set; }
        public Task<CalibrationResult> GetPersistedCalibrationAsync() =>
            Task.FromResult(Calibration is null
                ? new CalibrationResult(CalibrationResultType.NotFound, null)
                : new CalibrationResult(CalibrationResultType.Found, Calibration));
        public Task<DashboardConfig?> GetPersistedDashboardConfigAsync() => Task.FromResult(Config);
        public Task SaveDashboardConfigAsync(DashboardConfig config) => Task.CompletedTask;
        public Task<CalibrationData> LoadAndParseAsync(Stream mefwStream) => throw new NotImplementedException();
        public Task SaveCalibrationAsync(CalibrationData calibration) => Task.CompletedTask;
        public Task<(string Product, string Model, string Version)> GetEcuInfoAsync(ProtocolService protocol) =>
            Task.FromResult(("", "", ""));
        public bool MatchesEcu(CalibrationData calibration, string product, string model, string version) => false;
    }

    private sealed class FakeConnection : IEcuConnectionService
    {
        public ConnectionState State { get; set; }
        public ProtocolInfo? ProtocolInfo { get; set; }
        public string? LastError => null;
        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
        public Task<bool> ConnectAsync(ConnectionTarget target, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task DisconnectAsync() => Task.CompletedTask;
        public ProtocolService GetProtocolService() => throw new NotImplementedException();
        public Task<ProtocolInfo> EnableReportingAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DisableReportingAsync() => Task.CompletedTask;
        public void PauseHeartbeat() { }
        public void ResumeHeartbeat() { }
    }

    private static MirrorSnapshotSerializer Create(CalibrationData? cal, DashboardConfig? config, ConnectionState state, ProtocolInfo? info) =>
        new(new FakeCalibration { Calibration = cal, Config = config },
            new FakeConnection { State = state, ProtocolInfo = info },
            appVersion: () => "1.2.3",
            logger: NullLogger<MirrorSnapshotSerializer>.Instance);

    [Fact]
    public async Task SnapshotCarriesCalibrationTablesAndDrivers()
    {
        var cal = new CalibrationData
        {
            Tables = [new TableDefinition { Id = 1, Name = "T1", Output = [1, 2, 3] }],
            Drivers = [new DriverDefinition { Id = 2, Name = "D1" }],
        };

        var snapshot = await Create(cal, null, ConnectionState.Connected, null).BuildAsync(42);

        snapshot.Seq.Should().Be(42);
        snapshot.EcuConnected.Should().BeTrue();
        snapshot.Calibration.Tables.Should().ContainSingle(t => t.Id == 1);
        snapshot.Calibration.Drivers.Should().ContainSingle(d => d.Id == 2);
    }

    [Fact]
    public async Task SnapshotCarriesDashboardConfigAndActiveDashboard()
    {
        var config = new DashboardConfig { ActiveDashboard = "Race" };
        config.Dashboards.Remove("default");
        config.Dashboards["Race"] = new DashboardDefinition { HeaderVisible = false };

        var snapshot = await Create(null, config, ConnectionState.Disconnected, null).BuildAsync(1);

        snapshot.DashboardConfig.ActiveDashboard.Should().Be("Race");
        snapshot.DashboardConfig.Dashboards.Should().ContainKey("Race");
    }

    [Fact]
    public async Task SnapshotEmbedsReferencedImageBytes()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "me221-img-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            var bgPath = Path.Combine(tmp, "bg.png");
            await File.WriteAllBytesAsync(bgPath, [0x89, 0x50, 0x4E, 0x47]);
            var config = new DashboardConfig { ActiveDashboard = "default" };
            config.Dashboards["default"].BackgroundImagePath = bgPath;

            var snapshot = await Create(null, config, ConnectionState.Disconnected, null).BuildAsync(1);

            snapshot.Images.Should().ContainKey(bgPath);
            snapshot.Images[bgPath].Should().Be(Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47]));
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task MissingImageFileIsSkipped()
    {
        var config = new DashboardConfig { ActiveDashboard = "default" };
        config.Dashboards["default"].BackgroundImagePath = @"C:\does-not-exist-me221\bg.png";

        var snapshot = await Create(null, config, ConnectionState.Disconnected, null).BuildAsync(1);

        snapshot.Images.Should().BeEmpty();
    }

    [Fact]
    public async Task EcuInfoComesFromProtocolInfo()
    {
        var info = new ProtocolInfo(ReportingVersion.V1, "ME221", "MX5", "2.0", []);
        var snapshot = await Create(null, null, ConnectionState.Connected, info).BuildAsync(1);

        snapshot.EcuInfo.Should().NotBeNull();
        snapshot.EcuInfo!.ProductName.Should().Be("ME221");
        snapshot.EcuInfo.ModelName.Should().Be("MX5");
        snapshot.EcuInfo.Version.Should().Be("2.0");
    }

    [Fact]
    public async Task SnapshotRoundTripsThroughV2JsonContext()
    {
        var cal = new CalibrationData
        {
            Tables = [new TableDefinition { Id = 1, Name = "T1", Input0 = [100, 200], Output = [1.5f, 2.5f] }],
        };
        var config = new DashboardConfig { ActiveDashboard = "default" };
        config.Dashboards["default"].Gauges.Add(new GaugeConfigEntry { Id = 3, ShapeCategory = 0 });
        var snapshot = await Create(cal, config, ConnectionState.Connected, null).BuildAsync(7);

        var json = MirrorProtocol.Serialize(snapshot);
        var parsed = MirrorProtocol.DeserializeSnapshot(json);

        parsed.Should().NotBeNull();
        parsed!.Seq.Should().Be(7);
        parsed.Calibration.Tables[0].Output.Should().Equal(1.5f, 2.5f);
        parsed.DashboardConfig.Dashboards["default"].Gauges.Should().ContainSingle(g => g.Id == 3);
    }
}
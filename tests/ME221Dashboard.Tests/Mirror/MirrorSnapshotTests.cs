using System.Buffers.Binary;
using System.Threading.Channels;
using FluentAssertions;
using ME221.Comms;
using ME221.Comms.Channels;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using ME221.Data.Infrastructure;
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
        public ProtocolService? Protocol { get; set; }
        public string? LastError => null;
        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
        public Task<bool> ConnectAsync(ConnectionTarget target, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task DisconnectAsync() => Task.CompletedTask;
        public ProtocolService GetProtocolService() =>
            Protocol ?? throw new InvalidOperationException("FakeConnection has no protocol");
        public Task<ProtocolInfo> EnableReportingAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DisableReportingAsync() => Task.CompletedTask;
        public void PauseHeartbeat() { }
        public void ResumeHeartbeat() { }
    }

    /// <summary>
    /// Channel that answers GetTable/GetDriver requests from canned payloads while
    /// the request arrives (the snapshot serializer reads each entity sequentially).
    /// Unknown entities get a Failure status so the capture path skips them fast
    /// instead of waiting out the per-request timeout.
    /// </summary>
    private sealed class RespondingChannel(
        Dictionary<ushort, byte[]>? tablePayloads = null,
        Dictionary<ushort, byte[]>? driverPayloads = null) : IChannel
    {
        private readonly Channel<MessageFrame> _incoming = Channel.CreateUnbounded<MessageFrame>();

        public bool IsOpen => true;
        public DeviceStatus Status => DeviceStatus.Opened;
        public IAsyncEnumerable<MessageFrame> IncomingFrames => _incoming.Reader.ReadAllAsync();

        public Task SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
        {
            if (FrameParser.TryParse(frame.Span, out var message, out _) && message is Request request)
            {
                switch (request)
                {
                    case GetTableRequest tr when tablePayloads?.TryGetValue(tr.TableId, out var table) == true:
                        _incoming.Writer.TryWrite(new GetTableResponse(table));
                        break;
                    case GetTableRequest:
                        _incoming.Writer.TryWrite(new GetTableResponse([(byte)MessageStatus.Failure]));
                        break;
                    case GetDriverRequest dr when driverPayloads?.TryGetValue(dr.DriverId, out var driver) == true:
                        _incoming.Writer.TryWrite(new GetDriverResponse(driver));
                        break;
                    case GetDriverRequest:
                        _incoming.Writer.TryWrite(new GetDriverResponse([(byte)MessageStatus.Failure]));
                        break;
                }
            }
            return Task.CompletedTask;
        }

        public Task OpenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            _incoming.Writer.TryComplete();
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() => new(CloseAsync());
    }

    private static MirrorSnapshotSerializer Create(CalibrationData? cal, DashboardConfig? config, ConnectionState state, ProtocolInfo? info, ProtocolService? protocol = null) =>
        new(new FakeCalibration { Calibration = cal, Config = config },
            new FakeConnection { State = state, ProtocolInfo = info, Protocol = protocol },
            appVersion: () => "1.2.3",
            logger: NullLogger<MirrorSnapshotSerializer>.Instance);

    /// <summary>Wire payload for GetTableResponse: [status][tableId:2][dataSize:2][serialized].</summary>
    private static byte[] TableResponsePayload(TableDefinition def, bool enabled, float[] input0, float[] input1, float[] output)
    {
        var serialized = TableSerializer.Serialize(def, enabled, input0, input1, output);
        var payload = new byte[5 + serialized.Length];
        payload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), def.Id);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(3), (ushort)serialized.Length);
        serialized.CopyTo(payload.AsSpan(5));
        return payload;
    }

    /// <summary>Wire payload for GetDriverResponse: [status][driverId:2][dataSize:2][serialized].</summary>
    private static byte[] DriverResponsePayload(ushort id, float[] configs, IReadOnlyList<ushort> outputs, IReadOnlyList<ushort> inputs)
    {
        var serialized = DriverSerializer.Serialize(configs, [.. outputs], [.. inputs]);
        var payload = new byte[5 + serialized.Length];
        payload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), id);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(3), (ushort)serialized.Length);
        serialized.CopyTo(payload.AsSpan(5));
        return payload;
    }

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

    // ── KTD-4: the snapshot carries current ECU table and driver VALUES ───────

    private static CalibrationData CaptureCalibration()
    {
        var def = new TableDefinition
        {
            Id = 10,
            Name = "Ignition",
            Category = "Timing",
            ViewInTree = true,
            Enabled = true,
            TableType = "T1x16",
            Cols = 4,
            Rows = 1,
            Input0LinkId = 2,
            OutputLinkId = 10,
            Input0Name = "RPM",
            OutputName = "deg",
            IncrementValue = 1,
        };
        return new CalibrationData
        {
            DataLinks =
            [
                new DataLinkDefinition { Id = 2, Name = "RPM", MeasureUnit = "rpm", MeasurementUnitTypes = MeasurementUnitType.Rpm },
                new DataLinkDefinition { Id = 10, Name = "EGT", MeasureUnit = "\u00b0F", MeasurementUnitTypes = MeasurementUnitType.Fahrenheit },
            ],
            Tables = [def],
        };
    }

    [Fact]
    public async Task SnapshotCapturesEcuTableValuesConvertedToDisplayUnits()
    {
        // ECU wire: output carries raw Fahrenheit counts — the snapshot must carry
        // the display values B's editor expects (identical conversion to the ECU read path).
        var payloads = new Dictionary<ushort, byte[]>
        {
            [10] = TableResponsePayload(CaptureCalibration().Tables[0], enabled: true, [1000, 2000, 3000, 4000], [], [10, 12, 15, 18]),
        };
        await using var protocol = new ProtocolService(new RespondingChannel(payloads), NullLogger<ProtocolService>.Instance);
        await protocol.OpenAsync();

        var snapshot = await Create(CaptureCalibration(), null, ConnectionState.Connected, null, protocol).BuildAsync(3);

        var table = snapshot.Calibration.Tables.Should().ContainSingle(t => t.Id == 10).Subject;
        table.Input0.Should().Equal(1000, 2000, 3000, 4000);
        table.Output.Should().HaveCount(4);
        table.Output![0].Should().BeApproximately(50f, 0.01f);   // 10 × 1.8 + 32
        table.Output![1].Should().BeApproximately(53.6f, 0.01f); // 12 × 1.8 + 32
        table.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task FailedTableReadLeavesDefinitionsOnlyAndDoesNotFailBuild()
    {
        // No canned payload for table 10 → the channel returns Failure, the capture
        // skips it, and the snapshot still builds (B renders that table empty).
        await using var protocol = new ProtocolService(new RespondingChannel(), NullLogger<ProtocolService>.Instance);
        await protocol.OpenAsync();

        var snapshot = await Create(CaptureCalibration(), null, ConnectionState.Connected, null, protocol).BuildAsync(4);

        var table = snapshot.Calibration.Tables.Should().ContainSingle(t => t.Id == 10).Subject;
        table.Input0.Should().BeNull();
        table.Output.Should().BeNull();
    }

    [Fact]
    public async Task SnapshotCapturesDriverConfigValues()
    {
        var drivers = new CalibrationData
        {
            Drivers =
            [
                new DriverDefinition
                {
                    Id = 20,
                    Name = "Test Driver",
                    Category = "Outputs",
                    ViewInTree = true,
                    NumberOfConfigs = 2,
                    NumberOfOutputs = 1,
                    NumberOfInputs = 1,
                    Configs =
                    [
                        new DriverParamDefinition { Name = "P1", ParamType = "float", Value = 0.5f },
                        new DriverParamDefinition { Name = "P2", ParamType = "float", Value = 0f },
                    ],
                },
            ],
        };
        var payloads = new Dictionary<ushort, byte[]>
        {
            [20] = DriverResponsePayload(20, [42, 7.5f], [1], [9]),
        };
        await using var protocol = new ProtocolService(new RespondingChannel(driverPayloads: payloads), NullLogger<ProtocolService>.Instance);
        await protocol.OpenAsync();

        var snapshot = await Create(drivers, null, ConnectionState.Connected, null, protocol).BuildAsync(5);

        var driver = snapshot.Calibration.Drivers.Should().ContainSingle(d => d.Id == 20).Subject;
        driver.Configs[0].Value.Should().Be(42f);
        driver.Configs[1].Value.Should().Be(7.5f);
        driver.OutputLinkIds.Should().Equal(1);
        driver.InputLinkIds.Should().Equal(9);
    }

    [Fact]
    public async Task SnapshotNeverSharesLiveCalibrationReferences()
    {
        // The snapshot must carry fresh copies — mutating the snapshot's table list
        // must not reach the host's live calibration object.
        var cal = CaptureCalibration();
        await using var protocol = new ProtocolService(new RespondingChannel(), NullLogger<ProtocolService>.Instance);
        await protocol.OpenAsync();

        var snapshot = await Create(cal, null, ConnectionState.Connected, null, protocol).BuildAsync(6);

        snapshot.Calibration.Tables.Clear();
        cal.Tables.Should().ContainSingle(t => t.Id == 10);
    }
}
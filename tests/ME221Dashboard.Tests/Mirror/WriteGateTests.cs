using System.Text.Json;
using FluentAssertions;
using ME221.Data.Models;
using ME221Dashboard.Services;
using ME221Dashboard.Services.Mirror;
using Xunit;

namespace ME221Dashboard.Tests.Mirror;

/// <summary>U7: companion mode gates every classified write and serves reads from the snapshot store.</summary>
public sealed class WriteGateTests
{
    private readonly FakeCalibrationService _calibration = new();
    private readonly HybridBridgeService _bridge;
    private readonly MirrorSnapshotStore _store = new();

    public WriteGateTests()
    {
        _calibration.Config = new Services.DashboardConfig();
        _bridge = TestBridge.Create(_calibration);
        _bridge.EnterMirrorMode(_store);
    }

    private static async Task<(bool Success, string? Error)> ParseAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return (root.TryGetProperty("success", out var s) && s.GetBoolean(),
                root.TryGetProperty("error", out var e) ? e.GetString() : null);
    }

    // ── Every gated write returns { success: false, error } in companion mode ──

    [Theory]
    [InlineData("WriteTableData", "{\"tableId\":1}")]
    [InlineData("WriteTableDataBatch", "[]")]
    [InlineData("SetDriverConfig", "{\"driverId\":1}")]
    [InlineData("StoreDriver", "{\"driverId\":1}")]
    [InlineData("SetVehicleConfig", "{}")]
    [InlineData("SaveWarningSettings", "[]")]
    [InlineData("SaveWarningDelay", "{\"delayMs\":500}")]
    [InlineData("CreateDashboard", "NewDash")]
    [InlineData("DeleteDashboard", "default")]
    [InlineData("RenameDashboard", "a|b")]
    [InlineData("SetActiveDashboard", "default")]
    [InlineData("SaveSensorSelection", "{\"dashboardName\":\"default\",\"selectedIds\":[]}")]
    [InlineData("SaveDashboardTables", "{\"dashboardName\":\"default\",\"tables\":[]}")]
    [InlineData("DeleteDashboardGauges", "default|[]")]
    [InlineData("SaveDashboardLayout", "{\"dashboardName\":\"default\",\"gauges\":[]}")]
    [InlineData("SaveDashboardViewState", "{\"dashboardName\":\"default\"}")]
    [InlineData("SaveWarningDatalink", "{}")]
    [InlineData("SaveFile", "{}")]
    [InlineData("ImportYamlTable", null)]
    [InlineData("ImportCsvTable", null)]
    [InlineData("ImportDashboard", null)]
    [InlineData("ImportMecal", null)]
    [InlineData("ApplyMecalImport", "e30=|[]|[]")]
    public async Task GatedWritesReturnErrorInCompanionMode(string method, string? args)
    {
        var result = await InvokeAsync(method, args);
        var (success, error) = await ParseAsync(result);
        success.Should().BeFalse($"write '{method}' must be gated in companion mode");
        error.Should().NotBeNullOrWhiteSpace();
    }

    private Task<string> InvokeAsync(string method, string? args)
    {
        return method switch
        {
            "WriteTableData" => _bridge.WriteTableData(args!),
            "WriteTableDataBatch" => _bridge.WriteTableDataBatch(args!),
            "SetDriverConfig" => _bridge.SetDriverConfig(args!),
            "StoreDriver" => _bridge.StoreDriver(args!),
            "SetVehicleConfig" => _bridge.SetVehicleConfig(args!),
            "SaveWarningSettings" => _bridge.SaveWarningSettings(args!),
            "SaveWarningDelay" => _bridge.SaveWarningDelay(args!),
            "CreateDashboard" => _bridge.CreateDashboard(args!),
            "DeleteDashboard" => _bridge.DeleteDashboard(args!),
            "RenameDashboard" => _bridge.RenameDashboard(args!.Split('|')[0], args.Split('|')[1]),
            "SetActiveDashboard" => _bridge.SetActiveDashboard(args!),
            "SaveSensorSelection" => _bridge.SaveSensorSelection(args!),
            "SaveDashboardTables" => _bridge.SaveDashboardTables(args!),
            "DeleteDashboardGauges" => _bridge.DeleteDashboardGauges(args!.Split('|')[0], args.Split('|')[1]),
            "SaveDashboardLayout" => _bridge.SaveDashboardLayout(args!),
            "SaveDashboardViewState" => _bridge.SaveDashboardViewState(args!),
            "SaveWarningDatalink" => _bridge.SaveWarningDatalink(args!),
            "SaveFile" => _bridge.SaveFile(args!),
            "ImportYamlTable" => _bridge.ImportYamlTable(),
            "ImportCsvTable" => _bridge.ImportCsvTable(),
            "ImportDashboard" => _bridge.ImportDashboard(),
            "ImportMecal" => _bridge.ImportMecal(),
            "ApplyMecalImport" => _bridge.ApplyMecalImport(args!.Split('|')[0], args.Split('|')[1], args.Split('|')[2]),
            _ => throw new ArgumentException($"Unhandled method {method}"),
        };
    }

    [Fact]
    public async Task OdometerSettersAreGated()
    {
        await _bridge.ResetOdometer();
        await _bridge.SetOdometerValue(123);
        await _bridge.SetOdometerUnit(true);
        await _bridge.SetOdometerSpeedSource(0);
        await _bridge.SetOdometerSpeedConfig("{}");
        // These are void-ish writes; the gate must keep B's odometer untouched.
        _bridge.GetOdometer().Should().Contain("\"value\":0");
    }

    [Fact]
    public async Task DebugSpeedIsGated()
    {
        var result = _bridge.SetDebugSpeed(50);
        var (success, error) = await ParseAsync(result);
        success.Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SameWritesWorkOutsideCompanionMode()
    {
        _bridge.ExitMirrorMode();
        // Outside companion mode the writes are NOT gated; file-picker imports fail
        // with a real error rather than the gate message — proving the gate is off.
        var result = await _bridge.ImportYamlTable();
        var (success, error) = await ParseAsync(result);
        error.Should().NotBe("Read-only in companion mode");
        _ = success;
    }

    // ── B-local exceptions stay ungated ──────────────────────────────────────

    [Fact]
    public async Task BLocalExceptionsAreNotGated()
    {
        _bridge.ClearLogs().Should().Contain("\"success\":true");
        var presets = await _bridge.GetMonitoringPresets();
        presets.Should().NotContain("Read-only in companion mode");
        var history = await _bridge.SaveWarningHistory("[]");
        history.Should().NotContain("Read-only in companion mode");
        var ledger = await _bridge.SaveBatchLedger("[]");
        ledger.Should().NotContain("Read-only in companion mode");
        var notices = await _bridge.SaveWarningQueuedNotices("[]");
        notices.Should().NotContain("Read-only in companion mode");
    }

    // ── KTD-5: mirroring must never overwrite B's own dashboard config ────────

    [Fact]
    public async Task MirrorSnapshotNeverLeaksIntoBLocalConfigViaWriters()
    {
        // B-local config carries a distinctive local dashboard; A's snapshot carries "default".
        _calibration.Config.Dashboards["Local-B"] = new DashboardDefinition { Gauges = [new GaugeConfigEntry { Id = 99 }] };
        _store.Apply(MirrorTestData.SampleSnapshot());

        // B-local exception writes must target B's own store, never A's snapshot data.
        await _bridge.SaveWarningHistory("[]");
        await _bridge.SaveBatchLedger("[]");
        await _bridge.SaveWarningQueuedNotices("{}");
        await _bridge.CreateMonitoringPreset("{\"name\":\"P1\",\"datalinkIds\":[1]}");
        await _bridge.SaveLastConnection("{\"type\":\"tcp\",\"host\":\"10.0.0.9\",\"port\":22100}");
        await _bridge.SaveFavoriteTables("[1,2]");

        // B's own dashboard is untouched and the B-local data landed on B's config…
        _calibration.Config.Dashboards["Local-B"].Gauges.Should().ContainSingle(g => g.Id == 99);
        _calibration.Config.MonitoringPresets.Should().ContainSingle(p => p.Name == "P1");
        _calibration.Config.Preferences.FavoriteTableIds.Should().Equal([1, 2]);
        // …and "default" may only exist as a B-local empty stub (created by SaveWarningHistory),
        // never carrying A's snapshot content (gauge id 3 / table id 10).
        if (_calibration.Config.Dashboards.TryGetValue("default", out var stub))
        {
            stub.Gauges.Should().BeEmpty();
            stub.Tables.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task MirrorReadsNeverWriteToBLocalConfig()
    {
        // GetVehicleConfig runs EnsureVehicleMigrationAsync — in companion mode the
        // migration must not mutate or persist A's snapshot onto B's disk.
        _calibration.Config.Dashboards["Local-B"] = new DashboardDefinition();
        _store.Apply(MirrorTestData.SampleSnapshot());

        var vehicle = await _bridge.GetVehicleConfig();
        vehicle.Should().NotContain("error");

        // B's own dashboards survive; its fresh "default" template never gains A's
        // snapshot content (gauge id 3 / table id 10), and no migration wrote
        // A's config (migrated or not) onto B's disk.
        _calibration.Config.Dashboards.Keys.Should().Contain("Local-B");
        _calibration.Config.Dashboards["default"].Gauges.Should().BeEmpty();
        _calibration.Config.Dashboards["default"].Tables.Should().BeEmpty();
        _calibration.Config.VehicleSeedTemplate.Should().BeNull();
    }
}

/// <summary>U7: mirror-served reads answer from the snapshot store, never B-local data.</summary>
public sealed class RemoteReadTests
{
    private readonly FakeCalibrationService _calibration = new();
    private readonly HybridBridgeService _bridge;
    private readonly MirrorSnapshotStore _store = new();

    public RemoteReadTests()
    {
        _calibration.Config = new Services.DashboardConfig(); // B-local: a different dashboard
        _bridge = TestBridge.Create(_calibration);
    }

    private static (bool Success, string? Error) ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return (root.TryGetProperty("success", out var s) && s.GetBoolean(),
                root.TryGetProperty("error", out var e) ? e.GetString() : null);
    }

    [Fact]
    public async Task TableReadsAnswerFromSnapshotNeverBLocal()
    {
        _store.Apply(MirrorTestData.SampleSnapshot());
        _bridge.EnterMirrorMode(_store);

        var tables = await _bridge.GetTableDefinitions();
        tables.Should().Contain("Ignition");
        tables.Should().NotContain("B-Local");

        var data = await _bridge.ReadTableData(10);
        data.Should().Contain("10");
        data.Should().Contain("18");
    }

    [Fact]
    public async Task DriverAndDatalinkReadsAnswerFromSnapshot()
    {
        _store.Apply(MirrorTestData.SampleSnapshot());
        _bridge.EnterMirrorMode(_store);

        var drivers = await _bridge.GetDriverDefinitions();
        drivers.Should().Contain("Test Driver");

        var dataLinks = await _bridge.GetDataLinks();
        dataLinks.Should().Contain("RPM");
    }

    [Fact]
    public async Task DashboardReadsAnswerFromSnapshot()
    {
        _store.Apply(MirrorTestData.SampleSnapshot());
        _bridge.EnterMirrorMode(_store);

        var names = await _bridge.GetDashboardNames();
        names.Should().Contain("default");

        var config = await _bridge.GetDashboardConfig("default");
        config.Should().Contain("\"found\":true");

        var vehicle = await _bridge.GetVehicleConfig();
        vehicle.Should().NotContain("error");
    }

    [Fact]
    public async Task WarningReadsAnswerFromSnapshot()
    {
        var snapshot = MirrorTestData.SampleSnapshot();
        snapshot.DashboardConfig.WarningSettings.Add(new ME221.Data.Models.DataLinkWarningSetting
        {
            DataId = 1,
            Name = "RPM",
            Status = ME221.Data.Models.WarningSettingStatus.Typical,
        });
        _store.Apply(snapshot);
        _bridge.EnterMirrorMode(_store);

        var settings = await _bridge.GetWarningSettings();
        settings.Should().NotBeEmpty();

        var notices = await _bridge.GetWarningQueuedNotices();
        notices.Should().NotBeNull();

        var history = await _bridge.GetWarningHistory();
        history.Should().NotBeNull();
    }

    [Fact]
    public async Task OdometerReadsAnswerFromRelayedValues()
    {
        _store.Apply(MirrorTestData.SampleSnapshot());
        _store.UpdateOdometer(12345.6, useKilometers: false);
        _bridge.EnterMirrorMode(_store);

        var json = _bridge.GetOdometer();
        json.Should().Contain("\"value\":12345.6");
        json.Should().Contain("\"useKilometers\":false");
    }

    [Fact]
    public async Task SnapshotImageResolvesWithoutTouchingFilesystem()
    {
        var snapshot = MirrorTestData.SampleSnapshot();
        snapshot.Images = new Dictionary<string, string> { ["C:\\me221\\images\\bg.png"] = "aGVsbG8=" };
        _store.Apply(snapshot);
        _bridge.EnterMirrorMode(_store);

        var json = await _bridge.GetImageBase64("C:\\me221\\images\\bg.png");
        json.Should().Contain("data:image/png;base64,aGVsbG8=");

        // A path absent from the snapshot does not fall back to B's disk.
        var missing = await _bridge.GetImageBase64("C:\\definitely-missing-me221.png");
        missing.Should().Contain("Image not in snapshot");
    }

    [Fact]
    public async Task ReadsBeforeSnapshotApplyReturnNotReadyError()
    {
        _bridge.EnterMirrorMode(_store); // store is empty

        // Table/driver data reads (the mirror-branched surface) return the not-ready
        // error; list-style reads return an empty success shape.
        var batch = await _bridge.ReadTableDataBatch("[1]");
        ParseJson(batch).Error.Should().Be("Snapshot not applied yet");

        var driver = await _bridge.ReadDriverData("{\"driverId\":1}");
        ParseJson(driver).Error.Should().Be("Snapshot not applied yet");

        var tables = await _bridge.GetTableDefinitions();
        tables.Should().Contain("\"tables\":[]", "list reads degrade to an empty shape");
    }

    [Fact]
    public async Task MirrorModeImageReadNeverFallsBackToBLocalDisk()
    {
        // R9: an image absent from the snapshot must error, even if B has a file at
        // that path — the mirror read must never touch B's filesystem.
        var tmp = Path.Combine(Path.GetTempPath(), "me221-imgleak-" + Guid.NewGuid().ToString("N") + ".png");
        await File.WriteAllBytesAsync(tmp, [1, 2, 3]);

        try
        {
            _store.Apply(MirrorTestData.SampleSnapshot()); // no Images entries
            _bridge.EnterMirrorMode(_store);

            var json = await _bridge.GetImageBase64(tmp);
            json.Should().Contain("\"success\":false");
            json.Should().Contain("Image not in snapshot");
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task DefinitionsOnlySnapshotRendersTablesEmptyNotFailing()
    {
        // Real calibrations store structure only (KTD-4) — table value arrays are
        // null until the ECU is read. Such a snapshot must answer the table load
        // with empty arrays (the plan's "empty tables render as empty, not missing"),
        // never a failure that trips the editor's load guard.
        var snapshot = MirrorTestData.SampleSnapshot();
        snapshot.Calibration.Tables.Clear();
        snapshot.Calibration.Tables.Add(new TableDefinition
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
        });
        _store.Apply(snapshot);
        _bridge.EnterMirrorMode(_store);

        var data = await _bridge.ReadTableData(10);
        data.Should().Contain("\"success\":true");
        data.Should().Contain("\"input0\":[]");
        data.Should().Contain("\"output\":[]");
    }

    [Fact]
    public async Task ExitMirrorModeRestoresLocalData()
    {
        _store.Apply(MirrorTestData.SampleSnapshot());
        _bridge.EnterMirrorMode(_store);
        _bridge.ExitMirrorMode();

        var tables = await _bridge.GetTableDefinitions();
        tables.Should().NotContain("Ignition", "B-local calibration is restored after exit");
    }
}
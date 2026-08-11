using System.Text.Json;
using FluentAssertions;
using ME221.Data.Models;
using ME221Dashboard.Services;
using Xunit;

namespace ME221Dashboard.Tests;

public sealed class DerivedEntityBridgeTests
{
    [Fact]
    public async Task GetAvailableSensors_exposes_both_shift_entities_with_metadata()
    {
        var cal = new FakeCalibrationService();
        var bridge = TestBridge.Create(cal);

        var json = await bridge.GetAvailableSensors("default");
        using var doc = JsonDocument.Parse(json);
        var sensors = doc.RootElement.GetProperty("sensors").EnumerateArray().ToList();

        var countdown = sensors.FirstOrDefault(s => s.GetProperty("id").GetInt32() == -3005);
        countdown.ValueKind.Should().Be(JsonValueKind.Object, "countdown entity -3005 must be listed");
        countdown.GetProperty("name").GetString().Should().Be("RPM to Shift");
        countdown.GetProperty("category").GetString().Should().Be("Derived");
        countdown.GetProperty("unit").GetString().Should().Be("rpm");
        countdown.GetProperty("minValue").GetDouble().Should().Be(0);
        countdown.GetProperty("maxValue").GetDouble().Should().Be(9000);

        var shiftState = sensors.FirstOrDefault(s => s.GetProperty("id").GetInt32() == -3006);
        shiftState.ValueKind.Should().Be(JsonValueKind.Object, "shift-state entity -3006 must be listed");
        shiftState.GetProperty("name").GetString().Should().Be("Shift State");
        shiftState.GetProperty("unit").GetString().Should().Be("");
        shiftState.GetProperty("minValue").GetDouble().Should().Be(-1);
        shiftState.GetProperty("maxValue").GetDouble().Should().Be(1);
    }

    [Fact]
    public async Task GetAvailableSensors_totalCount_equals_links_plus_derived_six()
    {
        var cal = new FakeCalibrationService();
        var bridge = TestBridge.Create(cal);

        var json = await bridge.GetAvailableSensors("default");
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("totalCount").GetInt32().Should().Be(6,
            "no links and no running GPS → exactly the six always-available derived entities");
        doc.RootElement.GetProperty("sensors").GetArrayLength().Should().Be(6);
    }

    private static DashboardConfig TwoDashboardConfig()
    {
        return new DashboardConfig
        {
            Dashboards = new Dictionary<string, DashboardDefinition>
            {
                ["default"] = new()
                {
                    Vehicle = new VehicleConfig { FinalDriveRatio = 4.1, TireDiameterInches = 23, GearRatios = new[] { 3.6, 2.2 } },
                    ShifterConfig = new ShifterConfig { ShiftPointRpm = 7000, DownshiftFloorRpm = 5000 },
                },
                ["track"] = new()
                {
                    Vehicle = new VehicleConfig { FinalDriveRatio = 4.6, TireDiameterInches = 24, GearRatios = new[] { 3.9, 2.4 } },
                    ShifterConfig = new ShifterConfig { ShiftPointRpm = 6500, DownshiftFloorRpm = 0 },
                },
            },
            ActiveDashboard = "default",
        };
    }

    [Fact]
    public async Task DeleteDashboard_of_active_dashboard_serves_vehicle_of_new_active_and_never_recreates_the_deleted()
    {
        var cal = new FakeCalibrationService { Config = TwoDashboardConfig() };
        var bridge = TestBridge.Create(cal);

        await bridge.SetActiveDashboard("default");

        var deleteJson = await bridge.DeleteDashboard("default");
        using var deleteDoc = JsonDocument.Parse(deleteJson);
        deleteDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        // Vehicle reads must now target the new active dashboard (track, fd 4.6),
        // not the deleted name — a stale name would fall back to the seed.
        var readJson = await bridge.GetVehicleConfig();
        using var readDoc = JsonDocument.Parse(readJson);
        readDoc.RootElement.GetProperty("finalDriveRatio").GetDouble().Should().Be(4.6);
        readDoc.RootElement.GetProperty("shifter").GetProperty("shiftPointRpm").GetDouble().Should().Be(6500);

        // A subsequent save must update the new active dashboard — and must NOT
        // silently re-create the deleted dashboard (zombie regression).
        var savePayload = JsonSerializer.Serialize(new
        {
            finalDriveRatio = 5.0,
            gearRatios = new[] { 3.6, 2.2 },
            tireDiameterInches = 23.0,
            wheelSlipPercent = 3.0,
            enabled = true,
            rpmEntityId = (int?)null,
            vssSpeedEntityId = (int?)null,
            mapEntityId = (int?)null,
            baroEntityId = (int?)null,
            gearEntityId = (int?)null,
        });
        var saveJson = await bridge.SetVehicleConfig(savePayload);
        using var saveDoc = JsonDocument.Parse(saveJson);
        saveDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        cal.Config.Dashboards.Keys.Should().BeEquivalentTo(new[] { "track" });
        cal.Config.Dashboards["track"].Vehicle!.FinalDriveRatio.Should().Be(5.0);
    }

    [Fact]
    public async Task RenameDashboard_of_active_dashboard_makes_vehicle_reads_follow_the_new_name()
    {
        var cal = new FakeCalibrationService { Config = TwoDashboardConfig() };
        var bridge = TestBridge.Create(cal);

        await bridge.SetActiveDashboard("default");

        var renameJson = await bridge.RenameDashboard("default", "street");
        using var renameDoc = JsonDocument.Parse(renameJson);
        renameDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        var readJson = await bridge.GetVehicleConfig();
        using var readDoc = JsonDocument.Parse(readJson);
        readDoc.RootElement.GetProperty("finalDriveRatio").GetDouble().Should().Be(4.1,
            "the renamed (still active) dashboard's own vehicle must be served");
    }

    [Fact]
    public async Task GetDashboardNames_returns_the_persisted_active_dashboard()
    {
        var cal = new FakeCalibrationService
        {
            Config = new DashboardConfig
            {
                ActiveDashboard = "Normal Dash",
                Dashboards = new Dictionary<string, DashboardDefinition>
                {
                    ["Normal Dash"] = new(),
                },
            },
        };
        var bridge = TestBridge.Create(cal);

        var json = await bridge.GetDashboardNames();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("activeDashboard").GetString().Should().Be("Normal Dash");
        doc.RootElement.GetProperty("names").EnumerateArray().Select(n => n.GetString())
            .Should().ContainSingle("Normal Dash");
    }

    [Fact]
    public async Task GetDashboardNames_falls_back_to_the_first_dashboard_when_active_is_stale()
    {
        var cal = new FakeCalibrationService
        {
            Config = new DashboardConfig
            {
                ActiveDashboard = "deleted-dashboard",
                Dashboards = new Dictionary<string, DashboardDefinition>
                {
                    ["street"] = new(),
                    ["track"] = new(),
                },
            },
        };
        var bridge = TestBridge.Create(cal);

        var json = await bridge.GetDashboardNames();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("activeDashboard").GetString().Should().Be("street",
            "a stale ActiveDashboard must not make pages query a nonexistent name — the first available dashboard wins");
    }

    [Fact]
    public async Task GetDashboardNames_returns_default_on_a_fresh_config()
    {
        var cal = new FakeCalibrationService { Config = new DashboardConfig() };
        var bridge = TestBridge.Create(cal);

        var json = await bridge.GetDashboardNames();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("activeDashboard").GetString().Should().Be("default");
        doc.RootElement.GetProperty("names").EnumerateArray().Select(n => n.GetString())
            .Should().ContainSingle("default");
    }

    [Fact]
    public async Task Derived_entity_customization_survives_save_and_read_back()
    {
        var cal = new FakeCalibrationService { Config = TwoDashboardConfig() };
        var bridge = TestBridge.Create(cal);

        // Save a customization for the derived Gear entity (-3001) — e.g. renamed to "True Gear".
        var saveJson = JsonSerializer.Serialize(new
        {
            dashboardName = "default",
            selectedIds = new[] { -3001 },
            customizations = new Dictionary<string, object>
            {
                ["-3001"] = new
                {
                    customName = "True Gear",
                    customUnit = "gear",
                    minRange = -1.0,
                    maxRange = 10.0,
                    minRangeBypass = false,
                    maxRangeBypass = false,
                },
            },
        });
        var saveResult = await bridge.SaveSensorSelection(saveJson);
        using var saveDoc = JsonDocument.Parse(saveResult);
        saveDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();

        // Read back through GetAvailableSensors — the derived block must surface the
        // customization (regression: it was hardcoded null, so a restart showed the
        // default and the NEXT save deleted the customization from the dict).
        var json = await bridge.GetAvailableSensors("default");
        using var doc = JsonDocument.Parse(json);
        var gear = doc.RootElement.GetProperty("sensors").EnumerateArray()
            .FirstOrDefault(s => s.GetProperty("id").GetInt32() == -3001);
        gear.ValueKind.Should().Be(JsonValueKind.Object, "derived entity -3001 must be listed");
        gear.GetProperty("customization").ValueKind.Should().Be(JsonValueKind.Object,
            "a saved derived-entity customization must be read back (not null)");
        gear.GetProperty("customization").GetProperty("customName").GetString().Should().Be("True Gear");
        gear.GetProperty("customization").GetProperty("maxRange").GetDouble().Should().Be(10.0);
    }
}

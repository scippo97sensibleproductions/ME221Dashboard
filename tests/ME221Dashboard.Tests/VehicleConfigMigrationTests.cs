using System.Text.Json;
using FluentAssertions;
using ME221.Data.Models;
using ME221Dashboard.Services;
using Xunit;

namespace ME221Dashboard.Tests;

public sealed class VehicleConfigMigrationTests
{
    // ─── Pure helper tests ──────────────────────────────────────────────────

    [Fact]
    public void ResolveSeed_prefers_global_over_dashboard_over_defaults()
    {
        var global = NewVc(4.3, [3.6, 2.2, 1.5]);
        var config = new DashboardConfig
        {
            Vehicle = global,
            Dashboards = new Dictionary<string, DashboardDefinition>
            {
                ["a"] = new() { Vehicle = NewVc(5.0, [4.0, 3.0]) },
            },
        };

        var seed = VehicleConfigMigration.ResolveSeed(config);
        seed.FinalDriveRatio.Should().Be(4.3);
        seed.GearRatios.Should().Equal(3.6, 2.2, 1.5);
        seed.Should().NotBeSameAs(global, "the seed is a copy, never the live global reference");
    }

    [Fact]
    public void ResolveSeed_falls_back_to_first_dashboard_with_vehicle()
    {
        var config = new DashboardConfig
        {
            Dashboards = new Dictionary<string, DashboardDefinition>
            {
                ["a"] = new(),
                ["b"] = new() { Vehicle = NewVc(5.0, [4.0, 3.0]) },
            },
        };

        var seed = VehicleConfigMigration.ResolveSeed(config);
        seed.FinalDriveRatio.Should().Be(5.0);
        seed.GearRatios.Should().Equal(4.0, 3.0);
    }

    [Fact]
    public void ResolveSeed_returns_defaults_when_nothing_carries_a_vehicle()
    {
        var seed = VehicleConfigMigration.ResolveSeed(new DashboardConfig());
        seed.FinalDriveRatio.Should().Be(4.3);
    }

    [Theory]
    [InlineData(true, 23, 4.3, true)]          // complete
    [InlineData(false, 23, 4.3, true)]         // zero gear
    [InlineData(true, 0, 4.3, true)]           // zero tire
    [InlineData(true, 23, 0, true)]            // zero final drive
    [InlineData(true, 23, 4.3, false)]         // empty gear list
    public void IsComplete_gate(bool gearsNonZero, double tire, double fd, bool hasGears)
    {
        var vc = new VehicleConfig
        {
            TireDiameterInches = tire,
            FinalDriveRatio = fd,
            GearRatios = hasGears ? (gearsNonZero ? [3.6, 2.2] : [0, 2.2]) : [],
        };
        VehicleConfigMigration.IsComplete(vc).Should().Be(gearsNonZero && tire > 0 && fd > 0 && hasGears);
    }

    [Fact]
    public void ShouldRefreshSeed_requires_complete_and_differing()
    {
        var seed = NewVc(4.3, [3.6, 2.2, 1.5]);
        VehicleConfigMigration.ShouldRefreshSeed(seed, NewVc(4.3, [3.6, 2.2, 1.5])).Should().BeFalse();
        VehicleConfigMigration.ShouldRefreshSeed(seed, NewVc(4.6, [3.6, 2.2, 1.5])).Should().BeTrue();
        VehicleConfigMigration.ShouldRefreshSeed(seed, new VehicleConfig { GearRatios = [0, 1] }).Should().BeFalse();
        VehicleConfigMigration.ShouldRefreshSeed(null, NewVc(4.3, [3.6, 2.2, 1.5])).Should().BeTrue();
    }

    [Fact]
    public void MaterializeSeed_writes_seed_into_active_dashboard_when_null()
    {
        var config = new DashboardConfig
        {
            VehicleSeedTemplate = NewVc(4.6, [3.6, 2.2, 1.5]),
            Dashboards = new Dictionary<string, DashboardDefinition>
            {
                ["active"] = new(),
                ["other"] = new() { Vehicle = NewVc(5.0, [4.0]) },
            },
        };

        VehicleConfigMigration.MaterializeSeed(config, config.VehicleSeedTemplate, "active");
        config.Dashboards["active"].Vehicle.Should().NotBeNull();
        config.Dashboards["active"].Vehicle!.FinalDriveRatio.Should().Be(4.6);
        config.Dashboards["other"].Vehicle!.FinalDriveRatio.Should().Be(5.0, "existing dashboard vehicles are untouched");
    }

    [Fact]
    public void MaterializeSeed_leaves_existing_vehicle_alone()
    {
        var config = new DashboardConfig
        {
            VehicleSeedTemplate = NewVc(4.6, [3.6]),
            Dashboards = new Dictionary<string, DashboardDefinition>
            {
                ["active"] = new() { Vehicle = NewVc(5.0, [4.0]) },
            },
        };

        VehicleConfigMigration.MaterializeSeed(config, config.VehicleSeedTemplate, "active");
        config.Dashboards["active"].Vehicle!.FinalDriveRatio.Should().Be(5.0);
    }

    // ─── Round-trip through V2JsonContext ──────────────────────────────────

    [Fact]
    public void ShifterConfig_and_per_dashboard_vehicle_survive_round_trip()
    {
        var config = new DashboardConfig
        {
            VehicleSeedTemplate = NewVc(4.3, [3.6, 2.2, 1.5, 1.1, 0.85, 0.7]),
            Dashboards = new Dictionary<string, DashboardDefinition>
            {
                ["default"] = new()
                {
                    Vehicle = NewVc(4.6, [3.8, 2.4, 1.6, 1.2, 0.9]),
                    ShifterConfig = new ShifterConfig { ShiftPointRpm = 7000, DownshiftFloorRpm = 5000 },
                },
            },
        };

        var json = JsonSerializer.Serialize(config, V2JsonContext.Default.DashboardConfig);
        var restored = JsonSerializer.Deserialize(json, V2JsonContext.Default.DashboardConfig);

        restored!.Dashboards["default"].Vehicle!.FinalDriveRatio.Should().Be(4.6);
        restored.Dashboards["default"].ShifterConfig.Should().NotBeNull();
        restored.Dashboards["default"].ShifterConfig!.ShiftPointRpm.Should().Be(7000);
        restored.Dashboards["default"].ShifterConfig.DownshiftFloorRpm.Should().Be(5000);
        restored.VehicleSeedTemplate!.TireDiameterInches.Should().Be(23);
    }

    // ─── Bridge interaction tests (fakes for service dependencies) ─────────

    private static DashboardConfig NewConfig(
        Dictionary<string, DashboardDefinition>? dashboards = null,
        VehicleConfig? global = null,
        VehicleConfig? seed = null)
    {
#pragma warning disable CS0618 // Intentional: seeding the legacy global slot in tests
        return new DashboardConfig
        {
            Dashboards = dashboards ?? new Dictionary<string, DashboardDefinition> { ["default"] = new() },
            Vehicle = global,
            VehicleSeedTemplate = seed,
        };
#pragma warning restore CS0618
    }

    [Fact]
    public async Task Migration_copies_global_into_seed_once_and_stops_writing_global()
    {
        var cal = new FakeCalibrationService();
        cal.Config = NewConfig(global: NewVc(4.3, [3.6, 2.2, 1.5]));
        var bridge = NewBridge(cal);

        await bridge.GetVehicleConfig();

        cal.Config.VehicleSeedTemplate.Should().NotBeNull();
        cal.Config.VehicleSeedTemplate!.FinalDriveRatio.Should().Be(4.3);
        cal.Config.Vehicle.Should().NotBeNull("the legacy slot is kept for deserialization");

        await bridge.SetVehicleConfig(VcPayload(4.9, [4.0, 2.8, 1.9]));

        cal.Config.VehicleSeedTemplate!.FinalDriveRatio.Should().Be(4.9, "seed refreshed from a complete per-dashboard save");
        cal.Config.Vehicle!.FinalDriveRatio.Should().Be(4.3, "the global slot is never written after migration");
    }

    [Fact]
    public async Task Legacy_fallback_seed_picks_up_first_dashboard_vehicle()
    {
        var cal = new FakeCalibrationService();
        cal.Config = NewConfig(new Dictionary<string, DashboardDefinition>
        {
            ["default"] = new() { Vehicle = NewVc(5.0, [4.0, 3.0, 2.0]) },
        });
        var bridge = NewBridge(cal);

        await bridge.GetVehicleConfig();

        cal.Config.VehicleSeedTemplate.Should().NotBeNull();
        cal.Config.VehicleSeedTemplate!.FinalDriveRatio.Should().Be(5.0);
    }

    [Fact]
    public async Task Migration_preserves_existing_dashboard_values_byte_identical()
    {
        var cal = new FakeCalibrationService();
        cal.Config = NewConfig(new Dictionary<string, DashboardDefinition>
        {
            ["default"] = new() { Vehicle = NewVc(4.6, [3.8, 2.4, 1.6, 1.2, 0.9], rpm: 940) },
        });
        var bridge = NewBridge(cal);

        var json = await bridge.GetVehicleConfig();
        using var doc = JsonDocument.Parse(json);
        var vc = doc.RootElement;

        vc.GetProperty("finalDriveRatio").GetDouble().Should().Be(4.6);
        vc.GetProperty("rpmEntityId").GetInt32().Should().Be(940);
        vc.GetProperty("gearRatios").GetArrayLength().Should().Be(5);
        vc.GetProperty("shifter").GetProperty("shiftPointRpm").GetDouble().Should().Be(0);
        vc.GetProperty("shifter").GetProperty("downshiftFloorRpm").GetDouble().Should().Be(0);
    }

    [Fact]
    public async Task Seed_only_dashboard_is_materialized_during_migration()
    {
        var cal = new FakeCalibrationService();
        cal.Config = NewConfig(global: NewVc(4.3, [3.6, 2.2, 1.5]));
        var bridge = NewBridge(cal);

        await bridge.GetVehicleConfig();

        cal.Config.Dashboards["default"].Vehicle.Should().NotBeNull();
        cal.Config.Dashboards["default"].Vehicle!.FinalDriveRatio.Should().Be(4.3);
    }

    [Fact]
    public async Task Per_dashboard_isolation_and_partial_payload_ordering()
    {
        var cal = new FakeCalibrationService();
        cal.Config = NewConfig(new Dictionary<string, DashboardDefinition>
        {
            ["a"] = new(),
            ["b"] = new(),
        }, global: NewVc(4.3, [3.6, 2.2, 1.5]));
        var bridge = NewBridge(cal);

        await bridge.SetActiveDashboard("a");
        await bridge.SetVehicleConfig(VcPayload(4.6, [3.8, 2.4], shifter: new ShifterConfig { ShiftPointRpm = 7000, DownshiftFloorRpm = 5000 }));

        // Partial payload (vehicle-only) must preserve the persisted shifter block.
        await bridge.SetVehicleConfig(VcPayload(4.6, [3.8, 2.4]));

        cal.Config.Dashboards["a"].Vehicle!.FinalDriveRatio.Should().Be(4.6);
        cal.Config.Dashboards["a"].ShifterConfig!.ShiftPointRpm.Should().Be(7000, "absent shifter block never zeroes the persisted value");
        cal.Config.Dashboards["a"].ShifterConfig.DownshiftFloorRpm.Should().Be(5000);
        cal.Config.Dashboards["b"].Vehicle.Should().BeNull("saving one dashboard leaves the other untouched");
    }

    [Fact]
    public async Task Explicit_shifter_block_replaces_persisted_value()
    {
        var cal = new FakeCalibrationService();
        cal.Config = NewConfig(new Dictionary<string, DashboardDefinition>
        {
            ["a"] = new() { ShifterConfig = new ShifterConfig { ShiftPointRpm = 7000, DownshiftFloorRpm = 5000 } },
        }, global: NewVc(4.3, [3.6, 2.2, 1.5]));
        var bridge = NewBridge(cal);

        await bridge.SetActiveDashboard("a");
        await bridge.SetVehicleConfig(VcPayload(4.6, [3.8, 2.4], shifter: new ShifterConfig { ShiftPointRpm = 6500, DownshiftFloorRpm = 0 }));

        cal.Config.Dashboards["a"].ShifterConfig!.ShiftPointRpm.Should().Be(6500);
        cal.Config.Dashboards["a"].ShifterConfig.DownshiftFloorRpm.Should().Be(0);
    }

    [Fact]
    public async Task Seed_refresh_gate_excludes_incomplete_and_auto_detect_writes()
    {
        var cal = new FakeCalibrationService();
        cal.Config = NewConfig(new Dictionary<string, DashboardDefinition> { ["a"] = new() }, global: NewVc(4.3, [3.6, 2.2, 1.5]));
        var bridge = NewBridge(cal);
        await bridge.SetActiveDashboard("a");
        await bridge.GetVehicleConfig();
        var seedBefore = VehicleConfigMigration.Clone(cal.Config.VehicleSeedTemplate!);

        // Incomplete save (empty gear list) → seed untouched. (Zero ratios can never
        // reach the gate — the bridge parse drops non-positive ratios on the way in.)
        await bridge.SetVehicleConfig(VcPayload(4.6, []));
        cal.Config.VehicleSeedTemplate.Should().BeEquivalentTo(seedBefore);

        // Seed-identical save → seed untouched.
        await bridge.SetVehicleConfig(VcPayload(4.3, [3.6, 2.2, 1.5]));
        cal.Config.VehicleSeedTemplate.Should().BeEquivalentTo(seedBefore);

        // Auto-detect flagged write → seed untouched even though complete and differing.
        await bridge.SetVehicleConfig(VcPayload(5.0, [4.0, 3.0, 2.0], autoDetect: true));
        cal.Config.VehicleSeedTemplate.Should().BeEquivalentTo(seedBefore);

        // Complete, differing, non-auto-detect → seed refreshed.
        await bridge.SetVehicleConfig(VcPayload(5.0, [4.0, 3.0, 2.0]));
        cal.Config.VehicleSeedTemplate!.FinalDriveRatio.Should().Be(5.0);
    }

    [Fact]
    public async Task CreateDashboard_seeds_vehicle_and_zero_shifter()
    {
        var cal = new FakeCalibrationService();
        cal.Config = NewConfig(new Dictionary<string, DashboardDefinition> { ["a"] = new() }, global: NewVc(4.3, [3.6, 2.2, 1.5]));
        var bridge = NewBridge(cal);
        await bridge.GetVehicleConfig();

        var result = await bridge.CreateDashboard("fresh");

        JsonSerializer.Deserialize<JsonElement>(result).GetProperty("success").GetBoolean().Should().BeTrue();
        var fresh = cal.Config.Dashboards["fresh"];
        fresh.Vehicle.Should().NotBeNull();
        fresh.Vehicle!.FinalDriveRatio.Should().Be(4.3, "vehicle seeded from the hidden template (R7)");
        fresh.ShifterConfig.Should().NotBeNull();
        fresh.ShifterConfig!.ShiftPointRpm.Should().Be(0, "shifter settings always initialize to zero (R9)");
        fresh.ShifterConfig.DownshiftFloorRpm.Should().Be(0);
        cal.Config.ActiveDashboard.Should().Be("fresh");
    }

    [Fact]
    public async Task SaveSensorSelection_preserves_shifter_vehicle_and_odometer()
    {
        var cal = new FakeCalibrationService();
        cal.Config = NewConfig(new Dictionary<string, DashboardDefinition>
        {
            ["a"] = new()
            {
                Gauges = new System.Collections.ObjectModel.Collection<GaugeConfigEntry>
                {
                    new() { Id = 940 },
                    new() { Id = -3001 },
                },
                Vehicle = NewVc(4.6, [3.8, 2.4]),
                ShifterConfig = new ShifterConfig { ShiftPointRpm = 7000, DownshiftFloorRpm = 5000 },
                Odometer = new OdometerConfig { CurrentValue = 1234.5, UseKilometers = true },
                WarningHistory = [new WarningHistoryEntry { Id = 1, DataId = 940 }],
            },
        });
        var bridge = NewBridge(cal);

        var payload = Json(new
        {
            dashboardName = "a",
            selectedIds = new[] { 940, -3001 },
            customizations = new { },
            backgroundImagePath = (string?)null,
        });
        var result = await bridge.SaveSensorSelection(payload);

        JsonSerializer.Deserialize<JsonElement>(result).GetProperty("success").GetBoolean().Should().BeTrue();
        var def = cal.Config.Dashboards["a"];
        def.Vehicle!.FinalDriveRatio.Should().Be(4.6);
        def.ShifterConfig!.ShiftPointRpm.Should().Be(7000);
        def.Odometer!.CurrentValue.Should().Be(1234.5);
        def.WarningHistory.Should().HaveCount(1);
    }

    [Fact]
    public async Task Emulator_file_follows_active_dashboard_on_save_and_switch()
    {
        // Tests must never touch the real user profile — redirect the shared
        // file into a throwaway temp dir for the duration of the test.
        var tempDir = Path.Combine(Path.GetTempPath(), "me221-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var sharedPath = Path.Combine(tempDir, "vehicle-config.json");
        HybridBridgeService.SharedDirOverride = tempDir;
        try
        {
        var cal = new FakeCalibrationService();
        cal.Config = NewConfig(new Dictionary<string, DashboardDefinition> { ["a"] = new() }, global: NewVc(4.3, [3.6, 2.2, 1.5]));
        var bridge = NewBridge(cal);

            await bridge.SetActiveDashboard("a");

            // Create a dashboard BEFORE any save refreshes the seed — it seeds from the
            // original template (4.3) and becomes active, so the file follows it.
            await bridge.CreateDashboard("b");
            File.Exists(sharedPath).Should().BeTrue();
            using (var doc = JsonDocument.Parse(await File.ReadAllTextAsync(sharedPath)))
            {
                doc.RootElement.GetProperty("finalDriveRatio").GetDouble().Should().Be(4.3,
                    "created dashboard became active; the file follows its seeded vehicle (R10)");
            }

            // Saving 'a' rewrites the file from its config AND refreshes the seed.
            await bridge.SetActiveDashboard("a");
            await bridge.SetVehicleConfig(VcPayload(4.6, [3.8, 2.4]));
            using (var doc = JsonDocument.Parse(await File.ReadAllTextAsync(sharedPath)))
            {
                doc.RootElement.GetProperty("finalDriveRatio").GetDouble().Should().Be(4.6);
            }

            await bridge.SetActiveDashboard("b");
            using (var doc = JsonDocument.Parse(await File.ReadAllTextAsync(sharedPath)))
            {
                doc.RootElement.GetProperty("finalDriveRatio").GetDouble().Should().Be(4.3,
                    "switching to 'b' rewrites the file from its seeded vehicle (R10)");
            }
        }
        finally
        {
            HybridBridgeService.SharedDirOverride = null;
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task Emulator_file_carries_the_active_dashboards_shift_points()
    {
        // Tests must never touch the real user profile — redirect the shared
        // file into a throwaway temp dir for the duration of the test.
        var tempDir = Path.Combine(Path.GetTempPath(), "me221-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var sharedPath = Path.Combine(tempDir, "vehicle-config.json");
        HybridBridgeService.SharedDirOverride = tempDir;
        try
        {
            var cal = new FakeCalibrationService();
            cal.Config = NewConfig(new Dictionary<string, DashboardDefinition>
            {
                ["a"] = new()
                {
                    Vehicle = NewVc(4.6, [3.8, 2.4]),
                    ShifterConfig = new ShifterConfig { ShiftPointRpm = 8500, DownshiftFloorRpm = 4200 },
                },
            });
            var bridge = NewBridge(cal);
            await bridge.SetActiveDashboard("a");

            // Saving the vehicle config triggers the shared-file write; it must carry
            // the dashboard's shift-up point so the emulator shifts where the shift
            // light is configured. The shift-light downshift floor is a display
            // threshold and must NOT leak into the emulator's coast behavior.
            await bridge.SetVehicleConfig(VcPayload(4.6, [3.8, 2.4]));
            using (var doc = JsonDocument.Parse(await File.ReadAllTextAsync(sharedPath)))
            {
                doc.RootElement.GetProperty("finalDriveRatio").GetDouble().Should().Be(4.6);
                doc.RootElement.GetProperty("shiftUpRpm").GetDouble().Should().Be(8500);
                doc.RootElement.TryGetProperty("shiftDownRpm", out _).Should().BeFalse(
                    "the shift-light floor is not the emulator's coast floor");
            }
        }
        finally
        {
            HybridBridgeService.SharedDirOverride = null;
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task Emulator_file_omits_shift_points_when_the_dashboard_has_no_shifter()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "me221-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var sharedPath = Path.Combine(tempDir, "vehicle-config.json");
        HybridBridgeService.SharedDirOverride = tempDir;
        try
        {
            var cal = new FakeCalibrationService();
            cal.Config = NewConfig(new Dictionary<string, DashboardDefinition> { ["a"] = new() }, global: NewVc(4.3, [3.6, 2.2, 1.5]));
            var bridge = NewBridge(cal);
            await bridge.SetActiveDashboard("a");
            await bridge.SetVehicleConfig(VcPayload(4.3, [3.6, 2.2, 1.5]));
            using (var doc = JsonDocument.Parse(await File.ReadAllTextAsync(sharedPath)))
            {
                doc.RootElement.GetProperty("shiftUpRpm").ValueKind.Should().Be(JsonValueKind.Null,
                    "the emulator falls back to its own defaults when no shifter is configured");
            }
        }
        finally
        {
            HybridBridgeService.SharedDirOverride = null;
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static HybridBridgeService NewBridge(FakeCalibrationService cal) => TestBridge.Create(cal);

    private static string VcPayload(double fd, double[] gears, int? rpm = null, ShifterConfig? shifter = null, bool autoDetect = false)
    {
        return Json(new
        {
            enabled = true,
            tireDiameterInches = 23,
            finalDriveRatio = fd,
            gearRatios = gears,
            wheelSlipPercent = 3,
            rpmEntityId = rpm,
            vssSpeedEntityId = (int?)null,
            mapEntityId = (int?)null,
            baroEntityId = (int?)null,
            gearEntityId = (int?)null,
            autoDetect,
            shifter = shifter is null
                ? null
                : new { shiftPointRpm = shifter.ShiftPointRpm, downshiftFloorRpm = shifter.DownshiftFloorRpm },
        });
    }

    private static string Json(object payload)
    {
        return JsonSerializer.Serialize(payload);
    }

    private static VehicleConfig NewVc(
        double fd,
        double[] gears,
        int? rpm = null)
    {
        return new VehicleConfig
        {
            Enabled = true,
            TireDiameterInches = 23,
            FinalDriveRatio = fd,
            GearRatios = gears,
            WheelSlipPercent = 3,
            RpmEntityId = rpm,
        };
    }
}

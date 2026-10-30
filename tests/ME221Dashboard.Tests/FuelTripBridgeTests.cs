using System.Text.Json;
using FluentAssertions;
using ME221Dashboard.Services;
using Xunit;

namespace ME221Dashboard.Tests;

/// <summary>
/// The trip totals behind the average fuel-efficiency entity. The JS side owns
/// integration; the bridge owns canonical-unit persistence, so changing the
/// display unit never invalidates an in-progress trip.
/// </summary>
public sealed class FuelTripBridgeTests
{
    private const string Dash = "default";

    private static (HybridBridgeService Bridge, FakeCalibrationService Cal) NewBridge(
        Dictionary<string, DashboardDefinition>? dashboards = null)
    {
        var cal = new FakeCalibrationService
        {
            Config = new DashboardConfig
            {
                ActiveDashboard = Dash,
                Dashboards = dashboards ?? new Dictionary<string, DashboardDefinition> { [Dash] = new() },
            },
        };
        return (TestBridge.Create(cal), cal);
    }

    private static async Task<(double DistanceKm, double FuelLiters)> ReadTrip(HybridBridgeService bridge)
    {
        var json = await bridge.GetFuelTrip();
        using var doc = JsonDocument.Parse(json);
        return (
            doc.RootElement.GetProperty("distanceKm").GetDouble(),
            doc.RootElement.GetProperty("fuelLiters").GetDouble());
    }

    [Fact]
    public async Task Get_returns_zero_before_anything_was_driven()
    {
        var (bridge, _) = NewBridge();

        var (distance, fuel) = await ReadTrip(bridge);

        distance.Should().Be(0);
        fuel.Should().Be(0);
    }

    [Fact]
    public async Task Set_persists_totals_that_Get_reads_back()
    {
        var (bridge, cal) = NewBridge();

        var result = await bridge.SetFuelTrip(123.4, 9.87);
        using (var doc = JsonDocument.Parse(result))
        {
            doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        }

        var persisted = cal.Config.Dashboards[Dash].FuelTrip!;
        persisted.DistanceKm.Should().BeApproximately(123.4, 1e-9);
        persisted.FuelLiters.Should().BeApproximately(9.87, 1e-9);
        persisted.UpdatedAtUtc.Should().NotBeNull();

        var (distance, fuel) = await ReadTrip(bridge);
        distance.Should().BeApproximately(123.4, 1e-9);
        fuel.Should().BeApproximately(9.87, 1e-9);
    }

    [Fact]
    public async Task Set_clamps_negative_and_non_finite_values_to_zero()
    {
        var (bridge, cal) = NewBridge();

        await bridge.SetFuelTrip(double.NaN, -5);

        cal.Config.Dashboards[Dash].FuelTrip!.DistanceKm.Should().Be(0);
        cal.Config.Dashboards[Dash].FuelTrip!.FuelLiters.Should().Be(0);
    }

    [Fact]
    public async Task Set_only_touches_the_active_dashboard()
    {
        var (bridge, cal) = NewBridge(new Dictionary<string, DashboardDefinition>
        {
            ["a"] = new(),
            ["b"] = new(),
        });
        await bridge.SetActiveDashboard("b");

        await bridge.SetFuelTrip(50, 4);

        cal.Config.Dashboards["b"].FuelTrip!.DistanceKm.Should().Be(50);
        cal.Config.Dashboards["a"].FuelTrip.Should().BeNull();
    }

    [Fact]
    public async Task Set_creates_the_active_dashboard_when_none_exists()
    {
        // No dashboards at all: the resolved active name ("default") has no entry
        // yet, and a trip write must create it rather than fail.
        var (bridge, cal) = NewBridge(new Dictionary<string, DashboardDefinition>());

        await bridge.SetFuelTrip(10, 1);

        cal.Config.Dashboards.Should().ContainKey(Dash);
        cal.Config.Dashboards[Dash].FuelTrip!.DistanceKm.Should().Be(10);
    }
}

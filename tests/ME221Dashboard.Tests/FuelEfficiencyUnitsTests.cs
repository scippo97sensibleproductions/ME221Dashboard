using FluentAssertions;
using ME221Dashboard.Services;
using Xunit;

namespace ME221Dashboard.Tests;

/// <summary>
/// Contract tests for the unit/speed-source strings shared with the Svelte
/// client (FUEL_EFFICIENCY_UNITS / FUEL_SPEED_SOURCE_LABELS). A rename on
/// either side silently changes what the gauge shows, so the literals are
/// pinned here as well as in the TS fixture.
/// </summary>
public sealed class FuelEfficiencyUnitsTests
{
    [Fact]
    public void Normalize_accepts_only_the_known_units_and_falls_back_to_km_per_litre()
    {
        FuelEfficiencyUnits.Normalize("l100km").Should().Be("l100km");
        FuelEfficiencyUnits.Normalize("kmL").Should().Be("kmL");
        FuelEfficiencyUnits.Normalize("mpg").Should().Be("mpg");

        FuelEfficiencyUnits.Normalize(null).Should().Be("kmL");
        FuelEfficiencyUnits.Normalize("").Should().Be("kmL");
        FuelEfficiencyUnits.Normalize("furlongs").Should().Be("kmL");
    }

    [Fact]
    public void NormalizeSpeedSource_accepts_only_the_known_sources_and_falls_back_to_auto()
    {
        FuelEfficiencyUnits.NormalizeSpeedSource("auto").Should().Be("auto");
        FuelEfficiencyUnits.NormalizeSpeedSource("gps").Should().Be("gps");
        FuelEfficiencyUnits.NormalizeSpeedSource("trueSpeed").Should().Be("trueSpeed");

        FuelEfficiencyUnits.NormalizeSpeedSource(null).Should().Be("auto");
        FuelEfficiencyUnits.NormalizeSpeedSource("vss").Should().Be("auto");
    }

    [Theory]
    [InlineData("kmL", "km/L", 0, 50)]
    [InlineData("l100km", "L/100km", 0, 50)]
    [InlineData("mpg", "MPG", 0, 80)]
    public void Label_and_range_follow_the_configured_unit(string unit, string label, double min, double max)
    {
        FuelEfficiencyUnits.Label(unit).Should().Be(label);
        FuelEfficiencyUnits.Range(unit).Should().Be((min, max));
    }
}

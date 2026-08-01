using System.Globalization;
using System.Xml.Linq;
using FluentAssertions;
using ME221.Data.Infrastructure;
using ME221.Data.Models;
using Xunit;

namespace ME221.Data.Tests;

public class MeasurementUnitConverterTests
{
    private const float Tolerance = 0.01f;

    /// <summary>Pin the current thread culture so format assertions are deterministic on any machine.</summary>
    private static IDisposable InvariantCulture()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        return new ActionDisposable(() => CultureInfo.CurrentCulture = original);
    }

    private sealed class ActionDisposable(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    // ─── FromRaw / ToRaw ──────────────────────────────────────────────────

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(32767.5f, 2.5f)]
    [InlineData(65535f, 5f)]
    public void FromRaw_Volt_ScalesToFiveVolts(float raw, float expected)
    {
        MeasurementUnitConverter.FromRaw(raw, MeasurementUnitType.Volt).Should().BeApproximately(expected, Tolerance);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(2.5f, 32767.5f)]
    [InlineData(5f, 65535f)]
    public void ToRaw_Volt_ScalesToRaw(float display, float expected)
    {
        MeasurementUnitConverter.ToRaw(display, MeasurementUnitType.Volt).Should().BeApproximately(expected, Tolerance);
    }

    [Fact]
    public void FromRaw_Ohm_SaturatesAtMaxRaw()
    {
        MeasurementUnitConverter.FromRaw(65535f, MeasurementUnitType.Ohm).Should().Be(float.MaxValue);
    }

    [Fact]
    public void FromRaw_Ohm_ConvertsResistance()
    {
        // 2700 * 0.5 / 0.5 = 2700 at half scale
        MeasurementUnitConverter.FromRaw(32767.5f, MeasurementUnitType.Ohm)
            .Should().BeApproximately(2700f, 1f);
    }

    [Fact]
    public void ToRaw_Ohm_SaturatesAtMaxRaw()
    {
        MeasurementUnitConverter.ToRaw(2700f + 65535f, MeasurementUnitType.Ohm).Should().Be(65535f);
    }

    [Fact]
    public void ToRaw_Ohm_ConvertsBack()
    {
        MeasurementUnitConverter.ToRaw(2700f, MeasurementUnitType.Ohm)
            .Should().BeApproximately(32767.5f, 10f);
    }

    [Theory]
    [InlineData(6894.76f, 1000f)] // psi → kPa
    [InlineData(0f, 0f)]
    public void FromRaw_PSI_ConvertsToKPa(float raw, float expected)
    {
        MeasurementUnitConverter.FromRaw(raw, MeasurementUnitType.PSI)
            .Should().BeApproximately(expected, Tolerance);
    }

    [Fact]
    public void ToRaw_PSI_ConvertsBack()
    {
        MeasurementUnitConverter.ToRaw(1000f, MeasurementUnitType.PSI)
            .Should().BeApproximately(6894.76f, Tolerance);
    }

    [Theory]
    [InlineData(0f, 32f)]
    [InlineData(100f, 212f)]
    [InlineData(-40f, -40f)]
    public void FromRaw_Fahrenheit_ConvertsFromCelsius(float raw, float expected)
    {
        MeasurementUnitConverter.FromRaw(raw, MeasurementUnitType.Fahrenheit)
            .Should().BeApproximately(expected, Tolerance);
    }

    [Theory]
    [InlineData(32f, 0f)]
    [InlineData(212f, 100f)]
    public void ToRaw_Fahrenheit_ConvertsBack(float display, float expected)
    {
        MeasurementUnitConverter.ToRaw(display, MeasurementUnitType.Fahrenheit)
            .Should().BeApproximately(expected, Tolerance);
    }

    [Theory]
    [InlineData(MeasurementUnitType.KPa)]
    [InlineData(MeasurementUnitType.Celsius)]
    [InlineData(MeasurementUnitType.Percent)]
    [InlineData(MeasurementUnitType.Rpm)]
    [InlineData(MeasurementUnitType.Deg)]
    [InlineData(MeasurementUnitType.Ms)]
    [InlineData(MeasurementUnitType.Bar)]
    public void FromRaw_PassthroughUnits_ReturnRaw(MeasurementUnitType unit)
    {
        MeasurementUnitConverter.FromRaw(123.5f, unit).Should().Be(123.5f);
    }

    [Fact]
    public void FromRaw_VoltPlusKPa_FlagsTakePrecedence()
    {
        MeasurementUnitConverter.FromRaw(32767.5f, MeasurementUnitType.Volt | MeasurementUnitType.KPa)
            .Should().BeApproximately(2.5f, Tolerance);
    }

    // ─── Array helpers ────────────────────────────────────────────────────

    [Fact]
    public void FromRawArray_UnknownOrRaw_ReturnsInputArray()
    {
        var raw = new[] { 1f, 2f, 3f };

        MeasurementUnitConverter.FromRawArray(raw, MeasurementUnitType.Unknown).Should().BeSameAs(raw);
        MeasurementUnitConverter.FromRawArray(raw, MeasurementUnitType.Raw).Should().BeSameAs(raw);
    }

    [Fact]
    public void FromRawArray_ConvertsEachElement()
    {
        var result = MeasurementUnitConverter.FromRawArray([0f, 32767.5f, 65535f], MeasurementUnitType.Volt);

        result.Should().HaveCount(3);
        result[0].Should().BeApproximately(0f, Tolerance);
        result[1].Should().BeApproximately(2.5f, Tolerance);
        result[2].Should().BeApproximately(5f, Tolerance);
    }

    [Fact]
    public void ToRawArray_ConvertsEachElement()
    {
        var result = MeasurementUnitConverter.ToRawArray([0f, 2.5f, 5f], MeasurementUnitType.Volt);

        result[0].Should().BeApproximately(0f, Tolerance);
        result[1].Should().BeApproximately(32767.5f, Tolerance);
        result[2].Should().BeApproximately(65535f, Tolerance);
    }

    // ─── FormatValue ──────────────────────────────────────────────────────

    [Fact]
    public void FormatValue_TrimModPercent_NegativeValue_SubtractsOne()
    {
        using var _ = InvariantCulture();
        MeasurementUnitConverter.FormatValue(0.85f, DataType.TrimModPercent).Should().Be("-15.0 %");
    }

    [Fact]
    public void FormatValue_TrimModPercent_PositiveValue_SubtractsOne()
    {
        using var _ = InvariantCulture();
        MeasurementUnitConverter.FormatValue(1.15f, DataType.TrimModPercent).Should().Be("+15.0 %");
    }

    [Fact]
    public void FormatValue_TrimModPercent_ExactlyOne_TakesPositiveBranch()
    {
        using var _ = InvariantCulture();
        MeasurementUnitConverter.FormatValue(1.0f, DataType.TrimModPercent).Should().Be("+0.0 %");
    }

    [Fact]
    public void FormatValue_Percent_UsesPercentFormat()
    {
        using var _ = InvariantCulture();
        MeasurementUnitConverter.FormatValue(0.5f, DataType.Percent).Should().Be("50.0 %");
    }

    [Fact]
    public void FormatValue_Normal_UsesDecimalPlaces()
    {
        using var _ = InvariantCulture();
        MeasurementUnitConverter.FormatValue(3.14159f, DataType.Normal).Should().Be("3.14");
        MeasurementUnitConverter.FormatValue(3.14159f, DataType.Normal, 3).Should().Be("3.142");
        MeasurementUnitConverter.FormatValue(3.14159f, DataType.Normal, 0).Should().Be("3");
    }

    // ─── ParseUnitTypes ───────────────────────────────────────────────────

    [Fact]
    public void ParseUnitTypes_NullElement_ReturnsUnknown()
    {
        MeasurementUnitConverter.ParseUnitTypes(null).Should().Be(MeasurementUnitType.Unknown);
    }

    [Theory]
    [InlineData("Celsius", MeasurementUnitType.Celsius)]
    [InlineData("Fahrenheit", MeasurementUnitType.Fahrenheit)]
    [InlineData("Volt", MeasurementUnitType.Volt)]
    [InlineData("Ohm", MeasurementUnitType.Ohm)]
    [InlineData("Percent", MeasurementUnitType.Percent)]
    [InlineData("Rpm", MeasurementUnitType.Rpm)]
    [InlineData("Deg", MeasurementUnitType.Deg)]
    [InlineData("Ms", MeasurementUnitType.Ms)]
    [InlineData("Bar", MeasurementUnitType.Bar)]
    [InlineData("Kpa", MeasurementUnitType.KPa)]
    [InlineData("KPa", MeasurementUnitType.KPa)]
    [InlineData("Psi", MeasurementUnitType.PSI)]
    [InlineData("PSI", MeasurementUnitType.PSI)]
    [InlineData("Raw", MeasurementUnitType.Raw)]
    [InlineData("Whatever", MeasurementUnitType.Unknown)]
    public void ParseUnitTypes_SingleElement_MapsToFlag(string elementName, MeasurementUnitType expected)
    {
        var element = XElement.Parse($"<MeasurementUnitTypes><{elementName} /></MeasurementUnitTypes>");

        MeasurementUnitConverter.ParseUnitTypes(element).Should().Be(expected);
    }

    [Fact]
    public void ParseUnitTypes_MultipleElements_CombinesFlags()
    {
        var element = XElement.Parse("<MeasurementUnitTypes><Volt /><KPa /></MeasurementUnitTypes>");

        var result = MeasurementUnitConverter.ParseUnitTypes(element);

        result.Should().HaveFlag(MeasurementUnitType.Volt);
        result.Should().HaveFlag(MeasurementUnitType.KPa);
        result.Should().NotHaveFlag(MeasurementUnitType.Celsius);
    }
}

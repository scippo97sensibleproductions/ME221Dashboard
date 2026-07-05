using System.Xml.Linq;
using ME221.Data.Models;

namespace ME221.Data.Infrastructure;

public static class MeasurementUnitConverter
{
    private const float InternalResistance = 2700f;
    private const float RawMax = 65535f;
    private const float VoltMax = 5f;
    private const float PsitoKpaMult = 6.89476f;

    /// <summary>
    /// Convert raw ECU float to display value for the given unit type.
    /// </summary>
    public static float FromRaw(float raw, MeasurementUnitType unitType)
    {
        if (unitType.HasFlag(MeasurementUnitType.Volt))
            return VoltMax * raw / RawMax;

        if (unitType.HasFlag(MeasurementUnitType.Ohm))
        {
            if (raw >= RawMax) return float.MaxValue;
            return InternalResistance * raw / (RawMax - raw);
        }

        if (unitType.HasFlag(MeasurementUnitType.PSI))
            return raw / PsitoKpaMult;

        if (unitType.HasFlag(MeasurementUnitType.Fahrenheit))
            return raw * 1.8f + 32f;

        // KPa, Celsius, Raw, Percent, Rpm, Deg, Ms, Bar — pass through
        return raw;
    }

    /// <summary>
    /// Convert display value back to raw ECU float for writing.
    /// </summary>
    public static float ToRaw(float display, MeasurementUnitType unitType)
    {
        if (unitType.HasFlag(MeasurementUnitType.Volt))
            return RawMax * display / VoltMax;

        if (unitType.HasFlag(MeasurementUnitType.Ohm))
        {
            if (display >= InternalResistance + RawMax) return RawMax;
            return RawMax * display / (InternalResistance + display);
        }

        if (unitType.HasFlag(MeasurementUnitType.PSI))
            return display * PsitoKpaMult;

        if (unitType.HasFlag(MeasurementUnitType.Fahrenheit))
            return (display - 32f) / 1.8f;

        // KPa, Celsius, Raw, Percent, Rpm, Deg, Ms, Bar — pass through
        return display;
    }

    /// <summary>
    /// Convert an array of raw values to display values.
    /// </summary>
    public static float[] FromRawArray(float[] raw, MeasurementUnitType unitType)
    {
        if (unitType == MeasurementUnitType.Unknown || unitType == MeasurementUnitType.Raw)
            return raw;

        var result = new float[raw.Length];
        for (int i = 0; i < raw.Length; i++)
            result[i] = FromRaw(raw[i], unitType);
        return result;
    }

    /// <summary>
    /// Convert an array of display values back to raw for writing.
    /// </summary>
    public static float[] ToRawArray(float[] display, MeasurementUnitType unitType)
    {
        if (unitType == MeasurementUnitType.Unknown || unitType == MeasurementUnitType.Raw)
            return display;

        var result = new float[display.Length];
        for (int i = 0; i < display.Length; i++)
            result[i] = ToRaw(display[i], unitType);
        return result;
    }

    /// <summary>
    /// Format a display value based on its DataType.
    /// </summary>
    public static string FormatValue(float value, DataType dataType, int decimalPlaces = 2)
    {
        return dataType switch
        {
            DataType.TrimModPercent =>
                value < 0f
                    ? (value - 1f).ToString("#0.0# %")
                    : (value - 1f).ToString("+#0.0# %"),
            DataType.Percent => value.ToString("#0.0# %"),
            _ => value.ToString("F" + decimalPlaces),
        };
    }

    /// <summary>
    /// Parse MeasurementUnitType flags from DEF XML element names.
    /// </summary>
    public static MeasurementUnitType ParseUnitTypes(XElement? unitTypesElement)
    {
        if (unitTypesElement is null) return MeasurementUnitType.Unknown;

        var result = MeasurementUnitType.Unknown;
        foreach (var child in unitTypesElement.Elements())
        {
            result |= child.Name.LocalName switch
            {
                "Celsius" => MeasurementUnitType.Celsius,
                "Fahrenheit" => MeasurementUnitType.Fahrenheit,
                "Volt" => MeasurementUnitType.Volt,
                "Ohm" => MeasurementUnitType.Ohm,
                "Percent" => MeasurementUnitType.Percent,
                "Rpm" => MeasurementUnitType.Rpm,
                "Deg" => MeasurementUnitType.Deg,
                "Ms" => MeasurementUnitType.Ms,
                "Bar" => MeasurementUnitType.Bar,
                "Kpa" or "KPa" => MeasurementUnitType.KPa,
                "Psi" or "PSI" => MeasurementUnitType.PSI,
                "Raw" => MeasurementUnitType.Raw,
                _ => MeasurementUnitType.Unknown,
            };
        }
        return result;
    }
}

namespace ME221Dashboard.Services;

/// <summary>
/// Display metadata for the derived fuel-efficiency entity (−3007). The stored
/// value is unit-independent; only the gauge/picker label and nominal range
/// follow the dashboard's configured output unit.
/// </summary>
public static class FuelEfficiencyUnits
{
    public const string LitersPer100Km = "l100km";
    public const string KilometersPerLiter = "kmL";
    public const string MilesPerGallon = "mpg";

    /// <summary>Unit label shown in the sensor picker and gauge defaults.</summary>
    public static string Label(string? unit) => Normalize(unit) switch
    {
        LitersPer100Km => "L/100km",
        MilesPerGallon => "MPG",
        _ => "km/L",
    };

    /// <summary>Nominal display range for the configured unit.</summary>
    public static (double Min, double Max) Range(string? unit) => Normalize(unit) switch
    {
        MilesPerGallon => (0, 80),
        _ => (0, 50),
    };

    /// <summary>Accept only the known units; anything else falls back to km/L.</summary>
    public static string Normalize(string? unit) => unit switch
    {
        LitersPer100Km => LitersPer100Km,
        MilesPerGallon => MilesPerGallon,
        _ => KilometersPerLiter,
    };

    /// <summary>Accept only the known speed sources; anything else falls back to auto.</summary>
    public static string NormalizeSpeedSource(string? source) => source switch
    {
        Gps => Gps,
        TrueSpeed => TrueSpeed,
        _ => Auto,
    };

    public const string Auto = "auto";
    public const string Gps = "gps";
    public const string TrueSpeed = "trueSpeed";
}

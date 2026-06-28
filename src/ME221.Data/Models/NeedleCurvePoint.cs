namespace ME221.Data.Models;

/// <summary>
/// A single control point in a piecewise needle curve mapping.
/// RawValue is the actual ECU value (e.g. 90 for 90°C), Angle is in degrees.
/// </summary>
public sealed class NeedleCurvePoint
{
    public double RawValue { get; set; }
    public double Angle { get; set; }
}

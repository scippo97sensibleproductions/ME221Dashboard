namespace ME221.Data.Models;

public sealed class SensorCustomization
{
    public int Id { get; set; }
    public string? CustomName { get; set; }
    public string? CustomUnit { get; set; }
    public float? MinRange { get; set; }
    public float? MaxRange { get; set; }
    public bool MinRangeBypass { get; set; }
    public bool MaxRangeBypass { get; set; }
}

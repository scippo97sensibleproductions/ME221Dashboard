namespace ME221.Data.Models;

public sealed class DataLinkDefinition
{
    public ushort Id { get; init; }
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public bool ViewInTree { get; init; }
    public bool StandardLogging { get; init; }
    public string MeasureUnit { get; init; } = "";
    public MeasurementUnitType MeasurementUnitTypes { get; init; }
    public DataType DataTypeSet { get; init; }
    public float MinValue { get; init; }
    public float MaxValue { get; init; }
    public string? DataKey { get; init; }
    public List<TextValueMapping>? TextValues { get; init; }
}

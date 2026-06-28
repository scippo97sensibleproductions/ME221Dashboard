namespace ME221.Data.Models;

public sealed class DataLinkDefinition
{
    public ushort Id { get; init; }
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public bool ViewInTree { get; init; }
    public bool StandardLogging { get; init; }
    public string MeasureUnit { get; init; } = "";
    public string? DataKey { get; init; }
    public List<TextValueMapping>? TextValues { get; init; }
}

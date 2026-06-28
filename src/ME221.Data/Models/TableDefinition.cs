namespace ME221.Data.Models;

public sealed class TableDefinition
{
    public ushort Id { get; init; }
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public bool ViewInTree { get; init; }
    public bool Enabled { get; init; }
    public string TableType { get; init; } = "";
    public ushort Cols { get; init; }
    public ushort Rows { get; init; }

    public ushort Input0LinkId { get; init; }
    public ushort Input1LinkId { get; init; }
    public ushort OutputLinkId { get; init; }

    public string Input0Name { get; init; } = "";
    public string Input1Name { get; init; } = "";
    public string OutputName { get; init; } = "";
    public float IncrementValue { get; init; } = 0.1f;
    public float? DefaultValue { get; init; }

    public List<float>? Input0 { get; init; }
    public List<float>? Input1 { get; init; }
    public List<float>? Output { get; init; }
}

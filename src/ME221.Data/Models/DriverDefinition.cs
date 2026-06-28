namespace ME221.Data.Models;

public sealed class DriverDefinition
{
    public ushort Id { get; init; }
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public bool ViewInTree { get; init; }
    public int NumberOfConfigs { get; init; }
    public List<DriverParamDefinition> Configs { get; init; } = [];
    public int NumberOfOutputs { get; init; }
    public List<ushort> OutputLinkIds { get; set; } = [];
    public bool EditableOutputs { get; init; }
    public List<string> OutputNames { get; init; } = [];
    public int NumberOfInputs { get; init; }
    public List<ushort> InputLinkIds { get; set; } = [];
    public bool EditableInputs { get; init; }
    public List<string> InputNames { get; init; } = [];
}

public sealed class DriverParamDefinition
{
    public string Name { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string SectionName { get; init; } = "";
    public string ParamType { get; init; } = "";
    public bool ReadOnly { get; init; }
    public bool RequiresReset { get; init; } = false;
    public float Value { get; init; }
    public float Min { get; init; }
    public float Max { get; init; }
    public bool CheckRange { get; init; }
    public string ToolTipText { get; init; } = "";
    public List<ComboOption>? Options { get; init; }
    public ViewConstraint? ViewConstraint { get; init; }
}

public sealed class ViewConstraint
{
    public int ParamIndex { get; init; }
    public float[] AcceptedValues { get; init; } = [];
}

public sealed class ComboOption
{
    public ushort Id { get; init; }
    public string Name { get; init; } = "";
}

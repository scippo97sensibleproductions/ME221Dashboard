namespace ME221.Data.Models;

public sealed class CalibrationData
{
    public DeviceInfoMetadata Metadata { get; init; } = new();
    public List<DataLinkDefinition> DataLinks { get; init; } = [];
    public List<TableDefinition> Tables { get; init; } = [];
    public List<DriverDefinition> Drivers { get; init; } = [];
}

public sealed class DeviceInfoMetadata
{
    public string ProductName { get; init; } = "";
    public string ModelName { get; init; } = "";
    public string Version { get; init; } = "";
}

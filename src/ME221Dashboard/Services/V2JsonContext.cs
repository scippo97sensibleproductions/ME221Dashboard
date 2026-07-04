using System.Text.Json.Serialization;
using ME221.Data.Models;

namespace ME221Dashboard.Services;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CalibrationData))]
[JsonSerializable(typeof(DataLinkDefinition))]
[JsonSerializable(typeof(TableDefinition))]
[JsonSerializable(typeof(DriverDefinition))]
[JsonSerializable(typeof(DriverParamDefinition))]
[JsonSerializable(typeof(ComboOption))]
[JsonSerializable(typeof(ViewConstraint))]
[JsonSerializable(typeof(TextValueMapping))]
[JsonSerializable(typeof(DeviceInfoMetadata))]
[JsonSerializable(typeof(GaugeConfigEntry))]
[JsonSerializable(typeof(DashboardTableEntry))]
[JsonSerializable(typeof(SensorCustomization))]
[JsonSerializable(typeof(DashboardConfig))]
[JsonSerializable(typeof(DashboardDefinition))]
[JsonSerializable(typeof(List<DataLinkDefinition>))]
[JsonSerializable(typeof(List<TableDefinition>))]
[JsonSerializable(typeof(List<DriverDefinition>))]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(List<GaugeConfigEntry>))]
[JsonSerializable(typeof(LiveDataUpdateMessage))]
[JsonSerializable(typeof(OdometerConfig))]
[JsonSerializable(typeof(VehicleConfig))]
public partial class V2JsonContext : JsonSerializerContext
{
}

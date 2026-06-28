using System.Text.Json.Serialization;
using ME221.Data.Models;

namespace ME221.Data;

/// <summary>
/// Trim-safe JSON serializer context for CalibrationData.
/// Uses source generators instead of reflection for better compatibility with trimming.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CalibrationData))]
[JsonSerializable(typeof(DataLinkDefinition))]
[JsonSerializable(typeof(TableDefinition))]
[JsonSerializable(typeof(DriverDefinition))]
[JsonSerializable(typeof(TextValueMapping))]
[JsonSerializable(typeof(DeviceInfoMetadata))]
[JsonSerializable(typeof(GaugeConfigEntry))]
[JsonSerializable(typeof(SensorCustomization))]
[JsonSerializable(typeof(List<DataLinkDefinition>))]
[JsonSerializable(typeof(List<TableDefinition>))]
[JsonSerializable(typeof(List<DriverDefinition>))]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(List<GaugeConfigEntry>))]
public partial class CalibrationJsonContext : JsonSerializerContext
{
}

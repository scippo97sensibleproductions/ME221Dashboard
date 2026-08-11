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
[JsonSerializable(typeof(ValueTransformStep))]
[JsonSerializable(typeof(ChartOverlayEntry))]
[JsonSerializable(typeof(List<ChartOverlayEntry>))]
[JsonSerializable(typeof(SensorCustomization))]
[JsonSerializable(typeof(List<DataLinkDefinition>))]
[JsonSerializable(typeof(List<TableDefinition>))]
[JsonSerializable(typeof(List<DriverDefinition>))]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(List<GaugeConfigEntry>))]
[JsonSerializable(typeof(List<ValueTransformStep>))]
[JsonSerializable(typeof(DataLinkWarningSetting))]
[JsonSerializable(typeof(WarningSettingStatus))]
[JsonSerializable(typeof(List<DataLinkWarningSetting>))]
[JsonSerializable(typeof(WarningLevel))]
[JsonSerializable(typeof(List<WarningLevel>))]
[JsonSerializable(typeof(WarningPoint))]
[JsonSerializable(typeof(List<WarningPoint>))]
[JsonSerializable(typeof(BatchLedgerEntry))]
[JsonSerializable(typeof(List<BatchLedgerEntry>))]
[JsonSerializable(typeof(QueuedBanner))]
[JsonSerializable(typeof(List<QueuedBanner>))]
[JsonSerializable(typeof(UndoExpiryNotice))]
[JsonSerializable(typeof(List<UndoExpiryNotice>))]
[JsonSerializable(typeof(DataLinkFeedback))]
[JsonSerializable(typeof(DataLinkFeedbackSeverity))]
[JsonSerializable(typeof(List<DataLinkFeedback>))]
public partial class CalibrationJsonContext : JsonSerializerContext
{
}

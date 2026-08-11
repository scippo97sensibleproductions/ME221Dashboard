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
[JsonSerializable(typeof(LinkedEntityEntry))]
[JsonSerializable(typeof(List<LinkedEntityEntry>))]
[JsonSerializable(typeof(ChartOverlayEntry))]
[JsonSerializable(typeof(List<ChartOverlayEntry>))]
[JsonSerializable(typeof(ValueTransformStep))]
[JsonSerializable(typeof(List<ValueTransformStep>))]
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
[JsonSerializable(typeof(ShifterConfig))]
[JsonSerializable(typeof(VehicleConfig))]
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
[JsonSerializable(typeof(WarningHistoryEntry))]
[JsonSerializable(typeof(List<WarningHistoryEntry>))]
[JsonSerializable(typeof(UserPreferences))]
[JsonSerializable(typeof(ConnectionPreference))]
[JsonSerializable(typeof(DataLinkFeedback))]
[JsonSerializable(typeof(DataLinkFeedbackSeverity))]
[JsonSerializable(typeof(List<DataLinkFeedback>))]
[JsonSerializable(typeof(MonitoringPreset))]
[JsonSerializable(typeof(List<MonitoringPreset>))]
public partial class V2JsonContext : JsonSerializerContext
{
}

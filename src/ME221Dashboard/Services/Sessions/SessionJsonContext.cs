using System.Text.Json.Serialization;

namespace ME221Dashboard.Services.Sessions;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(RecordedSession))]
[JsonSerializable(typeof(SamplePoint))]
[JsonSerializable(typeof(FreezeFrame))]
[JsonSerializable(typeof(SessionSummary))]
[JsonSerializable(typeof(SessionManifest))]
[JsonSerializable(typeof(List<RecordedSession>))]
[JsonSerializable(typeof(List<SessionSummary>))]
[JsonSerializable(typeof(ImportSessionResponse))]
internal partial class SessionJsonContext : JsonSerializerContext
{
}

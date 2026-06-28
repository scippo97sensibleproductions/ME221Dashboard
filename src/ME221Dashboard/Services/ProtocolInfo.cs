using ME221.Comms.Messages;

namespace ME221Dashboard.Services;

public record EntityMapEntry(ushort Id, ReportingType Type);

public record ProtocolInfo(
    ReportingVersion ReportingVersion,
    string ProductName,
    string ModelName,
    string FirmwareVersion,
    IReadOnlyList<EntityMapEntry> EntityMap
);

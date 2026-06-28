using ME221.Comms.Messages;

namespace ME221.Emulator.Domain;

public sealed class EcuState
{
    public bool ReportingEnabled { get; set; }
    public ReportingVersion ProtocolVersion { get; set; } = ReportingVersion.V2;
    public byte ReportingFrequency { get; set; } = 10;
    public List<SetStateResponse.EntityDescriptor> EntityMap { get; set; } = [];
    public List<ushort> SpecialCfgEntityIds { get; set; } = [];
    public byte SpecialCfgFrequency { get; set; }
    public bool SpecialCfgActive { get; set; }
}

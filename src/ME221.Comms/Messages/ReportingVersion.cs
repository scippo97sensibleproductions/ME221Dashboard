namespace ME221.Comms.Messages;

/// <summary>
/// Reporting protocol version used by the ECU.
/// </summary>
public enum ReportingVersion : byte
{
    /// <summary>Not applicable / unknown.</summary>
    NA = 0,

    /// <summary>Version 1 of the reporting protocol.</summary>
    V1 = 1,

    /// <summary>Version 2 of the reporting protocol.</summary>
    V2 = 2,
}

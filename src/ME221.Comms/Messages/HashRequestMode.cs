namespace ME221.Comms.Messages;

/// <summary>
/// Mode for hash requests.
/// </summary>
public enum HashRequestMode : byte
{
    /// <summary>Request overall ECU hash only.</summary>
    Overall = 0x00,

    /// <summary>Request detailed per-entity hashes.</summary>
    Detailed = 0x01,
}

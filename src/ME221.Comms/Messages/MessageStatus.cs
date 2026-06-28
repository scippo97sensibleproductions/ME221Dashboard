namespace ME221.Comms.Messages;

/// <summary>
/// Status returned in every response message, matching the real ECU's enumeration.
/// </summary>
public enum MessageStatus : byte
{
    /// <summary>Command completed successfully.</summary>
    Success = 0x00,

    /// <summary>Command failed (generic).</summary>
    Failure = 0x01,

    /// <summary>Invalid parameter in the request.</summary>
    InvalidParameter = 0x02,

    /// <summary>Request not supported by the target device.</summary>
    UnsupportedRequest = 0x03,

    /// <summary>Insufficient memory to complete the request.</summary>
    InsufficientMemory = 0x04,

    /// <summary>CRC check failed on received data.</summary>
    FailedCrc = 0x05,

    /// <summary>Flash write operation failed.</summary>
    FailedFlashWrite = 0x06,

    /// <summary>ECU model or feature lock-down prevented operation.</summary>
    ModelFeatureLockDown = 0x07,

    /// <summary>Protocol busy — retry later.</summary>
    ProtocolBusy = 0x08,

    /// <summary>Unknown or unrecognised status.</summary>
    Unknown = 0xFF,
}

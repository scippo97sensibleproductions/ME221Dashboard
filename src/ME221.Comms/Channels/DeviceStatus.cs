namespace ME221.Comms.Channels;

/// <summary>
/// Represents the connection status of a device channel.
/// </summary>
public enum DeviceStatus
{
    /// <summary>Channel is not open and not attempting to connect.</summary>
    Closed = 0,

    /// <summary>Channel is opening or attempting to connect.</summary>
    Opening = 1,

    /// <summary>Channel is open and ready for communication.</summary>
    Opened = 2,

    /// <summary>Channel is connected and actively communicating.</summary>
    Connected = 3,

    /// <summary>Channel is closing or disconnecting.</summary>
    Closing = 4,

    /// <summary>Channel is in a waiting-to-reconnect state.</summary>
    WaitingToReconnect = 5,
}

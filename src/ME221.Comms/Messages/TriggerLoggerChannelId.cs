namespace ME221.Comms.Messages;

/// <summary>
/// Trigger logger channel identifiers used in trigger logger configuration.
/// </summary>
public enum TriggerLoggerChannelId
{
    /// <summary>Event-based trigger (special channel).</summary>
    Events = -1,

    /// <summary>Crank angle sensor channel.</summary>
    Crank = 0,

    /// <summary>Camshaft A sensor channel.</summary>
    CamA = 1,

    /// <summary>Camshaft B sensor channel.</summary>
    CamB = 2,

    /// <summary>Camshaft C sensor channel.</summary>
    CamC = 3,

    /// <summary>Camshaft D sensor channel.</summary>
    CamD = 4,
}

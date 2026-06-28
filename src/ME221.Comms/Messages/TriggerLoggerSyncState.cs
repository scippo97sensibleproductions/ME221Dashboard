namespace ME221.Comms.Messages;

/// <summary>
/// Trigger logger synchronization state indicating how much of the trigger
/// pattern has been acquired.
/// </summary>
public enum TriggerLoggerSyncState : byte
{
    /// <summary>No synchronization acquired.</summary>
    NotSynced = 0,

    /// <summary>Crank half-synced (one edge detected).</summary>
    CrankHalfSynced = 1,

    /// <summary>Crank fully synced.</summary>
    CrankSynced = 2,

    /// <summary>Crank synced and waiting for cam sync.</summary>
    CrankAndCamSyncedWaiting = 3,

    /// <summary>Fully synced — both crank and cam acquired.</summary>
    FullySyncedReporting = 4,
}

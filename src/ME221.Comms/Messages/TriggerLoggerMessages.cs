using System.Buffers.Binary;
using ME221.Comms.Internal;

namespace ME221.Comms.Messages;

/// <summary>
/// Request to check if trigger logger is supported.
/// Command: TriggerLogger / IsSupported (class 0x07, command 0x00).
/// </summary>
public sealed class TriggerLoggerIsSupportedRequest() : Request(WireFormat.ClassTriggerLogger,
    WireFormat.TriggerLoggerIsSupported, Array.Empty<byte>());

/// <summary>
/// Response to a TriggerLogger IsSupported request.
/// Payload layout (after status byte):
///   [1 byte channel bitmask] — bit 0=Crank, bit 1=CamA, etc.
/// If no payload beyond status, defaults to Crank + CamA as available channels.
/// </summary>
public sealed class TriggerLoggerIsSupportedResponse(ReadOnlySpan<byte> payload) : Response(
    WireFormat.ClassTriggerLogger, WireFormat.TriggerLoggerIsSupported,
    payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure,
    payload.ToArray().AsMemory())
{
    public byte AvailableChannelBitmask { get; } = payload.Length > 1 ? payload[1] : (byte)0x03; // default: Crank + CamA

    public TriggerLoggerIsSupportedResponse(MessageStatus status) : this(new[] { (byte)status }) { }
}

/// <summary>
/// Request to set trigger logger state (enable/disable).
/// Command: TriggerLogger / SetState (class 0x07, command 0x01).
/// Payload: [1 byte enabled] [1 byte channel bitmask].
/// The bitmask selects which channels (bit 0=Crank, bit 1=CamA, etc.) to log.
/// </summary>
public sealed class TriggerLoggerSetStateRequest(bool enabled, byte bitmask) : Request(WireFormat.ClassTriggerLogger,
    WireFormat.TriggerLoggerSetState, new[] { (byte)(enabled ? 1 : 0), bitmask })
{
    public bool Enabled { get; } = enabled;
    public byte Bitmask { get; } = bitmask;

    public TriggerLoggerSetStateRequest(bool enabled) : this(enabled, 0) { }
}

/// <summary>
/// Response to a TriggerLogger SetState request.
/// Payload layout (after status byte):
///   [1 byte enabled] [1 byte channel bitmask] [4 byte freq LE]
/// The channel bitmask indicates which trigger channels are active.
/// Freq is the sampling frequency in Hz.
/// </summary>
public sealed class TriggerLoggerSetStateResponse(ReadOnlySpan<byte> payload) : Response(WireFormat.ClassTriggerLogger,
    WireFormat.TriggerLoggerSetState,
    payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure,
    payload.ToArray().AsMemory())
{
    public bool Enabled { get; } = payload.Length > 1 && payload[1] != 0;
    public byte ChannelBitmask { get; } = payload.Length > 2 ? payload[2] : (byte)0;
    public uint Freq { get; } = payload.Length > 6
        ? BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(3))
        : 0u;

    public TriggerLoggerSetStateResponse(MessageStatus status) : this(new[] { (byte)status }) { }
}

/// <summary>
/// Trigger logger report response (sent by ECU when trigger logger is active).
/// Command: TriggerLogger / Report (class 0x07, command 0x02).
///
/// Payload layout (after status byte):
///   [1 byte entity count LE] [for each entity: 1 byte flags + 2 bytes gap LE]
///   Flags byte: [sync_state:4 bits][state:1 bit][channel_id:3 bits]
/// </summary>
public sealed class ReportResponse : Response
{
    /// <summary>Number of trigger logger entities in this report.</summary>
    public int NumEntries { get; }

    /// <summary>Trigger logger entity entries.</summary>
    public List<Entity> Entries { get; }

    public ReportResponse(ReadOnlySpan<byte> payload)
        : base(WireFormat.ClassTriggerLogger, WireFormat.TriggerLoggerReport, payload.Length > 0 ? (MessageStatus)payload[0] : MessageStatus.Failure, payload.ToArray().AsMemory())
    {
        NumEntries = payload.Length > 1 ? payload[1] : 0;
        Entries = new List<Entity>(NumEntries);

        var offset = 2;
        for (var i = 0; i < NumEntries && offset + 2 < payload.Length; i++)
        {
            var flags = payload[offset++];
            var channelId = (TriggerLoggerChannelId)(flags & 0x07);
            var state = (flags & 0x08) != 0;
            var syncState = (TriggerLoggerSyncState)((flags & 0xF0) >> 4);
            var gap = offset + 1 < payload.Length
                ? BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset))
                : (ushort)0;
            offset += 2;
            Entries.Add(new Entity(channelId, state, gap, syncState));
        }
    }

    /// <summary>A single trigger logger entity with channel, state, gap, and sync state.</summary>
    public readonly struct Entity(
        TriggerLoggerChannelId channel,
        bool state,
        ushort gap,
        TriggerLoggerSyncState syncState)
    {
        /// <summary>The channel ID (crank, cam, etc.).</summary>
        public TriggerLoggerChannelId Channel { get; } = channel;

        /// <summary>Whether this entity is in a triggered state.</summary>
        public bool State { get; } = state;

        /// <summary>The gap value (time/distance between events).</summary>
        public ushort Gap { get; } = gap;

        /// <summary>The synchronization state of this entity.</summary>
        public TriggerLoggerSyncState SyncState { get; } = syncState;
    }
}

using System.Buffers.Binary;
using ME221.Comms.Messages;

namespace ME221.Comms.Internal;

/// <summary>
/// Zero-allocation frame parser for the ME221 ECU wire protocol.
///
/// Wire format:
///   [2 bytes sync "ME"] [2 bytes payload length LE] [1 byte type]
///   [1 byte class] [1 byte command] [N bytes payload] [2 bytes CRC LE]
///
/// All parsing operates on <see cref="Span{T}"/> — no allocations.
/// The caller is responsible for buffering incoming bytes (e.g. from ArrayPool).
/// </summary>
public sealed class FrameParser
{
    /// <summary>
    /// Tries to parse a complete frame from the beginning of <paramref name="data"/>.
    /// Zero allocations on all paths — operates entirely on spans.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> data, out MessageFrame? frame, out int bytesConsumed)
    {
        frame = null;
        bytesConsumed = 0;

        if (data.Length < WireFormat.FixedFrameOverhead)
            return false;

        var syncIndex = FindSyncBytes(data);
        if (syncIndex < 0)
            return false;

        var remaining = data[syncIndex..];

        if (remaining.Length < WireFormat.FixedFrameOverhead)
            return false;

        var payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(
            remaining[WireFormat.SyncLength..]);

        var totalFrameLength = WireFormat.SyncLength
                               + WireFormat.PayloadLengthFieldLength
                               + WireFormat.HeaderLengthAfterSync
                               + payloadLength
                               + WireFormat.CrcLength;

        if (remaining.Length < totalFrameLength)
            return false;

        var messageType = remaining[WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength];
        var messageClass = remaining[WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength + 1];
        var messageCommand = remaining[WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength + 2];

        var payloadStart = WireFormat.SyncLength
                           + WireFormat.PayloadLengthFieldLength
                           + WireFormat.HeaderLengthAfterSync;

        var payloadSpan = remaining[payloadStart..(payloadStart + payloadLength)];

        const int crcCoverageStart = WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength;
        var crcSpan = remaining[crcCoverageStart..totalFrameLength];
        if (!Crc16.Verify(crcSpan))
            return false;

        frame = MessageRegistry.Create(messageType, messageClass, messageCommand, payloadSpan);
        if (frame is null)
            return false;

        bytesConsumed = syncIndex + totalFrameLength;
        return true;
    }

    /// <summary>
    /// Scans for the sync byte pair "ME" (0x4D, 0x45).
    /// Returns the index of the first sync byte, or -1 if not found.
    /// Vectorized-friendly loop — JIT elides bounds checks after first data[i].
    /// </summary>
    internal static int FindSyncBytes(ReadOnlySpan<byte> data)
    {
        for (var i = 0; i < data.Length - 1; i++)
        {
            if (data[i] == WireFormat.SyncByteOne && data[i + 1] == WireFormat.SyncByteTwo)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Calculates how many bytes to discard after a parse failure to avoid
    /// re-scanning from the beginning on the next call.
    /// </summary>
    public static int CalculateSkipAhead(ReadOnlySpan<byte> data, int syncIndex, int payloadLength, int totalFrameLength)
    {
        if (syncIndex < 0)
            return data.Length;

        if (payloadLength < 0)
            return syncIndex + WireFormat.SyncLength;

        if (totalFrameLength < 0)
            return syncIndex + WireFormat.SyncLength;

        return syncIndex + totalFrameLength;
    }
}

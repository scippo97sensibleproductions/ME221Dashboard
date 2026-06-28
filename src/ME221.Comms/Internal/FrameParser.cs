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
    /// </summary>
    /// <param name="data">The buffer containing frame bytes.</param>
    /// <param name="frame">The parsed frame, or <c>null</c> if parsing failed.</param>
    /// <param name="bytesConsumed">
    /// The number of bytes consumed from <paramref name="data"/>.
    /// Zero if no frame was parsed (incomplete, bad sync, or bad CRC).
    /// </param>
    /// <returns>
    /// <c>true</c> if a complete, valid frame was parsed; otherwise <c>false</c>.
    /// </returns>
    public static bool TryParse(ReadOnlySpan<byte> data, out MessageFrame? frame, out int bytesConsumed)
    {
        frame = null;
        bytesConsumed = 0;

        // Need at least: sync(2) + length(2) + type(1) + class(1) + command(1) + crc(2) = 9 bytes minimum
        if (data.Length < WireFormat.FixedFrameOverhead)
            return false;

        // ── Step 1: Find sync bytes ──────────────────────────────────────
        var syncIndex = FindSyncBytes(data);
        if (syncIndex < 0)
            return false;

        var remaining = data[syncIndex..];

        // Need at least the fixed overhead from the sync position
        if (remaining.Length < WireFormat.FixedFrameOverhead)
            return false;

        // ── Step 2: Read payload length (2 bytes LE) ─────────────────────
        var payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(
            remaining[WireFormat.SyncLength..]);

        // Total frame = sync(2) + length(2) + header(3) + payload(N) + crc(2)
        var totalFrameLength = WireFormat.SyncLength
                               + WireFormat.PayloadLengthFieldLength
                               + WireFormat.HeaderLengthAfterSync
                               + payloadLength
                               + WireFormat.CrcLength;

        // Check if we have enough data for a complete frame
        if (remaining.Length < totalFrameLength)
            return false;

        // ── Step 3: Read type, class, command ────────────────────────────
        var messageType = remaining[WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength];
        var messageClass = remaining[WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength + 1];
        var messageCommand = remaining[WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength + 2];

        // ── Step 4: Extract payload span ─────────────────────────────────
        var payloadStart = WireFormat.SyncLength
                           + WireFormat.PayloadLengthFieldLength
                           + WireFormat.HeaderLengthAfterSync;

        var payloadSpan = remaining[payloadStart..(payloadStart + payloadLength)];

        // ── Step 5: Verify CRC ───────────────────────────────────────────
        // CRC covers type + class + command + payload (bytes 4..4+header+payload).
        // The Verify helper expects [data... | crc_lo crc_hi] so we pass
        // exactly the CRC-coverage bytes followed by the 2-byte CRC trailer.
        // Use totalFrameLength to avoid trailing data past the frame boundary.
        const int crcCoverageStart = WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength;
        var crcSpan = remaining[crcCoverageStart..totalFrameLength];
        if (!Crc16.Verify(crcSpan))
            return false;

        // ── Step 6: Create the message frame ─────────────────────────────
        frame = MessageRegistry.Create(messageType, messageClass, messageCommand, payloadSpan);
        if (frame is null)
            return false;

        bytesConsumed = syncIndex + totalFrameLength;
        return true;
    }

    /// <summary>
    /// Scans <paramref name="data"/> for the sync byte pair "ME" (0x4D, 0x45).
    /// Returns the index of the first sync byte, or -1 if not found.
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
    /// <param name="data">The original data buffer.</param>
    /// <param name="syncIndex">The index where sync bytes were found (-1 if not found).</param>
    /// <param name="payloadLength">The declared payload length (-1 if not readable).</param>
    /// <param name="totalFrameLength">The total frame length (-1 if incomplete).</param>
    /// <returns>
    /// Number of bytes to skip. Returns <paramref name="data"/>.Length if the sync
    /// bytes were not found (force a full rescan).
    /// </returns>
    public static int CalculateSkipAhead(ReadOnlySpan<byte> data, int syncIndex, int payloadLength, int totalFrameLength)
    {
        if (syncIndex < 0)
            return data.Length; // not found — force full rescan

        if (payloadLength < 0)
            return syncIndex + WireFormat.SyncLength; // found sync but can't read length

        if (totalFrameLength < 0)
            return syncIndex + WireFormat.SyncLength; // found sync but frame incomplete

        return syncIndex + totalFrameLength; // skip past this frame and retry
    }
}

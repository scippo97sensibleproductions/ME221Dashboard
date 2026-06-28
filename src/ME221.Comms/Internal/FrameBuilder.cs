using System.Buffers;
using System.Buffers.Binary;
using ME221.Comms.Messages;

namespace ME221.Comms.Internal;

/// <summary>
/// Zero-allocation frame builder for the ME221 ECU wire protocol.
/// Writes frame bytes into a pooled buffer — no intermediate arrays.
/// </summary>
public static class FrameBuilder
{
    /// <summary>
    /// Builds a complete wire-frame byte array from a <paramref name="frame"/>.
    /// Uses <see cref="ArrayPool{T}"/> for zero-allocation in hot paths.
    /// </summary>
    /// <param name="frame">The message frame to serialize.</param>
    /// <returns>
    /// A <see cref="PooledMemory"/> wrapping the pooled buffer.
    /// The caller MUST dispose it (e.g. <c>using</c>) to return the buffer to the pool.
    /// </returns>
    public static PooledMemory Build(MessageFrame frame)
    {
        // Calculate total frame length
        var payloadLength = frame.Payload.Length;
        var totalLength = WireFormat.FixedFrameOverhead + payloadLength;

        // Get a pooled buffer
        var pool = ArrayPool<byte>.Shared.Rent(totalLength);
        var buffer = pool.AsSpan(0, totalLength);

        BuildIntoBuffer(frame, buffer);

        return new PooledMemory(pool, totalLength);
    }

    /// <summary>
    /// Wraps a rented <see cref="ArrayPool{T}"/> buffer so it can be returned to the pool
    /// when disposed. Avoids the extra <c>.ToArray()</c> copy that would defeat pooling.
    /// </summary>
    public readonly struct PooledMemory : IDisposable
    {
        private readonly byte[] _pool;
        private readonly int _length;

        internal PooledMemory(byte[] pool, int length)
        {
            _pool = pool;
            _length = length;
        }

        /// <summary>Zero-allocation view of the pooled bytes.</summary>
        public ReadOnlyMemory<byte> Memory => _pool.AsMemory(0, _length);

        /// <summary>Returns the buffer to the shared array pool.</summary>
        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_pool);
        }
    }

    /// <summary>
    /// Builds a frame into the provided <paramref name="buffer"/>, returning the number of bytes written.
    /// This overload avoids pool allocation entirely when the caller provides a pre-sized buffer.
    /// </summary>
    /// <param name="frame">The message frame to serialize.</param>
    /// <param name="buffer">The output buffer (must be at least <see cref="WireFormat.FixedFrameOverhead"/> + payload length bytes).</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="buffer"/>.</param>
    /// <returns><c>true</c> if the frame was built successfully; otherwise <c>false</c>.</returns>
    public static bool BuildIntoBuffer(MessageFrame frame, Span<byte> buffer, out int bytesWritten)
    {
        var payloadLength = frame.Payload.Length;
        var totalLength = WireFormat.FixedFrameOverhead + payloadLength;

        if (buffer.Length < totalLength)
        {
            bytesWritten = 0;
            return false;
        }

        BuildIntoBuffer(frame, buffer);
        bytesWritten = totalLength;
        return true;
    }

    /// <summary>
    /// Internal method that writes frame bytes into the buffer.
    /// </summary>
    private static void BuildIntoBuffer(MessageFrame frame, Span<byte> buffer)
    {
        var payloadLength = frame.Payload.Length;

        // ── Step 1: Write sync bytes ─────────────────────────────────────
        buffer[0] = WireFormat.SyncByteOne;
        buffer[1] = WireFormat.SyncByteTwo;

        // ── Step 2: Write payload length (2 bytes LE) ────────────────────
        BinaryPrimitives.WriteUInt16LittleEndian(
            buffer[WireFormat.SyncLength..], (ushort)payloadLength);

        // ── Step 3: Write type, class, command ───────────────────────────
        buffer[WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength] = frame.Type;
        buffer[WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength + 1] = frame.Class;
        buffer[WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength + 2] = frame.Command;

        // ── Step 4: Write payload ────────────────────────────────────────
        var payloadOffset = WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength + WireFormat.HeaderLengthAfterSync;
        frame.Payload.Span.CopyTo(buffer[payloadOffset..]);

        // ── Step 5: Compute and write CRC ────────────────────────────────
        // CRC covers type + class + command + payload
        const int crcCoverageStart = WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength;
        var crcCoverageLength = WireFormat.HeaderLengthAfterSync + payloadLength;

        var crc = Crc16.Compute(buffer.Slice(crcCoverageStart, crcCoverageLength));
        BinaryPrimitives.WriteUInt16LittleEndian(
            buffer[(crcCoverageStart + crcCoverageLength)..], crc);
    }
}

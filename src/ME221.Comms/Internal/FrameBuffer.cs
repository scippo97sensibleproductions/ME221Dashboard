using System.Buffers;
using System.Buffers.Binary;
using ME221.Comms.Messages;

namespace ME221.Comms.Internal;

public sealed class FrameBuffer(int bufferSize = 8192) : IDisposable
{
    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
    private int _bufferIndex;

    public Span<byte> AppendSpan => _buffer.AsSpan(_bufferIndex);
    public Memory<byte> AppendMemory => _buffer.AsMemory(_bufferIndex);

    public void Advance(int bytesRead) => _bufferIndex += bytesRead;

    public void Reset()
    {
        _bufferIndex = 0;
    }

    public int BufferedLength => _bufferIndex;

    public bool TryExtractFrame(out MessageFrame? frame)
    {
        while (true)
        {
            if (_bufferIndex == 0)
            {
                frame = null;
                return false;
            }

            var bufferSpan = _buffer.AsSpan(0, _bufferIndex);

            var syncIndex = FrameParser.FindSyncBytes(bufferSpan);

            if (syncIndex < 0)
            {
                var lastMIndex = bufferSpan.LastIndexOf(WireFormat.SyncByteOne);
                if (lastMIndex >= 0)
                {
                    var remaining = _bufferIndex - lastMIndex;
                    bufferSpan.Slice(lastMIndex, remaining).CopyTo(_buffer);
                    _bufferIndex = remaining;
                }
                else
                {
                    _bufferIndex = 0;
                }
                frame = null;
                return false;
            }

            if (syncIndex > 0)
            {
                var remaining = _bufferIndex - syncIndex;
                bufferSpan.Slice(syncIndex, remaining).CopyTo(_buffer);
                _bufferIndex = remaining;
                continue;
            }

            const int lengthFieldEnd = WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength;
            if (_bufferIndex < lengthFieldEnd)
            {
                frame = null;
                return false;
            }

            var payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(
                bufferSpan.Slice(WireFormat.SyncLength, WireFormat.PayloadLengthFieldLength));

            var totalLength = WireFormat.FixedFrameOverhead + payloadLength;

            if (_bufferIndex < totalLength)
            {
                frame = null;
                return false;
            }

            if (FrameParser.TryParse(bufferSpan, out frame, out var bytesConsumed))
            {
                var remaining = _bufferIndex - bytesConsumed;
                if (remaining > 0)
                    bufferSpan.Slice(bytesConsumed, remaining).CopyTo(_buffer);
                _bufferIndex = remaining;
                return true;
            }

            const int skipBytes = WireFormat.SyncLength;
            var nextRemaining = _bufferIndex - skipBytes;
            if (nextRemaining > 0)
                bufferSpan.Slice(skipBytes, nextRemaining).CopyTo(_buffer);
            _bufferIndex = nextRemaining;
        }
    }

    public byte[] GetBuffer() => _buffer;

    public void Dispose()
    {
        var toReturn = _buffer;
        _buffer = null!;
        if (toReturn is not null)
            ArrayPool<byte>.Shared.Return(toReturn);
    }
}

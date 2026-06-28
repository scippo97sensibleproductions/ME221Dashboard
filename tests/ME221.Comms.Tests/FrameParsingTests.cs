using FluentAssertions;
using ME221.Comms.Internal;
using Xunit;

namespace ME221.Comms.Tests;

public class FrameParsingTests
{
    [Fact]
    public void TryParse_EmptySpan_ReturnsFalse()
    {
        // Act
        var result = FrameParser.TryParse(ReadOnlySpan<byte>.Empty, out _, out var bytesConsumed);

        // Assert
        result.Should().BeFalse();
        bytesConsumed.Should().Be(0);
    }

    [Fact]
    public void TryParse_TooShort_ReturnsFalse()
    {
        // Arrange
        var data = new byte[] { 0x4D, 0x45, 0x00, 0x00 }; // Only 4 bytes, need at least 9

        // Act
        var result = FrameParser.TryParse(data, out _, out var bytesConsumed);

        // Assert
        result.Should().BeFalse();
        bytesConsumed.Should().Be(0);
    }

    [Fact]
    public void TryParse_WrongSyncBytes_ReturnsFalse()
    {
        // Arrange
        var data = new byte[]
        {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 // All zeros, no sync
        };

        // Act
        var result = FrameParser.TryParse(data, out _, out var bytesConsumed);

        // Assert
        result.Should().BeFalse();
        bytesConsumed.Should().Be(0);
    }

    [Fact]
    public void TryParse_BadCrc_ReturnsFalse()
    {
        // Arrange — build a frame with bad CRC
        var payload = new byte[] { 0x00, 0x04, 0x00 }; // Request, Sys, GetECUInfo
        var crc = Crc16.Compute(payload);
        var crcBytes = new byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(crcBytes, (ushort)(crc + 1)); // Corrupt CRC
        var frameBytes = new byte[WireFormat.FixedFrameOverhead + payload.Length];
        frameBytes[0] = WireFormat.SyncByteOne;
        frameBytes[1] = WireFormat.SyncByteTwo;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frameBytes.AsSpan(2), (ushort)payload.Length);
        payload.CopyTo(frameBytes.AsSpan(4));
        crcBytes.CopyTo(frameBytes.AsSpan(4 + payload.Length));

        // Act
        var result = FrameParser.TryParse(frameBytes, out _, out var bytesConsumed);

        // Assert
        result.Should().BeFalse();
        bytesConsumed.Should().Be(0);
    }

    [Fact]
    public void TryParse_IncompleteFrame_ReturnsFalse()
    {
        // Arrange — frame with correct sync but truncated payload
        var data = new byte[]
        {
            0x4D, 0x45,                    // Sync "ME"
            0x10, 0x00,                    // Payload length = 16
            0x00, 0x04, 0x00,              // Request, Sys, GetECUInfo
            // Missing: 13 bytes of payload + 2 bytes CRC
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00
        };

        // Act
        var result = FrameParser.TryParse(data, out _, out var bytesConsumed);

        // Assert
        result.Should().BeFalse();
        bytesConsumed.Should().Be(0);
    }

    [Fact]
    public void TryParse_UnknownMessageType_ReturnsFalse()
    {
        // Arrange — valid frame structure but no registered handler
        var payload = Array.Empty<byte>();
        var crc = Crc16.Compute(new byte[] { 0x00, 0xFF, 0xFF }); // Unknown type/class/command
        var crcBytes = new byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(crcBytes, crc);
        var frameBytes = new byte[WireFormat.FixedFrameOverhead];
        frameBytes[0] = WireFormat.SyncByteOne;
        frameBytes[1] = WireFormat.SyncByteTwo;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frameBytes.AsSpan(2), (ushort)payload.Length);
        frameBytes[4] = 0x00; // Request
        frameBytes[5] = 0xFF; // Unknown class
        frameBytes[6] = 0xFF; // Unknown command
        crcBytes.CopyTo(frameBytes.AsSpan(7));

        // Act
        var result = FrameParser.TryParse(frameBytes, out _, out var bytesConsumed);

        // Assert
        result.Should().BeFalse();
        bytesConsumed.Should().Be(0);
    }

    [Fact]
    public void TryParse_DataBeforeSync_SkipsToSync()
    {
        // Arrange — garbage bytes before valid sync
        var payload = Array.Empty<byte>();
        var header = new byte[] { 0x00, 0x04, 0x00 }; // Request, Sys, GetECUInfo
        var crc = Crc16.Compute(header);
        var crcBytes = new byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(crcBytes, crc);

        var frameBytes = new byte[5 + WireFormat.FixedFrameOverhead];
        frameBytes[0] = 0xAA; // Garbage
        frameBytes[1] = 0xBB; // Garbage
        frameBytes[2] = WireFormat.SyncByteOne;
        frameBytes[3] = WireFormat.SyncByteTwo;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frameBytes.AsSpan(4), (ushort)payload.Length);
        header.CopyTo(frameBytes.AsSpan(6));
        crcBytes.CopyTo(frameBytes.AsSpan(9));

        // Act
        var result = FrameParser.TryParse(frameBytes, out var frame, out var bytesConsumed);

        // Assert
        result.Should().BeTrue();
        bytesConsumed.Should().BeGreaterThan(0);
        frame.Should().NotBeNull();
    }
}

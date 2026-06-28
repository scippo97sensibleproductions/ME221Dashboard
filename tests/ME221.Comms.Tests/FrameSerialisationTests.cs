using FluentAssertions;
using ME221.Comms.Internal;
using ME221.Comms.Messages;
using Xunit;

namespace ME221.Comms.Tests;

public class FrameSerialisationTests
{
    [Fact]
    public void BuildAndParse_RoundTripGetEcuInfoRequest_PreservesFields()
    {
        // Arrange
        var request = new GetEcuInfoRequest();
        using var pooled = FrameBuilder.Build(request);
        var frameBytes = pooled.Memory.ToArray();

        // Act
        var result = FrameParser.TryParse(frameBytes, out var parsedFrame, out _);

        // Assert
        result.Should().BeTrue();
        parsedFrame.Should().BeOfType<GetEcuInfoRequest>();
        parsedFrame!.Type.Should().Be(WireFormat.RequestType);
        parsedFrame.Class.Should().Be(WireFormat.ClassSys);
        parsedFrame.Command.Should().Be(WireFormat.SysGetEcuInfo);
    }

    [Fact]
    public void BuildAndParse_RoundTripGetDriverRequest_PreservesFields()
    {
        // Arrange
        var request = new GetDriverRequest(42);
        using var pooled = FrameBuilder.Build(request);
        var frameBytes = pooled.Memory.ToArray();

        // Act
        var result = FrameParser.TryParse(frameBytes, out var parsedFrame, out _);

        // Assert
        result.Should().BeTrue();
        parsedFrame.Should().BeOfType<GetDriverRequest>();
        var parsed = (GetDriverRequest)parsedFrame!;
        parsed.DriverId.Should().Be(42);
    }

    [Fact]
    public void BuildAndParse_RoundTripSetTableRequest_PreservesPayload()
    {
        // Arrange
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var request = new SetTableRequest(100, payload);
        using var pooled = FrameBuilder.Build(request);
        var frameBytes = pooled.Memory.ToArray();

        // Act
        var result = FrameParser.TryParse(frameBytes, out var parsedFrame, out _);

        // Assert
        result.Should().BeTrue();
        parsedFrame.Should().BeOfType<SetTableRequest>();
        var parsed = (SetTableRequest)parsedFrame!;
        parsed.TableId.Should().Be(100);
        // Wire payload is [tableId(2B) | dataSize(2B LE) | data...]; check data portion
        var fullPayload = parsed.Payload.ToArray();
        var dataSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(fullPayload.AsSpan(2));
        dataSize.Should().Be((ushort)payload.Length);
        var data = fullPayload[4..];
        data.Should().Equal(payload);
    }

    [Fact]
    public void BuildAndParse_RoundTripGetHashRequest_PreservesMode()
    {
        // Arrange
        var request = new GetHashRequest(HashRequestMode.Overall);
        using var pooled = FrameBuilder.Build(request);
        var frameBytes = pooled.Memory.ToArray();

        // Act
        var result = FrameParser.TryParse(frameBytes, out var parsedFrame, out _);

        // Assert
        result.Should().BeTrue();
        parsedFrame.Should().BeOfType<GetHashRequest>();
        var parsed = (GetHashRequest)parsedFrame!;
        parsed.Mode.Should().Be(HashRequestMode.Overall);
    }

    [Fact]
    public void BuildAndParse_RoundTripEmptyPayload_PreservesEmptyPayload()
    {
        // Arrange
        var request = new FactoryResetRequest();
        using var pooled = FrameBuilder.Build(request);
        var frameBytes = pooled.Memory.ToArray();

        // Act
        var result = FrameParser.TryParse(frameBytes, out var parsedFrame, out _);

        // Assert
        result.Should().BeTrue();
        parsedFrame.Should().BeOfType<FactoryResetRequest>();
        parsedFrame!.Payload.Length.Should().Be(0);
    }

    [Fact]
    public void Build_FrameHasCorrectSyncBytes()
    {
        // Arrange
        var request = new GetEcuInfoRequest();

        // Act
        using var frameBytes = FrameBuilder.Build(request);

        // Assert
        frameBytes.Memory.Span[0].Should().Be(WireFormat.SyncByteOne);
        frameBytes.Memory.Span[1].Should().Be(WireFormat.SyncByteTwo);
    }

    [Fact]
    public void Build_FrameHasCorrectPayloadLength()
    {
        // Arrange
        var payload = new byte[] { 0x01, 0x02, 0x03 };
        var request = new SetDriverRequest(1, payload);

        // Act
        using var frameBytes = FrameBuilder.Build(request);

        // Assert
        var payloadLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
            frameBytes.Memory.Span[WireFormat.SyncLength..]);
        // Wire payload = 2B entity ID + 2B dataSize + data
        payloadLength.Should().Be((ushort)(2 + 2 + payload.Length));
    }

    [Fact]
    public void Build_FrameHasValidCrc()
    {
        // Arrange
        var request = new SetTableRequest(1, new byte[] { 0x01, 0x02 });

        // Act — Crc16.Verify expects [type|class|cmd|payload | crc_lo crc_hi],
        // i.e. the CRC-coverage bytes plus the 2-byte CRC trailer (frame[4..]).
        using var frameBytes = FrameBuilder.Build(request);
        var crcStart = WireFormat.SyncLength + WireFormat.PayloadLengthFieldLength;
        var isValid = Crc16.Verify(frameBytes.Memory.Span[crcStart..]);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void BuildIntoBuffer_WithSufficientBuffer_ReturnsTrue()
    {
        // Arrange
        var request = new GetEcuInfoRequest();
        var buffer = new byte[1024];

        // Act
        var result = FrameBuilder.BuildIntoBuffer(request, buffer, out var bytesWritten);

        // Assert
        result.Should().BeTrue();
        bytesWritten.Should().BeGreaterThan(0);
    }

    [Fact]
    public void BuildIntoBuffer_WithInsufficientBuffer_ReturnsFalse()
    {
        // Arrange
        var request = new SetTableRequest(1, new byte[100]);
        var buffer = new byte[5]; // Too small

        // Act
        var result = FrameBuilder.BuildIntoBuffer(request, buffer, out var bytesWritten);

        // Assert
        result.Should().BeFalse();
        bytesWritten.Should().Be(0);
    }
}

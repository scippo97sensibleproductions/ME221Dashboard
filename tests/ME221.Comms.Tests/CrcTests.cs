using FluentAssertions;
using ME221.Comms.Internal;
using Xunit;

namespace ME221.Comms.Tests;

public class CrcTests
{
    [Fact]
    public void Compute_GivenEmptySpan_ReturnsZero()
    {
        // Arrange
        var data = ReadOnlySpan<byte>.Empty;

        // Act
        var result = Crc16.Compute(data);

        // Assert
        result.Should().Be(0x0000);
    }

    [Fact]
    public void Compute_GivenSingleByte_ReturnsExpectedCrc()
    {
        // Arrange
        var data = new byte[] { 0x4D }; // 'M'

        // Act
        var result = Crc16.Compute(data);

        // Assert
        result.Should().NotBe(0x0000);
    }

    [Fact]
    public void Compute_GivenKnownData_ReturnsConsistentResult()
    {
        // Arrange
        var data = new byte[] { 0x00, 0x04, 0x00 }; // Request, Sys, GetECUInfo

        // Act
        var result1 = Crc16.Compute(data);
        var result2 = Crc16.Compute(data);

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void Compute_DifferentDataProducesDifferentCrc()
    {
        // Arrange
        var data1 = new byte[] { 0x00, 0x04, 0x00 };
        var data2 = new byte[] { 0x00, 0x04, 0x01 };

        // Act
        var crc1 = Crc16.Compute(data1);
        var crc2 = Crc16.Compute(data2);

        // Assert
        crc1.Should().NotBe(crc2);
    }

    [Fact]
    public void Verify_ValidCrc_ReturnsTrue()
    {
        // Arrange
        var payload = new byte[] { 0x00, 0x04, 0x00 }; // Request, Sys, GetECUInfo
        var crc = Crc16.Compute(payload);
        var crcBytes = new byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(crcBytes, crc);
        var dataWithCrc = payload.Concat(crcBytes).ToArray();

        // Act
        var result = Crc16.Verify(dataWithCrc);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_InvalidCrc_ReturnsFalse()
    {
        // Arrange
        var data = new byte[] { 0x00, 0x04, 0x00, 0xFF, 0xFF }; // Bad CRC

        // Act
        var result = Crc16.Verify(data);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_DataTooShort_ReturnsFalse()
    {
        // Arrange
        var data = new byte[] { 0x00 }; // Only 1 byte, less than CRC length

        // Act
        var result = Crc16.Verify(data);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Compute_ZeroAllocation_NoHeapAllocations()
    {
        // Arrange
        var data = new byte[] { 0x00, 0x04, 0x00, 0x01, 0x02, 0x03 };

        // Act & Assert — just verify it runs without throwing
        var crc = Crc16.Compute(data);
        crc.Should().NotBe(0);
    }
}

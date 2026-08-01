using System.Buffers.Binary;
using FluentAssertions;
using ME221.Comms;
using ME221.Comms.Messages;
using ME221Dashboard.Comms;
using Xunit;

namespace ME221Dashboard.Comms.Tests;

public class ReportParserTests
{
    private static ReportEntity[] Parse(byte[] payload, params (ushort Id, ReportingType Type, int Size)[] map)
    {
        var buffer = new ReportEntity[map.Length];
        var count = ReportParser.ParseV2Report(payload, buffer, map);
        return buffer.Take(count).ToArray();
    }

    [Fact]
    public void ParseV2Report_EmptyPayload_ReturnsZero()
    {
        var result = Parse([], (1, ReportingType.Float4B, 4));

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseV2Report_FailureStatus_ReturnsZero()
    {
        var payload = new byte[5];
        payload[0] = (byte)MessageStatus.Failure;

        var result = Parse(payload, (1, ReportingType.Float4B, 4));

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseV2Report_Float4B_ReadsLittleEndianFloat()
    {
        var payload = new byte[5];
        payload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(1), 123.5f);

        var result = Parse(payload, (7, ReportingType.Float4B, 4));

        result.Should().ContainSingle();
        result[0].Id.Should().Be(7);
        result[0].Value.Should().BeApproximately(123.5f, 0.001f);
    }

    [Fact]
    public void ParseV2Report_Int2B_ReadsSignedValue()
    {
        var payload = new byte[3];
        payload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(1), -1234);

        var result = Parse(payload, (1, ReportingType.Int2B, 2));

        result.Single().Value.Should().Be(-1234);
    }

    [Fact]
    public void ParseV2Report_Uint2B_ReadsUnsignedValue()
    {
        var payload = new byte[3];
        payload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), 60000);

        var result = Parse(payload, (1, ReportingType.Uint2B, 2));

        result.Single().Value.Should().Be(60000);
    }

    [Fact]
    public void ParseV2Report_Int1B_ReadsSignedByte()
    {
        var payload = new byte[] { (byte)MessageStatus.Success, 0xFE }; // -2

        var result = Parse(payload, (1, ReportingType.Int1B, 1));

        result.Single().Value.Should().Be(-2);
    }

    [Fact]
    public void ParseV2Report_Uint1B_ReadsUnsignedByte()
    {
        var payload = new byte[] { (byte)MessageStatus.Success, 0xFE };

        var result = Parse(payload, (1, ReportingType.Uint1B, 1));

        result.Single().Value.Should().Be(254);
    }

    [Fact]
    public void ParseV2Report_Bool1B_ReadsBoolean()
    {
        var payload = new byte[] { (byte)MessageStatus.Success, 0x01 };

        var result = Parse(payload, (1, ReportingType.Bool1B, 1));

        result.Single().Value.Should().Be(1f);
    }

    [Fact]
    public void ParseV2Report_UnknownType_FallsBackToFloat()
    {
        var payload = new byte[5];
        payload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(1), 42f);

        var result = Parse(payload, (1, (ReportingType)0xFF, 4));

        result.Single().Value.Should().Be(42f);
    }

    [Fact]
    public void ParseV2Report_MixedTypes_ReadsInOrder()
    {
        var payload = new byte[1 + 4 + 2 + 1];
        payload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(1), 1.5f);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(5), -10);
        payload[7] = 200;

        var result = Parse(payload,
            (10, ReportingType.Float4B, 4),
            (20, ReportingType.Int2B, 2),
            (30, ReportingType.Uint1B, 1));

        result.Should().HaveCount(3);
        result[0].Id.Should().Be(10);
        result[0].Value.Should().BeApproximately(1.5f, 0.001f);
        result[1].Id.Should().Be(20);
        result[1].Value.Should().Be(-10);
        result[2].Id.Should().Be(30);
        result[2].Value.Should().Be(200);
    }

    [Fact]
    public void ParseV2Report_TruncatedTail_StopsAtLastCompleteEntity()
    {
        var payload = new byte[1 + 4 + 1]; // one full float + 1 leftover byte
        payload[0] = (byte)MessageStatus.Success;
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(1), 9f);

        var result = Parse(payload,
            (1, ReportingType.Float4B, 4),
            (2, ReportingType.Float4B, 4));

        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
    }

    [Fact]
    public void ParseV2Report_FillsBufferUpToCapacity()
    {
        var payload = new byte[1 + 4 * 3];
        payload[0] = (byte)MessageStatus.Success;
        for (var i = 0; i < 3; i++)
            BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(1 + i * 4), i);

        var buffer = new ReportEntity[2];
        var count = ReportParser.ParseV2Report(payload, buffer,
            [(1, ReportingType.Float4B, 4), (2, ReportingType.Float4B, 4), (3, ReportingType.Float4B, 4)]);

        count.Should().Be(2);
        buffer[0].Id.Should().Be(1);
        buffer[1].Id.Should().Be(2);
    }
}

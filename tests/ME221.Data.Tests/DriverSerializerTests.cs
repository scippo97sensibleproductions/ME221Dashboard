using System.Buffers.Binary;
using FluentAssertions;
using ME221.Data.Infrastructure;
using Xunit;

namespace ME221.Data.Tests;

public class DriverSerializerTests
{
    [Fact]
    public void RoundTrip_EmptyArrays_ProducesCorrectHeader()
    {
        var configs = Array.Empty<float>();
        var outputs = new List<ushort>();
        var inputs = new List<ushort>();

        var bytes = DriverSerializer.Serialize(configs, outputs, inputs);
        bytes.Should().HaveCount(3); // configCount(1) + outputCount(1) + inputCount(1)
        bytes[0].Should().Be(0);     // configCount
        bytes[1].Should().Be(0);     // outputCount
        bytes[2].Should().Be(0);     // inputCount

        var result = DriverSerializer.Deserialize(bytes);
        result.Configs.Should().BeEmpty();
        result.OutputIds.Should().BeEmpty();
        result.InputIds.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_ConfigsOnly_PreservesValues()
    {
        var configs = new float[] { 1.5f, -3.25f, 100.0f };
        var outputs = new List<ushort>();
        var inputs = new List<ushort>();

        var bytes = DriverSerializer.Serialize(configs, outputs, inputs);
        var result = DriverSerializer.Deserialize(bytes);

        result.Configs.Should().HaveCount(3);
        result.Configs[0].Should().Be(1.5f);
        result.Configs[1].Should().Be(-3.25f);
        result.Configs[2].Should().Be(100.0f);
        result.OutputIds.Should().BeEmpty();
        result.InputIds.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_AllData_PreservesValues()
    {
        var configs = new float[] { 1.0f, 2.0f };
        var outputs = new List<ushort> { 100, 200, 300 };
        var inputs = new List<ushort> { 400, 500 };

        var bytes = DriverSerializer.Serialize(configs, outputs, inputs);
        var result = DriverSerializer.Deserialize(bytes);

        result.Configs.Should().HaveCount(2);
        result.Configs[0].Should().Be(1.0f);
        result.Configs[1].Should().Be(2.0f);
        result.OutputIds.Should().HaveCount(3);
        result.OutputIds.Should().Equal(100, 200, 300);
        result.InputIds.Should().HaveCount(2);
        result.InputIds.Should().Equal(400, 500);
    }

    [Fact]
    public void RoundTrip_MaxRealisticSize_ProducesCorrectByteCount()
    {
        var configs = new float[34];
        var outputs = new List<ushort>(8);
        var inputs = new List<ushort>(8);

        for (var i = 0; i < 34; i++) configs[i] = i * 0.5f;
        for (var i = 0; i < 8; i++) outputs.Add((ushort)(i * 100));
        for (var i = 0; i < 8; i++) inputs.Add((ushort)(i * 200));

        var bytes = DriverSerializer.Serialize(configs, outputs, inputs);
        var expectedLength = 3 + 34 * 4 + 8 * 2 + 8 * 2; // header(3) + data
        bytes.Should().HaveCount(expectedLength);

        var result = DriverSerializer.Deserialize(bytes);
        result.Configs.Should().HaveCount(34);
        result.OutputIds.Should().HaveCount(8);
        result.InputIds.Should().HaveCount(8);
    }

    [Fact]
    public void Deserialize_ZeroLength_ThrowsArgumentException()
    {
        var act = () => DriverSerializer.Deserialize(ReadOnlySpan<byte>.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RoundTrip_PreservesFloatPrecision()
    {
        var configs = new float[] { 0.001f, 99.99f, float.MinValue / 1000f };
        var bytes = DriverSerializer.Serialize(configs, [], []);
        var result = DriverSerializer.Deserialize(bytes);

        result.Configs[0].Should().Be(0.001f);
        result.Configs[1].Should().Be(99.99f);
        result.Configs[2].Should().Be(float.MinValue / 1000f);
    }

    // ─── Edge-case tests ─────────────────────────────────────────────────────

    [Fact]
    public void Serialize_SingleConfig_ProducesCorrectHeader()
    {
        var bytes = DriverSerializer.Serialize(new float[] { 42.0f }, [], []);
        bytes.Length.Should().Be(3 + 4); // header(3) + 1 float
        bytes[0].Should().Be(1);         // configCount
        bytes[1].Should().Be(0);         // outputCount
        bytes[2].Should().Be(0);         // inputCount
    }

    [Fact]
    public void RoundTrip_OutputOnly_NoConfigsNoInputs()
    {
        var outputs = new List<ushort> { 1, 2, 3, 4, 5 };
        var bytes = DriverSerializer.Serialize([], outputs, []);
        var result = DriverSerializer.Deserialize(bytes);

        result.Configs.Should().BeEmpty();
        result.OutputIds.Should().Equal(new ushort[] { 1, 2, 3, 4, 5 });
        result.InputIds.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_InputOnly_NoConfigsNoOutputs()
    {
        var inputs = new List<ushort> { 100, 200 };
        var bytes = DriverSerializer.Serialize([], [], inputs);
        var result = DriverSerializer.Deserialize(bytes);

        result.Configs.Should().BeEmpty();
        result.OutputIds.Should().BeEmpty();
        result.InputIds.Should().Equal(new ushort[] { 100, 200 });
    }

    [Fact]
    public void RoundTrip_BoundaryUshortValues_PreservesExactly()
    {
        var outputs = new List<ushort> { 0, ushort.MaxValue, 1, 32768 };
        var bytes = DriverSerializer.Serialize([], outputs, []);
        var result = DriverSerializer.Deserialize(bytes);

        result.OutputIds.Should().Equal(new ushort[] { 0, ushort.MaxValue, 1, 32768 });
    }

    [Fact]
    public void RoundTrip_SpecialFloatValues_PreservesBitPattern()
    {
        var configs = new float[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0.0f, -0.0f };
        var bytes = DriverSerializer.Serialize(configs, [], []);
        var result = DriverSerializer.Deserialize(bytes);

        float.IsNaN(result.Configs[0]).Should().BeTrue();
        result.Configs[1].Should().Be(float.PositiveInfinity);
        result.Configs[2].Should().Be(float.NegativeInfinity);
        result.Configs[3].Should().Be(0.0f);
        float.IsNegative(result.Configs[4]).Should().BeTrue();
    }

    [Fact]
    public void Deserialize_TruncatedData_ThrowsArgumentException()
    {
        var bytes = new byte[3];
        bytes[0] = 10; // configCount = 10
        bytes[1] = 0;
        bytes[2] = 0;

        var act = () => DriverSerializer.Deserialize(bytes);
        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("too short");
    }

    [Fact]
    public void Deserialize_HeaderClaimsOutputsButDataMissing_ThrowsArgumentException()
    {
        var bytes = new byte[3];
        bytes[0] = 0;  // configCount = 0
        bytes[1] = 3;  // outputCount = 3
        bytes[2] = 0;  // inputCount

        var act = () => DriverSerializer.Deserialize(bytes);
        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("too short");
    }

    [Fact]
    public void Deserialize_ExtraBytesAtEnd_Ignored()
    {
        var configs = new float[] { 1.0f };
        var bytes = DriverSerializer.Serialize(configs, [], []);
        var padded = new byte[bytes.Length + 10];
        bytes.CopyTo(padded, 0);

        var result = DriverSerializer.Deserialize(padded);
        result.Configs.Should().HaveCount(1);
        result.Configs[0].Should().Be(1.0f);
    }

    [Fact]
    public void RoundTrip_LargeNumberOfOutputs_PreservesOrder()
    {
        var outputs = new List<ushort>();
        for (ushort i = 0; i < 50; i++) outputs.Add(i);

        var bytes = DriverSerializer.Serialize([], outputs, []);
        var result = DriverSerializer.Deserialize(bytes);

        result.OutputIds.Should().HaveCount(50);
        result.OutputIds.Should().BeInAscendingOrder();
    }

    [Fact]
    public void ByteLayout_MatchesRealEcuFormat()
    {
        var configs = new float[] { 1.0f };
        var outputs = new List<ushort> { 200 };
        var inputs = new List<ushort> { 300 };

        var bytes = DriverSerializer.Serialize(configs, outputs, inputs);

        bytes[0].Should().Be(1);   // configCount
        bytes[1].Should().Be(1);   // outputCount
        bytes[2].Should().Be(1);   // inputCount

        // Config at offset 3-6 (LE float 1.0)
        BitConverter.ToSingle(bytes.AsSpan(3)).Should().Be(1.0f);

        // Output at offset 7-8 (LE ushort 200)
        BitConverter.ToUInt16(bytes.AsSpan(7)).Should().Be(200);

        // Input at offset 9-10 (LE ushort 300)
        BitConverter.ToUInt16(bytes.AsSpan(9)).Should().Be(300);
    }

    [Fact]
    public void Deserialize_MatchesRealEcuIdleSettings()
    {
        // Real V1 wire format: [configCount:1][outputCount:1][inputCount:1][data...]
        // Idle Settings (0x2006): 32 configs, 9 outputs, 9 inputs
        var data = new byte[3 + 32 * 4 + 9 * 2 + 9 * 2]; // = 169 bytes
        data[0] = 32;  // configCount
        data[1] = 9;   // outputCount
        data[2] = 9;   // inputCount

        for (var i = 0; i < 32; i++)
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(3 + i * 4), 1.0f);

        var result = DriverSerializer.Deserialize(data);
        result.Configs.Length.Should().Be(32);
        result.OutputIds.Count.Should().Be(9);
        result.InputIds.Count.Should().Be(9);
    }

    [Fact]
    public void GetSerializedSize_ReturnsCorrectValue()
    {
        var size = DriverSerializer.GetSerializedSize(32, 9, 9);
        size.Should().Be(3 + 32 * 4 + 9 * 2 + 9 * 2); // 169
    }
}

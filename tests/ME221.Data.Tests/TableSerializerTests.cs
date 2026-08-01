using FluentAssertions;
using ME221.Data.Infrastructure;
using ME221.Data.Models;
using Xunit;

namespace ME221.Data.Tests;

public class TableSerializerTests
{
    private static TableDefinition MakeTable(string type, ushort rows, ushort cols) => new()
    {
        Id = 1,
        TableType = type,
        Rows = rows,
        Cols = cols,
        IncrementValue = 0.1f,
    };

    [Fact]
    public void Serialize_16x16_WritesTypeByteTwo()
    {
        var table = MakeTable("T16x16", 16, 16);
        var bytes = TableSerializer.Serialize(table, true,
            Enumerable.Range(0, 16).Select(i => (float)i).ToArray(),
            Enumerable.Range(0, 16).Select(i => (float)i).ToArray(),
            Enumerable.Range(0, 256).Select(i => (float)i).ToArray());

        bytes.Should().HaveCount(4 + 16 * 4 + 16 * 4 + 256 * 4);
        bytes[0].Should().Be(2);
        bytes[1].Should().Be(1); // enabled
        bytes[2].Should().Be(16); // rows
        bytes[3].Should().Be(16); // cols
    }

    [Fact]
    public void Serialize_TypeMapping_UsesCorrectTypeBytes()
    {
        TableSerializer.Serialize(MakeTable("T1x16", 1, 16), true, new float[16], Array.Empty<float>(), new float[16])[0].Should().Be(0);
        TableSerializer.Serialize(MakeTable("T1x32", 1, 32), true, new float[32], Array.Empty<float>(), new float[32])[0].Should().Be(1);
        TableSerializer.Serialize(MakeTable("T32x32", 32, 32), true, new float[32], new float[32], new float[1024])[0].Should().Be(3);
        TableSerializer.Serialize(MakeTable("T16x16", 16, 16), true, new float[16], new float[16], new float[256])[0].Should().Be(2);
    }

    [Fact]
    public void Serialize_Disabled_SetsEnabledByteZero()
    {
        var table = MakeTable("T16x16", 16, 16);
        var bytes = TableSerializer.Serialize(table, false,
            new float[16], new float[16], new float[256]);

        bytes[1].Should().Be(0);
    }

    [Fact]
    public void RoundTrip_16x16_PreservesAllValues()
    {
        var table = MakeTable("T16x16", 16, 16);
        var input0 = Enumerable.Range(0, 16).Select(i => (float)(i * 500)).ToArray();
        var input1 = Enumerable.Range(0, 16).Select(i => (float)(i * 10)).ToArray();
        var output = Enumerable.Range(0, 256).Select(i => (float)(i * 0.5f - 5f)).ToArray();

        var bytes = TableSerializer.Serialize(table, true, input0, input1, output);
        var result = TableSerializer.Deserialize(table, bytes);

        result.Enabled.Should().BeTrue();
        result.Input0.Should().Equal(input0);
        result.Input1.Should().Equal(input1);
        result.Output.Should().Equal(output);
    }

    [Fact]
    public void RoundTrip_1x16_OmitsInput1()
    {
        var table = MakeTable("T1x16", 1, 16);
        var input0 = Enumerable.Range(0, 16).Select(i => (float)(i * 250)).ToArray();
        var output = Enumerable.Range(0, 16).Select(i => (float)(i * 2f)).ToArray();

        var bytes = TableSerializer.Serialize(table, false, input0, Array.Empty<float>(), output);

        bytes.Should().HaveCount(4 + 16 * 4 + 16 * 4); // no input1 section
        var result = TableSerializer.Deserialize(table, bytes);

        result.Enabled.Should().BeFalse();
        result.Input0.Should().Equal(input0);
        result.Input1.Should().Equal([0f]); // rows == 1 → single dummy entry
        result.Output.Should().Equal(output);
    }

    [Fact]
    public void RoundTrip_1x32_PreservesValues()
    {
        var table = MakeTable("T1x32", 1, 32);
        var input0 = Enumerable.Range(0, 32).Select(i => (float)(i * 100)).ToArray();
        var output = Enumerable.Range(0, 32).Select(i => (float)(-i)).ToArray();

        var bytes = TableSerializer.Serialize(table, true, input0, Array.Empty<float>(), output);
        var result = TableSerializer.Deserialize(table, bytes);

        result.Input0.Should().Equal(input0);
        result.Output.Should().Equal(output);
    }

    [Fact]
    public void RoundTrip_32x32_HandlesLargeTable()
    {
        var table = MakeTable("T32x32", 32, 32);
        var input0 = Enumerable.Range(0, 32).Select(i => (float)(i * 100)).ToArray();
        var input1 = Enumerable.Range(0, 32).Select(i => (float)(i * 10)).ToArray();
        var output = Enumerable.Range(0, 1024).Select(i => (float)(i % 500)).ToArray();

        var bytes = TableSerializer.Serialize(table, true, input0, input1, output);
        var result = TableSerializer.Deserialize(table, bytes);

        result.Input0.Should().Equal(input0);
        result.Input1.Should().Equal(input1);
        result.Output.Should().Equal(output);
    }

    [Fact]
    public void Deserialize_DimensionMismatch_Throws()
    {
        var table = MakeTable("T16x16", 16, 16);
        var bytes = TableSerializer.Serialize(MakeTable("T1x16", 1, 16), true,
            new float[16], Array.Empty<float>(), new float[16]);

        var act = () => TableSerializer.Deserialize(table, bytes);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*dimension mismatch*");
    }

    [Fact]
    public void Deserialize_TooShortData_Throws()
    {
        var table = MakeTable("T16x16", 16, 16);

        var act = () => TableSerializer.Deserialize(table, new byte[3]);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*too short*");
    }

    [Fact]
    public void Deserialize_TruncatedBody_ThrowsIndexOutOfRange()
    {
        var table = MakeTable("T16x16", 16, 16);
        var bytes = new byte[4 + 10]; // header + partial payload

        var act = () => TableSerializer.Deserialize(table, bytes);

        act.Should().Throw<Exception>();
    }
}

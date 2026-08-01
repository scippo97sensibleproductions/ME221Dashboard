using System.Text;
using FluentAssertions;
using ME221.Data.Infrastructure;
using Xunit;

namespace ME221.Data.Tests;

public class MefwReaderTests
{
    [Fact]
    public void ReadDefXml_ValidFile_ExtractsDefXml()
    {
        const string defXml = "<ecu><DeviceDataInformationModel><ProductName>ME221</ProductName></DeviceDataInformationModel></ecu>";
        var bytes = new byte[8 + Encoding.UTF8.GetByteCount(defXml) + 4];
        "MEFW"u8.CopyTo(bytes);
        BitConverter.GetBytes((uint)(8 + Encoding.UTF8.GetByteCount(defXml))).CopyTo(bytes, 4);
        Encoding.UTF8.GetBytes(defXml).CopyTo(bytes, 8);

        var result = MefwReader.ReadDefXml(bytes);

        result.Should().Be(defXml);
    }

    [Fact]
    public void ReadDefXml_TooSmall_Throws()
    {
        var bytes = new byte[4] { (byte)'M', (byte)'E', (byte)'F', (byte)'W' };

        var act = () => MefwReader.ReadDefXml(bytes);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*too small*");
    }

    [Fact]
    public void ReadDefXml_WrongMagic_Throws()
    {
        var bytes = Encoding.UTF8.GetBytes("XXXX12345678");

        var act = () => MefwReader.ReadDefXml(bytes);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*magic*");
    }

    [Fact]
    public void ReadDefXml_OffsetBelowHeader_Throws()
    {
        var bytes = new byte[16];
        "MEFW"u8.CopyTo(bytes);
        BitConverter.GetBytes((uint)4).CopyTo(bytes, 4); // offset < 8 — inside header

        var act = () => MefwReader.ReadDefXml(bytes);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*DEF offset*");
    }

    [Fact]
    public void ReadDefXml_OffsetBeyondFileEnd_Throws()
    {
        var bytes = new byte[16];
        "MEFW"u8.CopyTo(bytes);
        BitConverter.GetBytes((uint)9999).CopyTo(bytes, 4);

        var act = () => MefwReader.ReadDefXml(bytes);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*DEF offset*");
    }

    [Fact]
    public void ReadDefXml_EmptyDefRegion_ReturnsEmptyString()
    {
        var bytes = new byte[8];
        "MEFW"u8.CopyTo(bytes);
        BitConverter.GetBytes((uint)8).CopyTo(bytes, 4); // def length 0

        var result = MefwReader.ReadDefXml(bytes);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ReadDefXml_FromFile_RoundTrips()
    {
        const string defXml = "<ecu />";
        var path = Path.Combine(Path.GetTempPath(), $"mefw-test-{Guid.NewGuid():N}.mefw");
        try
        {
            var bytes = new byte[8 + Encoding.UTF8.GetByteCount(defXml)];
            "MEFW"u8.CopyTo(bytes);
            BitConverter.GetBytes((uint)bytes.Length).CopyTo(bytes, 4);
            Encoding.UTF8.GetBytes(defXml).CopyTo(bytes, 8);
            File.WriteAllBytes(path, bytes);

            MefwReader.ReadDefXml(path).Should().Be(defXml);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadDefXml_UnicodeDef_IsUtf8Decoded()
    {
        const string defXml = "<ecu><name>Çelik 温度</name></ecu>";
        var bytes = new byte[8 + Encoding.UTF8.GetByteCount(defXml)];
        "MEFW"u8.CopyTo(bytes);
        BitConverter.GetBytes((uint)bytes.Length).CopyTo(bytes, 4);
        Encoding.UTF8.GetBytes(defXml).CopyTo(bytes, 8);

        var result = MefwReader.ReadDefXml(bytes);

        result.Should().Be(defXml);
    }
}

using System.Text;
using FluentAssertions;
using ME221.Data.Infrastructure;
using Xunit;

namespace ME221.Data.Tests;

public class MefwFirmwareTests
{
    [Fact]
    public void Read_ValidFile_ParsesHeaderAndRegions()
    {
        var bytes = BuildMefw(
            "<ecu />",
            "ME221-V2B-PNP", "MX5-0105", "62", "4.2.1", "",
            [(0x400u, [1, 2, 3, 4], false, 0), (0x500u, [5, 6], true, 8)]);

        var image = MefwFirmwareReader.Read(bytes);

        image.DefXml.Should().Be("<ecu />");
        image.ProductName.Should().Be("ME221-V2B-PNP");
        image.ModelName.Should().Be("MX5-0105");
        image.Version.Should().Be("62");
        image.FwVersion.Should().Be("4.2.1");
        image.Comments.Should().BeEmpty();
        image.Regions.Should().HaveCount(2);
        image.Regions[0].StartAddress.Should().Be(0x400u);
        image.Regions[0].Data.Should().Equal(1, 2, 3, 4);
        image.Regions[0].Last.Should().BeFalse();
        image.Regions[0].IsCrcValid.Should().BeTrue();
        image.Regions[1].StartAddress.Should().Be(0x500u);
        image.Regions[1].Data.Should().Equal(5, 6);
        image.Regions[1].Last.Should().BeTrue();
        image.Regions[1].PaddingBytes.Should().Be(8);
        image.Regions[1].IsCrcValid.Should().BeTrue();
        image.TotalDataBytes.Should().Be(6);
    }

    [Fact]
    public void Read_CrcMismatch_ToleratedButFlagged()
    {
        var bytes = BuildMefw(
            "<ecu />", "P", "M", "V", "F", "",
            [(0x400u, [1, 2, 3], false, 0)]);

        // Corrupt the single data byte region (header is 16 bytes after container prologue).
        bytes[^2] ^= 0xFF;

        var image = MefwFirmwareReader.Read(bytes);

        image.Regions.Should().HaveCount(1);
        image.Regions[0].IsCrcValid.Should().BeFalse();
        image.Regions[0].StoredCrc.Should().NotBe(image.Regions[0].ComputedCrc);
    }

    [Fact]
    public void ComputeCrc_EmptyAndKnownVector()
    {
        MefwFirmwareReader.ComputeCrc([]).Should().Be(0);
        MefwFirmwareReader.ComputeCrc([1, 2, 3]).Should().Be(0x0A06);
    }

    [Fact]
    public void Read_TooSmall_Throws()
    {
        var act = () => MefwFirmwareReader.Read(new byte[4]);

        act.Should().Throw<InvalidDataException>().WithMessage("*too small*");
    }

    [Fact]
    public void Read_WrongMagic_Throws()
    {
        var bytes = Encoding.ASCII.GetBytes("XXXX12345678");

        var act = () => MefwFirmwareReader.Read(bytes);

        act.Should().Throw<InvalidDataException>().WithMessage("*magic*");
    }

    [Fact]
    public void Read_NoRegions_Throws()
    {
        var bytes = BuildMefw("<ecu />", "P", "M", "V", "F", "", []);

        var act = () => MefwFirmwareReader.Read(bytes);

        act.Should().Throw<InvalidDataException>().WithMessage("*No firmware regions*");
    }

    [Fact]
    public void Read_BadRegionMagic_Throws()
    {
        var bytes = BuildMefw("<ecu />", "P", "M", "V", "F", "",
            [(0x400u, [1], false, 0)]);
        // Overwrite the region magic with ZZ.
        var regionOffset = FindRegionOffset(bytes);
        bytes[regionOffset] = (byte)'Z';
        bytes[regionOffset + 1] = (byte)'Z';

        var act = () => MefwFirmwareReader.Read(bytes);

        act.Should().Throw<InvalidDataException>().WithMessage("*region magic*");
    }

    [Fact]
    public void Read_TruncatedRegionData_Throws()
    {
        var bytes = BuildMefw("<ecu />", "P", "M", "V", "F", "",
            [(0x400u, [1, 2, 3], false, 0)]);

        var act = () => MefwFirmwareReader.Read(bytes[..^1]);

        act.Should().Throw<InvalidDataException>().WithMessage("*truncated*");
    }

    private static byte[] BuildMefw(
        string defXml, string product, string model, string version, string fwVersion,
        string comments, List<(uint Address, byte[] Data, bool Last, byte Pad)> regions)
    {
        using var ms = new MemoryStream();
        ms.Write("MEFW"u8.ToArray());
        var defBytes = Encoding.UTF8.GetBytes(defXml);
        ms.Write(BitConverter.GetBytes((uint)(8 + defBytes.Length)));
        ms.Write(defBytes);
        WritePascal(ms, product);
        WritePascal(ms, model);
        WritePascal(ms, version);
        WritePascal(ms, fwVersion);
        var commentBytes = Encoding.ASCII.GetBytes(comments);
        ms.Write(BitConverter.GetBytes((ushort)commentBytes.Length));
        ms.Write(commentBytes);
        ms.Write(new byte[8]);
        foreach (var (address, data, last, pad) in regions)
        {
            ms.Write("FW"u8.ToArray());
            ms.Write(BitConverter.GetBytes(address));
            ms.Write(BitConverter.GetBytes((uint)data.Length));
            ms.WriteByte(last ? (byte)1 : (byte)0);
            ms.WriteByte(pad);
            ms.Write(new byte[2]); // reserved
            ms.Write(BitConverter.GetBytes(MefwFirmwareReader.ComputeCrc(data)));
            ms.Write(data);
        }
        return ms.ToArray();
    }

    private static void WritePascal(MemoryStream ms, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        ms.WriteByte((byte)bytes.Length);
        ms.Write(bytes);
    }

    private static int FindRegionOffset(byte[] bytes)
    {
        for (var i = bytes.Length - 2; i >= 0; i--)
        {
            if (bytes[i] == 'F' && bytes[i + 1] == 'W')
                return i;
        }
        throw new InvalidOperationException("Test bug: no region magic found");
    }
}

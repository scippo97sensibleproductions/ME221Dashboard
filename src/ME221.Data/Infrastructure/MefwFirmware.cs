using System.Text;

namespace ME221.Data.Infrastructure;

// Reverse-engineered from MEITE 3.2.0:
//   MEITE.Models.Firmware.FWImageFile (container),
//   MEITE.Models.Firmware.FWImage (region list from Intel HEX),
//   MEITE.Models.Firmware.FWImageRegion (16-byte header + data).
// Container layout:
//   "MEFW" (4) + u32 LE defOffset + DEF XML (defOffset - 8 bytes)
//   + u8/len product + u8/len model + u8/len version + u8/len fwVersion
//   + u16 LE commentsLen + comments + 8 zero bytes
//   + regions to EOF.
// Region layout (16-byte header):
//   "FW" (2) + u32 LE startAddress + u32 LE length
//   + u8 lastFlag + u8 paddingBytes + 2 reserved bytes
//   + u16 LE crc + data[length].
// Note: v62 FW.mefw files in the wild do NOT validate against ComputeCrc
// (format drift vs MEITE 3.2), so the parser records validity but never throws on it.
public sealed record MefwFirmwareRegion(
    uint StartAddress,
    byte[] Data,
    bool Last,
    byte PaddingBytes,
    ushort StoredCrc,
    ushort ComputedCrc)
{
    public bool IsCrcValid => StoredCrc == ComputedCrc;
    public int Length => Data.Length;
}

public sealed record MefwFirmwareImage(
    string DefXml,
    string ProductName,
    string ModelName,
    string Version,
    string FwVersion,
    string Comments,
    IReadOnlyList<MefwFirmwareRegion> Regions)
{
    public int TotalDataBytes
    {
        get
        {
            var total = 0;
            foreach (var r in Regions) total += r.Data.Length;
            return total;
        }
    }
}

public static class MefwFirmwareReader
{
    private static readonly byte[] MagicStart = "MEFW"u8.ToArray();
    private static readonly byte[] RegionMagic = "FW"u8.ToArray();

    private const int RegionHeaderLength = 16;

    public static MefwFirmwareImage Read(string filePath)
    {
        return Read(File.ReadAllBytes(filePath));
    }

    public static MefwFirmwareImage Read(byte[] fileBytes)
    {
        if (fileBytes.Length < 8)
            throw new InvalidDataException("File too small to be a valid .mefw file");

        if (!fileBytes.AsSpan(0, 4).SequenceEqual(MagicStart))
            throw new InvalidDataException("Invalid .mefw magic — expected 'MEFW'");

        var defOffset = BitConverter.ToUInt32(fileBytes, 4);
        if (defOffset < 8 || defOffset > (uint)fileBytes.Length)
            throw new InvalidDataException($"Invalid DEF offset: {defOffset}");

        var defXml = Encoding.UTF8.GetString(fileBytes, 8, (int)defOffset - 8);

        var pos = (int)defOffset;
        var product = ReadPascalString(fileBytes, ref pos, "product");
        var model = ReadPascalString(fileBytes, ref pos, "model");
        var version = ReadPascalString(fileBytes, ref pos, "version");
        var fwVersion = ReadPascalString(fileBytes, ref pos, "fwVersion");
        var comments = ReadComments(fileBytes, ref pos);

        if (fileBytes.Length - pos < 8)
            throw new InvalidDataException("File truncated — missing firmware prologue");
        pos += 8; // 8 reserved zero bytes per MEITE FWImageFile writer

        var regions = new List<MefwFirmwareRegion>();
        while (pos < fileBytes.Length)
        {
            regions.Add(ReadRegion(fileBytes, ref pos));
        }

        if (regions.Count == 0)
            throw new InvalidDataException("No firmware regions found");

        return new MefwFirmwareImage(defXml, product, model, version, fwVersion, comments, regions);
    }

    public static ushort ComputeCrc(ReadOnlySpan<byte> data)
    {
        var num = 0;
        var num2 = 0;
        foreach (var b in data)
        {
            num = (num + b) % 255;
            num2 = (num2 + num) % 255;
        }
        return (ushort)((num2 << 8) | num);
    }

    private static string ReadPascalString(byte[] bytes, ref int pos, string field)
    {
        if (pos >= bytes.Length)
            throw new InvalidDataException($"File truncated — missing {field} length");
        var len = bytes[pos++];
        if (bytes.Length - pos < len)
            throw new InvalidDataException($"File truncated — missing {field} data");
        var value = Encoding.ASCII.GetString(bytes, pos, len);
        pos += len;
        return value;
    }

    private static string ReadComments(byte[] bytes, ref int pos)
    {
        if (bytes.Length - pos < 2)
            throw new InvalidDataException("File truncated — missing comments length");
        var len = BitConverter.ToUInt16(bytes, pos);
        pos += 2;
        if (bytes.Length - pos < len)
            throw new InvalidDataException("File truncated — missing comments data");
        var value = Encoding.ASCII.GetString(bytes, pos, len);
        pos += len;
        return value;
    }

    private static MefwFirmwareRegion ReadRegion(byte[] bytes, ref int pos)
    {
        if (bytes.Length - pos < RegionHeaderLength)
            throw new InvalidDataException($"File truncated — region header at {pos} needs {RegionHeaderLength} bytes");

        if (!bytes.AsSpan(pos, 2).SequenceEqual(RegionMagic))
            throw new InvalidDataException($"Invalid firmware region magic at {pos} — expected 'FW'");

        var address = BitConverter.ToUInt32(bytes, pos + 2);
        var length = BitConverter.ToUInt32(bytes, pos + 6);
        var last = bytes[pos + 10] == 1;
        var padding = bytes[pos + 11];
        // bytes[pos+12..pos+14] are reserved, skipped like MEITE FWImageRegion(Stream)
        var storedCrc = BitConverter.ToUInt16(bytes, pos + 14);

        if (length > int.MaxValue || bytes.Length - (pos + RegionHeaderLength) < (long)length)
            throw new InvalidDataException($"File truncated — region at {pos} needs {length} data bytes");

        var data = new byte[length];
        Array.Copy(bytes, pos + RegionHeaderLength, data, 0, (int)length);
        pos += RegionHeaderLength + (int)length;

        return new MefwFirmwareRegion(address, data, last, padding, storedCrc, ComputeCrc(data));
    }
}

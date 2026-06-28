using System.Text;

namespace ME221.Data.Infrastructure;

public static class MefwReader
{
    private static readonly byte[] MagicStart = "MEFW"u8.ToArray();

    public static string ReadDefXml(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        return ReadDefXml(bytes);
    }

    public static string ReadDefXml(byte[] fileBytes)
    {
        if (fileBytes.Length < 8)
            throw new InvalidDataException("File too small to be a valid .mefw file");

        if (!fileBytes.AsSpan(0, 4).SequenceEqual(MagicStart))
            throw new InvalidDataException("Invalid .mefw magic — expected 'MEFW'");

        var metadataOffset = BitConverter.ToUInt32(fileBytes, 4);
        if (metadataOffset < 8 || metadataOffset > (uint)fileBytes.Length)
            throw new InvalidDataException($"Invalid DEF offset: {metadataOffset}");

        var defLength = (int)metadataOffset - 8;

        if (fileBytes.Length < (int)metadataOffset)
            throw new InvalidDataException("File truncated — metadata offset beyond file end");

        return Encoding.UTF8.GetString(fileBytes, 8, defLength);
    }
}

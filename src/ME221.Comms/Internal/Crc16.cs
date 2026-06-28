using System.Buffers.Binary;

namespace ME221.Comms.Internal;

/// <summary>
/// CRC-16 computation using the Mod-255 accumulator algorithm used by the ME221 ECU protocol.
///
/// Wire algorithm (from legacy code):
///   num  = (num  + byte) mod 255
///   num2 = (num2 + num)  mod 255
///   crc  = (num2  8) | num
///
/// ARMv7-safe: the modulus 255 is replaced with a conditional subtraction
/// (since the max intermediate value is 508, one subtraction is always enough).
/// This avoids the expensive 32-bit division instruction on 32-bit ARM.
/// </summary>
public static class Crc16
{
    /// <summary>Modulus value for the CRC accumulator (255).</summary>
    private const int Modulus = 255;

    /// <summary>
    /// Computes the CRC-16 over the given data span using the Mod-255 accumulator.
    /// Zero allocations — operates entirely on <paramref name="data"/>.
    /// </summary>
    /// <param name="data">The bytes to compute the CRC over (type, class, command, payload).</param>
    /// <returns>The 16-bit CRC value.</returns>
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        var num = WireFormat.CrcInitial;
        var num2 = WireFormat.CrcInitial;

        for (var i = 0; i < data.Length; i++)
        {
            num += data[i];
            if (num >= Modulus)
                num -= Modulus;

            num2 += num;
            if (num2 >= Modulus)
                num2 -= Modulus;
        }

        return (ushort)((num2 << 8) | num);
    }

    /// <summary>
    /// Verifies whether the CRC stored at the end of <paramref name="dataWithCrc"/>
    /// matches the computed CRC over the preceding bytes.
    /// </summary>
    /// <param name="dataWithCrc">The full data including the 2-byte CRC trailer.</param>
    /// <returns><c>true</c> if the CRC is valid; otherwise <c>false</c>.</returns>
    public static bool Verify(ReadOnlySpan<byte> dataWithCrc)
    {
        if (dataWithCrc.Length < WireFormat.CrcLength)
            return false;

        var dataWithoutCrc = dataWithCrc[..^WireFormat.CrcLength];
        var storedCrcBytes = dataWithCrc[^WireFormat.CrcLength..];

        var storedCrc = BinaryPrimitives.ReadUInt16LittleEndian(storedCrcBytes);
        var computedCrc = Compute(dataWithoutCrc);

        return storedCrc == computedCrc;
    }
}

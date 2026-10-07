namespace GLAZOV.M.CONVERTER.Formats.Detection.Archives;

/// <summary>
/// TAR: "ustar" at byte 257 of the first 512-byte block and the checksum of the block as it is written in the block.
/// Old TAR files without "ustar" are not named.
/// </summary>
internal static class TarSignature
{
    internal const int BlockLength = 512;

    private const int MagicOffset = 257;
    private const int ChecksumOffset = 148;
    private const int ChecksumLength = 8;

    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        if (head.Length < BlockLength || !head.Slice(MagicOffset, 5).SequenceEqual("ustar"u8))
        {
            return false;
        }

        return ReadOctal(head.Slice(ChecksumOffset, ChecksumLength)) is { } written && written == Sum(head[..BlockLength]);
    }

    // The checksum field counts as eight spaces while the sum is taken.
    private static long Sum(ReadOnlySpan<byte> block)
    {
        long sum = 0;
        for (int i = 0; i < block.Length; i++)
        {
            sum += i is >= ChecksumOffset and < ChecksumOffset + ChecksumLength ? (byte)' ' : block[i];
        }

        return sum;
    }

    // Octal digits, then NUL or a space; nothing else is a number.
    private static long? ReadOctal(ReadOnlySpan<byte> field)
    {
        long value = 0;
        int digits = 0;
        foreach (byte b in field)
        {
            if (b is >= (byte)'0' and <= (byte)'7')
            {
                value = (value * 8) + (b - '0');
                digits++;
            }
            else if (b is 0 or (byte)' ')
            {
                if (digits > 0)
                {
                    return value;
                }
            }
            else
            {
                return null;
            }
        }

        return digits > 0 ? value : null;
    }
}

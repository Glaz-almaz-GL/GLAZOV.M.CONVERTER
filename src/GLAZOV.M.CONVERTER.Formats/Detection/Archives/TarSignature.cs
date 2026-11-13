namespace GLAZOV.M.CONVERTER.Formats.Detection.Archives;

/// <summary>
/// TAR: the checksum of the first 512-byte block as it is written in the block, together with "ustar" at byte 257 (POSIX and GNU)
/// or, for the old format without any magic, an octal mode and size, a name and a plain type flag. The checksum alone is not
/// taken for a sign: a block of zeroes or of random bytes does not pass it.
/// </summary>
internal static class TarSignature
{
    internal const int BlockLength = 512;

    private const int MagicOffset = 257;
    private const int ChecksumOffset = 148;
    private const int ChecksumLength = 8;
    private const int ModeOffset = 100;
    private const int SizeOffset = 124;
    private const int TypeFlagOffset = 156;

    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.Length >= BlockLength && ReadOctal(head.Slice(ChecksumOffset, ChecksumLength)) is { } written && written == Sum(head[..BlockLength]) && (head.Slice(MagicOffset, 5).SequenceEqual("ustar"u8) || LooksLikeOldHeader(head));
    }

    // The old format has no magic: a name, an octal mode (bytes 100-107) and size (124-135), a type flag that is 0 or "0"-"7".
    private static bool LooksLikeOldHeader(ReadOnlySpan<byte> head)
    {
        byte type = head[TypeFlagOffset];
        return head[0] != 0
            && ReadOctal(head.Slice(ModeOffset, 8)) is not null
            && ReadOctal(head.Slice(SizeOffset, 12)) is not null
            && (type is 0 || type is >= (byte)'0' and <= (byte)'7');
    }

    // The checksum field counts as eight spaces while the sum is taken.
    private static long Sum(ReadOnlySpan<byte> block)
    {
        long sum = 0;
        for (int i = 0; i < block.Length; i++)
        {
            if (i is >= ChecksumOffset and < (ChecksumOffset + ChecksumLength))
            {
                sum += (byte)' ';
            }
            else
            {
                sum += block[i];
            }
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

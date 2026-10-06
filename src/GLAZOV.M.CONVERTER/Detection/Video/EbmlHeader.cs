namespace GLAZOV.M.CONVERTER.Detection.Video;

/// <summary>
/// The EBML header of Matroska and WebM files: its DocType names the format.
/// </summary>
internal static class EbmlHeader
{
    private static readonly byte[] Magic = [0x1A, 0x45, 0xDF, 0xA3];

    // How far after the EBML magic the DocType element is looked for.
    private const int SearchLength = 64;

    /// <summary>
    /// Returns the DocType (<c>matroska</c> or <c>webm</c>) of the file the data starts like, or <c>null</c>.
    /// </summary>
    /// <param name="head">Leading bytes of the input.</param>
    internal static string? DocType(ReadOnlySpan<byte> head)
    {
        if (head.Length < 12 || !head.StartsWith(Magic))
        {
            return null;
        }

        // DocType element: id 0x42 0x82, one-byte size (high bit set), then the text.
        ReadOnlySpan<byte> area = head[Magic.Length..Math.Min(head.Length, Magic.Length + SearchLength)];
        for (int i = 0; i + 3 <= area.Length; i++)
        {
            if (area[i] != 0x42 || area[i + 1] != 0x82 || (area[i + 2] & 0x80) == 0)
            {
                continue;
            }

            int length = area[i + 2] & 0x7F;
            if (i + 3 + length > area.Length)
            {
                return null;
            }

            ReadOnlySpan<byte> value = area.Slice(i + 3, length);
            if (value.SequenceEqual("matroska"u8))
            {
                return "matroska";
            }

            if (value.SequenceEqual("webm"u8))
            {
                return "webm";
            }
        }

        return null;
    }

    /// <summary>
    /// True when the data starts with the EBML magic.
    /// </summary>
    /// <param name="head">Leading bytes of the input.</param>
    internal static bool StartsLikeEbml(ReadOnlySpan<byte> head)
    {
        return head.StartsWith(Magic);
    }
}

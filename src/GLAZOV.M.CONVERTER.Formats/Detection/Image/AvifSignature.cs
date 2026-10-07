using System.Buffers.Binary;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Image;

/// <summary>
/// AVIF: an ISO base media file whose "ftyp" box names the brand "avif" or "avis" as the main one or among the compatible ones.
/// HEIC and other "mif1" files without these brands are not AVIF.
/// </summary>
internal static class AvifSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        if (head.Length < 16 || !head[4..8].SequenceEqual("ftyp"u8))
        {
            return false;
        }

        uint size = BinaryPrimitives.ReadUInt32BigEndian(head[..4]);
        if (size < 16)
        {
            return false;
        }

        ReadOnlySpan<byte> box = head[..(int)Math.Min(size, (uint)head.Length)];
        if (IsAvifBrand(box[8..12]))
        {
            return true;
        }

        // Compatible brands start after the main brand and its minor version.
        for (int at = 16; at + 4 <= box.Length; at += 4)
        {
            if (IsAvifBrand(box[at..(at + 4)]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAvifBrand(ReadOnlySpan<byte> brand)
    {
        return brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8);
    }
}

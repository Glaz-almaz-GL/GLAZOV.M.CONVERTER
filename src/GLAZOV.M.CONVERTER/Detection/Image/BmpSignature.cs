using System.Buffers.Binary;

namespace GLAZOV.M.CONVERTER.Detection.Image;

/// <summary>
/// BMP: "BM", zero reserved fields, one of the valid sizes of the image header.
/// </summary>
internal static class BmpSignature
{
    private static readonly uint[] HeaderSizes = [12, 40, 52, 56, 64, 108, 124];

    /// <summary>
    /// True when the data starts like BMP.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.Length >= 18
            && head.StartsWith("BM"u8)
            && head[6..10].IndexOfAnyExcept((byte)0) < 0
            && HeaderSizes.Contains(BinaryPrimitives.ReadUInt32LittleEndian(head[14..18]));
    }
}

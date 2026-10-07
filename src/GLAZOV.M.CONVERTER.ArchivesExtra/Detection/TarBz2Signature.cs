using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Detection;

/// <summary>
/// TAR.BZ2: a BZip2 file whose first 512 unpacked bytes are a TAR header.
/// </summary>
internal static class TarBz2Signature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        if (!Bzip2Signature.Matches(head))
        {
            return false;
        }

        try
        {
            using MemoryStream packed = new(head.ToArray());
            using BZip2Stream unpacked = new(packed, CompressionMode.Decompress, false);
            return TarProbe.Matches(unpacked);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }
}

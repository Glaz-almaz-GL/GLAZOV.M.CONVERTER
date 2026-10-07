using GLAZOV.M.CONVERTER.Formats;
using SharpCompress.Compressors.Xz;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Detection;

/// <summary>
/// TAR.XZ: an XZ file whose first 512 unpacked bytes are a TAR header. The window holds the compressed start, which is enough
/// because a header packs to far less than the window.
/// </summary>
internal static class TarXzSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        if (!XzSignature.Matches(head))
        {
            return false;
        }

        try
        {
            using MemoryStream packed = new(head.ToArray());
            using XZStream unpacked = new(packed);
            return TarProbe.Matches(unpacked);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }
}

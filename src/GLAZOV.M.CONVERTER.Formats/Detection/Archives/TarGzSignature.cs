using System.IO.Compression;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Archives;

/// <summary>
/// TAR.GZ: a GZip file whose first 512 unpacked bytes are a TAR header. The window holds the compressed start, which is
/// enough because a header packs to far less than the window.
/// </summary>
internal static class TarGzSignature
{
    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        if (head.Length < GzipSignature.HeaderLength || !head.StartsWith(new byte[] { 0x1F, 0x8B, 0x08 }))
        {
            return false;
        }

        try
        {
            using MemoryStream packed = new(head.ToArray());
            using GZipStream unpacked = new(packed, CompressionMode.Decompress);
            byte[] header = new byte[TarSignature.BlockLength];
            int total = 0;
            int read;
            while (total < header.Length && (read = unpacked.Read(header, total, header.Length - total)) > 0)
            {
                total += read;
            }

            return total == header.Length && TarSignature.Matches(header);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or EndOfStreamException)
        {
            return false;
        }
    }
}

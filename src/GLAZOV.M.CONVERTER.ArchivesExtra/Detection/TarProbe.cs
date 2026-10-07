using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Detection;

/// <summary>
/// Asks the TAR detector of the Formats package whether the first block of an unpacked stream is a TAR header.
/// </summary>
internal static class TarProbe
{
    private const int BlockLength = 512;

    /// <summary>
    /// True when the first 512 bytes of the stream are a TAR header.
    /// </summary>
    /// <param name="unpacked">The unpacked stream, read from its start.</param>
    internal static bool Matches(Stream unpacked)
    {
        byte[] block = new byte[BlockLength];
        int total = 0;
        int read;
        while (total < block.Length && (read = unpacked.Read(block, total, block.Length - total)) > 0)
        {
            total += read;
        }

        return total == block.Length && StandardDetectors.Archives.Detect(block)?.Equals(FileFormat.Tar) == true;
    }
}

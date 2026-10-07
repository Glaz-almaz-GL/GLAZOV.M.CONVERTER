using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Formats;

/// <summary>
/// The archive formats of the standard set.
/// </summary>
public static class ArchiveFormats
{
    /// <summary>
    /// ZIP. Office and OpenDocument packages are ZIP files too, but they are named by their own formats.
    /// </summary>
    public static FileFormat Zip { get; } = FileFormat.Create("ZIP", "application/zip", "zip");

    /// <summary>
    /// TAR (POSIX "ustar" headers).
    /// </summary>
    public static FileFormat Tar { get; } = FileFormat.Create("TAR", "application/x-tar", "tar");

    /// <summary>
    /// TAR compressed with GZip. Its MIME type differs from <see cref="Gzip"/> because two different formats may not share one.
    /// </summary>
    public static FileFormat TarGz { get; } = FileFormat.Create("TAR.GZ", "application/x-gtar", "tar.gz");

    /// <summary>
    /// GZip: one compressed file.
    /// </summary>
    public static FileFormat Gzip { get; } = FileFormat.Create("GZip", "application/gzip", "gz");
}

using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.ArchivesExtra;

/// <summary>
/// The archive formats this package adds to the standard set.
/// </summary>
public static class ArchivesExtraFormats
{
    /// <summary>
    /// 7z. Read only.
    /// </summary>
    public static FileFormat SevenZip { get; } = FileFormat.Create("7z", "application/x-7z-compressed", "7z");

    /// <summary>
    /// RAR (versions 4 and 5). Read only: the format cannot be written by a free library.
    /// </summary>
    public static FileFormat Rar { get; } = FileFormat.Create("RAR", "application/vnd.rar", "rar");

    /// <summary>
    /// XZ: one compressed file. Read only.
    /// </summary>
    public static FileFormat Xz { get; } = FileFormat.Create("XZ", "application/x-xz", "xz");

    /// <summary>
    /// TAR compressed with XZ. Read only.
    /// </summary>
    public static FileFormat TarXz { get; } = FileFormat.Create("TAR.XZ", "application/x-xz-compressed-tar", "tar.xz");

    /// <summary>
    /// BZip2: one compressed file.
    /// </summary>
    public static FileFormat Bzip2 { get; } = FileFormat.Create("BZip2", "application/x-bzip2", "bz2");

    /// <summary>
    /// TAR compressed with BZip2.
    /// </summary>
    public static FileFormat TarBz2 { get; } = FileFormat.Create("TAR.BZ2", "application/x-bzip2-compressed-tar", "tar.bz2");
}

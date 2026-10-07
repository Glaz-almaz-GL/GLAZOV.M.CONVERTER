using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.ArchivesExtra;

/// <summary>
/// The written form <c>FileFormat.SevenZip</c> for the values of this package: after <c>using GLAZOV.M.CONVERTER.ArchivesExtra;</c>
/// every value is read the way a member of <see cref="FileFormat"/> is. Each member returns the field of <see cref="ArchivesExtraFormats"/>.
/// </summary>
public static class ArchivesExtraFormatMembers
{
    extension(FileFormat)
    {
        /// <summary>Same as <see cref="ArchivesExtraFormats.SevenZip"/>.</summary>
        public static FileFormat SevenZip => ArchivesExtraFormats.SevenZip;
        /// <summary>Same as <see cref="ArchivesExtraFormats.Rar"/>.</summary>
        public static FileFormat Rar => ArchivesExtraFormats.Rar;
        /// <summary>Same as <see cref="ArchivesExtraFormats.Xz"/>.</summary>
        public static FileFormat Xz => ArchivesExtraFormats.Xz;
        /// <summary>Same as <see cref="ArchivesExtraFormats.TarXz"/>.</summary>
        public static FileFormat TarXz => ArchivesExtraFormats.TarXz;
        /// <summary>Same as <see cref="ArchivesExtraFormats.Bzip2"/>.</summary>
        public static FileFormat Bzip2 => ArchivesExtraFormats.Bzip2;
        /// <summary>Same as <see cref="ArchivesExtraFormats.TarBz2"/>.</summary>
        public static FileFormat TarBz2 => ArchivesExtraFormats.TarBz2;
    }
}

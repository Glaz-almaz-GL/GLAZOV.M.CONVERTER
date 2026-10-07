using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Detection;

/// <summary>
/// 7z, RAR, XZ, BZip2 and their TAR forms, by the signs at the start of the file. TAR.XZ and TAR.BZ2 are told from XZ and BZip2 by
/// unpacking the first block out of the window, so a stream that cannot seek is named too.
/// </summary>
internal sealed class ExtraArchiveDetector : IFormatDetector
{
    /// <inheritdoc />
    public int HeadLength => 4096;

    /// <inheritdoc />
    public FileFormat? Detect(ReadOnlySpan<byte> head)
    {
        if (TarXzSignature.Matches(head))
        {
            return ArchivesExtraFormats.TarXz;
        }

        if (XzSignature.Matches(head))
        {
            return ArchivesExtraFormats.Xz;
        }

        if (TarBz2Signature.Matches(head))
        {
            return ArchivesExtraFormats.TarBz2;
        }

        if (Bzip2Signature.Matches(head))
        {
            return ArchivesExtraFormats.Bzip2;
        }

        if (SevenZipSignature.Matches(head))
        {
            return ArchivesExtraFormats.SevenZip;
        }

        return RarSignature.Matches(head) ? ArchivesExtraFormats.Rar : null;
    }
}

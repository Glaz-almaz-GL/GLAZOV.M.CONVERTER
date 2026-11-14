using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats.Detection.Documents;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Archives;

/// <summary>
/// Archives: TAR.GZ, GZip, ZIP, TAR. A ZIP file that holds a document (DOCX, ODT and the like) is left to the document detector; when the start of
/// the input does not settle it, the second look reads the central directory and names ZIP only if no document format fits.
/// </summary>
internal sealed class ArchiveDetector() : FamilyDetector(
[
    (FileFormat.TarGz, TarGzSignature.Matches),
    (FileFormat.Gzip, GzipSignature.Matches),
    (FileFormat.Zip, ZipSignature.Matches),
    (FileFormat.Tar, TarSignature.Matches),
]), IStreamFormatDetector
{
    /// <inheritdoc />
    public bool CanInspect(ReadOnlySpan<byte> head)
    {
        return head.StartsWith(ZipPackage.ZipMagic);
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public async ValueTask<FileFormat?> DetectAsync(Stream input, CancellationToken cancellationToken = default)
    {
        if (await ContainerProbe.MatchAsync(input, cancellationToken).ConfigureAwait(false) is not null)
        {
            return null;
        }

        try
        {
            input.Position = 0;
            await using System.IO.Compression.ZipArchive zip = new(input, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true);
            return zip.Entries.Count > 0 ? FileFormat.Zip : null;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }
}
using GLAZOV.M.CONVERTER.ArchivesExtra.Detection;
using GLAZOV.M.CONVERTER.ArchivesExtra.Engines;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.ArchivesExtra;

/// <summary>
/// Extension "ArchivesExtra": archives of the extra kinds on SharpCompress. Input: 7z, RAR, XZ, TAR.XZ, BZip2, TAR.BZ2 (and the base kinds ZIP, TAR, TAR.GZ, GZip);
/// result: BZip2, TAR.BZ2 and the base kinds. 7z, RAR and XZ cannot be written, so they are never a result. Plugged in with
/// <c>ConverterSettings.Extensions</c>; works alone or together with the package of the base kinds. Needs no external program.
/// </summary>
public sealed class ArchivesExtraExtension : IConverterExtension
{
    /// <summary>
    /// Creates the extension with default settings.
    /// </summary>
    public ArchivesExtraExtension()
        : this(new ArchivesExtraSettings())
    {
    }

    /// <summary>
    /// Creates the extension with the given settings; their values are copied.
    /// </summary>
    /// <param name="settings">Settings of the engine.</param>
    public ArchivesExtraExtension(ArchivesExtraSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Engines = [new ExtraArchiveEngine(settings.Copy())];
    }

    /// <inheritdoc />
    public string Name => "ArchivesExtra";

    /// <inheritdoc />
    public IReadOnlyList<FileFormat> Formats { get; } =
    [
        ArchivesExtraFormats.SevenZip, ArchivesExtraFormats.Rar, ArchivesExtraFormats.Xz, ArchivesExtraFormats.TarXz, ArchivesExtraFormats.Bzip2, ArchivesExtraFormats.TarBz2,
        FileFormat.Zip, FileFormat.Tar, FileFormat.TarGz, FileFormat.Gzip,
    ];

    /// <inheritdoc />
    /// <remarks>
    /// The detector of the extra kinds and the archive detector of the standard formats (ZIP, TAR, TAR.GZ, GZip), so that the package works alone.
    /// </remarks>
    public IReadOnlyList<IFormatDetector> Detectors { get; } = [new ExtraArchiveDetector(), StandardDetectors.Archives];

    /// <inheritdoc />
    public IReadOnlyList<IConversionEngine> Engines { get; }
}

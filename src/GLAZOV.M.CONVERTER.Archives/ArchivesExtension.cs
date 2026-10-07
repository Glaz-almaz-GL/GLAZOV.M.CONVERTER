using GLAZOV.M.CONVERTER.Archives.Engines;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Archives;

/// <summary>
/// Extension "Archives": an archive becomes an archive of another kind — ZIP, TAR, TAR.GZ and GZip, every pair between them.
/// The entries move one by one, without unpacking to disk. Plugged in with <c>ConverterSettings.Extensions</c>.
/// Needs no external program.
/// </summary>
public sealed class ArchivesExtension : IConverterExtension
{
    /// <summary>
    /// Creates the extension with default settings.
    /// </summary>
    public ArchivesExtension()
        : this(new ArchivesSettings())
    {
    }

    /// <summary>
    /// Creates the extension with the given settings; their values are copied.
    /// </summary>
    /// <param name="settings">Settings of the archive engine.</param>
    public ArchivesExtension(ArchivesSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Engines = [new ArchiveEngine(settings.Copy())];
    }

    /// <inheritdoc />
    public string Name => "Archives";

    /// <inheritdoc />
    public IReadOnlyList<FileFormat> Formats { get; } = [FileFormat.Zip, FileFormat.Tar, FileFormat.TarGz, FileFormat.Gzip];

    /// <inheritdoc />
    /// <remarks>
    /// Archives: the detector of the standard formats from the Formats package.
    /// </remarks>
    public IReadOnlyList<IFormatDetector> Detectors { get; } = [StandardDetectors.Archives];

    /// <inheritdoc />
    public IReadOnlyList<IConversionEngine> Engines { get; }
}

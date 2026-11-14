using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Archives.Engines;

/// <summary>
/// Built-in archive conversion engine supporting ZIP, TAR, TAR.GZ, and GZip in every combination.
/// </summary>
internal sealed class ArchiveEngine(ArchivesSettings settings) : ArchiveEngineBase(settings)
{
    private static readonly FileFormat[] Kinds = [FileFormat.Zip, FileFormat.Tar, FileFormat.TarGz, FileFormat.Gzip];

    /// <inheritdoc />
    public override IReadOnlyList<ConversionPair> Pairs { get; } =
        [.. Kinds.SelectMany(from => Kinds.Select(to => new ConversionPair(from, to)))];
}
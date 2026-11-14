using GLAZOV.M.CONVERTER.Archives.Engines;
using GLAZOV.M.CONVERTER.Archives.Model;
using GLAZOV.M.CONVERTER.Archives.Reading;
using GLAZOV.M.CONVERTER.Archives.Writing;
using GLAZOV.M.CONVERTER.ArchivesExtra.Reading;
using GLAZOV.M.CONVERTER.ArchivesExtra.Writing;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Xz;
using SharpMode = SharpCompress.Compressors.CompressionMode;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Engines;

/// <summary>
/// Extended archive engine powered by SharpCompress: supports 7z, RAR, XZ, TAR.XZ, BZip2, and TAR.BZ2 as inputs,
/// and BZip2, TAR.BZ2, plus standard formats as outputs. Pairs between base formats are left to <see cref="ArchiveEngine"/>.
/// </summary>
internal sealed class ExtraArchiveEngine(ArchivesExtraSettings settings) : ArchiveEngineBase(settings)
{
    private static readonly FileFormat[] BaseFormats = [FileFormat.Zip, FileFormat.Tar, FileFormat.TarGz, FileFormat.Gzip];

    private static readonly FileFormat[] Inputs =
    [
        .. BaseFormats,
        ArchivesExtraFormats.SevenZip, ArchivesExtraFormats.Rar,
        ArchivesExtraFormats.Xz, ArchivesExtraFormats.TarXz,
        ArchivesExtraFormats.Bzip2, ArchivesExtraFormats.TarBz2,
    ];

    private static readonly FileFormat[] Outputs = [.. BaseFormats, ArchivesExtraFormats.Bzip2, ArchivesExtraFormats.TarBz2];

    /// <inheritdoc />
    public override IReadOnlyList<ConversionPair> Pairs { get; } =
        [.. Inputs.SelectMany(from => Outputs.Where(to => !(BaseFormats.Contains(from) && BaseFormats.Contains(to))).Select(to => new ConversionPair(from, to)))];

    /// <inheritdoc />
    protected override bool RequiresSeekableFile(FileFormat format)
    {
        return base.RequiresSeekableFile(format)
            || format.Equals(ArchivesExtraFormats.SevenZip)
            || format.Equals(ArchivesExtraFormats.Rar);
    }

    /// <inheritdoc />
    protected override IArchiveReader OpenReader(ConversionRequest request, string? filePath, Stream? countedStream)
    {
        FileFormat from = request.From;

        return from switch
        {
            _ when from.Equals(ArchivesExtraFormats.SevenZip) =>
                new SharpArchiveReader(filePath!, "7z"),

            _ when from.Equals(ArchivesExtraFormats.Rar) =>
                new SharpArchiveReader(filePath!, "RAR"),

            _ when from.Equals(ArchivesExtraFormats.TarXz) =>
                new TarGzReader(UnpackSharp(countedStream!, static s => new XZStream(s))),

            _ when from.Equals(ArchivesExtraFormats.Xz) =>
                new SingleFileArchiveReader(
                    UnpackSharp(countedStream!, static s => new XZStream(s)),
                    StripSingleArchiveExtension(request.InputPath, ".xz"),
                    DateTimeOffset.UtcNow),

            _ when from.Equals(ArchivesExtraFormats.TarBz2) =>
                new TarGzReader(UnpackSharp(countedStream!, static s => new BZip2Stream(s, SharpMode.Decompress, decompressConcatenated: false))),

            _ when from.Equals(ArchivesExtraFormats.Bzip2) =>
                new SingleFileArchiveReader(
                    UnpackSharp(countedStream!, static s => new BZip2Stream(s, SharpMode.Decompress, decompressConcatenated: false)),
                    StripSingleArchiveExtension(request.InputPath, ".bz2"),
                    DateTimeOffset.UtcNow),

            _ => base.OpenReader(request, filePath, countedStream)
        };
    }

    /// <inheritdoc />
    protected override IArchiveWriter OpenWriter(ConversionRequest request)
    {
        FileFormat to = request.To;

        if (to.Equals(ArchivesExtraFormats.TarBz2))
        {
            return new TarBz2ArchiveWriter(request.Output, request.TempFolder);
        }
        else if (to.Equals(ArchivesExtraFormats.Bzip2))
        {
            return new SingleFileArchiveWriter(
                new BZip2Stream(new KeepOpenStream(request.Output), SharpMode.Compress, decompressConcatenated: false),
                "BZip2");
        }

        return base.OpenWriter(request);
    }

    private static ForwardReadStream UnpackSharp(Stream packed, Func<Stream, Stream> factory)
    {
        try
        {
            return new ForwardReadStream(factory(packed));
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            throw new InvalidDataException($"Файл повреждён или оборван: {ex.Message}", ex);
        }
    }
}

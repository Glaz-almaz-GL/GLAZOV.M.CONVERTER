using System.IO.Compression;
using System.Text;
using GLAZOV.M.CONVERTER.ArchivesExtra.Model;
using GLAZOV.M.CONVERTER.ArchivesExtra.Reading;
using GLAZOV.M.CONVERTER.ArchivesExtra.Safety;
using GLAZOV.M.CONVERTER.ArchivesExtra.Writing;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.IO;
using NetMode = System.IO.Compression.CompressionMode;
using SharpMode = SharpCompress.Compressors.CompressionMode;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Xz;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Engines;

/// <summary>
/// Archives of the extra kinds: 7z, RAR, XZ, TAR.XZ, BZip2 and TAR.BZ2 in, the base kinds and BZip2 and TAR.BZ2 out. A pair whose both ends are base kinds
/// (ZIP to TAR and the like) is left to the package of the base kinds. The entries are read and written one by one.
/// </summary>
internal sealed class ExtraArchiveEngine : IConversionEngine
{
    private static readonly FileFormat[] Base = [FileFormat.Zip, FileFormat.Tar, FileFormat.TarGz, FileFormat.Gzip];

    private static readonly FileFormat[] Inputs =
    [
        .. Base,
        ArchivesExtraFormats.SevenZip, ArchivesExtraFormats.Rar, ArchivesExtraFormats.Xz, ArchivesExtraFormats.TarXz, ArchivesExtraFormats.Bzip2, ArchivesExtraFormats.TarBz2,
    ];

    private static readonly FileFormat[] Outputs = [.. Base, ArchivesExtraFormats.Bzip2, ArchivesExtraFormats.TarBz2];

    private readonly ArchivesExtraSettings _settings;
    private readonly ArchivesLimits _limits;
    private readonly Encoding _zipNames;

    /// <summary>
    /// Creates the engine.
    /// </summary>
    /// <param name="settings">Copy of the settings.</param>
    internal ExtraArchiveEngine(ArchivesExtraSettings settings)
    {
        _settings = settings;
        _limits = ArchivesLimits.From(settings);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _zipNames = Encoding.GetEncoding(settings.ZipNameCodePage);
    }

    /// <inheritdoc />
    public bool SupportsOperations => false;

    /// <inheritdoc />
    public IReadOnlyList<ConversionPair> Pairs { get; } =
        [.. Inputs.SelectMany(from => Outputs.Where(to => !(Base.Contains(from) && Base.Contains(to))).Select(to => new ConversionPair(from, to)))];

    /// <inheritdoc />
    public async Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool needsFile = request.From.Equals(FileFormat.Zip) || request.From.Equals(ArchivesExtraFormats.SevenZip) || request.From.Equals(ArchivesExtraFormats.Rar);
        await using InputFile? file = needsFile ? await request.EnsureInputFileAsync(cancellationToken).ConfigureAwait(false) : null;
        CountingStream? counted = file is null ? new CountingStream(request.Input) : null;
        long? inputBytes = file is not null ? new FileInfo(file.Path).Length : request.Input.CanSeek ? request.Input.Length : null;
        ArchiveGuard guard = new(_limits, inputBytes, () => counted?.Count ?? 0);

        await using IArchiveReader reader = OpenReader(request, file?.Path, counted);
        await using IArchiveWriter writer = OpenWriter(request);
        HashSet<string> names = new(StringComparer.Ordinal);
        long done = 0;
        while (await reader.NextAsync(cancellationToken).ConfigureAwait(false) is { } item)
        {
            string name = EntryNames.Normalize(item.Info.Name, item.Info.IsDirectory);
            if (!names.Add(name))
            {
                throw new InvalidDataException($"В архиве две записи с одним именем «{name}»: такой архив не переносится, чтобы не потерять одну из них.");
            }

            ArchiveEntryInfo info = item.Info with { Name = name };
            guard.BeginEntry(name, info.Length);
            Stream? content = item.Content is null ? null : new GuardedReadStream(item.Content, count => guard.Unpacked(name, count));
            await writer.WriteAsync(info, content, cancellationToken).ConfigureAwait(false);

            done += item.CompressedLength ?? 0;
            Report(request, inputBytes, counted?.Count ?? done);
        }

        writer.Complete();
        request.ReportProgress(100);
    }

    private static void Report(ConversionRequest request, long? inputBytes, long consumed)
    {
        if (inputBytes is > 0)
        {
            request.ReportProgress((int)Math.Min(99, consumed * 100 / inputBytes.Value));
        }
    }

    private IArchiveReader OpenReader(ConversionRequest request, string? path, CountingStream? counted)
    {
        FileFormat from = request.From;
        if (from.Equals(FileFormat.Zip))
        {
            return OpenZip(path!);
        }

        if (from.Equals(ArchivesExtraFormats.SevenZip))
        {
            return new SharpArchiveReader(path!, "7z");
        }

        if (from.Equals(ArchivesExtraFormats.Rar))
        {
            return new SharpArchiveReader(path!, "RAR");
        }

        if (from.Equals(FileFormat.Tar))
        {
            return new TarArchiveReader(counted!);
        }

        Stream unpacked = Unpack(from, counted!);
        return from.Equals(FileFormat.TarGz) || from.Equals(ArchivesExtraFormats.TarXz) || from.Equals(ArchivesExtraFormats.TarBz2)
            ? new TarGzReader(unpacked)
            : new SingleFileArchiveReader(unpacked, SingleName(request.InputPath), DateTimeOffset.UtcNow);
    }

    // The decompressors of SharpCompress read the header when they are made, so a cut or foreign file fails here, with an exception of their own.
    private static Stream Unpack(FileFormat from, Stream packed)
    {
        try
        {
            if (from.Equals(FileFormat.Gzip) || from.Equals(FileFormat.TarGz))
            {
                return new GZipStream(packed, NetMode.Decompress);
            }

            if (from.Equals(ArchivesExtraFormats.Xz) || from.Equals(ArchivesExtraFormats.TarXz))
            {
                return new ForwardReadStream(new XZStream(packed));
            }

            return new ForwardReadStream(new BZip2Stream(packed, SharpMode.Decompress, false));
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            throw new InvalidDataException($"Файл повреждён или оборван: {ex.Message}", ex);
        }
    }

    private ZipArchiveReader OpenZip(string path)
    {
        try
        {
            return new ZipArchiveReader(path, _zipNames);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException("Файл ZIP повреждён или оборван: " + ex.Message, ex);
        }
    }

    private IArchiveWriter OpenWriter(ConversionRequest request)
    {
        FileFormat to = request.To;
        if (to.Equals(FileFormat.Zip))
        {
            return new ZipArchiveWriter(request.Output, _settings.CompressionLevel);
        }

        if (to.Equals(FileFormat.Tar))
        {
            return new TarArchiveWriter(request.Output, request.TempFolder);
        }

        if (to.Equals(FileFormat.TarGz))
        {
            return new TarGzArchiveWriter(request.Output, _settings.CompressionLevel, request.TempFolder);
        }

        if (to.Equals(ArchivesExtraFormats.TarBz2))
        {
            return new TarBz2ArchiveWriter(request.Output, request.TempFolder);
        }

        return to.Equals(ArchivesExtraFormats.Bzip2)
            ? new SingleFileArchiveWriter(new BZip2Stream(new KeepOpenStream(request.Output), SharpMode.Compress, false), "BZip2")
            : new SingleFileArchiveWriter(new GZipStream(request.Output, _settings.CompressionLevel, leaveOpen: true), "GZip");
    }

    // The one file of a compressed input is named like the input without ".gz", ".bz2" or ".xz"; a stream has no name, so the file is called "data".
    private static string SingleName(string? inputPath)
    {
        string name = inputPath is null ? string.Empty : Path.GetFileName(inputPath);
        foreach (string extension in new[] { ".gz", ".bz2", ".xz" })
        {
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^extension.Length];
                break;
            }
        }

        return name.Length == 0 ? "data" : name;
    }
}

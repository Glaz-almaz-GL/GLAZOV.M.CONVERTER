using System.IO.Compression;
using System.Text;
using GLAZOV.M.CONVERTER.Archives.Model;
using GLAZOV.M.CONVERTER.Archives.Reading;
using GLAZOV.M.CONVERTER.Archives.Safety;
using GLAZOV.M.CONVERTER.Archives.Writing;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.IO;

namespace GLAZOV.M.CONVERTER.Archives.Engines;

/// <summary>
/// Archive to archive: ZIP, TAR, TAR.GZ and GZip, every pair between them (also the same kind into itself, which packs it again).
/// The entries are read and written one by one, so the archive is never held in memory; a ZIP input is read as a file.
/// </summary>
internal sealed class ArchiveEngine : IConversionEngine
{
    private static readonly FileFormat[] Kinds = [FileFormat.Zip, FileFormat.Tar, FileFormat.TarGz, FileFormat.Gzip];

    private readonly ArchivesSettings _settings;
    private readonly ArchivesLimits _limits;
    private readonly Encoding _zipNames;

    /// <summary>
    /// Creates the engine.
    /// </summary>
    /// <param name="settings">Copy of the settings.</param>
    internal ArchiveEngine(ArchivesSettings settings)
    {
        _settings = settings;
        _limits = ArchivesLimits.From(settings);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _zipNames = Encoding.GetEncoding(settings.ZipNameCodePage);
    }

    /// <inheritdoc />
    public bool SupportsOperations => false;

    /// <inheritdoc />
    public IReadOnlyList<ConversionPair> Pairs { get; } = [.. Kinds.SelectMany(from => Kinds.Select(to => new ConversionPair(from, to)))];

    /// <inheritdoc />
    public async Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using InputFile? zipFile = request.From.Equals(FileFormat.Zip) ? await request.EnsureInputFileAsync(cancellationToken).ConfigureAwait(false) : null;
        CountingStream? counted = zipFile is null ? new CountingStream(request.Input) : null;
        long? inputBytes = zipFile is not null ? new FileInfo(zipFile.Path).Length : request.Input.CanSeek ? request.Input.Length : null;
        ArchiveGuard guard = new(_limits, inputBytes, () => counted?.Count ?? 0);

        await using IArchiveReader reader = OpenReader(request, zipFile?.Path, counted);
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

    private IArchiveReader OpenReader(ConversionRequest request, string? zipPath, CountingStream? counted)
    {
        if (request.From.Equals(FileFormat.Zip))
        {
            return OpenZip(zipPath!);
        }

        if (request.From.Equals(FileFormat.Tar))
        {
            return new TarArchiveReader(counted!);
        }

        GZipStream unpacked = new(counted!, CompressionMode.Decompress);
        return request.From.Equals(FileFormat.TarGz)
            ? new TarGzReader(unpacked)
            : new GzipArchiveReader(unpacked, GzipEntryName(request.InputPath), DateTimeOffset.UtcNow);
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
        if (request.To.Equals(FileFormat.Zip))
        {
            return new ZipArchiveWriter(request.Output, _settings.CompressionLevel);
        }

        if (request.To.Equals(FileFormat.Tar))
        {
            return new TarArchiveWriter(request.Output, request.TempFolder);
        }

        return request.To.Equals(FileFormat.TarGz)
            ? new TarGzArchiveWriter(request.Output, _settings.CompressionLevel, request.TempFolder)
            : new GzipArchiveWriter(request.Output, _settings.CompressionLevel);
    }

    // The one file of a GZip input is named like the input without ".gz"; a stream has no name, so the file is called "data".
    private static string GzipEntryName(string? inputPath)
    {
        string name = inputPath is null ? string.Empty : Path.GetFileName(inputPath);
        if (name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^3];
        }

        return name.Length == 0 ? "data" : name;
    }
}

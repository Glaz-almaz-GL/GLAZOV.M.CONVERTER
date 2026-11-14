using GLAZOV.M.CONVERTER.Archives.Model;
using GLAZOV.M.CONVERTER.Archives.Reading;
using GLAZOV.M.CONVERTER.Archives.Safety;
using GLAZOV.M.CONVERTER.Archives.Writing;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.IO;
using System.IO.Compression;
using System.Text;

namespace GLAZOV.M.CONVERTER.Archives.Engines;

/// <summary>
/// Base conversion engine for streaming archive-to-archive repacking with archive-bomb protection.
/// Entries are streamed one by one without extracting the entire archive to disk.
/// </summary>
public abstract class ArchiveEngineBase : IConversionEngine
{
    private readonly ArchivesLimits _limits;

    /// <summary>
    /// Initializes the archive engine with a copy of the specified settings.
    /// </summary>
    /// <param name="settings">Copied archive settings.</param>
    protected ArchiveEngineBase(ArchivesSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
        _limits = ArchivesLimits.From(settings);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ZipNameEncoding = Encoding.GetEncoding(settings.ZipNameCodePage);
    }

    /// <summary>
    /// Copied settings active for this engine instance.
    /// </summary>
    protected ArchivesSettings Settings { get; }

    /// <summary>
    /// Encoding used for non-UTF-8 entry names in ZIP archives.
    /// </summary>
    protected Encoding ZipNameEncoding { get; }

    /// <inheritdoc />
    public bool SupportsOperations => false;

    /// <inheritdoc />
    public abstract IReadOnlyList<ConversionPair> Pairs { get; }

    /// <inheritdoc />
    public async Task ConvertAsync(ConversionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool needsFile = RequiresSeekableFile(request.From);
        await using InputFile? file = needsFile ? await request.EnsureInputFileAsync(cancellationToken).ConfigureAwait(false) : null;
        CountingStream? counted = file is null ? new CountingStream(request.Input) : null;

        long? inputBytes;
        if (request.Input.CanSeek)
        {
            // Returns Input Length
            inputBytes = file is null
            ? request.Input.Length
            : new FileInfo(file.Path).Length;
        }
        else
        {
            // Returns FileInfo Length
            inputBytes = file is null
            ? null
            : new FileInfo(file.Path).Length;
        }

        ArchiveGuard guard = new(_limits, inputBytes, () => counted?.Count ?? 0);

        await using IArchiveReader reader = OpenReader(request, file?.Path, counted);
        await using IArchiveWriter writer = OpenWriter(request);

        HashSet<string> seenNames = new(StringComparer.Ordinal);
        long consumedCompressed = 0;

        while (await reader.NextAsync(cancellationToken).ConfigureAwait(false) is { } item)
        {
            string normalizedName = EntryNames.Normalize(item.Info.Name, item.Info.IsDirectory);
            if (!seenNames.Add(normalizedName))
            {
                throw new InvalidDataException($"В архиве две записи с одним именем «{normalizedName}»: такой архив не переносится, чтобы не потерять одну из них.");
            }

            ArchiveEntryInfo info = item.Info with { Name = normalizedName };
            guard.BeginEntry(normalizedName, info.Length);

            Stream? content = item.Content is null
                ? null
                : new GuardedReadStream(item.Content, count => guard.Unpacked(normalizedName, count));

            await writer.WriteAsync(info, content, cancellationToken).ConfigureAwait(false);

            consumedCompressed += item.CompressedLength ?? 0;
            ReportProgress(request, inputBytes, counted?.Count ?? consumedCompressed);
        }

        writer.Complete();
        request.ReportProgress(100);
    }

    /// <summary>
    /// Determines whether the input format requires a random-access file on disk (such as ZIP, 7z, or RAR).
    /// </summary>
    protected virtual bool RequiresSeekableFile(FileFormat format)
    {
        return format.Equals(FileFormat.Zip);
    }

    /// <summary>
    /// Opens an <see cref="IArchiveReader"/> for the request input.
    /// </summary>
    protected virtual IArchiveReader OpenReader(ConversionRequest request, string? filePath, Stream? countedStream)
    {
        if (request.From.Equals(FileFormat.Zip))
        {
            return OpenZipReader(filePath!);
        }

        if (request.From.Equals(FileFormat.Tar))
        {
            return new TarArchiveReader(countedStream!);
        }

        if (request.From.Equals(FileFormat.TarGz))
        {
            return new TarGzReader(new GZipStream(countedStream!, CompressionMode.Decompress));
        }

        if (request.From.Equals(FileFormat.Gzip))
        {
            return new SingleFileArchiveReader(
                new GZipStream(countedStream!, CompressionMode.Decompress),
                StripSingleArchiveExtension(request.InputPath, ".gz"),
                DateTimeOffset.UtcNow);
        }

        // Unsupported archive format
        throw new NotSupportedException($"Unsupported input archive format: {request.From.Name}.");
    }

    /// <summary>
    /// Opens an <see cref="IArchiveWriter"/> for the request target.
    /// </summary>
    protected virtual IArchiveWriter OpenWriter(ConversionRequest request)
    {
        return request.To switch
        {
            FileFormat format when format.Equals(FileFormat.Zip) =>
                new ZipArchiveWriter(request.Output, Settings.CompressionLevel),

            FileFormat format when format.Equals(FileFormat.Tar) =>
                new TarArchiveWriter(request.Output, request.TempFolder),

            FileFormat format when format.Equals(FileFormat.TarGz) =>
                new TarGzArchiveWriter(request.Output, Settings.CompressionLevel, request.TempFolder),

            FileFormat format when format.Equals(FileFormat.Gzip) =>
                new SingleFileArchiveWriter(
                    new GZipStream(request.Output, Settings.CompressionLevel, leaveOpen: true),
                    "GZip"),

            _ => throw new NotSupportedException($"Unsupported target archive format: {request.To?.Name}.")
        };
    }


    /// <summary>
    /// Opens a ZIP reader and wraps format errors into <see cref="InvalidDataException"/>.
    /// </summary>
    protected ZipArchiveReader OpenZipReader(string path)
    {
        try
        {
            return new ZipArchiveReader(path, ZipNameEncoding);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException("Файл ZIP повреждён или оборван: " + ex.Message, ex);
        }
    }

    /// <summary>
    /// Derives the inner entry name for single-file compressed streams (such as .gz, .bz2, or .xz) by stripping known extensions.
    /// </summary>
    protected static string StripSingleArchiveExtension(string? inputPath, params ReadOnlySpan<string> extensions)
    {
        string name = inputPath is null ? string.Empty : Path.GetFileName(inputPath);
        foreach (string ext in extensions)
        {
            if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^ext.Length];
                break;
            }
        }

        return name.Length == 0 ? "data" : name;
    }

    private static void ReportProgress(ConversionRequest request, long? inputBytes, long consumed)
    {
        if (inputBytes is > 0)
        {
            request.ReportProgress((int)Math.Min(99, consumed * 100 / inputBytes.Value));
        }
    }
}

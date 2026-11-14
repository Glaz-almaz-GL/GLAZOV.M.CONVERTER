using GLAZOV.M.CONVERTER.Archives.Model;
using System.IO.Compression;

namespace GLAZOV.M.CONVERTER.Archives.Writing;

/// <summary>
/// Writes a ZIP archive to a stream, entry by entry. Names are UTF-8.
/// </summary>
/// <remarks>
/// Creates the writer.
/// </remarks>
/// <param name="output">Where the archive goes; it stays open.</param>
/// <param name="level">Compression level of the entries.</param>
public sealed class ZipArchiveWriter(Stream output, CompressionLevel level) : IArchiveWriter
{
    private static readonly DateTimeOffset EarliestZipTime = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ZipArchive _zip = new(output, ZipArchiveMode.Create, leaveOpen: true);
    private readonly CompressionLevel _level = level;

    /// <inheritdoc />
    public async Task WriteAsync(ArchiveEntryInfo info, Stream? content, CancellationToken cancellationToken = default)
    {
        ZipArchiveEntry entry = _zip.CreateEntry(info.Name, _level);

        // ZIP keeps the local wall-clock time without a zone.
        DateTimeOffset local = info.Modified.ToLocalTime();
        entry.LastWriteTime = local < EarliestZipTime ? EarliestZipTime : local;
        UnixFileMode mode = info.Mode ?? (info.IsDirectory ? (UnixFileMode)0b111_101_101 : (UnixFileMode)0b110_100_100);
        entry.ExternalAttributes = ((int)mode << 16) | (info.IsDirectory ? 0x10 : 0);
        if (content is null)
        {
            return;
        }

        Stream target = await entry.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (target.ConfigureAwait(false))
        {
            await content.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Complete()
    {
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _zip.Dispose();
        return ValueTask.CompletedTask;
    }
}

using System.IO.Compression;
using GLAZOV.M.CONVERTER.Archives.Model;

namespace GLAZOV.M.CONVERTER.Archives.Writing;

/// <summary>
/// Writes a GZip file: it holds exactly one file, so an archive with more entries (or with none, or with a folder only) cannot be put into it.
/// </summary>
internal sealed class GzipArchiveWriter : IArchiveWriter
{
    private readonly GZipStream _gzip;
    private int _entries;
    private bool _file;

    /// <summary>
    /// Creates the writer.
    /// </summary>
    /// <param name="output">Where the file goes; it stays open.</param>
    /// <param name="level">Compression level.</param>
    internal GzipArchiveWriter(Stream output, System.IO.Compression.CompressionLevel level)
    {
        _gzip = new GZipStream(output, level, leaveOpen: true);
    }

    /// <inheritdoc />
    public async Task WriteAsync(ArchiveEntryInfo info, Stream? content, CancellationToken cancellationToken)
    {
        _entries++;
        if (_entries > 1)
        {
            throw new InvalidDataException("В GZip помещается одна запись, а в архиве их больше: GZip сжимает один файл.");
        }

        if (content is null)
        {
            return;
        }

        _file = true;
        await content.CopyToAsync(_gzip, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Complete()
    {
        if (!_file)
        {
            throw new InvalidDataException("В GZip помещается одна запись-файл, а в архиве " + (_entries == 0 ? "нет записей." : "только папка."));
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _gzip.DisposeAsync();
    }
}

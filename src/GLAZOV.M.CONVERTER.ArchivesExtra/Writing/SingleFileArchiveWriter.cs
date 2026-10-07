using GLAZOV.M.CONVERTER.ArchivesExtra.Model;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Writing;

/// <summary>
/// Writes a compressed file (GZip, BZip2): it holds exactly one file, so an archive with more entries (or with none, or with a folder only) cannot be put into it.
/// </summary>
internal sealed class SingleFileArchiveWriter : IArchiveWriter
{
    private readonly Stream _compressor;
    private readonly string _kind;
    private int _entries;
    private bool _file;

    /// <summary>
    /// Creates the writer.
    /// </summary>
    /// <param name="compressor">The compressing stream the file goes through; disposing the writer finishes it.</param>
    /// <param name="kind">Name of the kind, for the messages.</param>
    internal SingleFileArchiveWriter(Stream compressor, string kind)
    {
        _compressor = compressor;
        _kind = kind;
    }

    /// <inheritdoc />
    public async Task WriteAsync(ArchiveEntryInfo info, Stream? content, CancellationToken cancellationToken)
    {
        _entries++;
        if (_entries > 1)
        {
            throw new InvalidDataException($"В {_kind} помещается одна запись, а в архиве их больше: {_kind} сжимает один файл.");
        }

        if (content is null)
        {
            return;
        }

        _file = true;
        await content.CopyToAsync(_compressor, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Complete()
    {
        if (!_file)
        {
            throw new InvalidDataException($"В {_kind} помещается одна запись-файл, а в архиве " + (_entries == 0 ? "нет записей." : "только папка."));
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _compressor.DisposeAsync();
    }
}

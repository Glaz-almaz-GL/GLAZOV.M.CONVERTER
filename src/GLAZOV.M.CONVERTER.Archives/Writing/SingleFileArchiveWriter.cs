using GLAZOV.M.CONVERTER.Archives.Model;

namespace GLAZOV.M.CONVERTER.Archives.Writing;

/// <summary>
/// Writes a single-file compressed stream (GZip, BZip2). Refuses empty, directory-only, or multi-entry archives.
/// </summary>
/// <remarks>
/// Initializes the writer.
/// </remarks>
/// <param name="compressor">Compressing stream wrapping the target output; disposed when the writer is disposed.</param>
/// <param name="kind">Format display name used in error messages.</param>
public sealed class SingleFileArchiveWriter(Stream compressor, string kind) : IArchiveWriter
{
    private readonly Stream _compressor = compressor;
    private readonly string _kind = kind;
    private int _entries;
    private bool _file;

    /// <inheritdoc />
    public async Task WriteAsync(ArchiveEntryInfo info, Stream? content, CancellationToken cancellationToken = default)
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

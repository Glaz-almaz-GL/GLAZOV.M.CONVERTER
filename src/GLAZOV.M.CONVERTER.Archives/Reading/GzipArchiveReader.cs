using GLAZOV.M.CONVERTER.Archives.Model;

namespace GLAZOV.M.CONVERTER.Archives.Reading;

/// <summary>
/// Reads a GZip stream as an archive of one entry: the file that was compressed.
/// </summary>
internal sealed class GzipArchiveReader : IArchiveReader
{
    private readonly Stream _unpacked;
    private readonly string _name;
    private readonly DateTimeOffset _modified;
    private bool _given;

    /// <summary>
    /// Creates the reader.
    /// </summary>
    /// <param name="unpacked">The unpacked stream.</param>
    /// <param name="name">Name for the one entry.</param>
    /// <param name="modified">Time of the entry.</param>
    internal GzipArchiveReader(Stream unpacked, string name, DateTimeOffset modified)
    {
        _unpacked = unpacked;
        _name = name;
        _modified = modified;
    }

    /// <inheritdoc />
    public Task<ArchiveItem?> NextAsync(CancellationToken cancellationToken)
    {
        if (_given)
        {
            return Task.FromResult<ArchiveItem?>(null);
        }

        _given = true;
        return Task.FromResult<ArchiveItem?>(new ArchiveItem(new ArchiveEntryInfo(_name, false, _modified, null, null), _unpacked));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return _unpacked.DisposeAsync();
    }
}

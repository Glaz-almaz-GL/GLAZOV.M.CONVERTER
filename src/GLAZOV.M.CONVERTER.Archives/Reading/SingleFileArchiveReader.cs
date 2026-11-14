using GLAZOV.M.CONVERTER.Archives.Model;

namespace GLAZOV.M.CONVERTER.Archives.Reading;

/// <summary>
/// Reads a single-file compressed stream (GZip, BZip2, XZ) as an archive containing one entry.
/// </summary>
/// <remarks>
/// Initializes the reader.
/// </remarks>
/// <param name="unpacked">Decompressed content stream.</param>
/// <param name="name">Entry name assigned to the single file.</param>
/// <param name="modified">Modification timestamp for the entry.</param>
public sealed class SingleFileArchiveReader(Stream unpacked, string name, DateTimeOffset modified) : IArchiveReader
{
    private readonly Stream _unpacked = unpacked;
    private readonly string _name = name;
    private readonly DateTimeOffset _modified = modified;
    private bool _given;

    /// <inheritdoc />
    public Task<ArchiveItem?> NextAsync(CancellationToken cancellationToken = default)
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
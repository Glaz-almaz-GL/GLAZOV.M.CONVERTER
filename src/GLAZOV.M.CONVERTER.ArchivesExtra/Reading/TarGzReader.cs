using GLAZOV.M.CONVERTER.ArchivesExtra.Model;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Reading;

/// <summary>
/// Reads the entries of a TAR archive compressed with GZip.
/// </summary>
internal sealed class TarGzReader : IArchiveReader
{
    private readonly Stream _unpacked;
    private readonly TarArchiveReader _tar;

    /// <summary>
    /// Creates the reader.
    /// </summary>
    /// <param name="unpacked">The unpacked (TAR) stream.</param>
    internal TarGzReader(Stream unpacked)
    {
        _unpacked = unpacked;
        _tar = new TarArchiveReader(unpacked);
    }

    /// <inheritdoc />
    public Task<ArchiveItem?> NextAsync(CancellationToken cancellationToken)
    {
        return _tar.NextAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _tar.DisposeAsync().ConfigureAwait(false);
        await _unpacked.DisposeAsync().ConfigureAwait(false);
    }
}

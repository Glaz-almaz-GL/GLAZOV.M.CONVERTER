using GLAZOV.M.CONVERTER.Archives.Model;

namespace GLAZOV.M.CONVERTER.Archives.Reading;

/// <summary>
/// Reads the entries of a TAR archive compressed with GZip.
/// </summary>
/// <remarks>
/// Creates the reader.
/// </remarks>
/// <param name="unpacked">The unpacked (TAR) stream.</param>
public sealed class TarGzReader(Stream unpacked) : IArchiveReader
{
    private readonly Stream _unpacked = unpacked;
    private readonly TarArchiveReader _tar = new(unpacked);

    /// <inheritdoc />
    public Task<ArchiveItem?> NextAsync(CancellationToken cancellationToken = default)
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

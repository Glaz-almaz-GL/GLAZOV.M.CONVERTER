using GLAZOV.M.CONVERTER.Archives.Model;
using System.IO.Compression;

namespace GLAZOV.M.CONVERTER.Archives.Writing;

/// <summary>
/// Writes a TAR archive compressed with GZip to a stream.
/// </summary>
public sealed class TarGzArchiveWriter : IArchiveWriter
{
    private readonly GZipStream _gzip;
    private readonly TarArchiveWriter _tar;

    /// <summary>
    /// Creates the writer.
    /// </summary>
    /// <param name="output">Where the archive goes; it stays open.</param>
    /// <param name="level">Compression level.</param>
    /// <param name="tempFolder">Folder for temporary files.</param>
    public TarGzArchiveWriter(Stream output, CompressionLevel level, string tempFolder)
    {
        _gzip = new GZipStream(output, level, leaveOpen: true);
        _tar = new TarArchiveWriter(_gzip, tempFolder);
    }

    /// <inheritdoc />
    public Task WriteAsync(ArchiveEntryInfo info, Stream? content, CancellationToken cancellationToken = default)
    {
        return _tar.WriteAsync(info, content, cancellationToken);
    }

    /// <inheritdoc />
    public void Complete()
    {
        _tar.Complete();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _tar.DisposeAsync().ConfigureAwait(false);
        await _gzip.DisposeAsync().ConfigureAwait(false);
    }
}

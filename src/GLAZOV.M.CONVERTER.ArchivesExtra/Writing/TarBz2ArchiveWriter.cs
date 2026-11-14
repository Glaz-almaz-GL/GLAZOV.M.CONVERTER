using GLAZOV.M.CONVERTER.Archives.Model;
using GLAZOV.M.CONVERTER.Archives.Writing;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;

namespace GLAZOV.M.CONVERTER.ArchivesExtra.Writing;

/// <summary>
/// Writes a TAR archive compressed with BZip2 to a stream.
/// </summary>
internal sealed class TarBz2ArchiveWriter : IArchiveWriter
{
    private readonly BZip2Stream _bzip2;
    private readonly TarArchiveWriter _tar;

    /// <summary>
    /// Creates the writer.
    /// </summary>
    /// <param name="output">Where the archive goes; it stays open.</param>
    /// <param name="tempFolder">Folder for temporary files.</param>
    internal TarBz2ArchiveWriter(Stream output, string tempFolder)
    {
        _bzip2 = new BZip2Stream(new KeepOpenStream(output), CompressionMode.Compress, false);
        _tar = new TarArchiveWriter(_bzip2, tempFolder);
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
        await _bzip2.DisposeAsync().ConfigureAwait(false);
    }
}

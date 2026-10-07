namespace GLAZOV.M.CONVERTER.Archives.Model;

/// <summary>
/// Writes the entries of an archive one after another. Disposing it finishes the archive.
/// </summary>
internal interface IArchiveWriter : IAsyncDisposable
{
    /// <summary>
    /// Writes one entry.
    /// </summary>
    /// <param name="info">Description of the entry; its name is already checked.</param>
    /// <param name="content">Content to copy (read to the end), <c>null</c> for a folder.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task WriteAsync(ArchiveEntryInfo info, Stream? content, CancellationToken cancellationToken);

    /// <summary>
    /// Checks that what was written makes a whole archive of this kind; called once, before disposing.
    /// </summary>
    void Complete();
}

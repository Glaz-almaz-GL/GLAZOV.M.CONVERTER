namespace GLAZOV.M.CONVERTER.ArchivesExtra.Model;

/// <summary>
/// Reads the entries of an archive one after another.
/// </summary>
internal interface IArchiveReader : IAsyncDisposable
{
    /// <summary>
    /// Returns the next entry, or <c>null</c> when there are no more. The content of the previous entry is no longer valid.
    /// </summary>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<ArchiveItem?> NextAsync(CancellationToken cancellationToken);
}

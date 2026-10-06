using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.IO;

/// <summary>
/// The input of a request as a file on disk: the real input file when there is one,
/// otherwise a copy made in the temp folder and deleted on dispose (also after failure and cancellation).
/// </summary>
public sealed class InputFile : IAsyncDisposable
{
    private readonly bool _owned;

    private InputFile(string path, bool owned)
    {
        Path = path;
        _owned = owned;
    }

    /// <summary>
    /// Full path of the file to read.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Returns <see cref="ConversionRequest.InputPath"/> when set, otherwise copies <see cref="ConversionRequest.Input"/> to a temp file.
    /// </summary>
    /// <param name="request">The request being served.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    internal static async Task<InputFile> EnsureAsync(ConversionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.InputPath is not null)
        {
            return new InputFile(request.InputPath, owned: false);
        }

        string path = System.IO.Path.Combine(request.TempFolder, $"input-{Guid.NewGuid():N}.tmp");
        InputFile file = new(path, owned: true);
        try
        {
            await using FileStream copy = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await request.Input.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await file.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        return file;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_owned && File.Exists(Path))
        {
            File.Delete(Path);
        }

        return ValueTask.CompletedTask;
    }
}

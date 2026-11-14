using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Extensions;

/// <summary>
/// Format detector that inspects the stream beyond the initial header window (for example, archives or compound containers).
/// Invoked only on seekable inputs and only after fast header detectors find no match.
/// </summary>
public interface IStreamFormatDetector : IFormatDetector
{
    /// <summary>
    /// Checks whether the leading bytes look like a container this detector can inspect.
    /// </summary>
    /// <param name="head">Leading bytes of the input.</param>
    bool CanInspect(ReadOnlySpan<byte> head);

    /// <summary>
    /// Detects the format by reading from the seekable stream. Position 0 is the start of the input;
    /// total bytes read are capped by the library's inspection budget.
    /// </summary>
    /// <param name="input">Seekable stream positioned at the start of the input.</param>
    /// <param name="cancellationToken">Token to cancel the inspection.</param>
    /// <returns>The detected <see cref="FileFormat"/>, or <c>null</c> if the input does not match.</returns>
    ValueTask<FileFormat?> DetectAsync(Stream input, CancellationToken cancellationToken = default);
}
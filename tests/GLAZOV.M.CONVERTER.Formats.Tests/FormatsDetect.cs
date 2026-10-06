using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.IO;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Formats.Tests;

/// <summary>
/// Type detection the way a user of the package does it: a converter with <see cref="FormatsExtension"/> and <c>Converter.DetectAsync</c>.
/// </summary>
internal static class FormatsDetect
{
    /// <summary>
    /// Detects the type of a file or stream.
    /// </summary>
    /// <param name="source">File or stream.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    internal static Task<FileFormat> DetectAsync(Source source, CancellationToken cancellationToken = default)
    {
        return FormatsConverter.Create().DetectAsync(source, cancellationToken);
    }

    /// <summary>
    /// Detects the type of an input that is already open; its start stays readable.
    /// </summary>
    /// <param name="handle">Open input.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    internal static Task<FileFormat> DetectAsync(InputHandle handle, CancellationToken cancellationToken = default)
    {
        return InputDetection.DetectAsync(handle, new DetectorChain(TestDetectors.Create()), cancellationToken);
    }

    /// <summary>
    /// Detects the type of an input that is already open with a chain made of the given detectors.
    /// </summary>
    /// <param name="handle">Open input.</param>
    /// <param name="chain">The chain.</param>
    internal static Task<FileFormat> DetectAsync(InputHandle handle, DetectorChain chain)
    {
        return InputDetection.DetectAsync(handle, chain);
    }

    /// <summary>
    /// Detects the type of an input with a chain made of the given detectors.
    /// </summary>
    /// <param name="source">File or stream.</param>
    /// <param name="chain">The chain.</param>
    internal static Task<FileFormat> DetectAsync(Source source, DetectorChain chain)
    {
        return InputDetection.DetectAsync(source, chain, CancellationToken.None);
    }
}

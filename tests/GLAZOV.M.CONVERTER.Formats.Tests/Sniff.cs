using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats.Detection;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Formats.Tests;

/// <summary>
/// Names the format by the signs in the leading bytes alone: the built-in detectors by label, without the second look and without the guess of text.
/// </summary>
internal static class Sniff
{
    /// <summary>
    /// Returns the format the leading bytes start like, or <see cref="FileFormat.Unknown"/>.
    /// </summary>
    /// <param name="head">Leading bytes of the input.</param>
    /// <param name="window">Detection window in bytes.</param>
    internal static FileFormat Match(ReadOnlySpan<byte> head, int window = DetectionSizes.MinimumWindow)
    {
        foreach (IFormatDetector detector in TestDetectors.Signs(window))
        {
            FileFormat? found = detector.Detect(head);
            if (found is not null)
            {
                return found;
            }
        }

        return FileFormat.Unknown;
    }
}

using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests;

/// <summary>
/// Names the format by the signs in the leading bytes alone: the standard detectors by label, without the second look and without the guess of text.
/// </summary>
internal static class Sniff
{
    private static readonly IFormatDetector[] Detectors =
        [StandardDetectors.Image, StandardDetectors.Video, StandardDetectors.Documents, StandardDetectors.Audio, StandardDetectors.Svg];

    /// <summary>
    /// Returns the format the leading bytes start like, or <see cref="FileFormat.Unknown"/>.
    /// </summary>
    /// <param name="head">Leading bytes of the input.</param>
    internal static FileFormat Match(ReadOnlySpan<byte> head)
    {
        foreach (IFormatDetector detector in Detectors)
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

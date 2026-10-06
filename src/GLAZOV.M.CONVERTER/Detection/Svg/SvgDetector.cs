using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Detection.Svg;

/// <summary>
/// SVG by its markup. The window limits how much of a compressed SVGZ is unpacked to find the root; a short compressed input unpacks to more than its own length,
/// so the window is kept here and not taken from the length of the head.
/// </summary>
/// <param name="window">Detection window in bytes.</param>
internal sealed class SvgDetector(int window = ConverterSettings.DefaultDetectionWindow) : IFormatDetector
{
    /// <inheritdoc />
    public int HeadLength { get; } = window;

    /// <inheritdoc />
    public FileFormat? Detect(ReadOnlySpan<byte> head)
    {
        return SvgSignature.Matches(head, HeadLength) ? FileFormat.Svg : null;
    }
}

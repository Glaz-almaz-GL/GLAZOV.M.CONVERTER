using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Svg;

/// <summary>
/// SVG by its markup. The converter reads the head of the input as long as its detection window; a compressed SVGZ is unpacked up to
/// the longer of that head and <paramref name="unpackLimit"/>, because a short compressed input unpacks to more than its own length.
/// </summary>
/// <param name="unpackLimit">Bytes of a compressed SVG that are unpacked at least.</param>
internal sealed class SvgDetector(int unpackLimit = DetectionSizes.SvgWindow) : IFormatDetector
{
    /// <inheritdoc />
    public int HeadLength => DetectionSizes.MinimumWindow;

    /// <inheritdoc />
    public FileFormat? Detect(ReadOnlySpan<byte> head)
    {
        return SvgSignature.Matches(head, Math.Max(unpackLimit, head.Length)) ? FileFormat.Svg : null;
    }
}

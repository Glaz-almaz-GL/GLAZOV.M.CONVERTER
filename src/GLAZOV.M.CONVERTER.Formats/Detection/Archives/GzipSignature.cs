using GLAZOV.M.CONVERTER.Formats.Detection.Svg;

namespace GLAZOV.M.CONVERTER.Formats.Detection.Archives;

/// <summary>
/// GZip: "1F 8B 08" and a whole 10-byte header. A compressed SVG (SVGZ) is also a GZip file, but it is named SVG.
/// </summary>
internal static class GzipSignature
{
    internal const int HeaderLength = 10;

    /// <summary>
    /// True when the data starts like this format.
    /// </summary>
    /// <param name="head">Leading bytes of the input (the detection window).</param>
    internal static bool Matches(ReadOnlySpan<byte> head)
    {
        return head.Length >= HeaderLength && head.StartsWith(new byte[] { 0x1F, 0x8B, 0x08 }) && !SvgSignature.Matches(head, Math.Max(DetectionSizes.SvgWindow, head.Length));
    }
}

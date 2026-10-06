namespace GLAZOV.M.CONVERTER.Formats.Detection;

/// <summary>
/// Sizes of the leading bytes the detectors of this package ask for.
/// </summary>
internal static class DetectionSizes
{
    /// <summary>
    /// Room for the longest MP3 frame (1441 bytes) and the header of the frame after it.
    /// </summary>
    internal const int MinimumWindow = 4096;

    /// <summary>
    /// How much of a compressed SVG (SVGZ) is unpacked to find the root when the input head is shorter; the head of a longer input is used as it is.
    /// </summary>
    internal const int SvgWindow = 64 * 1024;
}

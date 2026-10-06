namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// Sizes of the leading bytes that detection reads.
/// </summary>
internal static class DetectionSizes
{
    /// <summary>
    /// Smallest window: room for the longest MP3 frame (1441 bytes) and the header of the frame after it.
    /// A longer window is needed only by an SVG with a long preamble (setting <c>ConverterSettings.DetectionWindow</c>).
    /// </summary>
    internal const int MinimumWindow = 4096;
}

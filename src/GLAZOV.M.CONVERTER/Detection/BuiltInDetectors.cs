using GLAZOV.M.CONVERTER.Detection.Audio;
using GLAZOV.M.CONVERTER.Detection.Documents;
using GLAZOV.M.CONVERTER.Detection.Image;
using GLAZOV.M.CONVERTER.Detection.Svg;
using GLAZOV.M.CONVERTER.Detection.Video;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Detection;

/// <summary>
/// The detectors of the formats the library knows, in the order of asking.
/// </summary>
internal static class BuiltInDetectors
{
    /// <summary>
    /// Creates the detectors. Images, video and documents go first: they have strong signs and exclude each other.
    /// Audio goes after them because its last sign, MP3 without a tag, is the weakest (a frame sync). SVG goes last of the signs: it is found by markup.
    /// </summary>
    /// <param name="window">Detection window in bytes; SVGZ is unpacked up to it.</param>
    internal static IReadOnlyList<IFormatDetector> Create(int window = ConverterSettings.DefaultDetectionWindow)
    {
        return [new ImageDetector(), new VideoDetector(), new DocumentDetector(), new AudioDetector(), new SvgDetector(window)];
    }
}

using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats.Detection;
using GLAZOV.M.CONVERTER.Formats.Detection.Archives;
using GLAZOV.M.CONVERTER.Formats.Detection.Audio;
using GLAZOV.M.CONVERTER.Formats.Detection.Documents;
using GLAZOV.M.CONVERTER.Formats.Detection.Image;
using GLAZOV.M.CONVERTER.Formats.Detection.Svg;
using GLAZOV.M.CONVERTER.Formats.Detection.Tables;
using GLAZOV.M.CONVERTER.Formats.Detection.Text;
using GLAZOV.M.CONVERTER.Formats.Detection.Video;

namespace GLAZOV.M.CONVERTER.Formats.Tests;

/// <summary>
/// The detectors of the standard formats in the order of <see cref="FormatsExtension"/>, built fresh so that a test can set the SVG window.
/// </summary>
internal static class TestDetectors
{
    /// <summary>
    /// Image, video, document, archive, audio, SVG, table and text detectors.
    /// </summary>
    /// <param name="window">Bytes of a compressed SVG unpacked at least.</param>
    internal static IReadOnlyList<IFormatDetector> Create(int window = DetectionSizes.SvgWindow)
    {
        return [new ImageDetector(), new VideoDetector(), new DocumentDetector(), new ArchiveDetector(), new AudioDetector(), new SvgDetector(window), new TableDetector(), new TextDetector()];
    }

    /// <summary>
    /// The same without the content detectors (tables and text): signs only.
    /// </summary>
    /// <param name="window">Bytes of a compressed SVG unpacked at least.</param>
    internal static IReadOnlyList<IFormatDetector> Signs(int window = DetectionSizes.SvgWindow)
    {
        return [.. Create(window).Where(detector => detector is not TextDetector and not TableDetector)];
    }
}

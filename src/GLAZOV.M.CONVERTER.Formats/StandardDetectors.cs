using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats.Detection.Audio;
using GLAZOV.M.CONVERTER.Formats.Detection.Documents;
using GLAZOV.M.CONVERTER.Formats.Detection.Image;
using GLAZOV.M.CONVERTER.Formats.Detection.Svg;
using GLAZOV.M.CONVERTER.Formats.Detection.Text;
using GLAZOV.M.CONVERTER.Formats.Detection.Video;

namespace GLAZOV.M.CONVERTER.Formats;

/// <summary>
/// The detectors of the standard formats, one shared instance each. A package that needs some of them
/// names these instances in its <c>Detectors</c>; an instance named twice is asked once.
/// </summary>
public static class StandardDetectors
{
    /// <summary>
    /// Images: PNG, JPEG, GIF, TIFF, PSD, DDS, WebP, BMP.
    /// </summary>
    public static IFormatDetector Image { get; } = new ImageDetector();

    /// <summary>
    /// Video containers: MP4, MOV, MKV, WebM, AVI.
    /// </summary>
    public static IFormatDetector Video { get; } = new VideoDetector();

    /// <summary>
    /// Documents: PDF, RTF, Office and OpenDocument packages, old Office files, HTML. Also takes a second look at the structure of a container.
    /// </summary>
    public static IFormatDetector Documents { get; } = new DocumentDetector();

    /// <summary>
    /// Audio: OGG, FLAC, WAV, M4A and, as the weakest sign, MP3.
    /// </summary>
    public static IFormatDetector Audio { get; } = new AudioDetector();

    /// <summary>
    /// SVG by its markup, including compressed SVGZ.
    /// </summary>
    public static IFormatDetector Svg { get; } = new SvgDetector();

    /// <summary>
    /// Plain text, by content: data that no other sign claimed and that reads as text. It has no label of its own,
    /// so it has to be connected after every other detector by content.
    /// </summary>
    public static IFormatDetector Text { get; } = new TextDetector();
}

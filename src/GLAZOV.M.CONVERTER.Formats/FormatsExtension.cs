using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;

namespace GLAZOV.M.CONVERTER.Formats;

/// <summary>
/// Extension "Formats": every standard format and its detectors, and no engine. Plugged in through
/// <c>ConverterSettings.Extensions</c> where only the type of a file is asked (<c>Converter.DetectAsync</c>).
/// </summary>
public sealed class FormatsExtension : IConverterExtension
{
    /// <inheritdoc />
    public string Name => "Formats";

    /// <inheritdoc />
    public IReadOnlyList<FileFormat> Formats { get; } =
    [
        AudioFormats.Mp3, AudioFormats.Wav, AudioFormats.Ogg, AudioFormats.Flac, AudioFormats.M4a,
        ImageFormats.Jpeg, ImageFormats.Png, ImageFormats.Webp, ImageFormats.Bmp, ImageFormats.Gif, ImageFormats.Tiff, ImageFormats.Tga, ImageFormats.Dds, ImageFormats.Psd, ImageFormats.Avif, ImageFormats.Ico, ImageFormats.Svg,
        VideoFormats.Mp4, VideoFormats.Mkv, VideoFormats.Webm, VideoFormats.Mov, VideoFormats.Avi,
        DocumentFormats.Pdf, DocumentFormats.Docx, DocumentFormats.Doc, DocumentFormats.Odt, DocumentFormats.Rtf,
        DocumentFormats.Xlsx, DocumentFormats.Xls, DocumentFormats.Ods, DocumentFormats.Pptx, DocumentFormats.Ppt, DocumentFormats.Odp,
        DocumentFormats.Html, DocumentFormats.Text,
        ArchiveFormats.Zip, ArchiveFormats.Tar, ArchiveFormats.TarGz, ArchiveFormats.Gzip,
    ];

    /// <inheritdoc />
    /// <remarks>
    /// Images, video and documents go first: they have strong signs and exclude each other. Audio goes after them
    /// because its last sign, MP3 without a tag, is the weakest. SVG is found by markup; archives go after documents, whose packages
    /// are ZIP files too (the archive detector leaves them alone); text goes last.
    /// </remarks>
    public IReadOnlyList<IFormatDetector> Detectors { get; } =
    [
        StandardDetectors.Image, StandardDetectors.Video, StandardDetectors.Documents, StandardDetectors.Archives, StandardDetectors.Audio, StandardDetectors.Svg, StandardDetectors.Text,
    ];

    /// <inheritdoc />
    public IReadOnlyList<IConversionEngine> Engines { get; } = [];
}

using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Formats.Tests.Values;

/// <summary>
/// The values of the standard formats: name, MIME type and extension equal the ones the core carried before they moved here,
/// the written form <c>FileFormat.X</c> returns the same value, and the extension declares every one of them.
/// </summary>
public sealed class FormatValuesTests
{
    private static readonly (string Field, string Name, string Mime, string Extension)[] Expected =
    [
        ("Mp3", "MP3", "audio/mpeg", "mp3"),
        ("Wav", "WAV", "audio/wav", "wav"),
        ("Ogg", "OGG", "audio/ogg", "ogg"),
        ("Jpeg", "JPEG", "image/jpeg", "jpg"),
        ("Flac", "FLAC", "audio/flac", "flac"),
        ("M4a", "M4A", "audio/mp4", "m4a"),
        ("Png", "PNG", "image/png", "png"),
        ("Webp", "WebP", "image/webp", "webp"),
        ("Bmp", "BMP", "image/bmp", "bmp"),
        ("Gif", "GIF", "image/gif", "gif"),
        ("Tiff", "TIFF", "image/tiff", "tiff"),
        ("Tga", "TGA", "image/x-tga", "tga"),
        ("Dds", "DDS", "image/vnd.ms-dds", "dds"),
        ("Psd", "PSD", "image/vnd.adobe.photoshop", "psd"),
        ("Svg", "SVG", "image/svg+xml", "svg"),
        ("Mp4", "MP4", "video/mp4", "mp4"),
        ("Mkv", "MKV", "video/x-matroska", "mkv"),
        ("Webm", "WebM", "video/webm", "webm"),
        ("Mov", "MOV", "video/quicktime", "mov"),
        ("Avi", "AVI", "video/x-msvideo", "avi"),
        ("Pdf", "PDF", "application/pdf", "pdf"),
        ("Docx", "DOCX", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"),
        ("Doc", "DOC", "application/msword", "doc"),
        ("Odt", "ODT", "application/vnd.oasis.opendocument.text", "odt"),
        ("Rtf", "RTF", "application/rtf", "rtf"),
        ("Xlsx", "XLSX", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
        ("Xls", "XLS", "application/vnd.ms-excel", "xls"),
        ("Ods", "ODS", "application/vnd.oasis.opendocument.spreadsheet", "ods"),
        ("Pptx", "PPTX", "application/vnd.openxmlformats-officedocument.presentationml.presentation", "pptx"),
        ("Ppt", "PPT", "application/vnd.ms-powerpoint", "ppt"),
        ("Odp", "ODP", "application/vnd.oasis.opendocument.presentation", "odp"),
        ("Html", "HTML", "text/html", "html"),
        ("Text", "Text", "text/plain", "txt"),
    ];

    private static readonly Type[] Classes = [typeof(AudioFormats), typeof(ImageFormats), typeof(VideoFormats), typeof(DocumentFormats)];

    private static FileFormat Field(string field)
    {
        return Classes.Select(type => type.GetProperty(field)).Single(property => property is not null)!.GetValue(null) as FileFormat
            ?? throw new InvalidOperationException(field);
    }

    public static TheoryData<string> Fields => [.. Expected.Select(row => row.Field)];

    [Theory]
    [MemberData(nameof(Fields))]
    public void Value_HasTheFormerNameMimeTypeAndExtension(string field)
    {
        (string _, string name, string mime, string extension) = Expected.Single(row => row.Field == field);

        FileFormat format = Field(field);

        Assert.Equal((name, mime, extension), (format.Name, format.MimeType, format.Extension));
    }

    [Fact]
    public void EveryPublicFieldOfTheClasses_IsInTheList_AndTheListHasNoOthers()
    {
        string[] fields = [.. Classes.SelectMany(type => type.GetProperties().Select(property => property.Name))];

        Assert.Equal(Expected.Select(row => row.Field).Order(), fields.Order());
    }

    [Fact]
    public void WrittenForm_ReturnsTheSameValueAsTheField()
    {
        Assert.Same(AudioFormats.Mp3, FileFormat.Mp3);
        Assert.Same(AudioFormats.M4a, FileFormat.M4a);
        Assert.Same(ImageFormats.Svg, FileFormat.Svg);
        Assert.Same(ImageFormats.Jpeg, FileFormat.Jpeg);
        Assert.Same(VideoFormats.Mkv, FileFormat.Mkv);
        Assert.Same(DocumentFormats.Pdf, FileFormat.Pdf);
        Assert.Same(DocumentFormats.Text, FileFormat.Text);
    }

    [Fact]
    public void FormatsExtension_DeclaresEveryValueOnce_AndHasNoEngine()
    {
        FormatsExtension extension = new();

        Assert.Equal(Expected.Select(row => Field(row.Field).Name).Order(), extension.Formats.Select(format => format.Name).Order());
        Assert.Equal(extension.Formats.Count, extension.Formats.Distinct().Count());
        Assert.Empty(extension.Engines);
    }

    [Fact]
    public void FormatsExtension_NamesEveryStandardDetectorOnce_TextLast()
    {
        FormatsExtension extension = new();

        Assert.Equal(
            [StandardDetectors.Image, StandardDetectors.Video, StandardDetectors.Documents, StandardDetectors.Audio, StandardDetectors.Svg, StandardDetectors.Text],
            extension.Detectors);
    }

    [Fact]
    public void TheConverterAcceptsTheExtension_NoTwoValuesShareAMimeType()
    {
        Assert.NotNull(FormatsConverter.Create());
    }
}

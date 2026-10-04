using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Tests.Data;

/// <summary>
/// Формат как расширяемый тип: готовые значения несут имя, MIME и расширение, а свой формат создаётся без правки библиотеки.
/// </summary>
public class FileFormatTests
{
    [Fact]
    public void BuiltInFormats_CarryNameMimeAndExtension()
    {
        Assert.Equal(("MP3", "audio/mpeg", "mp3"), (FileFormat.Mp3.Name, FileFormat.Mp3.MimeType, FileFormat.Mp3.Extension));
        Assert.Equal(("WAV", "audio/wav", "wav"), (FileFormat.Wav.Name, FileFormat.Wav.MimeType, FileFormat.Wav.Extension));
        Assert.Equal(("OGG", "audio/ogg", "ogg"), (FileFormat.Ogg.Name, FileFormat.Ogg.MimeType, FileFormat.Ogg.Extension));
        Assert.Equal(("JPEG", "image/jpeg", "jpg"), (FileFormat.Jpeg.Name, FileFormat.Jpeg.MimeType, FileFormat.Jpeg.Extension));
    }

    [Fact]
    public void DocumentFormats_CarryNameMimeAndExtension()
    {
        Assert.Equal(("PDF", "application/pdf", "pdf"), (FileFormat.Pdf.Name, FileFormat.Pdf.MimeType, FileFormat.Pdf.Extension));
        Assert.Equal(("DOCX", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "docx"), (FileFormat.Docx.Name, FileFormat.Docx.MimeType, FileFormat.Docx.Extension));
        Assert.Equal(("DOC", "application/msword", "doc"), (FileFormat.Doc.Name, FileFormat.Doc.MimeType, FileFormat.Doc.Extension));
        Assert.Equal(("ODT", "application/vnd.oasis.opendocument.text", "odt"), (FileFormat.Odt.Name, FileFormat.Odt.MimeType, FileFormat.Odt.Extension));
        Assert.Equal(("RTF", "application/rtf", "rtf"), (FileFormat.Rtf.Name, FileFormat.Rtf.MimeType, FileFormat.Rtf.Extension));
        Assert.Equal(("XLSX", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"), (FileFormat.Xlsx.Name, FileFormat.Xlsx.MimeType, FileFormat.Xlsx.Extension));
        Assert.Equal(("XLS", "application/vnd.ms-excel", "xls"), (FileFormat.Xls.Name, FileFormat.Xls.MimeType, FileFormat.Xls.Extension));
        Assert.Equal(("ODS", "application/vnd.oasis.opendocument.spreadsheet", "ods"), (FileFormat.Ods.Name, FileFormat.Ods.MimeType, FileFormat.Ods.Extension));
        Assert.Equal(("PPTX", "application/vnd.openxmlformats-officedocument.presentationml.presentation", "pptx"), (FileFormat.Pptx.Name, FileFormat.Pptx.MimeType, FileFormat.Pptx.Extension));
        Assert.Equal(("PPT", "application/vnd.ms-powerpoint", "ppt"), (FileFormat.Ppt.Name, FileFormat.Ppt.MimeType, FileFormat.Ppt.Extension));
        Assert.Equal(("ODP", "application/vnd.oasis.opendocument.presentation", "odp"), (FileFormat.Odp.Name, FileFormat.Odp.MimeType, FileFormat.Odp.Extension));
        Assert.Equal(("HTML", "text/html", "html"), (FileFormat.Html.Name, FileFormat.Html.MimeType, FileFormat.Html.Extension));
        Assert.Equal(("Text", "text/plain", "txt"), (FileFormat.Text.Name, FileFormat.Text.MimeType, FileFormat.Text.Extension));
    }

    [Fact]
    public void DocumentFormats_AreDistinctFromEachOtherAndFromTheOlderOnes()
    {
        FileFormat[] documents = [FileFormat.Pdf, FileFormat.Docx, FileFormat.Doc, FileFormat.Odt, FileFormat.Rtf, FileFormat.Xlsx, FileFormat.Xls,
            FileFormat.Ods, FileFormat.Pptx, FileFormat.Ppt, FileFormat.Odp, FileFormat.Html, FileFormat.Text];

        Assert.Equal(documents.Length, documents.Distinct().Count());
        Assert.DoesNotContain(FileFormat.Unknown, documents);
        Assert.All(documents, d => Assert.NotEqual(FileFormat.Svg, d));
    }

    [Fact]
    public void Unknown_IsTheDetectionResultForUnrecognizedData_AndCannotBeATarget()
    {
        Assert.Equal("Unknown", FileFormat.Unknown.Name);
        Assert.Equal(string.Empty, FileFormat.Unknown.Extension);

        Assert.Throws<ArgumentException>(() => Target.Memory(FileFormat.Unknown));
        Assert.Throws<ArgumentException>(() => Target.File("a.bin", FileFormat.Unknown));
        Assert.Throws<ArgumentException>(() => Target.Folder("out", FileFormat.Unknown));
        Assert.Throws<ArgumentException>(() => Target.Stream(new MemoryStream(), FileFormat.Unknown));
    }

    [Theory]
    [InlineData("FLAC", "audio/flac", "flac")]
    [InlineData("M4A", "audio/mp4", "m4a")]
    [InlineData("PNG", "image/png", "png")]
    [InlineData("WebP", "image/webp", "webp")]
    [InlineData("BMP", "image/bmp", "bmp")]
    [InlineData("GIF", "image/gif", "gif")]
    [InlineData("TIFF", "image/tiff", "tiff")]
    [InlineData("TGA", "image/x-tga", "tga")]
    [InlineData("DDS", "image/vnd.ms-dds", "dds")]
    [InlineData("PSD", "image/vnd.adobe.photoshop", "psd")]
    [InlineData("SVG", "image/svg+xml", "svg")]
    public void NewFormats_CarryNameMimeAndExtension(string name, string mime, string extension)
    {
        FileFormat[] all =
        [
            FileFormat.Flac, FileFormat.M4a, FileFormat.Png, FileFormat.Webp, FileFormat.Bmp, FileFormat.Gif,
            FileFormat.Tiff, FileFormat.Tga, FileFormat.Dds, FileFormat.Psd, FileFormat.Svg,
        ];

        FileFormat found = Assert.Single(all, f => f.Name == name);

        Assert.Equal(mime, found.MimeType);
        Assert.Equal(extension, found.Extension);
    }

    [Fact]
    public void AllReadyFormats_AreDistinct_AndNoneIsUnknown()
    {
        FileFormat[] all =
        [
            FileFormat.Mp3, FileFormat.Wav, FileFormat.Ogg, FileFormat.Jpeg, FileFormat.Flac, FileFormat.M4a, FileFormat.Png,
            FileFormat.Webp, FileFormat.Bmp, FileFormat.Gif, FileFormat.Tiff, FileFormat.Tga, FileFormat.Dds, FileFormat.Psd, FileFormat.Svg,
        ];

        Assert.Equal(all.Length, all.Distinct().Count());
        Assert.DoesNotContain(FileFormat.Unknown, all);
    }

    [Fact]
    public void Create_MakesAFormatForAnUnregisteredType_AndNormalizesTheExtension()
    {
        FileFormat flac = FileFormat.Create("FLAC", "audio/flac", ".FLAC");

        Assert.Equal("FLAC", flac.Name);
        Assert.Equal("audio/flac", flac.MimeType);
        Assert.Equal("flac", flac.Extension);
        Assert.Equal("FLAC", flac.ToString());
    }

    [Fact]
    public void Equality_IsByValue_NotByReference()
    {
        Assert.Equal(FileFormat.Create("MP3", "audio/mpeg", "mp3"), FileFormat.Mp3);
        Assert.Equal(FileFormat.Create("mp3", "AUDIO/MPEG", ".MP3").GetHashCode(), FileFormat.Mp3.GetHashCode());
        Assert.NotEqual(FileFormat.Mp3, FileFormat.Wav);
        Assert.False(FileFormat.Mp3.Equals(null));
    }

    [Theory]
    [InlineData("", "audio/flac", "flac")]
    [InlineData("FLAC", " ", "flac")]
    [InlineData("FLAC", "audio/flac", "")]
    [InlineData("FLAC", "audio/flac", ".")]
    [InlineData("FLAC", "audio/flac", "tar.gz")]
    [InlineData("FLAC", "audio/flac", "fl/ac")]
    public void Create_RejectsEmptyAndUnsuitableParts(string name, string mime, string extension)
    {
        Assert.ThrowsAny<ArgumentException>(() => FileFormat.Create(name, mime, extension));
    }
}

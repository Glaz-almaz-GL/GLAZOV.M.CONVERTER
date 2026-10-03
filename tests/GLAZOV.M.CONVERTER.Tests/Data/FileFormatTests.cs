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
    public void Unknown_IsTheDetectionResultForUnrecognizedData_AndCannotBeATarget()
    {
        Assert.Equal("Unknown", FileFormat.Unknown.Name);
        Assert.Equal(string.Empty, FileFormat.Unknown.Extension);

        Assert.Throws<ArgumentException>(() => Target.Memory(FileFormat.Unknown));
        Assert.Throws<ArgumentException>(() => Target.File("a.bin", FileFormat.Unknown));
        Assert.Throws<ArgumentException>(() => Target.Folder("out", FileFormat.Unknown));
        Assert.Throws<ArgumentException>(() => Target.Stream(new MemoryStream(), FileFormat.Unknown));
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

using GLAZOV.M.CONVERTER.Data;

namespace GLAZOV.M.CONVERTER.Tests.Data;

/// <summary>
/// Формат как расширяемый тип: готовые значения несут имя, MIME и расширение, а свой формат создаётся без правки библиотеки.
/// </summary>
public class FormatTests
{
    [Fact]
    public void BuiltInFormats_CarryNameMimeAndExtension()
    {
        Assert.Equal(("MP3", "audio/mpeg", "mp3"), (Format.Mp3.Name, Format.Mp3.MimeType, Format.Mp3.Extension));
        Assert.Equal(("WAV", "audio/wav", "wav"), (Format.Wav.Name, Format.Wav.MimeType, Format.Wav.Extension));
        Assert.Equal(("OGG", "audio/ogg", "ogg"), (Format.Ogg.Name, Format.Ogg.MimeType, Format.Ogg.Extension));
        Assert.Equal(("JPEG", "image/jpeg", "jpg"), (Format.Jpeg.Name, Format.Jpeg.MimeType, Format.Jpeg.Extension));
    }

    [Fact]
    public void Unknown_IsTheDetectionResultForUnrecognizedData_AndCannotBeATarget()
    {
        Assert.Equal("Unknown", Format.Unknown.Name);
        Assert.Equal(string.Empty, Format.Unknown.Extension);

        Assert.Throws<ArgumentException>(() => Target.Memory(Format.Unknown));
        Assert.Throws<ArgumentException>(() => Target.File("a.bin", Format.Unknown));
        Assert.Throws<ArgumentException>(() => Target.Folder("out", Format.Unknown));
        Assert.Throws<ArgumentException>(() => Target.Stream(new MemoryStream(), Format.Unknown));
    }

    [Fact]
    public void Create_MakesAFormatForAnUnregisteredType_AndNormalizesTheExtension()
    {
        Format flac = Format.Create("FLAC", "audio/flac", ".FLAC");

        Assert.Equal("FLAC", flac.Name);
        Assert.Equal("audio/flac", flac.MimeType);
        Assert.Equal("flac", flac.Extension);
        Assert.Equal("FLAC", flac.ToString());
    }

    [Fact]
    public void Equality_IsByValue_NotByReference()
    {
        Assert.Equal(Format.Create("MP3", "audio/mpeg", "mp3"), Format.Mp3);
        Assert.Equal(Format.Create("mp3", "AUDIO/MPEG", ".MP3").GetHashCode(), Format.Mp3.GetHashCode());
        Assert.NotEqual(Format.Mp3, Format.Wav);
        Assert.False(Format.Mp3.Equals(null));
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
        Assert.ThrowsAny<ArgumentException>(() => Format.Create(name, mime, extension));
    }
}

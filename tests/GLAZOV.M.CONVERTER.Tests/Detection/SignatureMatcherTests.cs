using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;

namespace GLAZOV.M.CONVERTER.Tests.Detection;

/// <summary>
/// Определение типа по начальным байтам на настоящих файлах (папка Samples) и на намеренно испорченных данных.
/// </summary>
public class SignatureMatcherTests
{
    private static byte[] Sample(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", name));

    [Theory]
    [InlineData("sample-id3.mp3", "MP3")]
    [InlineData("sample-raw.mp3", "MP3")]
    [InlineData("sample.wav", "WAV")]
    [InlineData("sample-vorbis.ogg", "OGG")]
    [InlineData("sample-opus.ogg", "OGG")]
    public void Match_RealFile_ReturnsItsType(string file, string expected)
    {
        var bytes = Sample(file);

        var type = SignatureMatcher.Match(bytes.AsSpan(0, Math.Min(bytes.Length, SignatureMatcher.HeadLength)));

        Assert.Equal(expected, type.Name);
    }

    [Fact]
    public void Match_OnlyBytesCount_NameOfTheFileIsNotAnInput()
    {
        // Те же байты MP3 — тип определяется по ним, как бы файл ни назывался: у буфера имени нет вовсе.
        var bytes = Sample("sample-id3.mp3");

        Assert.Same(FileFormat.Mp3, SignatureMatcher.Match(bytes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(11)]
    public void Match_EmptyOrTooShort_IsUnknownAndDoesNotThrow(int length)
    {
        var wav = Sample("sample.wav");

        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(wav.AsSpan(0, length)));
    }

    [Fact]
    public void Match_ZeroesAndText_AreUnknown()
    {
        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(new byte[4096]));
        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match("Просто текстовый файл, а не звук."u8));
    }

    [Fact]
    public void Match_CorruptedOggSignature_IsNotOgg()
    {
        var bytes = Sample("sample-vorbis.ogg");
        bytes[0] = (byte)'X';

        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(bytes));
    }

    [Fact]
    public void Match_RiffThatIsNotWave_IsUnknown()
    {
        var bytes = Sample("sample.wav");
        bytes[8] = (byte)'A'; // «AVE…» вместо «WAVE»

        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(bytes));
    }

    [Fact]
    public void Match_Mp3FrameWithoutSecondFrame_IsUnknown()
    {
        // Один кадр без подтверждающего второго — слишком слабый признак: данные не принимаются за MP3.
        var raw = Sample("sample-raw.mp3");

        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(raw.AsSpan(0, 120)));
    }

    [Fact]
    public void Match_ThousandRandomBuffers_NeverLookLikeAudio()
    {
        var random = new Random(20261003);
        var buffer = new byte[SignatureMatcher.HeadLength];

        for (var i = 0; i < 1000; i++)
        {
            random.NextBytes(buffer);

            Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(buffer));
        }
    }

    [Theory]
    [InlineData("sample.flac", "FLAC")]
    [InlineData("sample.m4a", "M4A")]
    [InlineData("sample.png", "PNG")]
    [InlineData("sample.jpg", "JPEG")]
    [InlineData("sample.webp", "WebP")]
    [InlineData("sample.gif", "GIF")]
    [InlineData("sample.bmp", "BMP")]
    [InlineData("sample.tiff", "TIFF")]
    [InlineData("sample.dds", "DDS")]
    [InlineData("sample.psd", "PSD")]
    [InlineData("sample-plain.svg", "SVG")]
    [InlineData("sample-prolog.svg", "SVG")]
    public void Match_NewRealFile_ReturnsItsType(string file, string expected)
    {
        byte[] bytes = Sample(file);

        FileFormat type = SignatureMatcher.Match(bytes.AsSpan(0, Math.Min(bytes.Length, SignatureMatcher.HeadLength)));

        Assert.Equal(expected, type.Name);
    }

    [Fact]
    public void Match_Tga_HasNoSignature_SoItIsUnknown()
    {
        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(Sample("sample.tga")));
    }

    [Fact]
    public void Match_XmlThatIsNotSvg_AndSvgWordInsideText_AreUnknown()
    {
        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(Sample("sample-notsvg.xml")));
        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match("Это письмо про формат <svg и ничего больше."u8));
        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match("<svgfoo></svgfoo>"u8));
    }

    [Fact]
    public void Match_Mp4WithAmbiguousBrand_IsUnknownUntilVideoIsSupported()
    {
        byte[] isom = [0, 0, 0, 0x18, .. "ftypisom"u8.ToArray(), 0, 0, 2, 0, .. "isomiso2"u8.ToArray()];
        byte[] mp42 = [0, 0, 0, 0x18, .. "ftypmp42"u8.ToArray(), 0, 0, 0, 0, .. "mp42isom"u8.ToArray()];

        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(isom));
        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(mp42));
    }

    [Theory]
    [InlineData("sample.flac", 3)]
    [InlineData("sample.m4a", 9)]
    [InlineData("sample.png", 7)]
    [InlineData("sample.jpg", 2)]
    [InlineData("sample.webp", 11)]
    [InlineData("sample.gif", 5)]
    [InlineData("sample.bmp", 17)]
    [InlineData("sample.tiff", 3)]
    [InlineData("sample.dds", 79)]
    [InlineData("sample.psd", 11)]
    [InlineData("sample-plain.svg", 3)]
    public void Match_NewFormatsTruncatedInsideTheSignature_AreUnknown(string file, int keep)
    {
        byte[] bytes = Sample(file);

        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(bytes.AsSpan(0, keep)));
    }

    [Fact]
    public void Match_BmpLookingStartWithBadFields_IsUnknown()
    {
        byte[] bad = Sample("sample.bmp");
        bad[14] = 7; // размер заголовка изображения 7 — такого не бывает

        Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(bad));
    }

    [Fact]
    public void Match_NewFormatsCorruptedFirstByte_AreNotRecognized()
    {
        foreach (string file in new[] { "sample.flac", "sample.png", "sample.jpg", "sample.gif", "sample.tiff", "sample.psd", "sample.dds", "sample.webp" })
        {
            byte[] bytes = Sample(file);
            bytes[0] ^= 0x55;

            Assert.Same(FileFormat.Unknown, SignatureMatcher.Match(bytes));
        }
    }
}

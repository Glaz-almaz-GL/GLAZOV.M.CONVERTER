using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats.Detection;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Formats.Tests.Detection;

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

        var type = Sniff.Match(bytes.AsSpan(0, Math.Min(bytes.Length, DetectionSizes.MinimumWindow)));

        Assert.Equal(expected, type.Name);
    }

    [Fact]
    public void Match_OnlyBytesCount_NameOfTheFileIsNotAnInput()
    {
        // Те же байты MP3 — тип определяется по ним, как бы файл ни назывался: у буфера имени нет вовсе.
        var bytes = Sample("sample-id3.mp3");

        Assert.Same(FileFormat.Mp3, Sniff.Match(bytes));
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

        Assert.Same(FileFormat.Unknown, Sniff.Match(wav.AsSpan(0, length)));
    }

    [Fact]
    public void Match_ZeroesAndText_AreUnknown()
    {
        Assert.Same(FileFormat.Unknown, Sniff.Match(new byte[4096]));
        Assert.Same(FileFormat.Unknown, Sniff.Match("Просто текстовый файл, а не звук."u8));
    }

    [Fact]
    public void Match_CorruptedOggSignature_IsNotOgg()
    {
        var bytes = Sample("sample-vorbis.ogg");
        bytes[0] = (byte)'X';

        Assert.Same(FileFormat.Unknown, Sniff.Match(bytes));
    }

    [Fact]
    public void Match_RiffThatIsNotWave_IsUnknown()
    {
        var bytes = Sample("sample.wav");
        bytes[8] = (byte)'A'; // «AVE…» вместо «WAVE»

        Assert.Same(FileFormat.Unknown, Sniff.Match(bytes));
    }

    [Fact]
    public void Match_Mp3FrameWithoutSecondFrame_IsUnknown()
    {
        // Один кадр без подтверждающего второго — слишком слабый признак: данные не принимаются за MP3.
        var raw = Sample("sample-raw.mp3");

        Assert.Same(FileFormat.Unknown, Sniff.Match(raw.AsSpan(0, 120)));
    }

    [Fact]
    public void Match_ThousandRandomBuffers_NeverLookLikeAudio()
    {
        var random = new Random(20261003);
        var buffer = new byte[DetectionSizes.MinimumWindow];

        for (var i = 0; i < 1000; i++)
        {
            random.NextBytes(buffer);

            Assert.Same(FileFormat.Unknown, Sniff.Match(buffer));
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

        FileFormat type = Sniff.Match(bytes.AsSpan(0, Math.Min(bytes.Length, DetectionSizes.MinimumWindow)));

        Assert.Equal(expected, type.Name);
    }

    [Fact]
    public void Match_Tga_HasNoSignature_SoItIsUnknown()
    {
        Assert.Same(FileFormat.Unknown, Sniff.Match(Sample("sample.tga")));
    }

    [Fact]
    public void Match_XmlThatIsNotSvg_AndSvgWordInsideText_AreUnknown()
    {
        Assert.Same(FileFormat.Unknown, Sniff.Match(Sample("sample-notsvg.xml")));
        Assert.Same(FileFormat.Unknown, Sniff.Match("Это письмо про формат <svg и ничего больше."u8));
        Assert.Same(FileFormat.Unknown, Sniff.Match("<svgfoo></svgfoo>"u8));
    }

    [Theory]
    [InlineData("sample.mp4", "MP4")]
    [InlineData("sample-audioonly.mp4", "MP4")]
    [InlineData("sample.mov", "MOV")]
    [InlineData("sample.mkv", "MKV")]
    [InlineData("sample-audioonly.mkv", "MKV")]
    [InlineData("sample.webm", "WebM")]
    [InlineData("sample.avi", "AVI")]
    [InlineData("sample.m4a", "M4A")]
    [InlineData("sample.wav", "WAV")]
    [InlineData("sample.webp", "WebP")]
    public void Match_RealVideoFile_ReturnsItsContainer_AndNeighboursStayApart(string file, string expected)
    {
        byte[] bytes = Sample(file);

        FileFormat type = Sniff.Match(bytes.AsSpan(0, Math.Min(bytes.Length, DetectionSizes.MinimumWindow)));

        Assert.Equal(expected, type.Name);
    }

    [Theory]
    [InlineData("isom")]
    [InlineData("iso2")]
    [InlineData("iso4")]
    [InlineData("iso5")]
    [InlineData("iso6")]
    [InlineData("mp41")]
    [InlineData("mp42")]
    [InlineData("avc1")]
    [InlineData("dash")]
    [InlineData("msnv")]
    [InlineData("f4v ")]
    public void Match_Mp4CommonBrands_AreMp4(string brand)
    {
        byte[] bytes = [0, 0, 0, 0x18, .. "ftyp"u8.ToArray(), .. System.Text.Encoding.ASCII.GetBytes(brand), 0, 0, 2, 0, .. "isomiso2"u8.ToArray()];

        Assert.Same(FileFormat.Mp4, Sniff.Match(bytes));
    }

    [Theory]
    [InlineData("M4A ", "M4A")]
    [InlineData("M4B ", "M4A")]
    [InlineData("qt  ", "MOV")]
    [InlineData("3gp4", "Unknown")]
    [InlineData("heic", "Unknown")]
    [InlineData("xxxx", "Unknown")]
    public void Match_OtherFtypBrands_AreNotGuessedAsMp4(string brand, string expected)
    {
        byte[] bytes = [0, 0, 0, 0x18, .. "ftyp"u8.ToArray(), .. System.Text.Encoding.ASCII.GetBytes(brand), 0, 0, 2, 0, .. "isomiso2"u8.ToArray()];

        Assert.Equal(expected, Sniff.Match(bytes).Name);
    }

    [Theory]
    [InlineData("moov")]
    [InlineData("mdat")]
    [InlineData("wide")]
    [InlineData("free")]
    public void Match_OldQuickTimeStartingWithAnAtom_IsMov(string atom)
    {
        byte[] bytes = [0, 0, 0x10, 0, .. System.Text.Encoding.ASCII.GetBytes(atom), 1, 2, 3, 4, 5, 6, 7, 8];

        Assert.Same(FileFormat.Mov, Sniff.Match(bytes));
    }

    [Fact]
    public void Match_AtomWithTooSmallSizeOrUnknownType_IsNotMov()
    {
        Assert.Same(FileFormat.Unknown, Sniff.Match([0, 0, 0, 8, .. "moov"u8.ToArray(), 1, 2, 3, 4]));
        Assert.Same(FileFormat.Unknown, Sniff.Match([0, 0, 0, 0, .. "free"u8.ToArray(), 1, 2, 3, 4]));
        Assert.Same(FileFormat.Unknown, Sniff.Match([0, 0, 0x10, 0, .. "skip"u8.ToArray(), 1, 2, 3, 4]));
    }

    [Fact]
    public void Match_AtomWhoseSizeIsPrintableText_OrWhoseNextAtomIsNoType_IsNotMov()
    {
        Assert.Same(FileFormat.Unknown, Sniff.Match("wxyzfree and more plain text"u8));

        byte[] nextIsNoType = [0, 0, 0, 0x10, .. "free"u8.ToArray(), 1, 2, 3, 4, 5, 6, 7, 8, 0, 0, 0, 0x10, 0, 0, 0, 0];
        byte[] nextIsAType = [0, 0, 0, 0x10, .. "free"u8.ToArray(), 1, 2, 3, 4, 5, 6, 7, 8, 0, 0, 0, 0x10, .. "mdat"u8.ToArray()];

        Assert.Same(FileFormat.Unknown, Sniff.Match(nextIsNoType));
        Assert.Same(FileFormat.Mov, Sniff.Match(nextIsAType));
    }

    [Theory]
    [InlineData("sample.mp4")]
    [InlineData("sample.mov")]
    [InlineData("sample.mkv")]
    [InlineData("sample.webm")]
    [InlineData("sample.avi")]
    public void Match_TruncatedBelowTheSignature_IsUnknownAndDoesNotThrow(string file)
    {
        byte[] bytes = Sample(file);

        for (int length = 0; length < 12; length++)
        {
            Assert.Same(FileFormat.Unknown, Sniff.Match(bytes.AsSpan(0, length)));
        }
    }

    [Theory]
    [InlineData("sample.mkv")]
    [InlineData("sample.webm")]
    public void Match_EbmlCutBeforeTheDocTypeValue_IsUnknown(string file)
    {
        byte[] bytes = Sample(file);

        for (int length = 12; length < 24; length++)
        {
            Assert.Same(FileFormat.Unknown, Sniff.Match(bytes.AsSpan(0, length)));
        }
    }

    [Fact]
    public void Match_EbmlWithAnotherDocType_AndTextStartingWithFtyp_AreUnknown()
    {
        byte[] other = [0x1A, 0x45, 0xDF, 0xA3, 0x93, 0x42, 0x82, 0x88, .. "othertyp"u8.ToArray(), 0, 0, 0, 0];

        Assert.Same(FileFormat.Unknown, Sniff.Match(other));
        Assert.Same(FileFormat.Unknown, Sniff.Match("ftypisom: это письмо, а не видео."u8));
        Assert.Same(FileFormat.Unknown, Sniff.Match("RIFF    ABCDEFGH"u8));
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

        Assert.Same(FileFormat.Unknown, Sniff.Match(bytes.AsSpan(0, keep)));
    }

    [Fact]
    public void Match_BmpLookingStartWithBadFields_IsUnknown()
    {
        byte[] bad = Sample("sample.bmp");
        bad[14] = 7; // размер заголовка изображения 7 — такого не бывает

        Assert.Same(FileFormat.Unknown, Sniff.Match(bad));
    }

    [Fact]
    public void Match_NewFormatsCorruptedFirstByte_AreNotRecognized()
    {
        foreach (string file in new[] { "sample.flac", "sample.png", "sample.jpg", "sample.gif", "sample.tiff", "sample.psd", "sample.dds", "sample.webp" })
        {
            byte[] bytes = Sample(file);
            bytes[0] ^= 0x55;

            Assert.Same(FileFormat.Unknown, Sniff.Match(bytes));
        }
    }
}

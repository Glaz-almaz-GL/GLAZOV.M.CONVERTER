using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Ffmpeg.Engines;
using GLAZOV.M.CONVERTER.Ffmpeg.Progress;
using GLAZOV.M.CONVERTER.Formats;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests.Engines;

/// <summary>
/// Аргументы FFmpeg для изображений — чистые проверки без запуска программы: кодек и умолчания по формату,
/// перевод качества 1–100, один кадр, пути без экранирования.
/// </summary>
public class FfmpegImageArgumentsTests
{
    private static string ValueAfter(IReadOnlyList<string> args, string name)
    {
        int index = args.ToList().IndexOf(name);
        Assert.True(index >= 0 && index + 1 < args.Count, $"Нет аргумента {name}");
        return args[index + 1];
    }

    [Theory]
    [InlineData("PNG", "png")]
    [InlineData("JPEG", "mjpeg")]
    [InlineData("WebP", "libwebp")]
    [InlineData("BMP", "bmp")]
    [InlineData("GIF", "gif")]
    [InlineData("TIFF", "tiff")]
    [InlineData("TGA", "targa")]
    public void CodecFollowsTheTargetFormat_AndEveryResultIsOneFrame(string name, string codec)
    {
        FileFormat to = new[] { FileFormat.Png, FileFormat.Jpeg, FileFormat.Webp, FileFormat.Bmp, FileFormat.Gif, FileFormat.Tiff, FileFormat.Tga }
            .Single(f => f.Name == name);

        IReadOnlyList<string> args = FfmpegImageArguments.Build(to, null, "in.bin", "out.bin");

        Assert.Equal(codec, ValueAfter(args, "-c:v"));
        Assert.Equal("1", ValueAfter(args, "-frames:v"));
        Assert.Equal("in.bin", ValueAfter(args, "-i"));
        Assert.Equal("out.bin", args[^1]);
        Assert.Equal("error", ValueAfter(args, "-loglevel"));
    }

    [Fact]
    public void JpegDefault_QualityNinety_MapsToQscale5_AndUsesFullRangeYuv()
    {
        IReadOnlyList<string> args = FfmpegImageArguments.Build(FileFormat.Jpeg, null, "i", "o");

        Assert.Equal("5", ValueAfter(args, "-q:v"));
        Assert.Equal("yuvj420p", ValueAfter(args, "-pix_fmt"));
    }

    [Theory]
    [InlineData(100, "2")]
    [InlineData(1, "31")]
    [InlineData(50, "17")]
    public void JpegQuality_ScaleIsInverted_AndStaysInsideFFmpegLimits(int quality, string expectedQscale)
    {
        IReadOnlyList<string> args = FfmpegImageArguments.Build(FileFormat.Jpeg, quality, "i", "o");

        Assert.Equal(expectedQscale, ValueAfter(args, "-q:v"));
    }

    [Fact]
    public void WebpQuality_DefaultAndExplicit()
    {
        Assert.Equal("80", ValueAfter(FfmpegImageArguments.Build(FileFormat.Webp, null, "i", "o"), "-quality"));
        Assert.Equal("35", ValueAfter(FfmpegImageArguments.Build(FileFormat.Webp, 35, "i", "o"), "-quality"));
    }

    [Fact]
    public void LosslessFormats_IgnoreQuality()
    {
        foreach (FileFormat to in new[] { FileFormat.Png, FileFormat.Bmp, FileFormat.Gif, FileFormat.Tiff, FileFormat.Tga })
        {
            IReadOnlyList<string> args = FfmpegImageArguments.Build(to, 10, "i", "o");

            Assert.DoesNotContain("-q:v", args);
            Assert.DoesNotContain("-quality", args);
        }
    }

    [Fact]
    public void Jpeg_FlattensTransparencyOnWhite_Gif_BuildsItsOwnPalette_Png_AddsNoFilter()
    {
        string jpeg = ValueAfter(FfmpegImageArguments.Build(FileFormat.Jpeg, null, "i", "o"), "-vf");
        string gif = ValueAfter(FfmpegImageArguments.Build(FileFormat.Gif, null, "i", "o"), "-vf");

        Assert.Contains("drawbox=c=white", jpeg);
        Assert.Contains("overlay", jpeg);
        Assert.Contains("palettegen", gif);
        Assert.Contains("paletteuse", gif);
        Assert.DoesNotContain("-vf", FfmpegImageArguments.Build(FileFormat.Png, null, "i", "o"));
    }

    [Fact]
    public void ActionFilters_GoFirst_InGivenOrder_BeforeTheFormatSpecificOnes()
    {
        IReadOnlyList<string> args = FfmpegImageArguments.Build(FileFormat.Jpeg, null, "i", "o", ["transpose=1", "scale=iw/2:ih/2"]);

        string chain = ValueAfter(args, "-vf");

        Assert.StartsWith("transpose=1,scale=iw/2:ih/2,split=2", chain);
    }

    [Fact]
    public void PathsWithSpacesAndQuotes_AreSingleArguments()
    {
        const string input = "C:\\Мои файлы\\a \"b\".png";
        const string output = "C:\\Мои файлы\\c d.jpg";

        IReadOnlyList<string> args = FfmpegImageArguments.Build(FileFormat.Jpeg, null, input, output);

        Assert.Single(args, a => a == input);
        Assert.Single(args, a => a == output);
    }

    [Theory]
    [InlineData("DDS")]
    [InlineData("PSD")]
    [InlineData("SVG")]
    [InlineData("Unknown")]
    public void ReadOnlyAndForeignFormats_AreRefusedAsResults(string name)
    {
        FileFormat to = new[] { FileFormat.Dds, FileFormat.Psd, FileFormat.Svg, FileFormat.Unknown }.Single(f => f.Name == name);

        Assert.Throws<ArgumentException>(() => FfmpegImageArguments.Build(to, null, "i", "o"));
    }

    [Fact]
    public void EmptyPaths_AreUsageErrors()
    {
        Assert.Throws<ArgumentException>(() => FfmpegImageArguments.Build(FileFormat.Png, null, " ", "o"));
        Assert.Throws<ArgumentException>(() => FfmpegImageArguments.Build(FileFormat.Png, null, "i", ""));
    }
}

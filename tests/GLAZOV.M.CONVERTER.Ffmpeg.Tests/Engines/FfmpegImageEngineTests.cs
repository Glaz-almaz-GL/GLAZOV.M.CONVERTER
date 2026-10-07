using System.Globalization;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Ffmpeg.Engines;
using GLAZOV.M.CONVERTER.Ffmpeg.Progress;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests.Engines;

/// <summary>
/// Движок изображений на настоящем FFmpeg: 90 пар «вход × результат», качество JPEG, прозрачность, нечётные размеры,
/// первый кадр GIF, испорченный вход, чистота временной папки. Нет FFmpeg — проверки пропускаются, а не проходят.
/// </summary>
[Collection("FFmpeg")]
public sealed class FfmpegImageEngineTests : IDisposable
{
    private static readonly (string File, FileFormat Format)[] Inputs =
    [
        ("sample.png", FileFormat.Png), ("sample.jpg", FileFormat.Jpeg), ("sample.gif", FileFormat.Gif), ("sample.webp", FileFormat.Webp),
        ("sample.bmp", FileFormat.Bmp), ("sample.tiff", FileFormat.Tiff), ("sample.dds", FileFormat.Dds), ("sample.psd", FileFormat.Psd),
        ("sample.avif", FileFormat.Avif), ("sample.ico", FileFormat.Ico),
    ];

    private static readonly (FileFormat Format, string Codec)[] Outputs =
    [
        (FileFormat.Png, "png"), (FileFormat.Jpeg, "mjpeg"), (FileFormat.Webp, "webp"), (FileFormat.Bmp, "bmp"),
        (FileFormat.Gif, "gif"), (FileFormat.Tiff, "tiff"), (FileFormat.Tga, "targa"), (FileFormat.Avif, "av1"), (FileFormat.Ico, "bmp"),
    ];

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));
    private readonly FfmpegImageEngine _engine = new();

    public FfmpegImageEngineTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        Directory.Delete(_temp, recursive: true);
    }

    private static byte[] Sample(string name)
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", name));
    }

    private static ToolStatus Ffmpeg()
    {
        return new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate(FfmpegTools.Ffmpeg);
    }

    private static string Ffprobe()
    {
        return Path.Combine(Path.GetDirectoryName(Ffmpeg().Path!)!, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
    }

    private async Task<byte[]> Convert(byte[] input, FileFormat from, FileFormat to, ImageOptions? image = null)
    {
        using MemoryStream source = new(input);
        using MemoryStream output = new();
        ConversionOptions options = new() { Image = image };

        await _engine.ConvertAsync(new ConversionRequest(source, output, from, to, options, Ffmpeg(), _temp), CancellationToken.None);

        return output.ToArray();
    }

    // Разбор результата настоящим ffprobe: кодек, ширина, высота.
    private async Task<Dictionary<string, string>> ProbeAsync(byte[] data, string extension)
    {
        string path = Path.Combine(_temp, "проба-" + Guid.NewGuid().ToString("N") + "." + extension);
        await File.WriteAllBytesAsync(path, data);
        try
        {
            using MemoryStream output = new();
            ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(
                Ffprobe(),
                ["-v", "error", "-select_streams", "v:0", "-show_entries", "stream=codec_name,width,height", "-of", "default=nw=1", path],
                StandardOutput: output));
            Assert.Equal(0, result.ExitCode);
            return System.Text.Encoding.UTF8.GetString(output.ToArray())
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split('=', 2))
                .ToDictionary(parts => parts[0], parts => parts[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Картинка, сделанная настоящим FFmpeg (источник lavfi): имя фильтра, размер и формат пикселей.
    private async Task<byte[]> MakeAsync(string source, string extension, params string[] extra)
    {
        string path = Path.Combine(_temp, "ист-" + Guid.NewGuid().ToString("N") + "." + extension);
        List<string> args = ["-v", "error", "-y", "-f", "lavfi", "-i", source, "-frames:v", "1", .. extra, path];
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(Ffmpeg().Path!, args));
        Assert.True(result.ExitCode == 0, result.ErrorOutput);
        byte[] data = await File.ReadAllBytesAsync(path);
        File.Delete(path);
        return data;
    }

    public static IEnumerable<object[]> Pairs()
    {
        foreach ((string file, FileFormat format) in Inputs)
        {
            foreach ((FileFormat to, string _) in Outputs)
            {
                yield return new object[] { file, format.Name, to.Name };
            }
        }
    }

    [Fact]
    public void Pairs_AreNinety_AndReadOnlyFormatsAreNeverResults()
    {
        Assert.Equal(90, _engine.Pairs.Count);
        Assert.All(_engine.Pairs, p => Assert.True(_engine.CanConvert(p.From, p.To)));
        Assert.False(_engine.CanConvert(FileFormat.Png, FileFormat.Dds));
        Assert.False(_engine.CanConvert(FileFormat.Png, FileFormat.Psd));
        Assert.False(_engine.CanConvert(FileFormat.Tga, FileFormat.Png));
        Assert.False(_engine.CanConvert(FileFormat.Svg, FileFormat.Png));
        Assert.False(_engine.CanConvert(FileFormat.Mp3, FileFormat.Png));
        Assert.Equal("ffmpeg", Assert.Single(_engine.Tools).Name);
    }

    [ExternalTheory]
    [MemberData(nameof(Pairs))]
    public async Task EveryPair_GivesTheTargetCodec_WithTheSameSize_AndLeavesNoTempFiles(string file, string fromName, string toName)
    {
        FileFormat from = Inputs.Single(i => i.File == file).Format;
        (FileFormat to, string codec) = Outputs.Single(o => o.Format.Name == toName);
        Assert.Equal(fromName, from.Name);
        byte[] source = Sample(file);
        Dictionary<string, string> before = await ProbeAsync(source, from.Extension);

        byte[] result = await Convert(source, from, to);

        Dictionary<string, string> after = await ProbeAsync(result, to.Extension);
        Assert.Equal(codec, after["codec_name"]);
        Assert.Equal(before["width"], after["width"]);
        Assert.Equal(before["height"], after["height"]);
        Assert.Empty(Directory.GetFiles(_temp));
    }

    [ExternalFact]
    public async Task JpegQuality_TenIsMuchSmallerThanNinetyFive_OnARealPicture()
    {
        byte[] picture = await MakeAsync("testsrc2=size=320x240:rate=1", "png");

        byte[] low = await Convert(picture, FileFormat.Png, FileFormat.Jpeg, new ImageOptions { Quality = 10 });
        byte[] high = await Convert(picture, FileFormat.Png, FileFormat.Jpeg, new ImageOptions { Quality = 95 });
        byte[] normal = await Convert(picture, FileFormat.Png, FileFormat.Jpeg);

        Assert.True(low.Length * 2 < high.Length, $"качество 10 — {low.Length} байт, 95 — {high.Length}");
        Assert.InRange(normal.Length, low.Length, high.Length);
    }

    [ExternalFact]
    public async Task WebpQuality_LowerIsSmaller()
    {
        byte[] picture = await MakeAsync("testsrc2=size=320x240:rate=1", "png");

        byte[] low = await Convert(picture, FileFormat.Png, FileFormat.Webp, new ImageOptions { Quality = 10 });
        byte[] high = await Convert(picture, FileFormat.Png, FileFormat.Webp, new ImageOptions { Quality = 95 });

        Assert.True(low.Length < high.Length, $"качество 10 — {low.Length} байт, 95 — {high.Length}");
    }

    [ExternalFact]
    public async Task TransparentPng_ToJpeg_IsFlattenedOnWhite()
    {
        byte[] transparent = await MakeAsync("color=c=red@0.0:s=16x16,format=rgba", "png", "-pix_fmt", "rgba");

        byte[] jpeg = await Convert(transparent, FileFormat.Png, FileFormat.Jpeg);

        // Пиксель результата читается обратно настоящим FFmpeg как сырые RGB-байты.
        string path = Path.Combine(_temp, "белый.jpg");
        await File.WriteAllBytesAsync(path, jpeg);
        using MemoryStream pixels = new();
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(
            Ffmpeg().Path!,
            ["-v", "error", "-i", path, "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "pipe:1"],
            StandardOutput: pixels));
        Assert.Equal(0, result.ExitCode);
        byte[] rgb = pixels.ToArray();
        Assert.All(rgb.Take(3), channel => Assert.True(channel >= 240, $"канал {channel}: прозрачное место должно стать белым"));
    }

    [ExternalFact]
    public async Task TransparentPng_ToPng_KeepsTransparency()
    {
        byte[] transparent = await MakeAsync("color=c=red@0.0:s=16x16,format=rgba", "png", "-pix_fmt", "rgba");

        byte[] png = await Convert(transparent, FileFormat.Png, FileFormat.Png);

        string path = Path.Combine(_temp, "прозрачный.png");
        await File.WriteAllBytesAsync(path, png);
        using MemoryStream pixels = new();
        await ToolRunner.RunAsync(new ToolInvocation(
            Ffmpeg().Path!,
            ["-v", "error", "-i", path, "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgba", "pipe:1"],
            StandardOutput: pixels));
        Assert.Equal(0, pixels.ToArray()[3]);
    }

    // First pixel of a picture as raw RGB(A) bytes, read back by the real FFmpeg.
    private async Task<byte[]> FirstPixelAsync(byte[] data, string extension, string pixelFormat)
    {
        string path = Path.Combine(_temp, "пиксель-" + Guid.NewGuid().ToString("N") + "." + extension);
        await File.WriteAllBytesAsync(path, data);
        using MemoryStream pixels = new();
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(
            Ffmpeg().Path!,
            ["-v", "error", "-i", path, "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", pixelFormat, "pipe:1"],
            StandardOutput: pixels));
        File.Delete(path);
        Assert.Equal(0, result.ExitCode);
        return pixels.ToArray();
    }

    [ExternalFact]
    public async Task AvifQuality_LowerIsSmaller_AndTheSettingIsTheDefault()
    {
        byte[] picture = await MakeAsync("testsrc2=size=320x240:rate=1", "png");
        FfmpegImageEngine lowByDefault = new(new FfmpegSettings { AvifQuality = 10 });

        byte[] low = await Convert(picture, FileFormat.Png, FileFormat.Avif, new ImageOptions { Quality = 10 });
        byte[] high = await Convert(picture, FileFormat.Png, FileFormat.Avif, new ImageOptions { Quality = 95 });
        byte[] normal = await Convert(picture, FileFormat.Png, FileFormat.Avif);
        using MemoryStream source = new(picture);
        using MemoryStream output = new();
        await lowByDefault.ConvertAsync(new ConversionRequest(source, output, FileFormat.Png, FileFormat.Avif, new ConversionOptions(), Ffmpeg(), _temp), CancellationToken.None);

        Assert.True(low.Length < high.Length, $"качество 10 — {low.Length} байт, 95 — {high.Length}");
        Assert.InRange(normal.Length, low.Length, high.Length);
        Assert.Equal(low.Length, output.Length);
        Assert.Same(FileFormat.Avif, Sniff.Match(normal));
    }

    [ExternalFact]
    public async Task TransparentPng_ToAvif_IsFlattenedOnWhite()
    {
        byte[] transparent = await MakeAsync("color=c=red@0.0:s=16x16,format=rgba", "png", "-pix_fmt", "rgba");

        byte[] avif = await Convert(transparent, FileFormat.Png, FileFormat.Avif);

        byte[] rgb = await FirstPixelAsync(avif, "avif", "rgb24");
        Assert.All(rgb.Take(3), channel => Assert.True(channel >= 230, $"канал {channel}: прозрачное место должно стать белым"));
    }

    [ExternalFact]
    public async Task AnimatedAvif_GivesTheFirstFrameOnly()
    {
        byte[] animated = await MakeAsync("testsrc=size=24x16:rate=5:duration=1", "avif", "-frames:v", "5", "-c:v", "libaom-av1", "-cpu-used", "6");

        byte[] png = await Convert(animated, FileFormat.Avif, FileFormat.Png);

        Dictionary<string, string> probe = await ProbeAsync(png, "png");
        Assert.Equal(("24", "16"), (probe["width"], probe["height"]));
    }

    [ExternalFact]
    public async Task IcoWithSeveralPictures_GivesTheLargestOne()
    {
        string path = Path.Combine(_temp, "несколько.ico");
        ToolResult made = await ToolRunner.RunAsync(new ToolInvocation(
            Ffmpeg().Path!,
            ["-v", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=64x64:rate=1", "-filter_complex", "[0:v]split=3[a][b][c];[a]scale=16:16[s1];[b]scale=48:48[s2];[c]scale=32:32[s3]",
             "-map", "[s1]", "-map", "[s2]", "-map", "[s3]", "-frames:v", "1", "-f", "ico", path]));
        Assert.True(made.ExitCode == 0, made.ErrorOutput);

        byte[] png = await Convert(await File.ReadAllBytesAsync(path), FileFormat.Ico, FileFormat.Png);

        Dictionary<string, string> probe = await ProbeAsync(png, "png");
        Assert.Equal(("48", "48"), (probe["width"], probe["height"]));
    }

    [ExternalFact]
    public async Task Ico_OfExactly256_Works_AndOf300_IsRefusedInPlainWords_WithoutAResult()
    {
        byte[] fits = await MakeAsync("testsrc2=size=256x256:rate=1", "png");
        byte[] tooWide = await MakeAsync("testsrc2=size=300x100:rate=1", "png");

        byte[] ico = await Convert(fits, FileFormat.Png, FileFormat.Ico);
        using MemoryStream source = new(tooWide);
        using MemoryStream output = new();
        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await _engine.ConvertAsync(new ConversionRequest(source, output, FileFormat.Png, FileFormat.Ico, new ConversionOptions(), Ffmpeg(), _temp), CancellationToken.None));

        Assert.Same(FileFormat.Ico, Sniff.Match(ico));
        Assert.Equal(("256", "256"), ((await ProbeAsync(ico, "ico"))["width"], (await ProbeAsync(ico, "ico"))["height"]));
        Assert.Equal(ConversionFailureReason.ToolFailed, ex.Reason);
        Assert.Contains("256", ex.Message);
        Assert.Contains("масштаба", ex.Message);
        Assert.Equal(0, output.Length);
        Assert.Empty(Directory.GetFiles(_temp));
    }

    [ExternalFact]
    public async Task OddDimensions_WorkForEveryResult()
    {
        byte[] odd = await MakeAsync("testsrc=size=25x17:rate=1", "png");

        foreach ((FileFormat to, string codec) in Outputs)
        {
            byte[] result = await Convert(odd, FileFormat.Png, to);

            Dictionary<string, string> probe = await ProbeAsync(result, to.Extension);
            Assert.Equal(codec, probe["codec_name"]);
            Assert.Equal("25", probe["width"]);
            Assert.Equal("17", probe["height"]);
        }
    }

    [ExternalFact]
    public async Task AnimatedGif_GivesOnlyTheFirstFrame()
    {
        byte[] animated = await MakeAsync("testsrc=size=24x16:rate=5:duration=1", "gif", "-frames:v", "5");

        byte[] png = await Convert(animated, FileFormat.Gif, FileFormat.Png);

        Dictionary<string, string> probe = await ProbeAsync(png, "png");
        Assert.Equal("png", probe["codec_name"]);
        Assert.Same(FileFormat.Png, Sniff.Match(png));
    }

    [ExternalFact]
    public async Task TruncatedPng_IsToolFailed_AndLeavesNoTempFiles()
    {
        byte[] bad = Sample("sample.png").AsSpan(0, 40).ToArray();

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () => await Convert(bad, FileFormat.Png, FileFormat.Jpeg));

        Assert.Equal(ConversionStage.Conversion, ex.Stage);
        Assert.Equal(ConversionFailureReason.ToolFailed, ex.Reason);
        Assert.Empty(Directory.GetFiles(_temp));
    }

    [ExternalFact]
    public async Task EmptyTiff_IsToolFailed()
    {
        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () => await Convert([], FileFormat.Tiff, FileFormat.Png));

        Assert.Equal(ConversionFailureReason.ToolFailed, ex.Reason);
        Assert.Empty(Directory.GetFiles(_temp));
    }

    [ExternalFact]
    public async Task Cancelled_BeforeStart_ThrowsOperationCanceled_AndLeavesNoTempFiles()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        using MemoryStream source = new(Sample("sample.png"));
        using MemoryStream output = new();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _engine.ConvertAsync(new ConversionRequest(source, output, FileFormat.Png, FileFormat.Jpeg, new ConversionOptions(), Ffmpeg(), _temp), cts.Token));

        Assert.Empty(Directory.GetFiles(_temp));
    }

    [Fact]
    public void ImageQualityInvariantCulture_NoDependencyOnTheMachineLocale()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");

            IReadOnlyList<string> args = FfmpegImageArguments.Build(FileFormat.Webp, 77, "i", "o");

            Assert.Equal("77", args[args.ToList().IndexOf("-quality") + 1]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}

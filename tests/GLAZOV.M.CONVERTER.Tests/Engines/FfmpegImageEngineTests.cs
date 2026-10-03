using System.Globalization;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Detection;
using GLAZOV.M.CONVERTER.Engines;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Tests.Engines;

/// <summary>
/// Движок изображений на настоящем FFmpeg: 56 пар «вход × результат», качество JPEG, прозрачность, нечётные размеры,
/// первый кадр GIF, испорченный вход, чистота временной папки. Нет FFmpeg — проверки пропускаются, а не проходят.
/// </summary>
[Collection("FFmpeg")]
public sealed class FfmpegImageEngineTests : IDisposable
{
    private static readonly (string File, FileFormat Format)[] Inputs =
    [
        ("sample.png", FileFormat.Png), ("sample.jpg", FileFormat.Jpeg), ("sample.gif", FileFormat.Gif), ("sample.webp", FileFormat.Webp),
        ("sample.bmp", FileFormat.Bmp), ("sample.tiff", FileFormat.Tiff), ("sample.dds", FileFormat.Dds), ("sample.psd", FileFormat.Psd),
    ];

    private static readonly (FileFormat Format, string Codec)[] Outputs =
    [
        (FileFormat.Png, "png"), (FileFormat.Jpeg, "mjpeg"), (FileFormat.Webp, "webp"), (FileFormat.Bmp, "bmp"),
        (FileFormat.Gif, "gif"), (FileFormat.Tiff, "tiff"), (FileFormat.Tga, "targa"),
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
        return new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate("ffmpeg");
    }

    private static string Ffprobe()
    {
        return Path.Combine(Path.GetDirectoryName(Ffmpeg().Path!)!, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
    }

    private async Task<byte[]> Convert(byte[] input, FileFormat from, FileFormat to, ImageOptions? image = null)
    {
        using MemoryStream source = new(input);
        using MemoryStream output = new();
        ConversionOptions? options = image is null ? null : new ConversionOptions { Image = image };

        await _engine.ConvertAsync(new EngineRequest(source, output, from, to, options, Ffmpeg(), _temp), CancellationToken.None);

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
    public void Pairs_AreFiftySix_AndReadOnlyFormatsAreNeverResults()
    {
        Assert.Equal(56, _engine.Pairs.Count);
        Assert.All(_engine.Pairs, p => Assert.True(_engine.CanConvert(p.From, p.To)));
        Assert.False(_engine.CanConvert(FileFormat.Png, FileFormat.Dds));
        Assert.False(_engine.CanConvert(FileFormat.Png, FileFormat.Psd));
        Assert.False(_engine.CanConvert(FileFormat.Tga, FileFormat.Png));
        Assert.False(_engine.CanConvert(FileFormat.Svg, FileFormat.Png));
        Assert.False(_engine.CanConvert(FileFormat.Mp3, FileFormat.Png));
        Assert.Equal("ffmpeg", _engine.ToolName);
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
        Assert.Same(FileFormat.Png, SignatureMatcher.Match(png));
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
            await _engine.ConvertAsync(new EngineRequest(source, output, FileFormat.Png, FileFormat.Jpeg, null, Ffmpeg(), _temp), cts.Token));

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

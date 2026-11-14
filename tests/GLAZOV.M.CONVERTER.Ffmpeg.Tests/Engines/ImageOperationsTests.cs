using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Ffmpeg.Engines;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Formats.Options;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tooling;

namespace GLAZOV.M.CONVERTER.Ffmpeg.Tests.Engines;

/// <summary>
/// Поворот вправо и масштаб в процентах: чистая сборка фильтров по порядку и живые проверки размеров и ориентации
/// на настоящем FFmpeg. Нет FFmpeg — живые проверки пропускаются, а не проходят.
/// </summary>
[Collection("FFmpeg")]
public sealed class ImageOperationsTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "convtests-" + Guid.NewGuid().ToString("N"));
    private readonly FfmpegImageEngine _engine = new();

    public ImageOperationsTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        Directory.Delete(_temp, recursive: true);
    }

    private static ToolStatus Ffmpeg()
    {
        return new ToolLocator(ConverterSettingsSnapshot.Create(new ConverterSettings())).Locate(FfmpegTools.Ffmpeg);
    }

    private static string Ffprobe()
    {
        return Path.Combine(Path.GetDirectoryName(Ffmpeg().Path!)!, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");
    }

    private async Task<byte[]> Run(byte[] input, FileFormat from, FileFormat to, params IConversionOperation[] operations)
    {
        using MemoryStream source = new(input);
        using MemoryStream output = new();
        ConversionOptions options = new();
        foreach (IConversionOperation operation in operations)
        {
            options.Operations.Add(operation);
        }

        await _engine.ConvertAsync(new ConversionRequest(source, output, from, to, options, Ffmpeg(), _temp), CancellationToken.None);
        return output.ToArray();
    }

    private async Task<(int Width, int Height)> SizeOf(byte[] data, string extension)
    {
        string path = Path.Combine(_temp, "размер-" + Guid.NewGuid().ToString("N") + "." + extension);
        await File.WriteAllBytesAsync(path, data);
        try
        {
            using MemoryStream output = new();
            await ToolRunner.RunAsync(new ToolInvocation(
                Ffprobe(),
                ["-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height", "-of", "csv=s=x:p=0", path],
                StandardOutput: output));
            string[] parts = System.Text.Encoding.UTF8.GetString(output.ToArray()).Trim().Split('x');
            return (int.Parse(parts[0]), int.Parse(parts[1]));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Картинка 24×16 из эталонных образцов; точка 2×2 с четырьмя разными цветами — для проверки ориентации.
    private static byte[] Sample()
    {
        return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", "sample.png"));
    }

    private async Task<byte[]> FourColorsPng()
    {
        // Исходные пиксели (слева направо, сверху вниз): красный, зелёный / синий, жёлтый.
        string raw = Path.Combine(_temp, "четыре.raw");
        string png = Path.Combine(_temp, "четыре.png");
        await File.WriteAllBytesAsync(raw, [255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 0]);
        ToolResult made = await ToolRunner.RunAsync(new ToolInvocation(
            Ffmpeg().Path!,
            ["-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", "2x2", "-i", raw, "-frames:v", "1", png]));
        Assert.Equal(0, made.ExitCode);
        return await File.ReadAllBytesAsync(png);
    }

    private async Task<byte[]> Rgb24(byte[] image)
    {
        string path = Path.Combine(_temp, "пиксели-" + Guid.NewGuid().ToString("N") + ".png");
        await File.WriteAllBytesAsync(path, image);
        using MemoryStream pixels = new();
        ToolResult result = await ToolRunner.RunAsync(new ToolInvocation(
            Ffmpeg().Path!,
            ["-v", "error", "-i", path, "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgb24", "pipe:1"],
            StandardOutput: pixels));
        Assert.Equal(0, result.ExitCode);
        return pixels.ToArray();
    }

    [Fact]
    public void Filters_RotateIsTranspose_ScaleKeepsAtLeastOnePixel_NoOperationsNoFilters()
    {
        Assert.Empty(FfmpegImageArguments.Filters(null));
        Assert.Empty(FfmpegImageArguments.Filters([]));
        Assert.Equal(["transpose=1"], FfmpegImageArguments.Filters([VisualOperations.RotateRight]));

        string scale = Assert.Single(FfmpegImageArguments.Filters([VisualOperations.ScalePercent(75)]));
        Assert.Contains("max(1,trunc(iw*75/100))", scale);
        Assert.Contains("max(1,trunc(ih*75/100))", scale);
    }

    [Fact]
    public void Filters_KeepTheGivenOrder_RotateThenScaleDiffersFromScaleThenRotate()
    {
        IReadOnlyList<string> rotateFirst = FfmpegImageArguments.Filters([VisualOperations.RotateRight, VisualOperations.ScalePercent(50)]);
        IReadOnlyList<string> scaleFirst = FfmpegImageArguments.Filters([VisualOperations.ScalePercent(50), VisualOperations.RotateRight]);

        Assert.Equal("transpose=1", rotateFirst[0]);
        Assert.StartsWith("scale=", rotateFirst[1]);
        Assert.StartsWith("scale=", scaleFirst[0]);
        Assert.Equal("transpose=1", scaleFirst[1]);
        Assert.NotEqual(rotateFirst, scaleFirst);
    }

    [Fact]
    public void Filters_PercentIsWrittenWithTheInvariantCulture()
    {
        System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");

            string scale = FfmpegImageArguments.Filters([VisualOperations.ScalePercent(1234)]).Single();

            Assert.Contains("iw*1234/100", scale);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Engines_DeclareWhetherTheyKnowOperations()
    {
        Assert.True(_engine.SupportsOperations);
        Assert.False(new FfmpegAudioEngine().SupportsOperations);
    }

    [Fact]
    public void AudioConversion_WithOperations_IsRefusedBeforeAnyWork_ByTheRealConverter()
    {
        Converter converter = FfmpegConverter.Create();
        using MemoryStream wav = new(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", "sample.wav")));
        ConversionOptions options = new() { Operations = { VisualOperations.RotateRight } };

        Task<ConversionResult> call = converter.ConvertAsync(Source.Stream(wav), Target.Memory(FileFormat.Mp3), options);

        ConversionException ex = Assert.Throws<ConversionException>(() => call.GetAwaiter().GetResult());
        Assert.Equal(ConversionFailureReason.UnsupportedConversion, ex.Reason);
        Assert.Contains("Действия не поддерживаются для WAV → MP3", ex.Message);
    }

    [ExternalFact]
    public async Task RotateRight_SwapsWidthAndHeight()
    {
        byte[] result = await Run(Sample(), FileFormat.Png, FileFormat.Png, VisualOperations.RotateRight);

        Assert.Equal((16, 24), await SizeOf(result, "png"));
    }

    [ExternalFact]
    public async Task RotateRight_TurnsTheImageClockwise_PixelByPixel()
    {
        byte[] result = await Run(await FourColorsPng(), FileFormat.Png, FileFormat.Png, VisualOperations.RotateRight);

        // Исходно: красный, зелёный / синий, жёлтый. По часовой стрелке: синий, красный / жёлтый, зелёный.
        byte[] rgb = await Rgb24(result);
        Assert.Equal(new byte[] { 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 0 }, rgb);
    }

    [ExternalFact]
    public async Task ScalePercent_GivesTheRightFractionOfTheSize()
    {
        Assert.Equal((12, 8), await SizeOf(await Run(Sample(), FileFormat.Png, FileFormat.Png, VisualOperations.ScalePercent(50)), "png"));
        Assert.Equal((18, 12), await SizeOf(await Run(Sample(), FileFormat.Png, FileFormat.Png, VisualOperations.ScalePercent(75)), "png"));
        Assert.Equal((48, 32), await SizeOf(await Run(Sample(), FileFormat.Png, FileFormat.Png, VisualOperations.ScalePercent(200)), "png"));
    }

    [ExternalFact]
    public async Task ScaleToAFractionOfAPixel_StaysOnePixel_NotAnEmptyFile()
    {
        byte[] result = await Run(Sample(), FileFormat.Png, FileFormat.Png, VisualOperations.ScalePercent(1));

        Assert.Equal((1, 1), await SizeOf(result, "png"));
    }

    [ExternalFact]
    public async Task ChainOfThree_RotateScaleRotate_AppliesInOrder()
    {
        byte[] result = await Run(Sample(), FileFormat.Png, FileFormat.Png, VisualOperations.RotateRight, VisualOperations.ScalePercent(50), VisualOperations.RotateRight);

        // 24×16 → 16×24 → 8×12 → 12×8
        Assert.Equal((12, 8), await SizeOf(result, "png"));
    }

    [ExternalFact]
    public async Task OperationsWorkTogetherWithAnyResultFormat_IncludingOddResults()
    {
        foreach (FileFormat to in new[] { FileFormat.Png, FileFormat.Jpeg, FileFormat.Webp, FileFormat.Bmp, FileFormat.Gif, FileFormat.Tiff, FileFormat.Tga })
        {
            byte[] result = await Run(Sample(), FileFormat.Png, to, VisualOperations.RotateRight, VisualOperations.ScalePercent(75));

            // 24×16 → 16×24 → 12×18
            Assert.Equal((12, 18), await SizeOf(result, to.Extension));
        }
    }

    [ExternalFact]
    public async Task ThePublicCall_PngToJpeg_WithOperationsAndQuality_Works()
    {
        string input = Path.Combine(_temp, "вход.png");
        string output = Path.Combine(_temp, "выход.jpg");
        await File.WriteAllBytesAsync(input, Sample());

        ConversionResult result = await FfmpegConverter.Create().ConvertAsync(
            Source.File(input),
            Target.File(output, FileFormat.Jpeg),
            new ConversionOptions
            {
                Operations = { VisualOperations.RotateRight, VisualOperations.ScalePercent(75) },
                Image = new ImageOptions { Quality = 85 },
            });

        Assert.True(result.Succeeded);
        Assert.Equal((12, 18), await SizeOf(await File.ReadAllBytesAsync(output), "jpg"));
    }
}

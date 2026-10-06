using System.Text;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Settings;
using SkiaSharp;

namespace GLAZOV.M.CONVERTER.Svg.Tests.Engines;

/// <summary>
/// Расширение SVG через публичные вызовы основной библиотеки: пары, размеры, действия, качество, прозрачность,
/// повреждённый вход, пределы и безопасность (внешние ссылки и сущности XML не загружаются).
/// Результат читается обратно декодером Skia.
/// </summary>
public sealed class SvgExtensionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convsvg-" + Guid.NewGuid().ToString("N"));

    public SvgExtensionTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private static string Sample(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Samples", name);
    }

    private static Converter Svg(SvgSettings? settings = null)
    {
        ConverterSettings converterSettings = new();
        converterSettings.Extensions.Add(settings is null ? new SvgExtension() : new SvgExtension(settings));
        return new Converter(converterSettings);
    }

    private string Put(string name, string content)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    private static async Task<SKBitmap> RenderAsync(Converter converter, string path, FileFormat to, ConversionOptions? options = null)
    {
        ConversionResult result = await converter.ConvertAsync(Source.File(path), Target.Memory(to), options);
        Assert.True(result.Succeeded);
        Assert.Equal(FileFormat.Svg, result.SourceType);
        return SKBitmap.Decode(result.Bytes) ?? throw new InvalidDataException("Результат не декодируется.");
    }

    private static void AssertColor(SKColor expected, SKColor actual, int tolerance = 8)
    {
        Assert.True(
            Math.Abs(expected.Red - actual.Red) <= tolerance
                && Math.Abs(expected.Green - actual.Green) <= tolerance
                && Math.Abs(expected.Blue - actual.Blue) <= tolerance
                && Math.Abs(expected.Alpha - actual.Alpha) <= tolerance,
            $"Ожидался цвет {expected}, получен {actual}.");
    }

    private static int CountDark(SKBitmap bitmap)
    {
        int dark = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                SKColor c = bitmap.GetPixel(x, y);
                if (c.Alpha > 128 && c.Red + c.Green + c.Blue < 200)
                {
                    dark++;
                }
            }
        }

        return dark;
    }

    private static byte[] RedSquarePng()
    {
        using SKBitmap red = new(10, 10);
        red.Erase(SKColors.Red);
        using SKData encoded = red.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    [Fact]
    public void Extension_DeclaresThreePairs_OperationsAndNoTool()
    {
        SvgExtension extension = new();

        Assert.Equal("SVG", extension.Name);
        Assert.Same(StandardDetectors.Svg, Assert.Single(extension.Detectors));
        IConversionEngine engine = Assert.Single(extension.Engines);
        Assert.Empty(engine.Tools);
        Assert.True(engine.SupportsOperations);
        Assert.All(engine.Pairs, p => Assert.Equal(FileFormat.Svg, p.From));
        Assert.Equal([FileFormat.Png, FileFormat.Jpeg, FileFormat.Webp], engine.Pairs.Select(p => p.To));
    }

    [Fact]
    public void Capabilities_ShowSvgPairsReady_OnlyWithTheExtension()
    {
        Converter with = Svg();
        Converter without = new();

        Conversion[] svgPairs = [.. with.Capabilities.All.Where(c => c.From.Equals(FileFormat.Svg))];
        Assert.Equal(3, svgPairs.Length);
        Assert.All(svgPairs, c => Assert.Equal(ConversionAvailability.Ready, c.Availability));
        Assert.True(with.Capabilities.CanConvert(FileFormat.Svg, FileFormat.Webp));
        Assert.False(without.Capabilities.CanConvert(FileFormat.Svg, FileFormat.Png));
        Assert.False(with.Capabilities.CanConvert(FileFormat.Svg, FileFormat.Bmp));
    }

    [Fact]
    public async Task Png_HasNaturalSize_ColorsAndTransparency()
    {
        using SKBitmap png = await RenderAsync(Svg(), Sample("shapes.svg"), FileFormat.Png);

        Assert.Equal((120, 80), (png.Width, png.Height));
        AssertColor(SKColors.Red, png.GetPixel(10, 10));
        AssertColor(SKColors.Blue, png.GetPixel(90, 50));
        Assert.Equal(0, png.GetPixel(60, 5).Alpha);
    }

    [Fact]
    public async Task Jpeg_PutsTransparentPlacesOnWhite_AndQualityChangesSize()
    {
        Converter converter = Svg();
        using SKBitmap jpeg = await RenderAsync(converter, Sample("shapes.svg"), FileFormat.Jpeg);
        ConversionResult low = await converter.ConvertAsync(
            Source.File(Sample("gradient.svg")), Target.Memory(FileFormat.Jpeg), new ConversionOptions { Image = new ImageOptions { Quality = 5 } });
        ConversionResult high = await converter.ConvertAsync(
            Source.File(Sample("gradient.svg")), Target.Memory(FileFormat.Jpeg), new ConversionOptions { Image = new ImageOptions { Quality = 100 } });

        Assert.Equal((120, 80), (jpeg.Width, jpeg.Height));
        AssertColor(SKColors.White, jpeg.GetPixel(60, 5), tolerance: 12);
        AssertColor(SKColors.Red, jpeg.GetPixel(10, 10), tolerance: 24);
        Assert.True(low.Bytes!.Length < high.Bytes!.Length, $"Качество 5 дало {low.Bytes.Length} байт, 100 — {high.Bytes.Length}.");
    }

    [Fact]
    public async Task Jpeg_Background_IsASetting()
    {
        SvgSettings settings = new() { JpegBackground = System.Drawing.Color.Black };

        using SKBitmap jpeg = await RenderAsync(Svg(settings), Sample("shapes.svg"), FileFormat.Jpeg);

        AssertColor(SKColors.Black, jpeg.GetPixel(60, 5), tolerance: 12);
    }

    [Fact]
    public async Task Webp_KeepsTransparency()
    {
        using SKBitmap webp = await RenderAsync(Svg(), Sample("shapes.svg"), FileFormat.Webp);

        Assert.Equal((120, 80), (webp.Width, webp.Height));
        Assert.Equal(0, webp.GetPixel(60, 5).Alpha);
        Assert.True(webp.GetPixel(10, 10).Red > 200);
    }

    [Fact]
    public async Task Text_IsDrawn()
    {
        using SKBitmap png = await RenderAsync(Svg(), Sample("text.svg"), FileFormat.Png);

        Assert.True(CountDark(png) > 200, "Текст не нарисован.");
    }

    [Fact]
    public async Task Gradient_WithViewBoxOnly_TakesItsSize_AndGoesFromRedToBlue()
    {
        using SKBitmap png = await RenderAsync(Svg(), Sample("gradient.svg"), FileFormat.Png);

        Assert.Equal((200, 100), (png.Width, png.Height));
        Assert.True(png.GetPixel(2, 50).Red > 200 && png.GetPixel(2, 50).Blue < 50);
        Assert.True(png.GetPixel(197, 50).Blue > 200 && png.GetPixel(197, 50).Red < 50);
    }

    [Fact]
    public async Task RotateRight_TurnsTheImageClockwise()
    {
        ConversionOptions options = new() { Operations = { Operation.RotateRight } };

        using SKBitmap png = await RenderAsync(Svg(), Sample("shapes.svg"), FileFormat.Png, options);

        Assert.Equal((80, 120), (png.Width, png.Height));
        AssertColor(SKColors.Red, png.GetPixel(70, 10));
        Assert.Equal(0, png.GetPixel(5, 5).Alpha);
    }

    [Fact]
    public async Task ScaleUp_IsRedrawnSharp_NotStretchedPixels()
    {
        ConversionOptions options = new() { Operations = { Operation.ScalePercent(1000) } };

        using SKBitmap png = await RenderAsync(Svg(), Sample("shapes.svg"), FileFormat.Png, options);

        // Край квадрата 40 → 400: перерисовка даёт резкую границу между столбцами 399 и 400, без полосы растяжения.
        Assert.Equal((1200, 800), (png.Width, png.Height));
        AssertColor(SKColors.Red, png.GetPixel(399, 100));
        Assert.Equal(0, png.GetPixel(400, 100).Alpha);
    }

    [Fact]
    public async Task ScaleDown_HalvesTheSize()
    {
        ConversionOptions options = new() { Operations = { Operation.ScalePercent(50) } };

        using SKBitmap png = await RenderAsync(Svg(), Sample("shapes.svg"), FileFormat.Png, options);

        Assert.Equal((60, 40), (png.Width, png.Height));
    }

    [Fact]
    public async Task Stream_And_File_GiveTheSameResult()
    {
        Converter converter = Svg();
        await using FileStream stream = File.OpenRead(Sample("gradient.svg"));

        ConversionResult fromStream = await converter.ConvertAsync(Source.Stream(stream), Target.Memory(FileFormat.Png));
        ConversionResult fromFile = await converter.ConvertAsync(Source.File(Sample("gradient.svg")), Target.Memory(FileFormat.Png));

        Assert.Equal(fromFile.Bytes, fromStream.Bytes);
    }

    [Fact]
    public async Task BrokenXml_IsEngineFailed_WithAClearReason()
    {
        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Svg().ConvertAsync(Source.File(Sample("broken.svg")), Target.Memory(FileFormat.Png)));

        Assert.Equal(ConversionFailureReason.EngineFailed, ex.Reason);
        Assert.Equal(ConversionStage.Conversion, ex.Stage);
        Assert.Contains("повреждён", ex.Message);
    }

    [Fact]
    public async Task UnsupportedTarget_IsRejectedBeforeWork()
    {
        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Svg().ConvertAsync(Source.File(Sample("shapes.svg")), Target.Memory(FileFormat.Bmp)));

        Assert.Equal(ConversionFailureReason.UnsupportedConversion, ex.Reason);
    }

    [Fact]
    public async Task ExternalImageFile_AndNetworkAddress_AreNotLoaded()
    {
        string png = Path.Combine(_folder, "outside.png");
        File.WriteAllBytes(png, RedSquarePng());
        string svg = Put("external.svg", $"""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="50" height="50">
              <image width="50" height="50" xlink:href="{new Uri(png).AbsoluteUri}"/>
              <image width="50" height="50" href="http://127.0.0.1:9/x.png"/>
            </svg>
            """);

        using SKBitmap result = await RenderAsync(Svg(), svg, FileFormat.Png);

        Assert.Equal(0, result.GetPixel(25, 25).Alpha);
    }

    [Fact]
    public async Task EmbeddedDataImage_IsDrawn()
    {
        string svg = Put("embedded.svg", $"""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="50" height="50">
              <image width="50" height="50" xlink:href="data:image/png;base64,{Convert.ToBase64String(RedSquarePng())}"/>
            </svg>
            """);

        using SKBitmap result = await RenderAsync(Svg(), svg, FileFormat.Png);

        AssertColor(SKColors.Red, result.GetPixel(25, 25));
    }

    [Fact]
    public async Task ExternalXmlEntity_IsNotRead()
    {
        string secret = Path.Combine(_folder, "secret.txt");
        File.WriteAllText(secret, "SECRET SECRET SECRET");
        string svg = Put("entity.svg", $"""
            <?xml version="1.0"?>
            <!DOCTYPE svg [<!ENTITY leak SYSTEM "{new Uri(secret).AbsoluteUri}">]>
            <svg xmlns="http://www.w3.org/2000/svg" width="400" height="60">
              <text x="5" y="45" font-size="36" fill="#000000">&leak;</text>
            </svg>
            """);

        using SKBitmap result = await RenderAsync(Svg(), svg, FileFormat.Png);

        Assert.Equal(0, CountDark(result));
    }

    [Fact]
    public async Task EntityExpansionBomb_FinishesQuickly()
    {
        StringBuilder dtd = new("<!ENTITY a0 \"xxxxxxxxxx\">");
        for (int i = 1; i <= 9; i++)
        {
            dtd.Append($"<!ENTITY a{i} \"{string.Concat(Enumerable.Repeat($"&a{i - 1};", 10))}\">");
        }

        string svg = Put("bomb.svg", $"""
            <?xml version="1.0"?>
            <!DOCTYPE svg [{dtd}]>
            <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40"><text y="20">&a9;</text></svg>
            """);

        Task<ConversionResult> run = Svg().ConvertAsync(Source.File(svg), Target.Memory(FileFormat.Png));
        Task finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(20)));

        Assert.Same(run, finished);
    }

    [Fact]
    public async Task MaxPixels_IsASetting_AndNamedInTheError()
    {
        SvgSettings settings = new() { MaxPixels = 1000 };

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Svg(settings).ConvertAsync(Source.File(Sample("shapes.svg")), Target.Memory(FileFormat.Png)));

        Assert.Equal(ConversionFailureReason.EngineFailed, ex.Reason);
        Assert.Contains("MaxPixels", ex.Message);
    }

    [Fact]
    public async Task MaxInputBytes_IsASetting_AndNamedInTheError()
    {
        SvgSettings settings = new() { MaxInputBytes = 100 };

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Svg(settings).ConvertAsync(Source.File(Sample("shapes.svg")), Target.Memory(FileFormat.Png)));

        Assert.Contains("MaxInputBytes", ex.Message);
    }

    [Fact]
    public async Task Settings_AreCopied_AtCreation()
    {
        SvgSettings settings = new();
        Converter converter = Svg(settings);
        settings.MaxPixels = 1;

        using SKBitmap png = await RenderAsync(converter, Sample("shapes.svg"), FileFormat.Png);

        Assert.Equal(120, png.Width);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Settings_RejectQualityOutOfRange(int quality)
    {
        SvgSettings settings = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.DefaultJpegQuality = quality);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.DefaultWebpQuality = quality);
    }

    [Fact]
    public void Settings_RejectNonPositiveLimits()
    {
        SvgSettings settings = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => settings.MaxPixels = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.MaxInputBytes = -1);
    }

    [Fact]
    public async Task Canceled_BeforeStart_IsCancellation_NotAnError()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await Svg().ConvertAsync(Source.File(Sample("shapes.svg")), Target.Memory(FileFormat.Png), cancellationToken: cts.Token));
    }

    [Fact]
    public void MainPackage_DoesNotReferenceSkia()
    {
        string[] references = [.. typeof(Converter).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty)];

        Assert.DoesNotContain(references, n => n.Contains("Skia", StringComparison.OrdinalIgnoreCase) || n.StartsWith("Svg", StringComparison.OrdinalIgnoreCase));
    }
}

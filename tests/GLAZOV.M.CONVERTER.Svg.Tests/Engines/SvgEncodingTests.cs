using System.IO.Compression;
using System.Text;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Settings;
using SkiaSharp;

namespace GLAZOV.M.CONVERTER.Svg.Tests.Engines;

/// <summary>
/// SVG в UTF-16, сжатый SVGZ и SVG с длинным вступлением конвертируются так же, как обычный;
/// распаковка SVGZ ограничена пределом входа, повреждённое сжатие названо в ошибке.
/// </summary>
public sealed class SvgEncodingTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convsvg-" + Guid.NewGuid().ToString("N"));

    public SvgEncodingTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private static string Shapes()
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", "shapes.svg"));
    }

    private static Converter Svg(SvgSettings? settings = null)
    {
        ConverterSettings converterSettings = new();
        converterSettings.Extensions.Add(settings is null ? new SvgExtension() : new SvgExtension(settings));
        return new Converter(converterSettings);
    }

    private static byte[] Gzip(byte[] data)
    {
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(data);
        }

        return output.ToArray();
    }

    private string Put(string name, byte[] data)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, data);
        return path;
    }

    // Результат — те же фигуры: 120×80, красный квадрат слева вверху, прозрачная середина сверху.
    private static async Task AssertShapesAsync(Converter converter, string path)
    {
        ConversionResult result = await converter.ConvertAsync(Source.File(path), Target.Memory(FileFormat.Png));

        Assert.Equal(FileFormat.Svg, result.SourceType);
        using SKBitmap png = SKBitmap.Decode(result.Bytes);
        Assert.Equal((120, 80), (png.Width, png.Height));
        Assert.Equal(SKColors.Red, png.GetPixel(10, 10));
        Assert.Equal(0, png.GetPixel(60, 5).Alpha);
    }

    [Theory]
    [InlineData("utf-16LE", true)]
    [InlineData("utf-16BE", true)]
    [InlineData("utf-16LE", false)]
    public async Task Utf16_IsConverted(string encodingName, bool withBom)
    {
        Encoding encoding = Encoding.GetEncoding(encodingName);
        string text = Shapes().Replace("encoding=\"UTF-8\"", "encoding=\"UTF-16\"", StringComparison.Ordinal);
        byte[] bytes = encoding.GetBytes(text);

        await AssertShapesAsync(Svg(), Put("utf16.svg", withBom ? [.. encoding.GetPreamble(), .. bytes] : bytes));
    }

    [Fact]
    public async Task Svgz_IsConverted()
    {
        await AssertShapesAsync(Svg(), Put("фигуры.svgz", Gzip(Encoding.UTF8.GetBytes(Shapes()))));
    }

    [Fact]
    public async Task Svgz_FromAStreamWithoutFileName_IsConverted()
    {
        await using MemoryStream stream = new(Gzip(Encoding.UTF8.GetBytes(Shapes())));

        ConversionResult result = await Svg().ConvertAsync(Source.Stream(stream), Target.Memory(FileFormat.Webp));

        Assert.Equal(FileFormat.Svg, result.SourceType);
    }

    [Fact]
    public async Task LongProlog_IsConverted()
    {
        string text = Shapes();
        int root = text.IndexOf("<svg", StringComparison.Ordinal);
        string withProlog = text[..root] + "<!-- " + new string('л', 15_000) + " -->\n" + text[root..];

        await AssertShapesAsync(Svg(), Put("длинный.svg", Encoding.UTF8.GetBytes(withProlog)));
    }

    [Fact]
    public async Task Svgz_UnpackingBeyondMaxInputBytes_IsStopped()
    {
        // 10 МБ пробелов внутри корня сжимаются в десятки килобайт; распаковка обрывается на пределе.
        string bomb = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\">" + new string(' ', 10_000_000) + "</svg>";
        byte[] compressed = Gzip(Encoding.UTF8.GetBytes(bomb));
        SvgSettings settings = new() { MaxInputBytes = 1_000_000 };

        Assert.True(compressed.Length < settings.MaxInputBytes);
        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Svg(settings).ConvertAsync(Source.File(Put("бомба.svgz", compressed)), Target.Memory(FileFormat.Png)));

        Assert.Equal(ConversionFailureReason.EngineFailed, ex.Reason);
        Assert.Contains("MaxInputBytes", ex.Message);
    }

    [Fact]
    public async Task Svgz_DamagedAfterTheBeginning_IsEngineFailed_NamedAsDamaged()
    {
        byte[] data = Gzip(Encoding.UTF8.GetBytes(Shapes()));
        for (int i = data.Length - 20; i < data.Length; i++)
        {
            data[i] ^= 0xA5;
        }

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await Svg().ConvertAsync(Source.File(Put("битый.svgz", data)), Target.Memory(FileFormat.Png)));

        Assert.Equal(ConversionFailureReason.EngineFailed, ex.Reason);
        Assert.Contains("повреждён", ex.Message);
    }
}

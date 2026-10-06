using System.Text;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Settings;
using SkiaSharp;

namespace GLAZOV.M.CONVERTER.Svg.Tests.Engines;

/// <summary>
/// What the Svg package alone knows about types: it names the SVG detector and declares SVG, PNG, JPEG and WebP; a picture of another kind is not recognised.
/// </summary>
public sealed class SvgDetectionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convsvgdet-" + Guid.NewGuid().ToString("N"));

    public SvgDetectionTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private static Converter WithSvgOnly()
    {
        ConverterSettings settings = new();
        settings.Extensions.Add(new SvgExtension());
        return new Converter(settings);
    }

    private string Put(string name, byte[] bytes)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void Extension_NamesTheSvgDetector_AndDeclaresFourFormats()
    {
        SvgExtension extension = new();

        Assert.Same(StandardDetectors.Svg, Assert.Single(extension.Detectors));
        Assert.Equal(["SVG", "PNG", "JPEG", "WebP"], ((IConverterExtension)extension).Formats.Select(format => format.Name));
    }

    [Fact]
    public async Task OnlyTheSvgPackage_RecognisesSvgInEveryEncoding()
    {
        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"8\" height=\"8\"><rect width=\"8\" height=\"8\" fill=\"red\"/></svg>";
        byte[] utf16 = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(svg)];
        using MemoryStream packed = new();
        await using (System.IO.Compression.GZipStream gzip = new(packed, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            await gzip.WriteAsync(Encoding.UTF8.GetBytes(svg));
        }

        Converter converter = WithSvgOnly();

        Assert.Same(FileFormat.Svg, await converter.DetectAsync(Source.File(Put("а.svg", Encoding.UTF8.GetBytes(svg)))));
        Assert.Same(FileFormat.Svg, await converter.DetectAsync(Source.File(Put("б.svg", utf16))));
        Assert.Same(FileFormat.Svg, await converter.DetectAsync(Source.File(Put("в.svgz", packed.ToArray()))));
    }

    [Fact]
    public async Task APngInput_WithOnlyTheSvgPackage_IsNotRecognised()
    {
        using SKBitmap bitmap = new(4, 4);
        using SKData data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        string path = Put("картинка.png", data.ToArray());
        Converter converter = WithSvgOnly();

        Assert.Same(FileFormat.Unknown, await converter.DetectAsync(Source.File(path)));
        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await converter.ConvertAsync(Source.File(path), Target.Memory(FileFormat.Jpeg)));
        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, ex.Reason);
    }
}

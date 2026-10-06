using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Settings;

namespace GLAZOV.M.CONVERTER.Pdf.Tests.Engines;

/// <summary>
/// What the Pdf package alone knows about types: it names the image detector, so a picture is recognised and turned into PDF without any other package.
/// </summary>
public sealed class PdfDetectionTests
{
    public static TheoryData<string, string> Pictures => new()
    {
        { "sample.png", "PNG" },
        { "sample.jpg", "JPEG" },
        { "sample.webp", "WebP" },
        { "sample.bmp", "BMP" },
        { "sample.gif", "GIF" },
    };

    private static string Sample(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Samples", name);
    }

    [Fact]
    public void Extension_NamesTheImageDetector_AndDeclaresThePicturesAndPdf()
    {
        PdfExtension extension = new();

        Assert.Same(StandardDetectors.Image, Assert.Single(extension.Detectors));
        Assert.Equal(["BMP", "GIF", "JPEG", "PDF", "PNG", "WebP"], ((IConverterExtension)extension).Formats.Select(format => format.Name).Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Pictures))]
    public async Task OnlyThePdfPackage_RecognisesAndConvertsEveryPicture(string file, string name)
    {
        Converter converter = PdfKit.WithPdf();

        FileFormat detected = await converter.DetectAsync(Source.File(Sample(file)));
        ConversionResult result = await converter.ConvertAsync(Source.File(Sample(file)), Target.Memory(FileFormat.Pdf));

        Assert.Equal(name, detected.Name);
        Assert.True(result.Succeeded);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(result.Bytes!, 0, 4));
    }

    [Fact]
    public async Task TheImageDetectorNamedTwice_IsAskedOnce_AndTheFormatsAreListedOnce()
    {
        ConverterSettings settings = new();
        settings.Extensions.Add(new PdfExtension());
        settings.Extensions.Add(new FormatsExtension());
        Converter converter = new(settings);

        Assert.Same(FileFormat.Png, await converter.DetectAsync(Source.File(Sample("sample.png"))));
        Assert.Equal(converter.Capabilities.Formats.Count, converter.Capabilities.Formats.Distinct().Count());
    }
}

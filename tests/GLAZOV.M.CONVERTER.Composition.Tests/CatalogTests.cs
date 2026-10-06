using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Settings;
using GLAZOV.M.CONVERTER.Tests.Extension;

namespace GLAZOV.M.CONVERTER.Composition.Tests;

/// <summary>
/// The five packages together: one list of formats, one catalog of pairs, the earlier package wins a shared pair,
/// and a clash of descriptions is refused when the converter is created.
/// </summary>
public sealed class CatalogTests
{
    private static readonly string[] Orders = [.. AllPackages.Orders()];

    private static string Sample(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Samples", name);
    }

    [Fact]
    public void FormatsAreListedOnce_AndNoOrderRaisesAClash()
    {
        foreach (string order in Orders)
        {
            IReadOnlyList<FileFormat> formats = AllPackages.Converter(order).Capabilities.Formats;

            Assert.True(formats.Count == formats.Distinct().Count(), $"order {order}: a format is listed twice");
            Assert.True(formats.Count == 33, $"order {order}: {formats.Count} formats");
        }
    }

    [Fact]
    public void Catalog_IsTheSumOfThePairsOfThePackages_InEveryOrder()
    {
        // Ffmpeg 131 + Office 12 + Svg 3 + Pdf 5; the Formats package brings no pair.
        foreach (string order in Orders)
        {
            ConverterCapabilities caps = AllPackages.Converter(order).Capabilities;
            IReadOnlyList<Conversion> all = caps.All;

            Assert.True(all.Count == 151, $"order {order}: {all.Count} pairs");
            Assert.Equal(all.Count, all.Select(c => (c.From, c.To)).Distinct().Count());
            Assert.True(caps.CanConvert(FileFormat.Png, FileFormat.Pdf), $"order {order}: PNG → PDF");
            Assert.True(caps.CanConvert(FileFormat.Docx, FileFormat.Pdf), $"order {order}: DOCX → PDF");
            Assert.True(caps.CanConvert(FileFormat.Svg, FileFormat.Webp), $"order {order}: SVG → WebP");
            Assert.True(caps.CanConvert(FileFormat.Mp4, FileFormat.Mp3), $"order {order}: MP4 → MP3");
        }
    }

    [Fact]
    public void PngToPdf_NeedsNoProgram_AndDocxToPdf_NeedsLibreOffice()
    {
        ConverterCapabilities caps = AllPackages.Converter("Formats,Ffmpeg,Office,Svg,Pdf").Capabilities;

        Conversion picture = caps.All.Single(c => c.From.Equals(FileFormat.Png) && c.To.Equals(FileFormat.Pdf));
        Conversion document = caps.All.Single(c => c.From.Equals(FileFormat.Docx) && c.To.Equals(FileFormat.Pdf));

        Assert.Equal(ConversionAvailability.Ready, picture.Availability);
        Assert.Equal(caps.Tool("soffice").Found ? ConversionAvailability.Ready : ConversionAvailability.ToolMissing, document.Availability);
    }

    [Fact]
    public async Task APairShownByTwoPackages_GoesToTheOneConnectedFirst_AndTheCatalogShowsItOnce()
    {
        byte[] marker = "ПЕРВЫЙ:"u8.ToArray();
        LambdaEngine mine = new(null, [new ConversionPair(FileFormat.Png, FileFormat.Pdf)], marker: marker);
        ConfigurableExtension custom = new("мой", [], [mine]);

        foreach (bool customFirst in new[] { true, false })
        {
            ConverterSettings settings = new();
            IConverterExtension[] row = customFirst ? [custom, AllPackages.Create("Formats"), AllPackages.Create("Pdf")] : [AllPackages.Create("Formats"), AllPackages.Create("Pdf"), custom];
            foreach (IConverterExtension extension in row)
            {
                settings.Extensions.Add(extension);
            }

            Converter converter = new(settings);
            ConversionResult result = await converter.ConvertAsync(Source.File(Sample("sample.png")), Target.Memory(FileFormat.Pdf));

            Assert.Equal(1, converter.Capabilities.All.Count(c => c.From.Equals(FileFormat.Png) && c.To.Equals(FileFormat.Pdf)));
            Assert.Equal(customFirst, result.Bytes!.AsSpan().StartsWith(marker));
            Assert.Equal(!customFirst, result.Bytes!.AsSpan().StartsWith("%PDF"u8));
        }
    }

    [Fact]
    public void TheSameMimeTypeWithAnotherDescription_IsRefusedWithBothPackageNames()
    {
        ConfigurableExtension clash = new("чужой пакет", [], []) { Declared = [FileFormat.Create("PNG", "image/png", "pngx")] };
        ConverterSettings settings = new();
        settings.Extensions.Add(new FormatsExtension());
        settings.Extensions.Add(clash);

        ArgumentException ex = Assert.Throws<ArgumentException>(() => new Converter(settings));

        Assert.Contains("Formats", ex.Message);
        Assert.Contains("чужой пакет", ex.Message);
        Assert.Contains("image/png", ex.Message);
        Assert.Contains("pngx", ex.Message);
    }
}

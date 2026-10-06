using System.Text;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Formats;
using SkiaSharp;

namespace GLAZOV.M.CONVERTER.Composition.Tests;

/// <summary>
/// One converter with the five packages plugged in does sound, a picture, a document, SVG and video in a row, with the real programs.
/// </summary>
public sealed class OneConverterLiveTests : IDisposable
{
    private const string Order = "Formats,Ffmpeg,Office,Svg,Pdf";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convlive-" + Guid.NewGuid().ToString("N"));

    public OneConverterLiveTests()
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

    private async Task<byte[]> ConvertAsync(Converter converter, string sample, FileFormat to, FileFormat expectedSource)
    {
        string output = Path.Combine(_folder, Guid.NewGuid().ToString("N") + "." + to.Extension);
        ConversionResult result = await converter.ConvertAsync(Source.File(Sample(sample)), Target.File(output, to));

        Assert.True(result.Succeeded, $"{sample} → {to.Name}");
        Assert.Equal(expectedSource, result.SourceType);
        Assert.Same(to, await converter.DetectAsync(Source.File(output)));
        return await File.ReadAllBytesAsync(output);
    }

    [LiveFact("ffmpeg")]
    public async Task Sound_FromWavToMp3()
    {
        byte[] mp3 = await ConvertAsync(AllPackages.Converter(Order), "sample.wav", FileFormat.Mp3, FileFormat.Wav);

        Assert.True(mp3.Length > 100);
    }

    [LiveFact("ffmpeg")]
    public async Task Picture_FromPngToJpeg_AndToPdf()
    {
        Converter converter = AllPackages.Converter(Order);

        byte[] jpeg = await ConvertAsync(converter, "sample.png", FileFormat.Jpeg, FileFormat.Png);
        byte[] pdf = await ConvertAsync(converter, "sample.png", FileFormat.Pdf, FileFormat.Png);

        Assert.NotNull(SKBitmap.Decode(jpeg));
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [LiveFact("soffice")]
    public async Task Document_FromDocxToPdf()
    {
        byte[] pdf = await ConvertAsync(AllPackages.Converter(Order), "sample.docx", FileFormat.Pdf, FileFormat.Docx);

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [LiveFact]
    public async Task Svg_ToPng()
    {
        byte[] png = await ConvertAsync(AllPackages.Converter(Order), "sample-plain.svg", FileFormat.Png, FileFormat.Svg);

        Assert.NotNull(SKBitmap.Decode(png));
    }

    [LiveFact("ffmpeg")]
    public async Task Video_FromMp4ToWebm()
    {
        byte[] webm = await ConvertAsync(AllPackages.Converter(Order), "sample.mp4", FileFormat.Webm, FileFormat.Mp4);

        Assert.True(webm.Length > 100);
    }

    [LiveFact("ffmpeg", "soffice")]
    public async Task AllFiveKindsThroughOneConverter_InARow()
    {
        Converter converter = AllPackages.Converter(Order);

        await ConvertAsync(converter, "sample.wav", FileFormat.Flac, FileFormat.Wav);
        await ConvertAsync(converter, "sample.gif", FileFormat.Png, FileFormat.Gif);
        await ConvertAsync(converter, "sample.odt", FileFormat.Pdf, FileFormat.Odt);
        await ConvertAsync(converter, "sample-prolog.svg", FileFormat.Jpeg, FileFormat.Svg);
        await ConvertAsync(converter, "sample.mkv", FileFormat.Mp4, FileFormat.Mkv);
    }
}

using GLAZOV.M.CONVERTER.Batch;
using GLAZOV.M.CONVERTER.Capabilities;
using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Extensions;
using GLAZOV.M.CONVERTER.Formats;
using GLAZOV.M.CONVERTER.Formats.Options;
using SkiaSharp;
using System.Text;

namespace GLAZOV.M.CONVERTER.Pdf.Tests.Engines;

/// <summary>
/// The PDF extension through the public calls of the main library: pairs, real and generated pictures,
/// page size, transparency, EXIF orientation, streams, limits, damaged input. The PDF is read back with PdfPig.
/// </summary>
public sealed class PdfExtensionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "convpdf-" + Guid.NewGuid().ToString("N"));

    public PdfExtensionTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    public static TheoryData<string> RealSamples => ["sample.png", "sample.jpg", "sample.webp", "sample.bmp", "sample.gif"];

    [Fact]
    public void Extension_NamesTheImageDetector()
    {
        Assert.Same(StandardDetectors.Image, Assert.Single(new PdfExtension().Detectors));
    }

    [Fact]
    public void Extension_DeclaresFivePairs_NoOperationsAndNoTool()
    {
        IConversionEngine engine = Assert.Single(new PdfExtension().Engines);

        Assert.Empty(engine.Tools);
        Assert.False(engine.SupportsOperations);
        Assert.Equal(5, engine.Pairs.Count);
        Assert.All(engine.Pairs, pair => Assert.Equal(FileFormat.Pdf, pair.To));
        Assert.Equal(
            [FileFormat.Png, FileFormat.Jpeg, FileFormat.Webp, FileFormat.Bmp, FileFormat.Gif],
            engine.Pairs.Select(pair => pair.From));
    }

    [Fact]
    public void Capabilities_ShowFivePairsReady_OnlyWithTheExtension()
    {
        Converter with = PdfKit.WithPdf();
        Converter without = new();

        Conversion[] pdfPairs = [.. with.Capabilities.All.Where(c => (c.To.Equals(FileFormat.Pdf) && c.From.Equals(FileFormat.Png)) || (c.To.Equals(FileFormat.Pdf) && c.From.Equals(FileFormat.Jpeg))
            || (c.To.Equals(FileFormat.Pdf) && c.From.Equals(FileFormat.Webp)) || (c.To.Equals(FileFormat.Pdf) && c.From.Equals(FileFormat.Bmp)) || (c.To.Equals(FileFormat.Pdf) && c.From.Equals(FileFormat.Gif)))];
        Assert.Equal(5, pdfPairs.Length);
        Assert.All(pdfPairs, c => Assert.Equal(ConversionAvailability.Ready, c.Availability));
        Assert.True(with.Capabilities.CanConvert(FileFormat.Gif, FileFormat.Pdf));
        Assert.False(without.Capabilities.CanConvert(FileFormat.Png, FileFormat.Pdf));
        Assert.False(with.Capabilities.CanConvert(FileFormat.Tiff, FileFormat.Pdf));
        Assert.False(with.Capabilities.CanConvert(FileFormat.Pdf, FileFormat.Png));
    }

    [Theory]
    [MemberData(nameof(RealSamples))]
    public async Task RealPicture_BecomesOnePageOfItsSize(string sample)
    {
        byte[] picture = await File.ReadAllBytesAsync(PdfKit.Sample(sample));
        (int width, int height) = PdfKit.SizeOf(picture);

        byte[] pdf = await PdfKit.ToPdfAsync(PdfKit.WithPdf(), picture);

        (double Width, double Height) = Assert.Single(PdfKit.Pages(pdf));
        Assert.Equal(width, Width, 0.5);
        Assert.Equal(height, Height, 0.5);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
    }

    [Theory]
    [InlineData(1, 1, SKEncodedImageFormat.Png)]
    [InlineData(3, 7, SKEncodedImageFormat.Png)]
    [InlineData(640, 480, SKEncodedImageFormat.Jpeg)]
    [InlineData(1000, 300, SKEncodedImageFormat.Webp)]
    [InlineData(37, 911, SKEncodedImageFormat.Jpeg)]
    public async Task GeneratedPicture_OfAnySize_GivesAPageOfThatSize(int width, int height, SKEncodedImageFormat format)
    {
        byte[] pdf = await PdfKit.ToPdfAsync(PdfKit.WithPdf(), PdfKit.Draw(width, height, format));

        (double Width, double Height) = Assert.Single(PdfKit.Pages(pdf));
        Assert.Equal(width, Width, 0.5);
        Assert.Equal(height, Height, 0.5);
    }

    [Fact]
    public async Task ThePicture_IsReallyOnThePage_WithItsPixels()
    {
        byte[] pdf = await PdfKit.ToPdfAsync(PdfKit.WithPdf(), PdfKit.Draw(80, 60, SKEncodedImageFormat.Png));

        using UglyToad.PdfPig.PdfDocument document = UglyToad.PdfPig.PdfDocument.Open(pdf);
        UglyToad.PdfPig.Content.IPdfImage embedded = Assert.Single(document.GetPage(1).GetImages());
        Assert.True(embedded.TryGetPng(out byte[]? png));
        using SKBitmap bitmap = SKBitmap.Decode(png);

        Assert.Equal((80, 60), (bitmap.Width, bitmap.Height));
        Assert.True(bitmap.GetPixel(3, 3).Blue > bitmap.GetPixel(3, 3).Red + 50, "Top left square is dark blue.");
        Assert.Equal(SKColors.White, bitmap.GetPixel(70, 50));
    }

    [Fact]
    public async Task Transparency_IsKept_AnOpaquePictureHasNoAlphaMask()
    {
        byte[] withAlpha = await PdfKit.ToPdfAsync(PdfKit.WithPdf(), PdfKit.Draw(64, 48, SKEncodedImageFormat.Png, transparentRight: true));
        byte[] opaque = await PdfKit.ToPdfAsync(PdfKit.WithPdf(), PdfKit.Draw(64, 48, SKEncodedImageFormat.Png));

        Assert.Contains("/SMask", Encoding.Latin1.GetString(withAlpha));
        Assert.DoesNotContain("/SMask", Encoding.Latin1.GetString(opaque));
    }

    [Theory]
    [InlineData(1, 40, 20)]
    [InlineData(3, 40, 20)]
    [InlineData(6, 20, 40)]
    [InlineData(8, 20, 40)]
    public async Task ExifOrientation_OfAJpeg_IsApplied_QuarterTurnsSwapThePageSides(int orientation, int pageWidth, int pageHeight)
    {
        byte[] jpeg = PdfKit.WithExifOrientation(PdfKit.Draw(40, 20, SKEncodedImageFormat.Jpeg), orientation);
        using (SKCodec codec = SKCodec.Create(new MemoryStream(jpeg))!)
        {
            Assert.Equal(orientation, (int)codec.EncodedOrigin);
        }

        byte[] pdf = await PdfKit.ToPdfAsync(PdfKit.WithPdf(), jpeg);

        (double Width, double Height) = Assert.Single(PdfKit.Pages(pdf));
        Assert.Equal(pageWidth, Width, 0.5);
        Assert.Equal(pageHeight, Height, 0.5);
    }

    [Fact]
    public async Task FileToFile_WritesAReadablePdf()
    {
        string target = Path.Combine(_folder, "out.pdf");

        ConversionResult result = await PdfKit.WithPdf().ConvertAsync(Source.File(PdfKit.Sample("sample.png")), Target.File(target, FileFormat.Pdf));

        Assert.True(result.Succeeded);
        Assert.Single(PdfKit.Pages(await File.ReadAllBytesAsync(target)));
    }

    [Fact]
    public async Task Pictures_FromAFolder_GiveOnePdfEach()
    {
        string input = Path.Combine(_folder, "in");
        string output = Path.Combine(_folder, "pdfs");
        Directory.CreateDirectory(input);
        foreach (string sample in new[] { "sample.png", "sample.jpg", "sample.bmp" })
        {
            // Result names keep the stem of the input, so the stems must differ.
            File.Copy(PdfKit.Sample(sample), Path.Combine(input, sample.Replace('.', '-') + Path.GetExtension(sample)));
        }

        BatchReport batch = await PdfKit.WithPdf().ConvertBatchAsync(Source.Folder(input), Target.Folder(output, FileFormat.Pdf));

        Assert.Equal(3, batch.Succeeded);
        Assert.Equal(3, Directory.GetFiles(output, "*.pdf").Length);
    }

    [Fact]
    public async Task Operations_AreRefusedBeforeAnyWork()
    {
        using MemoryStream input = new(PdfKit.Draw(8, 8, SKEncodedImageFormat.Png));

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await PdfKit.WithPdf().ConvertAsync(
                Source.Stream(input),
                Target.Memory(FileFormat.Pdf),
                new ConversionOptions { Operations = { VisualOperations.RotateRight } }));

        Assert.Equal(ConversionFailureReason.UnsupportedConversion, ex.Reason);
        Assert.Equal(ConversionStage.Preparation, ex.Stage);
    }

    [Fact]
    public async Task WithoutTheExtension_PictureToPdfIsRefused()
    {
        using MemoryStream input = new(PdfKit.Draw(8, 8, SKEncodedImageFormat.Png));

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await new Converter().ConvertAsync(Source.Stream(input), Target.Memory(FileFormat.Pdf)));

        // Without the extension no detector knows the picture either: the type is not recognised.
        Assert.Equal(ConversionFailureReason.TypeNotConfirmed, ex.Reason);
    }

    [Fact]
    public async Task MaxPixels_IsASetting_AndNamedInTheError()
    {
        PdfSettings settings = new() { MaxPixels = 1000 };
        using MemoryStream input = new(PdfKit.Draw(100, 100, SKEncodedImageFormat.Png));

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await PdfKit.WithPdf(settings).ConvertAsync(Source.Stream(input), Target.Memory(FileFormat.Pdf)));

        Assert.Equal(ConversionFailureReason.EngineFailed, ex.Reason);
        Assert.Contains("MaxPixels", ex.Message);
    }

    [Fact]
    public async Task APictureJustWithinTheLimit_IsAccepted()
    {
        PdfSettings settings = new() { MaxPixels = 10_000 };

        byte[] pdf = await PdfKit.ToPdfAsync(PdfKit.WithPdf(settings), PdfKit.Draw(100, 100, SKEncodedImageFormat.Png));

        Assert.Single(PdfKit.Pages(pdf));
    }

    [Fact]
    public async Task MaxInputBytes_IsASetting_AndNamedInTheError()
    {
        PdfSettings settings = new() { MaxInputBytes = 50 };
        using MemoryStream input = new(PdfKit.Draw(64, 64, SKEncodedImageFormat.Png));

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await PdfKit.WithPdf(settings).ConvertAsync(Source.Stream(input), Target.Memory(FileFormat.Pdf)));

        Assert.Contains("MaxInputBytes", ex.Message);
    }

    [Fact]
    public async Task TruncatedPicture_IsAClearFailure_NotACrash()
    {
        byte[] png = PdfKit.Draw(64, 64, SKEncodedImageFormat.Png);
        using MemoryStream input = new([.. png.Take(40)]);

        ConversionException ex = await Assert.ThrowsAnyAsync<ConversionException>(async () =>
            await PdfKit.WithPdf().ConvertAsync(Source.Stream(input), Target.Memory(FileFormat.Pdf)));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Theory]
    [InlineData("sample.webp", 0.5)]
    [InlineData("sample.png", 0.9)]
    [InlineData("sample.jpg", 0.9)]
    [InlineData("sample.bmp", 0.9)]
    public async Task PictureCutNearTheEnd_IsRefused_NamingTheCut(string sample, double kept)
    {
        byte[] all = await File.ReadAllBytesAsync(PdfKit.Sample(sample));
        using MemoryStream input = new(all[..(int)(all.Length * kept)]);

        ConversionException ex = await Assert.ThrowsAsync<ConversionException>(async () =>
            await PdfKit.WithPdf().ConvertAsync(Source.Stream(input), Target.Memory(FileFormat.Pdf)));

        Assert.Equal(ConversionFailureReason.EngineFailed, ex.Reason);
    }

    [Fact]
    public async Task NotAPicture_IsRefusedByTypeDetection()
    {
        // Plain text is a type of its own (documents), so the junk here is binary.
        using MemoryStream input = new([0x00, 0x01, 0x02, 0x9F, 0xFF, 0x10, 0x00]);

        await Assert.ThrowsAsync<ConversionException>(async () =>
            await PdfKit.WithPdf().ConvertAsync(Source.Stream(input), Target.Memory(FileFormat.Pdf)));
    }

    [Fact]
    public async Task Cancellation_IsHonoured()
    {
        using MemoryStream input = new(PdfKit.Draw(64, 64, SKEncodedImageFormat.Png));
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await PdfKit.WithPdf().ConvertAsync(Source.Stream(input), Target.Memory(FileFormat.Pdf), cancellationToken: cancelled.Token));
    }
}

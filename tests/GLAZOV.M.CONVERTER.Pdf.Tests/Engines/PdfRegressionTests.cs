using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Errors;
using GLAZOV.M.CONVERTER.Formats;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Graphics.Operations;
using UglyToad.PdfPig.Graphics.Operations.SpecialGraphicsState;

namespace GLAZOV.M.CONVERTER.Pdf.Tests.Engines;

/// <summary>
/// Regression tests found at acceptance: the picture must stand upright on the page as a viewer draws it,
/// and a cut-off picture must be refused.
/// </summary>
public sealed class PdfRegressionTests
{
    // Stored pixel (x, y) of a w×h picture lands where each EXIF orientation (1-8) puts it upright.
    private static (double X, double Y) Upright(int orientation, double x, double y, double w, double h)
    {
        return orientation switch
        {
            1 => (x, y),
            2 => (w - x, y),
            3 => (w - x, h - y),
            4 => (x, h - y),
            5 => (y, x),
            6 => (h - y, x),
            7 => (h - y, w - x),
            _ => (y, w - x),
        };
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task ExifOrientation_PutsTheMarkWhereAViewerWillShowIt(int orientation)
    {
        const int w = 160, h = 80;
        byte[] jpeg = PdfKit.WithExifOrientation(PdfKit.Draw(w, h, SKEncodedImageFormat.Jpeg), orientation);
        byte[] pdf = await PdfKit.ToPdfAsync(PdfKit.WithPdf(), jpeg);

        using PdfDocument document = PdfDocument.Open(pdf);
        Page page = document.GetPage(1);
        IPdfImage image = Assert.Single(page.GetImages());
        byte[] bytes = image.TryGetPng(out byte[]? png) ? png : image.RawBytes.ToArray();
        using SKBitmap bitmap = SKBitmap.Decode(bytes);
        Assert.NotNull(bitmap);

        // Centroid of the dark mark inside the embedded image, in unit square coordinates (y up).
        long sx = 0, sy = 0, n = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                SKColor c = bitmap.GetPixel(x, y);
                if (c.Blue > 100 && c.Red < 60 && c.Green < 60) { sx += x; sy += y; n++; }
            }
        }

        Assert.True(n > 0);
        double u = (((double)sx / n) + 0.5) / bitmap.Width;
        double v = 1 - ((((double)sy / n) + 0.5) / bitmap.Height);

        // Compose the matrices of the page content in order: new = M x current (row vectors).
        double[] ctm = [1, 0, 0, 1, 0, 0];
        foreach (IGraphicsStateOperation op in page.Operations)
        {
            if (op is ModifyCurrentTransformationMatrix cm)
            {
                double[] m = [cm.Value[0], cm.Value[1], cm.Value[2], cm.Value[3], cm.Value[4], cm.Value[5]];
                ctm = [
                    (m[0] * ctm[0]) + (m[1] * ctm[2]), (m[0] * ctm[1]) + (m[1] * ctm[3]),
                    (m[2] * ctm[0]) + (m[3] * ctm[2]), (m[2] * ctm[1]) + (m[3] * ctm[3]),
                    (m[4] * ctm[0]) + (m[5] * ctm[2]) + ctm[4], (m[4] * ctm[1]) + (m[5] * ctm[3]) + ctm[5]];
            }
        }

        double px = (u * ctm[0]) + (v * ctm[2]) + ctm[4];
        double py = (u * ctm[1]) + (v * ctm[3]) + ctm[5];

        (double ex, double ey) = Upright(orientation, w / 8.0, h / 8.0, w, h);

        Assert.Equal(ex, px, 0.8);
        Assert.Equal(ey, page.Height - py, 0.8); // PDF space has y up
    }

    [Theory]
    [InlineData("sample.png")]
    [InlineData("sample.jpg")]
    [InlineData("sample.bmp")]
    [InlineData("sample.gif")]
    public async Task APictureCutInHalf_IsRefused_NotTurnedIntoAPartialPdf(string sample)
    {
        byte[] all = await File.ReadAllBytesAsync(PdfKit.Sample(sample));
        byte[] half = all[..(all.Length / 2)];

        using MemoryStream input = new(half);
        await Assert.ThrowsAsync<ConversionException>(async () =>
            await PdfKit.WithPdf().ConvertAsync(Source.Stream(input), Target.Memory(FileFormat.Pdf)));
    }
}

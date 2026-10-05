using GLAZOV.M.CONVERTER.Converting;
using GLAZOV.M.CONVERTER.Data;
using GLAZOV.M.CONVERTER.Settings;
using SkiaSharp;
using UglyToad.PdfPig;

namespace GLAZOV.M.CONVERTER.Pdf.Tests;

/// <summary>
/// Helpers of the PDF tests: a converter with the extension, real and generated pictures, reading a PDF back.
/// </summary>
internal static class PdfKit
{
    internal static string Sample(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Samples", name);
    }

    internal static Converter WithPdf(PdfSettings? settings = null)
    {
        ConverterSettings converterSettings = new();
        converterSettings.Extensions.Add(settings is null ? new PdfExtension() : new PdfExtension(settings));
        return new Converter(converterSettings);
    }

    internal static async Task<byte[]> ToPdfAsync(Converter converter, byte[] picture, ConversionOptions? options = null)
    {
        using MemoryStream input = new(picture);
        ConversionResult result = await converter.ConvertAsync(Source.Stream(input), Target.Memory(FileFormat.Pdf), options);
        Assert.True(result.Succeeded);
        Assert.Equal(FileFormat.Pdf, result.TargetFormat);
        return result.Bytes!;
    }

    /// <summary>
    /// Pages of a PDF read back: count and size of each.
    /// </summary>
    internal static (double Width, double Height)[] Pages(byte[] pdf)
    {
        using PdfDocument document = PdfDocument.Open(pdf);
        return [.. document.GetPages().Select(page => (page.Width, page.Height))];
    }

    internal static (int Width, int Height) SizeOf(byte[] picture)
    {
        using SKBitmap bitmap = SKBitmap.Decode(picture) ?? throw new InvalidDataException("Not a picture.");
        return (bitmap.Width, bitmap.Height);
    }

    /// <summary>
    /// A picture drawn by Skia: an opaque or half-transparent field with a dark square in the top left corner.
    /// </summary>
    internal static byte[] Draw(int width, int height, SKEncodedImageFormat format, bool transparentRight = false)
    {
        using SKBitmap bitmap = new(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (SKCanvas canvas = new(bitmap))
        {
            canvas.Clear(SKColors.White);
            if (transparentRight)
            {
                using SKPaint clear = new() { BlendMode = SKBlendMode.Clear };
                canvas.DrawRect(width / 2f, 0, width - (width / 2f), height, clear);
            }

            using SKPaint dark = new() { Color = SKColors.DarkBlue };
            canvas.DrawRect(0, 0, Math.Max(1, width / 4f), Math.Max(1, height / 4f), dark);
        }

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(format, 90);
        return data.ToArray();
    }

    /// <summary>
    /// Puts an EXIF block with the given orientation (1–8) right after the start of a JPEG.
    /// </summary>
    internal static byte[] WithExifOrientation(byte[] jpeg, int orientation)
    {
        byte[] tiff =
        [
            0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
            0x00, 0x01,
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, (byte)orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];
        byte[] exif = [.. "Exif\0\0"u8.ToArray(), .. tiff];
        int length = exif.Length + 2;
        byte[] segment = [0xFF, 0xE1, (byte)(length >> 8), (byte)(length & 0xFF), .. exif];
        return [.. jpeg.Take(2), .. segment, .. jpeg.Skip(2)];
    }
}

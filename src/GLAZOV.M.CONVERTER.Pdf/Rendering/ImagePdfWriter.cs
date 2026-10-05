using SkiaSharp;

namespace GLAZOV.M.CONVERTER.Pdf.Rendering;

/// <summary>
/// Draws one picture on a single PDF page of the same size (one pixel is one point).
/// </summary>
internal static class ImagePdfWriter
{
    /// <summary>
    /// Builds the PDF.
    /// </summary>
    /// <param name="picture">Bytes of the picture file.</param>
    /// <param name="limits">Limits of the engine.</param>
    /// <param name="cancellationToken">Cancellation between the steps.</param>
    /// <returns>The PDF file.</returns>
    /// <exception cref="InvalidDataException">The picture is damaged, empty or larger than the limit.</exception>
    internal static byte[] Write(byte[] picture, PdfLimits limits, CancellationToken cancellationToken)
    {
        using SKData data = SKData.CreateCopy(picture);
        using SKCodec codec = SKCodec.Create(data)
            ?? throw new InvalidDataException("The file is not a picture that can be decoded.");

        SKImageInfo info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > limits.MaxPixels)
        {
            throw new InvalidDataException(
                $"The picture is {info.Width}×{info.Height} pixels, above the limit of {limits.MaxPixels} (setting PdfSettings.MaxPixels).");
        }

        cancellationToken.ThrowIfCancellationRequested();

        SKEncodedOrigin origin = codec.EncodedOrigin;
        using SKBitmap bitmap = DecodeWhole(codec);
        cancellationToken.ThrowIfCancellationRequested();

        // The stored pixels are drawn and the orientation is applied once, by the matrix below.
        // A JPEG that needs no turning goes into the PDF as it is, without being re-encoded.
        using SKImage image = origin == SKEncodedOrigin.TopLeft
            ? SKImage.FromEncodedData(data) ?? throw new InvalidDataException("The picture could not be decoded.")
            : SKImage.FromBitmap(bitmap);

        bool swap = OrientationMatrix.SwapsSides(origin);
        float pageWidth = swap ? info.Height : info.Width;
        float pageHeight = swap ? info.Width : info.Height;

        using MemoryStream buffer = new();
        using (SKManagedWStream stream = new(buffer))
        using (SKDocument document = SKDocument.CreatePdf(stream)
            ?? throw new InvalidOperationException("Skia could not start a PDF document."))
        {
            using (SKCanvas canvas = document.BeginPage(pageWidth, pageHeight))
            {
                canvas.SetMatrix(OrientationMatrix.For(origin, info.Width, info.Height));
                canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);
            }

            document.Close();
        }

        cancellationToken.ThrowIfCancellationRequested();
        return buffer.ToArray();
    }

    // Decoding is lazy elsewhere and a cut-off file would give half a picture without an error, so the whole picture is decoded here.
    private static SKBitmap DecodeWhole(SKCodec codec)
    {
        SKImageInfo stored = codec.Info;
        SKAlphaType alpha = stored.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul;
        SKBitmap bitmap = new(new SKImageInfo(stored.Width, stored.Height, SKColorType.Rgba8888, alpha));
        SKCodecResult result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
        if (result != SKCodecResult.Success)
        {
            bitmap.Dispose();
            throw new InvalidDataException(result == SKCodecResult.IncompleteInput
                ? "The picture is cut off or damaged: the file ends before the picture does."
                : $"The picture could not be decoded ({result}).");
        }

        return bitmap;
    }
}
